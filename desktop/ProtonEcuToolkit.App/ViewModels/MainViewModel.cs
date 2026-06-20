using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonEcuToolkit.App.Gauges;
using ProtonEcuToolkit.App.Scanning;
using ProtonEcuToolkit.Core.Kwp;
using ProtonEcuToolkit.Core.Models;
using ProtonEcuToolkit.Core.Transport;

namespace ProtonEcuToolkit.App.ViewModels;

/// <summary>
/// Owns one KwpSession in-process - no server, no IPC, no serialization.
/// Replaces what used to be useServerConnection.ts plus the entire REST/WS
/// API layer in the web prototype. Also owns the user's custom dashboard
/// layout (which gauges exist, what each shows) and persists it.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxLogLines = 50;

    private static readonly HashSet<string> PersistedGaugePropertyNames =
    [
        nameof(PidGaugeViewModel.Id),
        nameof(PidGaugeViewModel.Min),
        nameof(PidGaugeViewModel.Max),
        nameof(PidGaugeViewModel.Redline),
        nameof(PidGaugeViewModel.Theme),
    ];

    private readonly KwpSession _session = new();
    private readonly Dispatcher _dispatcher;

    public MainViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _session.StateChanged += OnSessionStateChanged;
        _session.PidsUpdated += OnSessionPidsUpdated;

        foreach (var settings in GaugeSettingsStore.Load())
        {
            RegisterGauge(new PidGaugeViewModel(settings));
        }

        Scanner = new ScannerViewModel(_session, _dispatcher);

        RefreshPorts();
    }

    public ScannerViewModel Scanner { get; }

    public ObservableCollection<string> Ports { get; } = [];

    public ObservableCollection<PidGaugeViewModel> PidGauges { get; } = [];

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _selectedPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ConnectionState _state = ConnectionState.Disconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _statusDetail;

    public string StatusText => StatusDetail is not null ? $"{State} ({StatusDetail})" : State.ToString();

    [ObservableProperty]
    private string? _dtcScanResultText;

    [ObservableProperty]
    private bool _dtcScanResultIsError;

    [ObservableProperty]
    private string? _dtcClearResultText;

    [ObservableProperty]
    private bool _dtcClearResultIsError;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsConnected => State == ConnectionState.Connected;

    public bool CanConnect =>
        !IsBusy && State is ConnectionState.Disconnected or ConnectionState.Error && SelectedPort is not null;

    [RelayCommand]
    private void AddGauge()
    {
        RegisterGauge(PidGaugeViewModel.CreateDefault(KnownPids.All[0].Id));
        PersistGauges();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        Ports.Clear();
        foreach (var name in SerialTransport.ListPortNames())
        {
            Ports.Add(name);
        }
        SelectedPort ??= Ports.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        Log($"Connecting to {SelectedPort}...");
        IsBusy = true;
        NotifyCommandsCanExecuteChanged();
        try
        {
            await _session.ConnectAsync(SelectedPort);
        }
        catch (Exception ex)
        {
            Log($"Connect failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyCommandsCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task DisconnectAsync()
    {
        await _session.DisconnectAsync();
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task ScanDtcsAsync()
    {
        var result = await _session.ScanDtcsAsync();
        DtcScanResultIsError = !result.Positive;
        DtcScanResultText = result.Positive
            ? $"raw response 58{result.RawHex} (undecoded - format not yet reverse-engineered)"
            : result.Error;
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task ClearDtcsAsync()
    {
        var confirmed = MessageBox.Show(
            "Erase all stored diagnostic trouble codes? This cannot be undone.",
            "Confirm clear",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
        if (!confirmed) return;

        var result = await _session.ClearDtcsAsync();
        DtcClearResultIsError = !result.Positive;
        DtcClearResultText = result.Positive ? "codes erased" : result.Error;
    }

    private void OnSessionStateChanged(ConnectionState state, string? detail)
    {
        _dispatcher.Invoke(() =>
        {
            State = state;
            StatusDetail = detail;
            Log(detail is not null ? $"status: {state} ({detail})" : $"status: {state}");
            OnPropertyChanged(nameof(IsConnected));
            NotifyCommandsCanExecuteChanged();
        });
    }

    private void OnSessionPidsUpdated(List<PidReading> readings)
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var reading in readings)
            {
                foreach (var gauge in PidGauges.Where(g => g.Id == reading.Id))
                {
                    gauge.ApplyReading(reading);
                }
            }
        });
    }

    private void RegisterGauge(PidGaugeViewModel gauge)
    {
        gauge.RemoveRequested += OnGaugeRemoveRequested;
        gauge.PropertyChanged += OnGaugeSettingsChanged;
        PidGauges.Add(gauge);
    }

    private void OnGaugeRemoveRequested(PidGaugeViewModel gauge)
    {
        gauge.RemoveRequested -= OnGaugeRemoveRequested;
        gauge.PropertyChanged -= OnGaugeSettingsChanged;
        PidGauges.Remove(gauge);
        PersistGauges();
    }

    private void OnGaugeSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || !PersistedGaugePropertyNames.Contains(e.PropertyName)) return;
        PersistGauges();
    }

    private void PersistGauges() => GaugeSettingsStore.Save(PidGauges.Select(g => g.ToSettings()));

    private void NotifyCommandsCanExecuteChanged()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        ScanDtcsCommand.NotifyCanExecuteChanged();
        ClearDtcsCommand.NotifyCanExecuteChanged();
    }

    private void Log(string line)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogLines.Add($"[{timestamp}] {line}");
        while (LogLines.Count > MaxLogLines)
        {
            LogLines.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _session.StateChanged -= OnSessionStateChanged;
        _session.PidsUpdated -= OnSessionPidsUpdated;
        Scanner.Dispose();
        _session.Dispose();
    }
}
