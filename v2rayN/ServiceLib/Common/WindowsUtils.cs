using Microsoft.Win32;

namespace ServiceLib.Common;

[SupportedOSPlatform("windows")]
internal static class WindowsUtils
{
    private static readonly string _tag = "WindowsUtils";
    private static readonly string[] _tunNameList = ["wintunsingbox_tun", "xray_tun"];

    public static string? RegReadValue(string path, string name, string def)
    {
        RegistryKey? regKey = null;
        try
        {
            regKey = Registry.CurrentUser.OpenSubKey(path, false);
            var value = regKey?.GetValue(name) as string;
            return value.IsNullOrEmpty() ? def : value;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            regKey?.Close();
        }
        return def;
    }

    public static void RegWriteValue(string path, string name, object value)
    {
        RegistryKey? regKey = null;
        try
        {
            regKey = Registry.CurrentUser.CreateSubKey(path);
            if (value.ToString().IsNullOrEmpty())
            {
                regKey?.DeleteValue(name, false);
            }
            else
            {
                regKey?.SetValue(name, value);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            regKey?.Close();
        }
    }

    public static async Task RemoveTunDevice()
    {
        var totalStopwatch = Stopwatch.StartNew();
        Logging.SaveLog("TUN cleanup: starting removal of old Wintun devices.");

        foreach (var tunName in _tunNameList)
        {
            try
            {
                var instanceId = GetTunInstanceId(tunName);
                var presentBefore = IsTunDevicePresent(tunName, instanceId);
                Logging.SaveLog($"TUN cleanup: device={tunName}, instance={instanceId}, present before removal={presentBefore}.");

                var pnpUtilPath = @"C:\Windows\System32\pnputil.exe";
                var arg = $$""" /remove-device  "{{instanceId}}" """;
                var commandStopwatch = Stopwatch.StartNew();
                var output = await Utils.GetCliWrapOutput(pnpUtilPath, arg);
                var normalizedOutput = NormalizeCommandOutput(output);

                Logging.SaveLog(
                    $"TUN cleanup: pnputil finished for {tunName} after {commandStopwatch.ElapsedMilliseconds} ms; " +
                    $"result={normalizedOutput}.");
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"TUN cleanup: exception while removing {tunName}: {ex.Message}");
                Logging.SaveLog(_tag, ex);
            }
        }

        await WaitForTunDevicesRemoved(3000);
        Logging.SaveLog($"TUN cleanup: completed after {totalStopwatch.ElapsedMilliseconds} ms.");
    }

    private static async Task WaitForTunDevicesRemoved(int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var presentDevices = _tunNameList
                .Select(name => (Name: name, InstanceId: GetTunInstanceId(name)))
                .Where(item => IsTunDevicePresent(item.Name, item.InstanceId))
                .Select(item => item.Name)
                .ToList();

            if (presentDevices.Count == 0)
            {
                Logging.SaveLog($"TUN cleanup: no old TUN devices are visible after {stopwatch.ElapsedMilliseconds} ms.");
                return;
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                Logging.SaveLog(
                    $"TUN cleanup: timeout after {stopwatch.ElapsedMilliseconds} ms waiting for device removal; " +
                    $"still present: {string.Join(", ", presentDevices)}.");
                return;
            }

            await Task.Delay(200);
        }
    }

    private static string GetTunInstanceId(string tunName)
    {
        var sum = MD5.HashData(Encoding.UTF8.GetBytes(tunName));
        var guid = new Guid(sum);
        return $@"SWD\Wintun\{{{guid}}}";
    }

    private static bool IsTunDevicePresent(string tunName, string instanceId)
    {
        try
        {
            var registryPath = $@"SYSTEM\CurrentControlSet\Enum\{instanceId}";
            using var regKey = Registry.LocalMachine.OpenSubKey(registryPath, false);
            if (regKey != null)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"TUN cleanup: registry check failed for {tunName}: {ex.Message}");
        }

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(adapter =>
                adapter.Name.Equals(tunName, StringComparison.OrdinalIgnoreCase) ||
                adapter.Description.Contains(tunName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"TUN cleanup: network interface check failed for {tunName}: {ex.Message}");
            return false;
        }
    }

    private static string NormalizeCommandOutput(string? output)
    {
        if (output.IsNullOrEmpty())
        {
            return "<no successful output>";
        }

        return output!
            .Replace("\r", string.Empty)
            .Replace("\n", " | ")
            .Trim(' ', '|');
    }
}
