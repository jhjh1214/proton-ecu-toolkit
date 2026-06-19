namespace ProtonEcuToolkit.App.Gauges;

public sealed record GaugeRange(
    double Min,
    double Max,
    double? WarnLow = null,
    double? WarnHigh = null,
    double? DangerLow = null,
    double? DangerHigh = null);
