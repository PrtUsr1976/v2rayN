namespace ServiceLib.Services;

public sealed record AgentVRequestHeaders(string? UserAgent, IReadOnlyDictionary<string, string> Headers)
{
    public static AgentVRequestHeaders Empty { get; } =
        new(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public bool IsEmpty => string.IsNullOrWhiteSpace(UserAgent) && Headers.Count == 0;
}

public static class AgentVSubscriptionService
{
    public const string DefaultFileName = "agent_v";
    public const string PreferredFileName = "hwid";
    public const string PathEnvironmentVariable = "V2RAYN_AGENT_V_PATH";

    private const string LogTag = "AgentVSubscriptionService";

    public static AgentVRequestHeaders Load(string? configuredPath = null)
    {
        var path = ResolvePath(configuredPath);
        if (!File.Exists(path))
        {
            return AgentVRequestHeaders.Empty;
        }

        try
        {
            string? userAgent = null;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                var match = Regex.Match(line, @"^([^=\s]+)\s*(?:=|\s+)\s*(.+?)\s*$");
                if (!match.Success)
                {
                    continue;
                }

                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var headerName = NormalizeHeaderName(key);
                if (headerName.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    userAgent = value;
                }
                else
                {
                    headers[headerName] = value;
                }
            }

            return new AgentVRequestHeaders(userAgent, headers);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(LogTag, ex);
            return AgentVRequestHeaders.Empty;
        }
    }

    public static string BuildRequestHeadersLog(
        string url,
        string userAgent,
        IReadOnlyDictionary<string, string>? requestHeaders,
        bool hasBasicAuthorization)
    {
        var lines = new List<string>
        {
            "SUBSCRIPTION REQUEST",
            $"Server={GetSafeServerAddress(url)}",
            "HEADERS",
            $"User-Agent={userAgent}"
        };

        if (requestHeaders is { Count: > 0 })
        {
            foreach (var header in requestHeaders
                         .Where(x => !x.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = IsSensitiveHeader(header.Key) ? "***" : header.Value;
                lines.Add($"{header.Key}={value}");
            }
        }

        if (hasBasicAuthorization)
        {
            lines.Add("Authorization=Basic ***");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsSensitiveHeader(string name)
    {
        return name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
               || name.Contains("api-key", StringComparison.OrdinalIgnoreCase)
               || name.Contains("token", StringComparison.OrdinalIgnoreCase)
               || name.Contains("secret", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSafeServerAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "<invalid URL>";
        }

        var serverUri = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port).Uri;
        return serverUri.GetLeftPart(UriPartial.Authority);
    }

    private static string ResolvePath(string? configuredPath)
    {
        var path = configuredPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.GetEnvironmentVariable(PathEnvironmentVariable)?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return ResolvePreferredFile(AppContext.BaseDirectory);
        }

        path = Environment.ExpandEnvironmentVariables(path);
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        if (Directory.Exists(path))
        {
            return ResolvePreferredFile(path);
        }

        return Path.GetFullPath(path);
    }

    private static string ResolvePreferredFile(string directory)
    {
        var preferredPath = Path.Combine(directory, PreferredFileName);
        return File.Exists(preferredPath)
            ? preferredPath
            : Path.Combine(directory, DefaultFileName);
    }

    private static string NormalizeHeaderName(string key)
    {
        if (key.Equals("user_agent", StringComparison.OrdinalIgnoreCase)
            || key.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
        {
            return "User-Agent";
        }

        return key.Replace('_', '-').ToLowerInvariant();
    }
}
