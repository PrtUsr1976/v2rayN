namespace ServiceLib.Common;

internal sealed record TunStartupSettings(int ObservationSeconds)
{
    private const int DefaultObservationSeconds = 20;
    private const int MaxObservationSeconds = 300;
    private const string FileName = "finetunes.ini";
    private const string ObservationKey = "TunStartObservationSeconds";

    public static TunStartupSettings LoadOrCreate()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                WriteDefaultFile(filePath);
                Logging.SaveLog($"TUN startup settings: created {filePath} with {ObservationKey}={DefaultObservationSeconds}.");
                return new TunStartupSettings(DefaultObservationSeconds);
            }

            foreach (var sourceLine in File.ReadLines(filePath))
            {
                var line = sourceLine.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                if (!key.Equals(ObservationKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[(separator + 1)..].Trim();
                if (int.TryParse(value, out var seconds)
                    && seconds >= 1
                    && seconds <= MaxObservationSeconds)
                {
                    Logging.SaveLog($"TUN startup settings: {ObservationKey}={seconds} from {filePath}.");
                    return new TunStartupSettings(seconds);
                }

                Logging.SaveLog(
                    $"TUN startup settings: invalid {ObservationKey}='{value}' in {filePath}; " +
                    $"using {DefaultObservationSeconds} seconds.");
                return new TunStartupSettings(DefaultObservationSeconds);
            }

            File.AppendAllText(
                filePath,
                $"{Environment.NewLine}{ObservationKey}={DefaultObservationSeconds}{Environment.NewLine}",
                new UTF8Encoding(false));
            Logging.SaveLog(
                $"TUN startup settings: added missing {ObservationKey}={DefaultObservationSeconds} to {filePath}.");
            return new TunStartupSettings(DefaultObservationSeconds);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("TunStartupSettings", ex);
            Logging.SaveLog(
                $"TUN startup settings: using {DefaultObservationSeconds} seconds because {filePath} could not be read or created.");
            return new TunStartupSettings(DefaultObservationSeconds);
        }
    }

    private static void WriteDefaultFile(string filePath)
    {
        var content =
            "; v2rayN fine tuning settings" + Environment.NewLine +
            "; Time used to verify that a newly started TUN core remains alive." + Environment.NewLine +
            $"{ObservationKey}={DefaultObservationSeconds}" + Environment.NewLine;

        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }
}
