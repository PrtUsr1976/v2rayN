namespace ServiceLib.Services;

public sealed record XKeenServerReference(string SubscriptionName, string ServerPrefix);

public sealed record XKeenSetDefinition(string Name, IReadOnlyList<XKeenServerReference> Servers);

public sealed record XKeenExportResult(IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;

    public string GetMessage()
    {
        return Success
            ? "Экспорт конфигов успешно завершен!"
            : "Экспорт конфигов завершен с ошибками:" + Environment.NewLine + Environment.NewLine
              + string.Join(Environment.NewLine, Errors.Select(error => "• " + error));
    }
}

public class XKeenExportService
{
    public const string SettingsFileName = "xkeen_sets.ini";
    public const string OutputDirectoryName = "xkeen_sets";

    public async Task<XKeenExportResult> ExportAsync(string applicationDirectory)
    {
        var errors = new List<string>();
        var settingsPath = Path.Combine(applicationDirectory, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            return new([$"Файл {SettingsFileName} не найден рядом с программой."]);
        }

        IReadOnlyList<XKeenSetDefinition> sets;
        try
        {
            var parseResult = Parse(File.ReadAllLines(settingsPath));
            sets = parseResult.Sets;
            errors.AddRange(parseResult.Errors);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(XKeenExportService), ex);
            return new([$"Не удалось прочитать {SettingsFileName}: {ex.Message}"]);
        }

        if (sets.Count == 0)
        {
            errors.Add($"В {SettingsFileName} не найдено ни одного набора.");
            return new(errors);
        }

        var outputDirectory = Path.Combine(applicationDirectory, OutputDirectoryName);
        try
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(XKeenExportService), ex);
            errors.Add($"Не удалось пересоздать папку {OutputDirectoryName}: {ex.Message}");
            return new(errors);
        }

        var subscriptions = await AppManager.Instance.SubItems() ?? [];
        var subscriptionMap = subscriptions
            .GroupBy(item => item.Remarks, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var profilesCache = new Dictionary<string, List<ProfileItem>>();

        try
        {
            foreach (var set in sets)
            {
            var setDirectory = Path.Combine(outputDirectory, NormalizeSetDirectoryName(set.Name));
            Directory.CreateDirectory(setDirectory);
            var readmeNames = new List<string>();
            var readmeLinks = new List<string>();

            for (var index = 0; index < set.Servers.Count; index++)
            {
                var reference = set.Servers[index];
                var position = index + 1;
                var itemDirectory = Path.Combine(setDirectory, position.ToString());
                Directory.CreateDirectory(itemDirectory);

                if (!subscriptionMap.TryGetValue(reference.SubscriptionName, out var subscription))
                {
                    errors.Add($"{set.Name}, папка {position}: подписка «{reference.SubscriptionName}» не найдена.");
                    readmeNames.Add($"{position} - НЕ НАЙДЕН ({reference.SubscriptionName}/{reference.ServerPrefix})");
                    continue;
                }

                if (!profilesCache.TryGetValue(subscription.Id, out var profiles))
                {
                    profiles = await AppManager.Instance.ProfileItems(subscription.Id) ?? [];
                    profilesCache[subscription.Id] = profiles;
                }

                var profile = FindProfile(profiles, reference.ServerPrefix);
                if (profile is null)
                {
                    errors.Add($"{set.Name}, папка {position}: сервер «{reference.ServerPrefix}.*» "
                               + $"не найден в подписке «{reference.SubscriptionName}».");
                    readmeNames.Add($"{position} - НЕ НАЙДЕН ({reference.SubscriptionName}/{reference.ServerPrefix})");
                    continue;
                }

                try
                {
                    var json = await BuildOutboundsJsonAsync(AppManager.Instance.Config, profile);
                    await File.WriteAllTextAsync(Path.Combine(itemDirectory, "04_outbounds.json"), json);
                    readmeNames.Add($"{position} - {profile.Remarks}");
                    var link = FmtHandler.GetShareUri(profile);
                    if (link.IsNotEmpty())
                    {
                        readmeLinks.Add(link);
                    }
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(nameof(XKeenExportService), ex);
                    errors.Add($"{set.Name}, папка {position}: ошибка экспорта "
                               + $"«{reference.SubscriptionName}/{reference.ServerPrefix}»: {ex.Message}");
                    readmeNames.Add($"{position} - ОШИБКА ({reference.SubscriptionName}/{reference.ServerPrefix})");
                }
            }

            var readme = string.Join(Environment.NewLine, readmeNames)
                         + Environment.NewLine + Environment.NewLine
                         + string.Join(Environment.NewLine, readmeLinks)
                         + Environment.NewLine;
                await File.WriteAllTextAsync(Path.Combine(setDirectory, "readme.txt"), readme);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(XKeenExportService), ex);
            errors.Add($"Ошибка создания файлов XKeen: {ex.Message}");
        }

        return new(errors);
    }

    public static (IReadOnlyList<XKeenSetDefinition> Sets, IReadOnlyList<string> Errors) Parse(
        IEnumerable<string> lines)
    {
        var sets = new List<XKeenSetDefinition>();
        var errors = new List<string>();
        string? currentName = null;
        List<XKeenServerReference>? currentServers = null;
        var lineNumber = 0;

        foreach (var sourceLine in lines)
        {
            lineNumber++;
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            var sectionMatch = Regex.Match(line, @"^\[(set\.\d+)\]$", RegexOptions.IgnoreCase);
            if (sectionMatch.Success)
            {
                if (currentName is not null && currentServers is not null)
                {
                    sets.Add(new(currentName, currentServers));
                }
                currentName = sectionMatch.Groups[1].Value;
                currentServers = [];
                continue;
            }

            if (currentServers is null)
            {
                errors.Add($"Строка {lineNumber}: параметр находится вне секции [set.N].");
                continue;
            }

            var contentMatch = Regex.Match(line, @"^content\s*=\s*([^(]+?)\s*\(([^)]*)\)\s*$",
                RegexOptions.IgnoreCase);
            if (!contentMatch.Success)
            {
                errors.Add($"Строка {lineNumber}: неверный формат «{line}».");
                continue;
            }

            var subscriptionName = contentMatch.Groups[1].Value.Trim();
            var serverNames = contentMatch.Groups[2].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (subscriptionName.Length == 0 || serverNames.Length == 0)
            {
                errors.Add($"Строка {lineNumber}: не указана подписка или список серверов.");
                continue;
            }

            currentServers.AddRange(serverNames.Select(name => new XKeenServerReference(subscriptionName, name)));
        }

        if (currentName is not null && currentServers is not null)
        {
            sets.Add(new(currentName, currentServers));
        }

        return (sets, errors);
    }

    public static ProfileItem? FindProfile(IEnumerable<ProfileItem> profiles, string serverPrefix)
    {
        var addressPrefix = serverPrefix.Trim() + ".";
        return profiles.FirstOrDefault(profile =>
            profile.Address.StartsWith(addressPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> BuildOutboundsJsonAsync(Config config, ProfileItem profile)
    {
        var builderResult = await CoreConfigContextBuilder.Build(config, profile);
        var result = new CoreConfigV2rayService(builderResult.Context).GenerateClientConfigContent();
        if (!result.Success || result.Data is null)
        {
            throw new InvalidOperationException(result.Msg ?? "Не удалось сформировать конфигурацию Xray.");
        }

        var root = JsonNode.Parse(result.Data.ToString() ?? string.Empty) as JsonObject
                   ?? throw new InvalidOperationException("Генератор вернул некорректный JSON.");
        var sourceOutbounds = root["outbounds"] as JsonArray
                              ?? throw new InvalidOperationException("В конфигурации отсутствует outbounds.");
        var proxy = sourceOutbounds.FirstOrDefault(node =>
            node?["protocol"]?.GetValue<string>() is not ("freedom" or "blackhole" or "dns"));
        if (proxy is null)
        {
            throw new InvalidOperationException("Прокси-outbound не сформирован.");
        }

        var proxyCopy = proxy.DeepClone();
        proxyCopy["tag"] = "vless-reality";
        var output = new JsonArray(proxyCopy);
        foreach (var protocol in new[] { "freedom", "blackhole" })
        {
            var outbound = sourceOutbounds.FirstOrDefault(node =>
                node?["protocol"]?.GetValue<string>() == protocol);
            if (outbound is not null)
            {
                output.Add(outbound.DeepClone());
            }
        }

        return JsonUtils.Serialize(new JsonObject { ["outbounds"] = output });
    }

    private static string NormalizeSetDirectoryName(string sectionName)
    {
        return sectionName.Replace('.', '_').ToLowerInvariant();
    }
}
