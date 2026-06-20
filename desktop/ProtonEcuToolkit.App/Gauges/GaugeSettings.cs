namespace ProtonEcuToolkit.App.Gauges;

/// <summary>Persisted, user-editable configuration for one gauge tile.</summary>
public sealed record GaugeSettings(
    string GaugeId,
    string PidId,
    double Min,
    double Max,
    double? Redline,
    GaugeTheme Theme);
