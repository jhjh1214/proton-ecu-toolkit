using System.Windows.Media;

namespace ProtonEcuToolkit.App.Gauges;

public enum GaugeZone
{
    Ok,
    Danger,
}

public static class GaugeMath
{
    /// <summary>Where <paramref name="value"/> sits between min and max, clamped to [0, 1].</summary>
    public static double ClampFraction(double value, double min, double max)
    {
        if (max <= min) return 0;
        return Math.Min(1, Math.Max(0, (value - min) / (max - min)));
    }

    /// <summary>Danger once value reaches the redline (e.g. RPM redline, an overheat threshold, etc). No redline set = always ok.</summary>
    public static GaugeZone ZoneFor(double value, double? redline) =>
        redline is { } r && value >= r ? GaugeZone.Danger : GaugeZone.Ok;

    public static Color ColorForZone(GaugeZone zone) => zone switch
    {
        GaugeZone.Ok => Color.FromRgb(0xf2, 0xf2, 0xf2),
        GaugeZone.Danger => Color.FromRgb(0xe5, 0x35, 0x35),
        _ => throw new ArgumentOutOfRangeException(nameof(zone)),
    };
}
