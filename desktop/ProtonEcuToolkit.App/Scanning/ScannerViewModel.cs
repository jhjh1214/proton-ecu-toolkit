using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonEcuToolkit.Core.Kwp;
using ProtonEcuToolkit.Core.Models;
using ProtonEcuToolkit.Core.Scanning;

namespace ProtonEcuToolkit.App.Scanning;

public enum ScanRangeOption
{
    KnownCandidates,
    NearbyRange,
    WideRange,
}

/// <summary>
/// Drives a PidScanner against the shared KwpSession: pauses the regular
/// dashboard poll loop for the duration, logs every attempt to a JSONL
/// file, and keeps only positive hits in the live results table (the full
/// evidence trail lives in the log file, not in memory).
/// </summary>
public partial class ScannerViewModel : ObservableObject, IDisposable
{
    private readonly KwpSession _session;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _scanCts;
    private ScanResultLogWriter? _logWriter;

    public ScannerViewModel(KwpSession session, Dispatcher dispatcher)
    {
        _session = session;
        _dispatcher = dispatcher;
        _session.StateChanged += OnSessionStateChanged;
    }

    public ObservableCollection<ScanResultEntry> Hits { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeDescription))]
    private ScanRangeOption _selectedRange = ScanRangeOption.KnownCandidates;

    public string RangeDescription => SelectedRange switch
    {
        ScanRangeOption.KnownCandidates => $"{ScanPlans.KnownCandidateIds.Count} candidates - seconds",
        ScanRangeOption.NearbyRange => $"{ScanPlans.NearbyRange.Count} candidates - ~3-4 min",
        ScanRangeOption.WideRange => $"{ScanPlans.WideRange.Count} candidates - ~30+ min",
        _ => "",
    };

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private int _scannedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _negativeCount;

    [ObservableProperty]
    private int _noResponseCount;

    [ObservableProperty]
    private int _malformedCount;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _lastLogFilePath;

    public bool CanStart => !IsScanning && _session.State == ConnectionState.Connected;

    public bool CanStop => IsScanning;

    public bool CanExport => Hits.Count > 0;

    [RelayCommand]
    private void SetRange(ScanRangeOption range) => SelectedRange = range;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartScanAsync()
    {
        var candidateIds = SelectedRange switch
        {
            ScanRangeOption.KnownCandidates => ScanPlans.KnownCandidateIds,
            ScanRangeOption.NearbyRange => ScanPlans.NearbyRange,
            ScanRangeOption.WideRange => ScanPlans.WideRange,
            _ => ScanPlans.KnownCandidateIds,
        };

        Hits.Clear();
        ScannedCount = 0;
        TotalCount = candidateIds.Count;
        NegativeCount = 0;
        NoResponseCount = 0;
        MalformedCount = 0;
        StatusMessage = $"Scanning {TotalCount} candidate(s) under Service 0x22...";
        IsScanning = true;
        NotifyCommandsCanExecuteChanged();

        _logWriter = new ScanResultLogWriter();
        LastLogFilePath = _logWriter.FilePath;

        var scanner = new PidScanner(
            sendRaw: (hex, timeoutMs) => _session.SendRawAsync(hex, timeoutMs),
            ensureAlive: () => _session.EnsureAliveAsync());
        scanner.ResultLogged += OnResultLogged;
        scanner.ProgressChanged += OnProgressChanged;
        scanner.StatusChanged += OnScannerStatusChanged;

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        // Core's async methods use ConfigureAwait(false) internally, so continuations
        // after these awaits are not guaranteed to stay on the UI thread - every
        // state mutation below is explicitly dispatched rather than assumed safe.
        await _session.PausePollingAsync().ConfigureAwait(false);
        try
        {
            await scanner.RunAsync(ScanPlans.ReadByCommonIdentifierSid, candidateIds, 250, token)
                .ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (StatusMessage?.StartsWith("Scanning", StringComparison.Ordinal) == true)
                {
                    StatusMessage = $"Scan complete: {ScannedCount}/{TotalCount} checked, {Hits.Count} positive.";
                }
            });
        }
        catch (OperationCanceledException)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                StatusMessage = $"Scan stopped: {ScannedCount}/{TotalCount} checked, {Hits.Count} positive.";
            });
        }
        finally
        {
            scanner.ResultLogged -= OnResultLogged;
            scanner.ProgressChanged -= OnProgressChanged;
            scanner.StatusChanged -= OnScannerStatusChanged;
            _logWriter?.Dispose();
            _logWriter = null;
            _session.ResumePolling();

            await _dispatcher.InvokeAsync(() =>
            {
                IsScanning = false;
                NotifyCommandsCanExecuteChanged();
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopScan() => _scanCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportCsv()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProtonEcuToolkit",
            "scan-results");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"hits-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.csv");

        using var writer = new StreamWriter(path);
        writer.WriteLine("Timestamp,Sid,Identifier,RequestHex,ResponseHex,Classification,Nrc,LatencyMs");
        foreach (var hit in Hits)
        {
            writer.WriteLine(
                $"{hit.Timestamp:O},{hit.Sid:X2},{hit.IdentifierHex},{hit.RequestHex},{hit.ResponseHex}," +
                $"{hit.Classification},{hit.Nrc},{hit.LatencyMs}");
        }

        StatusMessage = $"Exported {Hits.Count} hit(s) to {path}";
    }

    private void OnResultLogged(ScanResultEntry entry)
    {
        _dispatcher.Invoke(() =>
        {
            _logWriter?.WriteEntry(entry);
            switch (entry.Classification)
            {
                case ResponseClassification.Positive:
                    Hits.Add(entry);
                    ExportCsvCommand.NotifyCanExecuteChanged();
                    break;
                case ResponseClassification.Negative:
                    NegativeCount++;
                    break;
                case ResponseClassification.NoResponse:
                    NoResponseCount++;
                    break;
                case ResponseClassification.Malformed:
                    MalformedCount++;
                    break;
            }
        });
    }

    private void OnProgressChanged(int done, int total)
    {
        _dispatcher.Invoke(() =>
        {
            ScannedCount = done;
            TotalCount = total;
        });
    }

    private void OnScannerStatusChanged(string message) => _dispatcher.Invoke(() => StatusMessage = message);

    private void OnSessionStateChanged(ConnectionState state, string? detail) =>
        _dispatcher.Invoke(() => StartScanCommand.NotifyCanExecuteChanged());

    private void NotifyCommandsCanExecuteChanged()
    {
        StartScanCommand.NotifyCanExecuteChanged();
        StopScanCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => _session.StateChanged -= OnSessionStateChanged;
}
