using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonEcuToolkit.App.Gauges;
using ProtonEcuToolkit.Core.Kwp;
using ProtonEcuToolkit.Core.Models;

namespace ProtonEcuToolkit.App.ViewModels;

/// <summary>
/// One gauge tile's full state: which PID it shows, its user-editable
/// min/max/redline/theme, and the live reading. Generalizes to any known
/// PID - a custom dashboard is just a list of these, freely added, removed,
/// and reconfigured, then persisted via GaugeSettingsStore.
/// </summary>
public partial class PidGaugeViewModel : ObservableObject
{
    private const int MaxHistoryPoints = 30;
    private const double SparklineWidth = 140;
    private const double SparklineHeight = 32;

    private readonly List<double> _history = [];

    public PidGaugeViewModel(GaugeSettings settings)
    {
        GaugeId = settings.GaugeId;
        _min = settings.Min;
        _max = settings.Max;
        _redline = settings.Redline;
        _theme = settings.Theme;
        _id = settings.PidId;

        var pid = KnownPids.All.FirstOrDefault(p => p.Id == settings.PidId) ?? KnownPids.All[0];
        _name = pid.Name;
        _unit = pid.Unit;
    }

    public static PidGaugeViewModel CreateDefault(string pidId)
    {
        var (min, max, redline) = GaugeConfig.GetDefaults(pidId);
        var settings = new GaugeSettings(Guid.NewGuid().ToString("N"), pidId, min, max, redline, GaugeTheme.Dial);
        return new PidGaugeViewModel(settings);
    }

    public string GaugeId { get; }

    public IReadOnlyList<PidDefinition> AvailablePids => KnownPids.All;

    public event Action<PidGaugeViewModel>? RemoveRequested;

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _unit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction))]
    private double _min;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fraction))]
    private double _max;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoneBrush))]
    [NotifyPropertyChangedFor(nameof(RedlineText))]
    private double? _redline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialTheme))]
    [NotifyPropertyChangedFor(nameof(IsDigitalTheme))]
    private GaugeTheme _theme;

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

    public double DisplayValue => Value ?? Min;

    public double Fraction => GaugeMath.ClampFraction(DisplayValue, Min, Max);

    public Brush ZoneBrush => HasValue
        ? new SolidColorBrush(GaugeMath.ColorForZone(GaugeMath.ZoneFor(DisplayValue, Redline)))
        : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    public string DisplayText => Error is not null ? "ERR" : Value is { } v ? v.ToString("F1") : "--";

    public bool IsDialTheme
    {
        get => Theme == GaugeTheme.Dial;
        set
        {
            if (value) Theme = GaugeTheme.Dial;
        }
    }

    public bool IsDigitalTheme
    {
        get => Theme == GaugeTheme.Digital;
        set
        {
            if (value) Theme = GaugeTheme.Digital;
        }
    }

    public string RedlineText
    {
        get => Redline?.ToString(CultureInfo.InvariantCulture) ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Redline = null;
            }
            else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                Redline = parsed;
            }
        }
    }

    public GaugeSettings ToSettings() => new(GaugeId, Id, Min, Max, Redline, Theme);

    [RelayCommand]
    private void Remove() => RemoveRequested?.Invoke(this);

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

    /// <summary>Changing which PID this gauge shows resets its range to that PID's defaults and clears history.</summary>
    partial void OnIdChanged(string value)
    {
        var pid = KnownPids.All.FirstOrDefault(p => p.Id == value);
        if (pid is null) return;

        Name = pid.Name;
        Unit = pid.Unit;

        var (min, max, redline) = GaugeConfig.GetDefaults(value);
        Min = min;
        Max = max;
        Redline = redline;

        _history.Clear();
        SparklinePoints = ComputeSparklinePoints();
        Value = null;
        Error = null;
        RawHex = null;
    }

    private PointCollection ComputeSparklinePoints()
    {
        var points = new PointCollection();
        if (_history.Count < 2) return points;

        for (var i = 0; i < _history.Count; i++)
        {
            var x = (double)i / (_history.Count - 1) * SparklineWidth;
            var y = SparklineHeight - GaugeMath.ClampFraction(_history[i], Min, Max) * (SparklineHeight - 2);
            points.Add(new Point(x, y));
        }

        return points;
    }
}
