using Xunit;

namespace ServiceLib.Tests.Services;

public class AgentVSubscriptionServiceTests
{
    [Fact]
    public void Load_ReadsAndNormalizesHeaders()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("v2rayn-agent-v-");
        var filePath = Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.DefaultFileName);

        try
        {
            File.WriteAllText(filePath,
                "user_agent=Throne/1.1.6\n" +
                "x_hwid=test-hwid\n" +
                "x_device_os=Windows\n" +
                "# comment\n");

            var result = AgentVSubscriptionService.Load(filePath);

            Assert.Equal("Throne/1.1.6", result.UserAgent);
            Assert.Equal("test-hwid", result.Headers["x-hwid"]);
            Assert.Equal("Windows", result.Headers["x-device-os"]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_PrefersHwidAndSupportsWhitespaceSeparatedHeaders()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("v2rayn-hwid-");
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.DefaultFileName),
                "user_agent=Fallback/1.0\nx_hwid=fallback\n");
            File.WriteAllText(
                Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.PreferredFileName),
                "accept-language\tru-RU,en,*\n" +
                "x-hwid    preferred-hwid\n" +
                "user-agent\tHapp/3.3.6/Windows/2607171516600\n");

            var result = AgentVSubscriptionService.Load(tempDirectory.FullName);

            Assert.Equal("Happ/3.3.6/Windows/2607171516600", result.UserAgent);
            Assert.Equal("preferred-hwid", result.Headers["x-hwid"]);
            Assert.Equal("ru-RU,en,*", result.Headers["accept-language"]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_FallsBackToAgentVAndAcceptsHwidFormat()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("v2rayn-agent-v-fallback-");
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.DefaultFileName),
                "x-device-os\tWindows\nx-app-version   3.3.6\n");

            var result = AgentVSubscriptionService.Load(tempDirectory.FullName);

            Assert.Equal("Windows", result.Headers["x-device-os"]);
            Assert.Equal("3.3.6", result.Headers["x-app-version"]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_LastDuplicateHeaderWinsIgnoringCase()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("v2rayn-header-duplicate-");
        var filePath = Path.Combine(tempDirectory.FullName, AgentVSubscriptionService.PreferredFileName);
        try
        {
            File.WriteAllText(filePath, "X-HWID first\nx_hwid=second\n");

            var result = AgentVSubscriptionService.Load(filePath);

            Assert.Equal("second", result.Headers["x-hwid"]);
            Assert.Single(result.Headers);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Downloaders_PreserveNonStandardUserAgentExactly()
    {
        const string userAgent = "Happ/3.3.6/Windows/2607171516600";

        using var request = new HttpRequestMessage();
        DownloadService.ApplyUserAgent(request.Headers, userAgent);
        Assert.Equal(userAgent, Assert.Single(request.Headers.GetValues("User-Agent")));

        var headers = new System.Net.WebHeaderCollection();
        DownloaderHelper.ApplyUserAgent(headers, userAgent);
        Assert.Equal(userAgent, headers[System.Net.HttpRequestHeader.UserAgent]);
    }

    [Fact]
    public void Load_ReturnsEmptyWhenFileDoesNotExist()
    {
        var result = AgentVSubscriptionService.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void BuildRequestHeadersLog_ContainsUserAgentAndCustomHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["x-hwid"] = "test-hwid",
            ["x-device-os"] = "Windows",
            ["Authorization"] = "Bearer secret-value",
            ["x-api-key"] = "secret-api-key"
        };

        var message = AgentVSubscriptionService.BuildRequestHeadersLog(
            "https://user:password@example.com/subscription?token=secret",
            "Throne/1.1.6",
            headers,
            false);

        Assert.Contains("SUBSCRIPTION REQUEST", message);
        Assert.Contains("Server=https://example.com", message);
        Assert.DoesNotContain("password", message);
        Assert.DoesNotContain("secret", message);
        Assert.Contains("User-Agent=Throne/1.1.6", message);
        Assert.Contains("x-hwid=test-hwid", message);
        Assert.Contains("x-device-os=Windows", message);
        Assert.Contains("Authorization=***", message);
        Assert.Contains("x-api-key=***", message);
        Assert.DoesNotContain("secret-value", message);
        Assert.DoesNotContain("secret-api-key", message);
    }

    [Fact]
    public void BuildRequestHeadersLog_LogsUserAgentWithoutAgentVHeaders()
    {
        var message = AgentVSubscriptionService.BuildRequestHeadersLog(
            "https://example.com/subscription",
            "v2rayN/7.24.1",
            null,
            true);

        Assert.Contains("User-Agent=v2rayN/7.24.1", message);
        Assert.Contains("Authorization=Basic ***", message);
    }
}
