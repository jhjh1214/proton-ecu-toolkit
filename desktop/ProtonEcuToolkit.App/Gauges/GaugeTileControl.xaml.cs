using System.Windows;
using System.Windows.Controls;

namespace ProtonEcuToolkit.App.Gauges;

/// <summary>
/// One gauge slot: renders as a dial or digital gauge per its own Theme
/// (not a global app-wide setting), with a gear icon that opens a small
/// popup editor for PID/min/max/redline/theme.
/// </summary>
public partial class GaugeTileControl : UserControl
{
    public GaugeTileControl()
    {
        InitializeComponent();
    }

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        // Popups don't reliably inherit DataContext through the visual tree -
        // set it explicitly rather than relying on that.
        EditorPopup.DataContext = DataContext;
        EditorPopup.IsOpen = !EditorPopup.IsOpen;
    }
}
