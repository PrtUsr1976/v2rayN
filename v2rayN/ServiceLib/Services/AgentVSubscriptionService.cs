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

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
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

    private static string ResolvePath(string? configuredPath)
    {
        var path = configuredPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.GetEnvironmentVariable(PathEnvironmentVariable)?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        }

        path = Environment.ExpandEnvironmentVariables(path);
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        if (Directory.Exists(path))
        {
            path = Path.Combine(path, DefaultFileName);
        }

        return Path.GetFullPath(path);
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
