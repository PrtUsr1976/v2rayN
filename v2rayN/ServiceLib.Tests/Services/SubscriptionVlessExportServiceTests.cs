using Xunit;

namespace ServiceLib.Tests.Services;

public class SubscriptionVlessExportServiceTests
{
    [Fact]
    public void BuildSortedLinksIncludesOnlyVlessAndSortsAlphabetically()
    {
        var profiles = new[]
        {
            new ProfileItem
            {
                ConfigType = EConfigType.VLESS,
                Address = "z.example.com",
                Port = 443,
                Password = Guid.NewGuid().ToString(),
                Remarks = "Zulu"
            },
            new ProfileItem
            {
                ConfigType = EConfigType.VMess,
                Address = "ignored.example.com",
                Port = 443,
                Password = Guid.NewGuid().ToString(),
                Remarks = "Ignored"
            },
            new ProfileItem
            {
                ConfigType = EConfigType.VLESS,
                Address = "a.example.com",
                Port = 443,
                Password = Guid.NewGuid().ToString(),
                Remarks = "Alpha"
            }
        };

        var result = SubscriptionVlessExportService.BuildSortedLinks(profiles);

        Assert.Equal(2, result.Count);
        Assert.All(result, link => Assert.StartsWith("vless://", link));
        Assert.Equal(result.OrderBy(link => link, StringComparer.OrdinalIgnoreCase), result);
    }

    [Theory]
    [InlineData("xtls", "xtls")]
    [InlineData("  xtls-2  ", "xtls-2")]
    [InlineData("", "subscription")]
    public void GetSafeFileNameReturnsUsableName(string remarks, string expected)
    {
        Assert.Equal(expected, SubscriptionVlessExportService.GetSafeFileName(remarks));
    }
}
