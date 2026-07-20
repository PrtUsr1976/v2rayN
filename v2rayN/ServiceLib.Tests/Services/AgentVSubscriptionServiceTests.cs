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
    public void Load_ReturnsEmptyWhenFileDoesNotExist()
    {
        var result = AgentVSubscriptionService.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.True(result.IsEmpty);
    }
}
