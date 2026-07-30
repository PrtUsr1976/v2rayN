using System.IO.Compression;
using Xunit;

namespace ServiceLib.Tests.Services;

public class XKeenExportServiceTests
{
    [Fact]
    public void Parse_PreservesContentOrderAcrossLines()
    {
        var result = XKeenExportService.Parse(
        [
            "[set.1]",
            "content = xtls(fn, de, es, nl, ch, fr, hk)",
            "content = other(sw3, ger3)"
        ]);

        Assert.Empty(result.Errors);
        var set = Assert.Single(result.Sets);
        Assert.Equal("set.1", set.Name);
        Assert.Equal(
            ["xtls/fn", "xtls/de", "xtls/es", "xtls/nl", "xtls/ch", "xtls/fr", "xtls/hk",
             "other/sw3", "other/ger3"],
            set.Servers.Select(item => $"{item.SubscriptionName}/{item.ServerPrefix}"));
    }

    [Fact]
    public void Parse_ReportsInvalidLinesAndContinues()
    {
        var result = XKeenExportService.Parse(
        [
            "content = outside(fn)",
            "[set.1]",
            "invalid",
            "content = xtls(fn)"
        ]);

        Assert.Single(result.Sets);
        Assert.Equal(2, result.Errors.Count);
        Assert.Single(result.Sets[0].Servers);
    }

    [Fact]
    public void FindProfile_MatchesAddressPrefixOnly()
    {
        var expected = new ProfileItem { Address = "fn.example.com" };
        var profiles = new[]
        {
            new ProfileItem { Address = "cdn.example.com", Remarks = "fn server" },
            new ProfileItem { Address = "fn2.example.com" },
            expected
        };

        Assert.Same(expected, XKeenExportService.FindProfile(profiles, "FN"));
        Assert.Null(XKeenExportService.FindProfile(profiles, "missing"));
    }

    [Fact]
    public void Result_UsesRequiredSuccessMessage()
    {
        var result = new XKeenExportResult([]);

        Assert.True(result.Success);
        Assert.Equal("Экспорт конфигов успешно завершен!", result.GetMessage());
    }

    [Fact]
    public void CreateSetArchive_IncludesNumberedFoldersAndReadme()
    {
        var root = Path.Combine(Path.GetTempPath(), "xkeen-test-" + Guid.NewGuid());
        var setDirectory = Path.Combine(root, "set_1");
        var archivePath = Path.Combine(root, "set_1.zip");
        try
        {
            Directory.CreateDirectory(Path.Combine(setDirectory, "1"));
            Directory.CreateDirectory(Path.Combine(setDirectory, "2"));
            File.WriteAllText(Path.Combine(setDirectory, "1", "04_outbounds.json"), "{}");
            File.WriteAllText(Path.Combine(setDirectory, "readme.txt"), "included");

            XKeenExportService.CreateSetArchive(setDirectory, archivePath, 2);

            using var archive = ZipFile.OpenRead(archivePath);
            var names = archive.Entries.Select(entry => entry.FullName).ToList();
            Assert.Contains("1/", names);
            Assert.Contains("1/04_outbounds.json", names);
            Assert.Contains("2/", names);
            Assert.Contains("readme.txt", names);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
