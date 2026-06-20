using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ProtonEcuToolkit.App.ViewModels;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// A digital instrument cluster look (in the spirit of a sport-mode digital
/// rev counter): a segmented bar that lights up left-to-right as the value
/// rises, turning red past the redline, with a marker at the redline
/// position, plus a bold numeral readout.
/// </summary>
public partial class DigitalGaugeControl : UserControl
{
    private const int SegmentCount = 24;
    private const double BarWidth = 166;
    private const double BarHeight = 18;
    private const double SegmentGap = 2;

    private static readonly SolidColorBrush UnlitBrush = new(Color.FromRgb(0x26, 0x26, 0x26));
    private static readonly SolidColorBrush LitBrush = new(Color.FromRgb(0x4F, 0xC3, 0xF7));
    private static readonly SolidColorBrush DangerBrush = new(Color.FromRgb(0xE5, 0x35, 0x35));

    public DigitalGaugeControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PidGaugeViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is PidGaugeViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            BuildSegments(newVm);
            UpdateReadout(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PidGaugeViewModel vm) return;

        if (e.PropertyName is nameof(PidGaugeViewModel.Fraction) or nameof(PidGaugeViewModel.Redline)
            or nameof(PidGaugeViewModel.Min) or nameof(PidGaugeViewModel.Max))
        {
            BuildSegments(vm);
        }
        if (e.PropertyName is nameof(PidGaugeViewModel.DisplayText) or nameof(PidGaugeViewModel.ZoneBrush)
            or nameof(PidGaugeViewModel.Unit))
        {
            UpdateReadout(vm);
        }
    }

    private void BuildSegments(PidGaugeViewModel vm)
    {
        SegmentBarCanvas.Children.Clear();

        var segmentWidth = (BarWidth - SegmentGap * (SegmentCount - 1)) / SegmentCount;
        var litCount = (int)Math.Round(vm.Fraction * SegmentCount);
        var redlineFraction = vm.Redline is { } r ? GaugeMath.ClampFraction(r, vm.Min, vm.Max) : (double?)null;
        var redlineSegmentIndex = redlineFraction is { } rf ? (int)(rf * SegmentCount) : -1;

        for (var i = 0; i < SegmentCount; i++)
        {
            var lit = i < litCount;
            var isDanger = redlineSegmentIndex >= 0 && i >= redlineSegmentIndex;
            var fill = !lit ? UnlitBrush : isDanger ? DangerBrush : LitBrush;

            var rect = new Rectangle
            {
                Width = segmentWidth,
                Height = BarHeight,
                Fill = fill,
                RadiusX = 1.5,
                RadiusY = 1.5,
            };
            Canvas.SetLeft(rect, i * (segmentWidth + SegmentGap));
            Canvas.SetTop(rect, 0);
            SegmentBarCanvas.Children.Add(rect);
        }

        if (redlineSegmentIndex is >= 0 and < SegmentCount)
        {
            var marker = new Polygon
            {
                Points = [new Point(-4, -7), new Point(4, -7), new Point(0, -1)],
                Fill = DangerBrush,
            };
            Canvas.SetLeft(marker, redlineSegmentIndex * (segmentWidth + SegmentGap));
            Canvas.SetTop(marker, 0);
            SegmentBarCanvas.Children.Add(marker);
        }
    }

    private void UpdateReadout(PidGaugeViewModel vm)
    {
        ValueText.Text = vm.DisplayText;
        ValueText.Foreground = vm.ZoneBrush;
        UnitText.Text = vm.Unit;
    }
}
