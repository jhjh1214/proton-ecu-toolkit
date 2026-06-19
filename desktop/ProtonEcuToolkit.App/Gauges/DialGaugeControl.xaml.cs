using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProtonEcuToolkit.App.ViewModels;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// Draws the dial face and reacts to Fraction/ZoneBrush changes on the bound
/// PidGaugeViewModel. Ports the arc-by-angle-sampling approach from the
/// React prototype's DialGauge.tsx 1:1, just with WPF's Path/Line APIs
/// instead of SVG.
/// </summary>
public partial class DialGaugeControl : UserControl
{
    private const double CenterX = 60;
    private const double CenterY = 60;
    private const double Radius = 48;
    private const double NeedleLength = 42;

    public DialGaugeControl()
    {
        InitializeComponent();
        BackgroundArc.Data = BuildArcGeometry(180, 0);
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
            UpdateGeometry(newVm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PidGaugeViewModel vm) return;
        if (e.PropertyName is nameof(PidGaugeViewModel.Fraction) or nameof(PidGaugeViewModel.ZoneBrush))
        {
            UpdateGeometry(vm);
        }
    }

    private void UpdateGeometry(PidGaugeViewModel vm)
    {
        var needleDeg = 180 - vm.Fraction * 180;
        ValueArc.Data = BuildArcGeometry(180, needleDeg);
        ValueArc.Stroke = vm.ZoneBrush;
        Needle.Stroke = vm.ZoneBrush;

        var needleRad = needleDeg * Math.PI / 180;
        Needle.X2 = CenterX + NeedleLength * Math.Cos(needleRad);
        Needle.Y2 = CenterY - NeedleLength * Math.Sin(needleRad);
    }

    private static Geometry BuildArcGeometry(double startDeg, double endDeg, int segments = 48)
    {
        var figure = new PathFigure { IsClosed = false };
        for (var i = 0; i <= segments; i++)
        {
            var deg = startDeg + (endDeg - startDeg) * i / segments;
            var rad = deg * Math.PI / 180;
            var point = new Point(CenterX + Radius * Math.Cos(rad), CenterY - Radius * Math.Sin(rad));
            if (i == 0)
            {
                figure.StartPoint = point;
            }
            else
            {
                figure.Segments.Add(new LineSegment(point, true));
            }
        }
        return new PathGeometry([figure]);
    }
}
