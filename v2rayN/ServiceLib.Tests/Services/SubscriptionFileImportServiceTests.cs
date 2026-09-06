using Xunit;

namespace ServiceLib.Tests.Services;

public class SubscriptionFileImportServiceTests
{
    [Fact]
    public void ParseUsesDomainAndAddsSequentialSuffixes()
    {
        var lines = new[]
        {
            "https://ent.xtls.win/sub/first",
            "https://ent.xtls.win/sub/second",
            "https://sub.example.com:52478/sub/token"
        };

        var result = SubscriptionFileImportService.Parse(lines, ["xtls"]);

        Assert.Collection(
            result,
            item => Assert.Equal("xtls-2", item.Remarks),
            item => Assert.Equal("xtls-3", item.Remarks),
            item => Assert.Equal("example", item.Remarks));
    }

    [Fact]
    public void ParseIgnoresBlankCommentsAndInvalidUrls()
    {
        var lines = new[]
        {
            "",
            "  ",
            "# comment",
            "; comment",
            "not a url",
            "ftp://example.com/file",
            "\uFEFFhttps://Example.COM/sub/value"
        };

        var result = SubscriptionFileImportService.Parse(lines, []);

        var item = Assert.Single(result);
        Assert.Equal("example", item.Remarks);
        Assert.Equal("https://Example.COM/sub/value", item.Url);
    }

    [Fact]
    public void ParseTreatsExistingNamesCaseInsensitively()
    {
        var result = SubscriptionFileImportService.Parse(
            ["https://example.com/sub/value"],
            ["EXAMPLE"]);

        Assert.Equal("example-2", Assert.Single(result).Remarks);
    }
}
