namespace ServiceLib.Services;

public static class SubscriptionVlessExportService
{
    private const string DirectoryName = "subs_links";

    public static async Task ExportAsync(SubItem subscription, string? originalContent = null)
    {
        try
        {
            var links = ExtractOriginalVlessLinks(originalContent);
            if (links.Count == 0)
            {
                links = ExtractVlessLinksFromXrayJson(originalContent);
            }
            if (links.Count == 0)
            {
                var profiles = await AppManager.Instance.ProfileItems(subscription.Id) ?? [];
                links = BuildSortedLinks(profiles);
            }
            var directory = Path.Combine(AppContext.BaseDirectory, DirectoryName);
            Directory.CreateDirectory(directory);

            var fileName = GetSafeFileName(subscription.Remarks) + ".txt";
            var filePath = Path.Combine(directory, fileName);
            var temporaryPath = filePath + ".tmp";
            var content = links.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, links) + Environment.NewLine;

            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, filePath, true);
            Logging.SaveLog(
                $"Subscription VLESS export: wrote {links.Count} links to {filePath}.");
        }
        catch (Exception ex)
        {
            Logging.SaveLog("SubscriptionVlessExportService", ex);
        }
    }

    public static IReadOnlyList<string> ExtractOriginalVlessLinks(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return SortLinks(Regex.Matches(content, @"vless://[^\s""'<>]+", RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal));
    }

    public static IReadOnlyList<string> ExtractVlessLinksFromXrayJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        try
        {
            var root = JsonNode.Parse(content);
            var configurations = root is JsonArray array
                ? array.OfType<JsonObject>()
                : root is JsonObject single ? [single] : [];
            var links = new List<string>();

            foreach (var configuration in configurations)
            {
                var remarks = configuration["remarks"]?.ToString();
                if (configuration["outbounds"] is not JsonArray outbounds)
                {
                    continue;
                }

                var outbound = outbounds.OfType<JsonObject>()
                    .FirstOrDefault(item => item["protocol"]?.ToString()
                        .Equals("vless", StringComparison.OrdinalIgnoreCase) == true);
                if (outbound is not null)
                {
                    var configurationLinks = new List<string>();
                    ExtractVlessOutboundLinks(outbound, remarks, configurationLinks);
                    var link = configurationLinks.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(link))
                    {
                        links.Add(link);
                    }
                }
            }

            return SortLinks(links.Distinct(StringComparer.Ordinal));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void ExtractVlessOutboundLinks(JsonObject outbound, string? remarks, List<string> links)
    {
        var streamSettings = outbound["streamSettings"] as JsonObject;
        var network = streamSettings?["network"]?.ToString();
        network = string.IsNullOrWhiteSpace(network) ? "tcp" : network;
        var security = streamSettings?["security"]?.ToString();
        security = string.IsNullOrWhiteSpace(security) ? "none" : security;

        if (outbound["settings"]?["vnext"] is not JsonArray servers)
        {
            return;
        }

        foreach (var server in servers.OfType<JsonObject>())
        {
            var address = server["address"]?.ToString();
            if (string.IsNullOrWhiteSpace(address)
                || !int.TryParse(server["port"]?.ToString(), out var port)
                || server["users"] is not JsonArray users)
            {
                continue;
            }

            foreach (var user in users.OfType<JsonObject>())
            {
                var id = user["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var query = new List<string>();
                AddQueryParameter(query, "encryption", user["encryption"]?.ToString() ?? "none");
                AddQueryParameter(query, "flow", user["flow"]?.ToString());
                AddQueryParameter(query, "security", security);

                if (security.Equals("reality", StringComparison.OrdinalIgnoreCase))
                {
                    var reality = streamSettings?["realitySettings"] as JsonObject;
                    AddQueryParameter(query, "sni", reality?["serverName"]?.ToString());
                    AddQueryParameter(query, "fp", reality?["fingerprint"]?.ToString());
                    AddQueryParameter(query, "pbk", reality?["publicKey"]?.ToString());
                    AddQueryParameter(query, "sid", reality?["shortId"]?.ToString());
                    AddQueryParameter(query, "spx", reality?["spiderX"]?.ToString(), reality?.ContainsKey("spiderX") == true);
                }
                else if (security.Equals("tls", StringComparison.OrdinalIgnoreCase))
                {
                    var tls = streamSettings?["tlsSettings"] as JsonObject;
                    AddQueryParameter(query, "sni", tls?["serverName"]?.ToString());
                    AddQueryParameter(query, "fp", tls?["fingerprint"]?.ToString());
                    if (tls?["alpn"] is JsonArray alpn)
                    {
                        AddQueryParameter(query, "alpn", string.Join(",", alpn
                            .Select(item => item?.ToString())
                            .Where(item => !string.IsNullOrWhiteSpace(item))));
                    }

                    var allowInsecure = GetBooleanQueryValue(tls?["allowInsecure"]);
                    AddQueryParameter(query, "insecure", allowInsecure);
                    AddQueryParameter(query, "allowInsecure", allowInsecure);
                }

                if (streamSettings?["finalmask"] is JsonNode finalMask)
                {
                    AddQueryParameter(query, "fm", finalMask.ToJsonString());
                }

                AddQueryParameter(query, "type", network);
                var transportSettings = streamSettings?[$"{network}Settings"] as JsonObject;
                if (network.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    AddQueryParameter(query, "headerType", transportSettings?["header"]?["type"]?.ToString() ?? "none");
                }
                else if (network.Equals("ws", StringComparison.OrdinalIgnoreCase))
                {
                    AddQueryParameter(query, "path", transportSettings?["path"]?.ToString());
                    AddQueryParameter(query, "host", transportSettings?["headers"]?["Host"]?.ToString());
                }
                else if (network.Equals("xhttp", StringComparison.OrdinalIgnoreCase))
                {
                    AddQueryParameter(query, "host", transportSettings?["host"]?.ToString());
                    AddQueryParameter(query, "path", transportSettings?["path"]?.ToString());
                    AddQueryParameter(query, "mode", transportSettings?["mode"]?.ToString());
                    if (transportSettings?["extra"] is JsonNode extra)
                    {
                        AddQueryParameter(query, "extra", extra.ToJsonString());
                    }
                }
                else if (network.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                {
                    AddQueryParameter(query, "authority", transportSettings?["authority"]?.ToString());
                    AddQueryParameter(query, "serviceName", transportSettings?["serviceName"]?.ToString());
                    var grpcMode = GetGrpcMode(transportSettings?["mode"] ?? transportSettings?["multiMode"]);
                    AddQueryParameter(query, "mode", grpcMode);
                }

                var host = address.Contains(':') && !address.StartsWith("[", StringComparison.Ordinal) ? $"[{address}]" : address;
                var name = string.IsNullOrWhiteSpace(remarks) ? address : remarks;
                links.Add($"vless://{Uri.EscapeDataString(id)}@{host}:{port}?{string.Join("&", query)}#{Uri.EscapeDataString(name)}");
            }
        }
    }

    private static string? GetGrpcMode(JsonNode? value)
    {
        return value?.ToString().ToLowerInvariant() switch
        {
            "true" => "multi",
            "false" => "gun",
            "multi" => "multi",
            "gun" => "gun",
            _ => null
        };
    }

    private static string GetBooleanQueryValue(JsonNode? value)
    {
        return value?.ToString() is "true" or "1" ? "1" : "0";
    }

    private static void AddQueryParameter(List<string> query, string name, string? value, bool includeEmpty = false)
    {
        if (value is null || (!includeEmpty && string.IsNullOrWhiteSpace(value)))
        {
            return;
        }

        query.Add($"{name}={Uri.EscapeDataString(value)}");
    }

    public static IReadOnlyList<string> BuildSortedLinks(IEnumerable<ProfileItem> profiles)
    {
        return SortLinks(profiles
            .Where(profile => profile.ConfigType == EConfigType.VLESS)
            .Select(FmtHandler.GetShareUri)
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link!));
    }

    public static IReadOnlyList<string> SortLinks(IEnumerable<string> links)
    {
        return links
            .OrderBy(GetLinkSortKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link, StringComparer.Ordinal)
            .ToList();
    }

    public static string GetLinkSortKey(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return link;
        }

        var name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')).Trim();
        return name.Length > 0 ? name : uri.Host;
    }

    public static string GetSafeFileName(string? remarks)
    {
        var name = string.IsNullOrWhiteSpace(remarks) ? "subscription" : remarks.Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(name
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');
        return string.IsNullOrWhiteSpace(safeName) ? "subscription" : safeName;
    }
}
