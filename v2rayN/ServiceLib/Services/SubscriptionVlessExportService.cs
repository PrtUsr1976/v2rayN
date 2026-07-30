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

        return Regex.Matches(content, @"vless://[^\s""'<>]+", RegexOptions.IgnoreCase)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> BuildSortedLinks(IEnumerable<ProfileItem> profiles)
    {
        return profiles
            .Where(profile => profile.ConfigType == EConfigType.VLESS)
            .Select(FmtHandler.GetShareUri)
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link!)
            .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link, StringComparer.Ordinal)
            .ToList();
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
