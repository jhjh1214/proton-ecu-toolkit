using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ProtonEcuToolkit.App.ViewModels;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// A proper analog instrument face: a 270-degree sweep with major/minor tick
/// marks, numeric labels, a red danger arc near the redline, and a tapered
/// needle - styled after a traditional automotive gauge (tach/speedo), with
/// a digital value readout inset in the open lower third of the dial.
/// </summary>
public partial class DialGaugeControl : UserControl
{
    private static readonly Point Center = new(85, 85);
    private const double OuterRadius = 78;
    private const double MajorTickInnerRadius = OuterRadius - 12;
    private const double MinorTickInnerRadius = OuterRadius - 6;
    private const double LabelRadius = 54;
    private const double DangerArcRadius = OuterRadius - 4;
    private const double NeedleLength = 58;
    private const double NeedleBaseWidth = 6;
    private const int TargetMajorTicks = 7;
    private const int MinorSubdivisions = 5;

    /// <summary>225deg (lower-left) sweeping clockwise through the top to -45deg (lower-right) - a classic 270-degree gauge.</summary>
    private const double StartAngleDeg = 225;
    private const double EndAngleDeg = -45;

    public DialGaugeControl()
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
            RebuildFace(newVm);
            UpdateNeedleAndReadout(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PidGaugeViewModel vm) return;

        if (e.PropertyName is nameof(PidGaugeViewModel.Min) or nameof(PidGaugeViewModel.Max)
            or nameof(PidGaugeViewModel.Redline))
        {
            RebuildFace(vm);
            UpdateNeedleAndReadout(vm);
        }
        else if (e.PropertyName is nameof(PidGaugeViewModel.Fraction) or nameof(PidGaugeViewModel.ZoneBrush)
                 or nameof(PidGaugeViewModel.DisplayText) or nameof(PidGaugeViewModel.Unit))
        {
            UpdateNeedleAndReadout(vm);
        }
    }

    private void RebuildFace(PidGaugeViewModel vm)
    {
        TicksCanvas.Children.Clear();

        var min = vm.Min;
        var max = vm.Max;
        if (max <= min) return;

        var step = NiceTickInterval(max - min, TargetMajorTicks);

        var firstMajorIndex = (int)Math.Ceiling(min / step);
        var lastMajorIndex = (int)Math.Floor(max / step);
        for (var i = firstMajorIndex; i <= lastMajorIndex; i++)
        {
            AddTick(i * step, min, max, major: true);
        }

        var minorStep = step / MinorSubdivisions;
        var firstMinorIndex = (int)Math.Floor(min / minorStep);
        var lastMinorIndex = (int)Math.Ceiling(max / minorStep);
        for (var i = firstMinorIndex; i <= lastMinorIndex; i++)
        {
            if (i % MinorSubdivisions == 0) continue; // coincides with a major tick
            var v = i * minorStep;
            if (v < min - 1e-9 || v > max + 1e-9) continue;
            AddTick(v, min, max, major: false);
        }

        UpdateDangerArc(vm);
    }

    private void AddTick(double value, double min, double max, bool major)
    {
        var fraction = GaugeMath.ClampFraction(value, min, max);
        var angleDeg = StartAngleDeg + (EndAngleDeg - StartAngleDeg) * fraction;
        var (dirX, dirY) = Direction(angleDeg);

        var innerR = major ? MajorTickInnerRadius : MinorTickInnerRadius;
        var p1 = new Point(Center.X + dirX * OuterRadius, Center.Y + dirY * OuterRadius);
        var p2 = new Point(Center.X + dirX * innerR, Center.Y + dirY * innerR);

        TicksCanvas.Children.Add(new Line
        {
            X1 = p1.X,
            Y1 = p1.Y,
            X2 = p2.X,
            Y2 = p2.Y,
            Stroke = Brushes.WhiteSmoke,
            StrokeThickness = major ? 2 : 1,
        });

        if (!major) return;

        var labelPoint = new Point(Center.X + dirX * LabelRadius, Center.Y + dirY * LabelRadius);
        var label = new TextBlock
        {
            Text = FormatTickLabel(value, max),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.WhiteSmoke,
        };
        var size = Measure(label);
        Canvas.SetLeft(label, labelPoint.X - size.Width / 2);
        Canvas.SetTop(label, labelPoint.Y - size.Height / 2);
        TicksCanvas.Children.Add(label);
    }

    private void UpdateDangerArc(PidGaugeViewModel vm)
    {
        if (vm.Redline is not { } redline || redline >= vm.Max)
        {
            DangerArc.Data = null;
            return;
        }

        var startFraction = GaugeMath.ClampFraction(redline, vm.Min, vm.Max);
        var startAngle = StartAngleDeg + (EndAngleDeg - StartAngleDeg) * startFraction;
        DangerArc.Data = BuildArcGeometry(startAngle, EndAngleDeg, DangerArcRadius);
    }

    private void UpdateNeedleAndReadout(PidGaugeViewModel vm)
    {
        var angleDeg = StartAngleDeg + (EndAngleDeg - StartAngleDeg) * vm.Fraction;
        Needle.Points = BuildNeedlePoints(angleDeg);
        Needle.Fill = vm.ZoneBrush;

        ValueText.Text = vm.DisplayText;
        ValueText.Foreground = vm.ZoneBrush;
        UnitText.Text = vm.Unit;

        var valueSize = Measure(ValueText);
        Canvas.SetLeft(ValueText, Center.X - valueSize.Width / 2);
        Canvas.SetTop(ValueText, Center.Y + 26);

        var unitSize = Measure(UnitText);
        Canvas.SetLeft(UnitText, Center.X - unitSize.Width / 2);
        Canvas.SetTop(UnitText, Center.Y + 50);
    }

    private static PointCollection BuildNeedlePoints(double angleDeg)
    {
        var (dirX, dirY) = Direction(angleDeg);
        var perpX = -dirY;
        var perpY = dirX;

        var tip = new Point(Center.X + dirX * NeedleLength, Center.Y + dirY * NeedleLength);
        var baseLeft = new Point(
            Center.X + perpX * NeedleBaseWidth / 2,
            Center.Y + perpY * NeedleBaseWidth / 2);
        var baseRight = new Point(
            Center.X - perpX * NeedleBaseWidth / 2,
            Center.Y - perpY * NeedleBaseWidth / 2);

        return new PointCollection([baseLeft, tip, baseRight]);
    }

    private static Geometry BuildArcGeometry(double startDeg, double endDeg, double radius, int segments = 48)
    {
        var figure = new PathFigure { IsClosed = false };
        for (var i = 0; i <= segments; i++)
        {
            var deg = startDeg + (endDeg - startDeg) * i / segments;
            var (dirX, dirY) = Direction(deg);
            var point = new Point(Center.X + dirX * radius, Center.Y + dirY * radius);
            if (i == 0) figure.StartPoint = point;
            else figure.Segments.Add(new LineSegment(point, true));
        }
        return new PathGeometry([figure]);
    }

    private static (double X, double Y) Direction(double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180;
        return (Math.Cos(rad), -Math.Sin(rad));
    }

    /// <summary>Rounds a raw tick step to a "nice" 1/2/5 x 10^n value, targeting ~targetTicks major ticks.</summary>
    private static double NiceTickInterval(double range, int targetTicks)
    {
        if (range <= 0) return 1;
        var rawStep = range / targetTicks;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var residual = rawStep / magnitude;
        var niceResidual = residual switch
        {
            <= 1.5 => 1,
            <= 3 => 2,
            <= 7 => 5,
            _ => 10,
        };
        return niceResidual * magnitude;
    }

    /// <summary>Formats large-range labels (e.g. RPM) as thousands ("0".."7") to match a classic tachometer face.</summary>
    private static string FormatTickLabel(double value, double max)
    {
        if (Math.Abs(max) >= 1000)
        {
            var scaled = value / 1000;
            return FormatNumber(scaled);
        }
        return FormatNumber(value);
    }

    private static string FormatNumber(double value) =>
        value == Math.Round(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);

    private static Size Measure(TextBlock block)
    {
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return block.DesiredSize;
    }
}
