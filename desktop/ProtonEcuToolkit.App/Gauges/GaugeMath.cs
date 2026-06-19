using System.Windows.Media;

namespace ProtonEcuToolkit.App.Gauges;

public enum GaugeZone
{
    Ok,
    Warn,
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

    public static GaugeZone ZoneFor(double value, GaugeRange range)
    {
        if ((range.DangerLow is { } dl && value <= dl) || (range.DangerHigh is { } dh && value >= dh))
        {
            return GaugeZone.Danger;
        }
        if ((range.WarnLow is { } wl && value <= wl) || (range.WarnHigh is { } wh && value >= wh))
        {
            return GaugeZone.Warn;
        }
        return GaugeZone.Ok;
    }

    public static Color ColorForZone(GaugeZone zone) => zone switch
    {
        GaugeZone.Ok => Color.FromRgb(0x4c, 0xaf, 0x50),
        GaugeZone.Warn => Color.FromRgb(0xe0, 0xc3, 0x4c),
        GaugeZone.Danger => Color.FromRgb(0xe5, 0x53, 0x53),
        _ => throw new ArgumentOutOfRangeException(nameof(zone)),
    };
}
