namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// Default min/max/redline per known PID, used only to pre-fill a gauge when
/// it's first added or its PID is changed - the user can edit any of these
/// per gauge afterward, and that edit is what gets persisted (see
/// GaugeSettingsStore). HANDOVER.md doesn't specify these - they're
/// reasonable starting points, not reverse-engineered facts.
/// </summary>
public static class GaugeConfig
{
    private static readonly Dictionary<string, (double Min, double Max, double? Redline)> Defaults = new()
    {
        ["1101"] = (-40, 120, 115), // coolant temp, °C
        ["1104"] = (0, 7000, 6500), // RPM
        ["110A"] = (0, 100, null), // TPS, %
        ["1110"] = (0, 16, 15), // battery, V
        ["1113"] = (0, 200, null), // vehicle speed, km/h
    };

    public static (double Min, double Max, double? Redline) GetDefaults(string pidId) =>
        Defaults.GetValueOrDefault(pidId, (0, 100, null));
}
