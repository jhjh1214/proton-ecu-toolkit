using System.Windows.Controls;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// Numeric readout + sparkline. Everything is plain data binding to
/// PidGaugeViewModel - no code-behind logic needed, the same as the React
/// prototype's DigitalGauge.tsx needed no extra math beyond the view model.
/// </summary>
public partial class DigitalGaugeControl : UserControl
{
    public DigitalGaugeControl()
    {
        InitializeComponent();
    }
}
