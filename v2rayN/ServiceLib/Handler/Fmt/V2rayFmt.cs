namespace ServiceLib.Handler.Fmt;

public class V2rayFmt : BaseFmt
{
    public static List<ProfileItem>? ResolveFullArray(string strData, string? subRemarks)
    {
        var configObjects = JsonUtils.Deserialize<object[]>(strData);
        if (configObjects is not { Length: > 0 })
        {
            return null;
        }

        List<ProfileItem> lstResult = [];
        foreach (var configObject in configObjects)
        {
            var objectString = JsonUtils.Serialize(configObject);
            var profileIt = ResolveFull(objectString, subRemarks);
            if (profileIt != null)
            {
                lstResult.Add(profileIt);
            }
        }

        return lstResult;
    }

    private static int? GetPreSocksPort(JsonNode? config)
    {
        if (config?["inbounds"] is not JsonArray inbounds)
        {
            return null;
        }

        foreach (var inbound in inbounds)
        {
            if (!string.Equals(inbound?["protocol"]?.ToString(), "socks", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(inbound?["port"]?.ToString(), out var port) && port is > 0 and <= 65535)
            {
                return port;
            }
        }

        return null;
    }

    public static ProfileItem? ResolveFull(string strData, string? subRemarks)
    {
        var config = JsonUtils.ParseJson(strData);
        if (config?["inbounds"] == null
            || config["outbounds"] == null
            || config["routing"] == null)
        {
            return null;
        }

        var fileName = WriteAllText(strData);

        var profileItem = new ProfileItem
        {
            CoreType = ECoreType.Xray,
            Address = fileName,
            Remarks = config?["remarks"]?.ToString() ?? subRemarks ?? "v2ray_custom",
            PreSocksPort = GetPreSocksPort(config)
        };

        return profileItem;
    }
}
