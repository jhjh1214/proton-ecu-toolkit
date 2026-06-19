using System.Diagnostics;
using System.Windows;

namespace ProtonEcuToolkit.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

        DispatcherUnhandledException += (_, args) =>
        {
            Console.WriteLine($"[UNHANDLED] {args.Exception}");
            args.Handled = true;
        };
    }
}
