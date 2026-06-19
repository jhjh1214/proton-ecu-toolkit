namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// Display ranges/zones per known PID, for gauge scaling only. HANDOVER.md
/// doesn't specify these - they're reasonable defaults, not reverse-engineered
/// facts, and can be retuned once real readings are seen. An id with no entry
/// here (e.g. a Phase 2 scanner discovery) falls back to a plain 0-100 range.
/// </summary>
public static class GaugeConfig
{
    private static readonly Dictionary<string, GaugeRange> Ranges = new()
    {
        ["1101"] = new GaugeRange(-40, 120, WarnHigh: 110, DangerHigh: 118), // coolant temp, °C
        ["1104"] = new GaugeRange(0, 7000, WarnHigh: 6000, DangerHigh: 6500), // RPM
        ["110A"] = new GaugeRange(0, 100), // TPS, %
        ["1110"] = new GaugeRange(0, 16, DangerLow: 11, WarnLow: 12, WarnHigh: 14.5, DangerHigh: 15), // battery, V
        ["1113"] = new GaugeRange(0, 200), // vehicle speed, km/h
    };

    private static readonly GaugeRange Default = new(0, 100);

    public static GaugeRange GetRange(string pidId) => Ranges.GetValueOrDefault(pidId, Default);
}
