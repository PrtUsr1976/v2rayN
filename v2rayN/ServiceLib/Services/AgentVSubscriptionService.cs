namespace ServiceLib.Services;

public sealed record AgentVRequestHeaders(string? UserAgent, IReadOnlyDictionary<string, string> Headers)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(UserAgent) && Headers.Count == 0;
}

public static class AgentVSubscriptionService
{
    public const string DefaultFileName = "agent_v";

    public static AgentVRequestHeaders Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        if (!File.Exists(path))
        {
            return new AgentVRequestHeaders(null, new Dictionary<string, string>());
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? userAgent = null;

        foreach (var line in File.ReadAllLines(path))
        {
            var s = line.Trim();
            if (s.Length == 0 || s.StartsWith("#"))
            {
                continue;
            }

            var pos = s.IndexOf('=');
            if (pos <= 0)
            {
                continue;
            }

            var key = s[..pos].Trim();
            var value = s[(pos + 1)..].Trim();

            if (key.Equals("user_agent", StringComparison.OrdinalIgnoreCase))
            {
                userAgent = value;
            }
            else
            {
                headers[key.Replace('_', '-')] = value;
            }
        }

        return new AgentVRequestHeaders(userAgent, headers);
    }
}
