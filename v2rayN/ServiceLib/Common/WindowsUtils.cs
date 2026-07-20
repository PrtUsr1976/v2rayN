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

    public static async Task<bool> WaitForTunDevicesReleased(int timeoutMs = 30000)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextProgressLogMs = 0L;
        var lastSummary = string.Empty;

        Logging.SaveLog($"TUN wait: checking for old Wintun devices; timeout={timeoutMs} ms.");

        while (true)
        {
            var presentDevices = GetTunDeviceStates()
                .Where(item => item.RegistryPresent || item.NetworkPresent)
                .ToList();

            if (presentDevices.Count == 0)
            {
                if (stopwatch.ElapsedMilliseconds == 0)
                {
                    Logging.SaveLog("TUN wait: no old TUN devices are visible; continuing immediately.");
                }
                else
                {
                    Logging.SaveLog($"TUN wait: old TUN devices disappeared after {stopwatch.ElapsedMilliseconds} ms.");
                }
                return true;
            }

            var summary = string.Join("; ", presentDevices.Select(FormatTunDeviceState));
            if (!summary.Equals(lastSummary, StringComparison.Ordinal) || stopwatch.ElapsedMilliseconds >= nextProgressLogMs)
            {
                Logging.SaveLog($"TUN wait: old device still visible after {stopwatch.ElapsedMilliseconds} ms: {summary}.");
                lastSummary = summary;
                nextProgressLogMs = stopwatch.ElapsedMilliseconds + 1000;
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                Logging.SaveLog(
                    $"TUN wait: timeout after {stopwatch.ElapsedMilliseconds} ms; startup with TUN must be cancelled. " +
                    $"Still visible: {summary}.");
                return false;
            }

            await Task.Delay(250);
        }
    }

    private static List<TunDeviceState> GetTunDeviceStates()
    {
        var result = new List<TunDeviceState>();
        NetworkInterface[] adapters;

        try
        {
            adapters = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (Exception ex)
        {
            Logging.SaveLog("TUN wait: failed to enumerate network interfaces: " + ex.Message);
            adapters = [];
        }

        foreach (var tunName in _tunNameList)
        {
            var instanceId = GetTunInstanceId(tunName);
            var registryPresent = false;
            var networkPresent = false;

            try
            {
                var registryPath = $@"SYSTEM\CurrentControlSet\Enum\{instanceId}";
                using var regKey = Registry.LocalMachine.OpenSubKey(registryPath, false);
                registryPresent = regKey != null;
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"TUN wait: registry check failed for {tunName}: {ex.Message}");
            }

            try
            {
                networkPresent = adapters.Any(adapter =>
                    adapter.Name.Equals(tunName, StringComparison.OrdinalIgnoreCase) ||
                    adapter.Description.Contains(tunName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Logging.SaveLog($"TUN wait: network interface check failed for {tunName}: {ex.Message}");
            }

            result.Add(new TunDeviceState(tunName, instanceId, registryPresent, networkPresent));
        }

        return result;
    }

    private static string GetTunInstanceId(string tunName)
    {
        var sum = MD5.HashData(Encoding.UTF8.GetBytes(tunName));
        var guid = new Guid(sum);
        return $@"SWD\Wintun\{{{guid}}}";
    }

    private static string FormatTunDeviceState(TunDeviceState state)
    {
        return $"device={state.Name}, instance={state.InstanceId}, " +
               $"registry={state.RegistryPresent}, network={state.NetworkPresent}";
    }

    private sealed record TunDeviceState(
        string Name,
        string InstanceId,
        bool RegistryPresent,
        bool NetworkPresent);
}
