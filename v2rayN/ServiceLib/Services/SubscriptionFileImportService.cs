namespace ServiceLib.Services;

public sealed record SubscriptionFileEntry(string Remarks, string Url);

public static class SubscriptionFileImportService
{
    public static IReadOnlyList<SubscriptionFileEntry> Parse(
        IEnumerable<string> lines,
        IEnumerable<string?> existingRemarks)
    {
        var usedRemarks = new HashSet<string>(
            existingRemarks.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!),
            StringComparer.OrdinalIgnoreCase);
        var result = new List<SubscriptionFileEntry>();

        foreach (var rawLine in lines)
        {
            var url = rawLine.Trim().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#') || url.StartsWith(';'))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrWhiteSpace(uri.IdnHost))
            {
                continue;
            }

            var baseRemarks = uri.IdnHost.ToLowerInvariant();
            var remarks = baseRemarks;
            for (var suffix = 1; usedRemarks.Contains(remarks); suffix++)
            {
                remarks = $"{baseRemarks}-{suffix}";
            }

            usedRemarks.Add(remarks);
            result.Add(new SubscriptionFileEntry(remarks, url));
        }

        return result;
    }
}
