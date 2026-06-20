using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtonEcuToolkit.Core.Kwp;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>Loads/saves the user's custom dashboard layout to a small JSON file in %AppData%.</summary>
public static class GaugeSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProtonEcuToolkit",
        "dashboard.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<GaugeSettings> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return DefaultGauges();
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<List<GaugeSettings>>(json, JsonOptions);
            return loaded is { Count: > 0 } ? loaded : DefaultGauges();
        }
        catch
        {
            return DefaultGauges();
        }
    }

    public static void Save(IEnumerable<GaugeSettings> gauges)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(gauges.ToList(), JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // best-effort - a failed save shouldn't crash the app
        }
    }

    private static List<GaugeSettings> DefaultGauges() =>
        KnownPids.All
            .Select(pid =>
            {
                var (min, max, redline) = GaugeConfig.GetDefaults(pid.Id);
                return new GaugeSettings(Guid.NewGuid().ToString("N"), pid.Id, min, max, redline, GaugeTheme.Dial);
            })
            .ToList();
}
