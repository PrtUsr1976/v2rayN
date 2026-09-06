using System.IO.Compression;
using Xunit;

namespace ServiceLib.Tests.Services;

public class XKeenExportServiceTests
{
    [Fact]
    public void FormatReadmeNames_WritesTwoServersPerLine()
    {
        var result = XKeenExportService.FormatReadmeNames(
        [
            "1 - Germany(xtls-2)",
            "2 - Estonia(un1c4d3)",
            "3 - Netherlands(arza)"
        ]);

        Assert.Equal(
            $"1 - Germany(xtls-2), 2 - Estonia(un1c4d3){Environment.NewLine}3 - Netherlands(arza)",
            result);
    }

    [Fact]
    public void Parse_PreservesContentOrderAcrossLines()
    {
        var result = XKeenExportService.Parse(
        [
            "[set.1]",
            "content = xtls(fn, de, es, nl, ch, fr, hk)",
            "content = other(sw3, ger3), spx=\"\""
        ]);

        Assert.Empty(result.Errors);
        var set = Assert.Single(result.Sets);
        Assert.Equal("set.1", set.Name);
        Assert.Equal(
            ["xtls/fn", "xtls/de", "xtls/es", "xtls/nl", "xtls/ch", "xtls/fr", "xtls/hk",
             "other/sw3", "other/ger3"],
            set.Servers.Select(item => $"{item.SubscriptionName}/{item.ServerPrefix}"));
        Assert.Null(set.Servers[6].SpiderXOverride);
        Assert.Equal(string.Empty, set.Servers[7].SpiderXOverride);
        Assert.Equal(string.Empty, set.Servers[8].SpiderXOverride);
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

    [Theory]
    [InlineData("vless://id@host:443?spx=%2F&type=tcp", "/")]
    [InlineData("vless://id@host:443?spx=&type=tcp", "")]
    [InlineData("vless://id@host:443?type=tcp", "/")]
    public void GetSpiderXFromOriginalLinkDistinguishesMissingAndEmpty(string link, string expected)
    {
        Assert.Equal(expected, XKeenExportService.GetSpiderXFromOriginalLink(link));
    }

    [Fact]
    public void NormalizeOutboundsForXKeen_MatchesReferenceShape()
    {
        var source = JsonNode.Parse("""
        {
          "tag": "proxy",
          "protocol": "vless",
          "settings": {
            "vnext": [{
              "address": "sw3.un1c4d3.ru",
              "port": 24448,
              "users": [{
                "id": "test-id",
                "email": "t@t.tt",
                "security": "auto",
                "encryption": "none",
                "flow": "xtls-rprx-vision"
              }]
            }]
          },
          "streamSettings": {
            "network": "raw",
            "security": "reality",
            "realitySettings": {
              "show": false,
              "publicKey": "test-key",
              "shortId": "",
              "spiderX": "/",
              "mldsa65Verify": ""
            }
          },
          "mux": { "enabled": false, "concurrency": -1 }
        }
        """)!;

        var result = XKeenExportService.NormalizeOutboundsForXKeen(source);
        var outbounds = Assert.IsType<JsonArray>(result["outbounds"]);
        var proxy = Assert.IsType<JsonObject>(outbounds[0]);
        var user = Assert.IsType<JsonObject>(proxy["settings"]?["vnext"]?[0]?["users"]?[0]);
        var stream = Assert.IsType<JsonObject>(proxy["streamSettings"]);
        var reality = Assert.IsType<JsonObject>(stream["realitySettings"]);
        var block = Assert.IsType<JsonObject>(outbounds[2]);

        Assert.Equal("tcp", stream["network"]?.GetValue<string>());
        Assert.Equal("/", reality["spiderX"]?.GetValue<string>());
        Assert.False(reality.ContainsKey("show"));
        Assert.False(reality.ContainsKey("mldsa65Verify"));
        Assert.False(proxy.ContainsKey("mux"));
        Assert.False(user.ContainsKey("email"));
        Assert.False(user.ContainsKey("security"));
        Assert.Equal(0, user["level"]?.GetValue<int>());
        Assert.Equal("http", block["settings"]?["response"]?["type"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeOutboundsForXKeen_UsesProfileSpiderXWhenNoOriginalLink()
    {
        var source = JsonNode.Parse("""
        {
          "protocol": "vless",
          "settings": { "vnext": [{ "users": [{}] }] },
          "streamSettings": {
            "network": "raw",
            "security": "reality",
            "realitySettings": { "spiderX": "/from-profile" }
          }
        }
        """)!;

        var result = XKeenExportService.NormalizeOutboundsForXKeen(source);
        var reality = result["outbounds"]?[0]?["streamSettings"]?["realitySettings"];

        Assert.Equal("/from-profile", reality?["spiderX"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeOutboundsForXKeen_UsesExplicitEmptySpiderXOverride()
    {
        var source = JsonNode.Parse("""
        {
          "protocol": "vless",
          "settings": { "vnext": [{ "users": [{}] }] },
          "streamSettings": {
            "security": "reality",
            "realitySettings": { "spiderX": "/from-link" }
          }
        }
        """)!;

        var result = XKeenExportService.NormalizeOutboundsForXKeen(source, string.Empty);
        var reality = result["outbounds"]?[0]?["streamSettings"]?["realitySettings"];

        Assert.Equal(string.Empty, reality?["spiderX"]?.GetValue<string>());
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
