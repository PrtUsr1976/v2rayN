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

    [Fact]
    public void ExtractVlessLinksFromXrayJsonBuildsShareLink()
    {
        const string content = """
            [{
              "remarks": "DE Germany",
              "outbounds": [{
                "protocol": "vless",
                "settings": { "vnext": [{
                  "address": "de.example.com",
                  "port": 443,
                  "users": [{ "id": "00000000-0000-0000-0000-000000000001", "encryption": "none", "flow": "xtls-rprx-vision" }]
                }]},
                "streamSettings": {
                  "network": "tcp",
                  "security": "reality",
                  "tcpSettings": {},
                  "realitySettings": {
                    "serverName": "de.example.com",
                    "fingerprint": "firefox",
                    "publicKey": "public-key",
                    "shortId": "abcd",
                    "spiderX": "/"
                  }
                }
              }]
            }]
            """;

        var result = SubscriptionVlessExportService.ExtractVlessLinksFromXrayJson(content);

        var link = Assert.Single(result);
        Assert.StartsWith("vless://00000000-0000-0000-0000-000000000001@de.example.com:443?", link);
        Assert.Contains("flow=xtls-rprx-vision", link);
        Assert.Contains("security=reality", link);
        Assert.Contains("sni=de.example.com", link);
        Assert.Contains("fp=firefox", link);
        Assert.Contains("pbk=public-key", link);
        Assert.Contains("sid=abcd", link);
        Assert.Contains("spx=%2F", link);
        Assert.Contains("type=tcp", link);
        Assert.Contains("headerType=none", link);
        Assert.Equal("DE Germany", SubscriptionVlessExportService.GetLinkSortKey(link));
    }

    [Fact]
    public void ExtractVlessLinksFromXrayJsonPreservesGrpcParameters()
    {
        const string content = """
            [{
              "remarks": "Finland gRPC",
              "outbounds": [{
                "protocol": "vless",
                "settings": { "vnext": [{
                  "address": "fi.example.com",
                  "port": 443,
                  "users": [{ "id": "00000000-0000-0000-0000-000000000001", "encryption": "none" }]
                }]},
                "streamSettings": {
                  "network": "grpc",
                  "grpcSettings": {
                    "serviceName": null,
                    "authority": "fi.example.com",
                    "mode": false
                  },
                  "security": "reality",
                  "realitySettings": {
                    "serverName": "fi.example.com",
                    "fingerprint": "qq",
                    "publicKey": "public-key",
                    "shortId": "abcd"
                  }
                }
              }]
            }]
            """;

        var link = Assert.Single(SubscriptionVlessExportService.ExtractVlessLinksFromXrayJson(content));
        var decoded = Uri.UnescapeDataString(link);

        Assert.Contains("type=grpc", decoded);
        Assert.Contains("authority=fi.example.com", decoded);
        Assert.Contains("mode=gun", decoded);
        Assert.DoesNotContain("serviceName=", decoded);
        Assert.Contains("security=reality", decoded);
    }

    [Fact]
    public void ExtractVlessLinksFromXrayJsonPreservesXhttpTlsParameters()
    {
        const string content = """
            [{
              "remarks": "Bypass 2",
              "outbounds": [{
                "protocol": "vless",
                "settings": { "vnext": [{
                  "address": "cdn.example.com",
                  "port": 443,
                  "users": [{ "id": "00000000-0000-0000-0000-000000000001", "encryption": "none" }]
                }]},
                "streamSettings": {
                  "network": "xhttp",
                  "xhttpSettings": {
                    "mode": "packet-up",
                    "host": "cdn.example.com",
                    "path": "/api/feed/articles/",
                    "extra": { "noSSEHeader": true }
                  },
                  "security": "tls",
                  "tlsSettings": {
                    "serverName": "cdn.example.com",
                    "fingerprint": "firefox",
                    "alpn": ["h2", "http/1.1"]
                  },
                  "finalmask": { "debug": false, "congestion": "bbr" }
                }
              }]
            }]
            """;

        var link = Assert.Single(SubscriptionVlessExportService.ExtractVlessLinksFromXrayJson(content));
        var decoded = Uri.UnescapeDataString(link);

        Assert.Contains("alpn=h2,http/1.1", decoded);
        Assert.Contains("insecure=0", decoded);
        Assert.Contains("allowInsecure=0", decoded);
        Assert.Contains("""fm={"debug":false,"congestion":"bbr"}""", decoded);
        Assert.Contains("type=xhttp", decoded);
        Assert.Contains("host=cdn.example.com", decoded);
        Assert.Contains("path=/api/feed/articles/", decoded);
        Assert.Contains("mode=packet-up", decoded);
        Assert.Contains("""extra={"noSSEHeader":true}""", decoded);
    }

    [Fact]
    public void ExtractVlessLinksFromXrayJsonUsesOnePrimaryOutboundPerConfiguration()
    {
        const string content = """
            [{
              "remarks": "Auto select",
              "outbounds": [
                {
                  "protocol": "vless",
                  "settings": { "vnext": [{
                    "address": "first.example.com",
                    "port": 443,
                    "users": [{ "id": "00000000-0000-0000-0000-000000000001" }]
                  }]}
                },
                {
                  "protocol": "vless",
                  "settings": { "vnext": [{
                    "address": "second.example.com",
                    "port": 443,
                    "users": [{ "id": "00000000-0000-0000-0000-000000000002" }]
                  }]}
                }
              ]
            }]
            """;

        var result = SubscriptionVlessExportService.ExtractVlessLinksFromXrayJson(content);

        var link = Assert.Single(result);
        Assert.Contains("@first.example.com:443", link);
        Assert.DoesNotContain("second.example.com", link);
        Assert.Equal("Auto select", SubscriptionVlessExportService.GetLinkSortKey(link));
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
