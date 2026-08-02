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
        Assert.Contains("Alpha", Uri.UnescapeDataString(new Uri(result[0]).Fragment));
        Assert.Contains("Zulu", Uri.UnescapeDataString(new Uri(result[1]).Fragment));
    }

    [Fact]
    public void ExtractOriginalVlessLinksPreservesExactParameters()
    {
        const string first = "vless://0000@a.example:443?spx=&type=tcp#%F0%9F%87%A9%F0%9F%87%AA%20Alpha";
        const string second = "vless://9999@z.example:443?type=tcp#%F0%9F%87%A8%F0%9F%87%AD%20Zulu";
        var content = $"{first}\n{second}\nss://ignored";

        var result = SubscriptionVlessExportService.ExtractOriginalVlessLinks(content);

        Assert.Equal([second, first], result);
        Assert.Contains("spx=", result[1]);
        Assert.Equal("\U0001F1E8\U0001F1ED Zulu", SubscriptionVlessExportService.GetLinkSortKey(second));
        Assert.Equal("\U0001F1E9\U0001F1EA Alpha", SubscriptionVlessExportService.GetLinkSortKey(first));
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
