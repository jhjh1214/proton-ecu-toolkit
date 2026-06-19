using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonEcuToolkit.App.Gauges;
using ProtonEcuToolkit.Core.Models;

namespace ProtonEcuToolkit.App.ViewModels;

/// <summary>
/// Per-PID bindable state for both gauge themes. Generalizes to any PID, not
/// just the 5 known ones - a future Phase 2 scanner discovery just needs a
/// new instance of this, no new view-model type.
/// </summary>
public partial class PidGaugeViewModel : ObservableObject
{
    private const int MaxHistoryPoints = 30;
    private const double SparklineWidth = 140;
    private const double SparklineHeight = 32;

    private readonly GaugeRange _range;
    private readonly List<double> _history = [];

    public PidGaugeViewModel(string id, string name, string unit)
    {
        Id = id;
        Name = name;
        Unit = unit;
        _range = GaugeConfig.GetRange(id);
    }

    public string Id { get; }
    public string Name { get; }
    public string Unit { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(Fraction))]
    [NotifyPropertyChangedFor(nameof(ZoneBrush))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double? _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    [NotifyPropertyChangedFor(nameof(ZoneBrush))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string? _error;

    [ObservableProperty]
    private string? _rawHex;

    [ObservableProperty]
    private PointCollection _sparklinePoints = new();

    public bool HasValue => Value is not null && Error is null;

    public double DisplayValue => Value ?? _range.Min;

    public double Fraction => GaugeMath.ClampFraction(DisplayValue, _range.Min, _range.Max);

    public Brush ZoneBrush => HasValue
        ? new SolidColorBrush(GaugeMath.ColorForZone(GaugeMath.ZoneFor(DisplayValue, _range)))
        : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    public string DisplayText => Error is not null ? "ERR" : Value is { } v ? v.ToString("F1") : "--";

    public void ApplyReading(PidReading reading)
    {
        Value = reading.Value;
        Error = reading.Error;
        RawHex = reading.RawHex;

        if (reading.Value is { } v)
        {
            _history.Add(v);
            if (_history.Count > MaxHistoryPoints)
            {
                _history.RemoveAt(0);
            }
        }

        SparklinePoints = ComputeSparklinePoints();
    }

    private PointCollection ComputeSparklinePoints()
    {
        var points = new PointCollection();
        if (_history.Count < 2) return points;

        for (var i = 0; i < _history.Count; i++)
        {
            var x = (double)i / (_history.Count - 1) * SparklineWidth;
            var y = SparklineHeight - GaugeMath.ClampFraction(_history[i], _range.Min, _range.Max) * (SparklineHeight - 2);
            points.Add(new System.Windows.Point(x, y));
        }

        return points;
    }
}
