using ProtonEcuToolkit.Core.Elm;
using ProtonEcuToolkit.Core.Models;
using ProtonEcuToolkit.Core.Transport;

namespace ProtonEcuToolkit.Core.Kwp;

/// <summary>
/// Owns the cold-start init sequence (§3.2), the keep-alive/recovery pattern
/// (§3.3), and polling the 5 known PIDs (§3.4). Knows what KWP2000 services
/// and CIDs mean; talks to the ECU only through ElmClient, which has no idea
/// what any of this means in protocol terms.
/// </summary>
public sealed class KwpSession : IDisposable
{
    private const int SettleDelayMs = 2000;

    /// <summary>Well under the 2-4s "tester present" ceiling from HANDOVER.md §3.3.</summary>
    private const int PollIntervalMs = 1000;

    private ConnectionState _state = ConnectionState.Disconnected;
    private SerialTransport? _transport;
    private ElmClient? _elm;
    private PidReader? _pidReader;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private bool _polling;
    private string? _connectedPath;

    public event Action<ConnectionState, string?>? StateChanged;

    public event Action<List<PidReading>>? PidsUpdated;

    public ConnectionState State => _state;

    public string? Port => _connectedPath;

    public async Task ConnectAsync(string path)
    {
        if (_state is ConnectionState.Connecting or ConnectionState.Connected)
        {
            throw new InvalidOperationException($"KwpSession: cannot connect while state is \"{_state}\"");
        }

        SetState(ConnectionState.Connecting, $"Opening {path}");
        var transport = new SerialTransport(path);
        _transport = transport;
        _elm = new ElmClient(transport);
        _pidReader = new PidReader(_elm);

        try
        {
            await transport.OpenAsync().ConfigureAwait(false);
            await RunInitSequenceAsync().ConfigureAwait(false);
            _connectedPath = path;
            SetState(ConnectionState.Connected);
            StartPolling();
        }
        catch (Exception err)
        {
            await SafeCloseAsync(transport).ConfigureAwait(false);
            _transport = null;
            _elm = null;
            _pidReader = null;
            SetState(ConnectionState.Error, err.Message);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        await StopPollingAsync().ConfigureAwait(false);
        if (_transport is { } transport)
        {
            await SafeCloseAsync(transport).ConfigureAwait(false);
        }
        _transport = null;
        _elm = null;
        _pidReader = null;
        _connectedPath = null;
        SetState(ConnectionState.Disconnected);
    }

    /// <summary>§3.7 ReadDiagnosticTroubleCodesByStatus. Codes come back as a raw, undecoded hex blob (see Dtc.cs).</summary>
    public Task<DtcActionResult> ScanDtcsAsync() =>
        RunDtcRequestAsync(Dtc.BuildDtcScanRequest(), Dtc.ParseDtcScanResponse);

    /// <summary>§3.7 ClearDiagnosticInformation.</summary>
    public Task<DtcActionResult> ClearDtcsAsync() =>
        RunDtcRequestAsync(Dtc.BuildDtcClearRequest(), Dtc.ParseDtcClearResponse);

    /// <summary>
    /// Sends an arbitrary already-built request hex string and returns the
    /// raw response, for the PID/CID scanner. Has no idea what the bytes
    /// mean - same level of knowledge as ElmClient, just gated on being connected.
    /// </summary>
    public Task<string> SendRawAsync(string requestHex, int timeoutMs) =>
        _state != ConnectionState.Connected
            ? throw new InvalidOperationException($"KwpSession: cannot send while state is \"{_state}\"")
            : RequireElm().SendCommandAsync(requestHex, timeoutMs);

    /// <summary>Stops the regular dashboard poll loop so the scanner has the transport to itself.</summary>
    public Task PausePollingAsync() => _state == ConnectionState.Connected ? StopPollingAsync() : Task.CompletedTask;

    /// <summary>Resumes the regular dashboard poll loop after a scan finishes.</summary>
    public void ResumePolling()
    {
        if (_state == ConnectionState.Connected) StartPolling();
    }

    /// <summary>Cheap re-ping, escalating to the full §3.3 recovery ladder if needed. Used as the scanner's keep-alive check.</summary>
    public Task<bool> EnsureAliveAsync() => RecoverAsync();

    private async Task<DtcActionResult> RunDtcRequestAsync(string requestHex, Func<string, DtcResponse?> parse)
    {
        if (_state != ConnectionState.Connected)
        {
            throw new InvalidOperationException(
                $"KwpSession: cannot send a DTC request while state is \"{_state}\"");
        }

        var timestamp = DateTimeOffset.UtcNow;
        try
        {
            var raw = await RequireElm().SendCommandAsync(requestHex, 2000).ConfigureAwait(false);
            var parsed = parse(raw);
            if (parsed is null)
            {
                return new DtcActionResult(false, raw, null, timestamp, $"Unrecognized response: \"{raw}\"");
            }
            if (!parsed.Positive)
            {
                var nrc = parsed.Nrc is { } nrcValue ? $"0x{nrcValue:x}" : "unknown";
                return new DtcActionResult(false, raw, parsed.Nrc, timestamp, $"Negative response, NRC={nrc}");
            }
            return new DtcActionResult(true, parsed.DataHex, null, timestamp);
        }
        catch (Exception err)
        {
            return new DtcActionResult(false, "", null, timestamp, err.Message);
        }
    }

    /// <summary>Cold-start init sequence per HANDOVER.md §3.2.</summary>
    private async Task RunInitSequenceAsync()
    {
        var elm = RequireElm();

        // The original app sends ATZ twice and doesn't check the response.
        await TryAsync(() => elm.SendCommandAsync("ATZ", 3000)).ConfigureAwait(false);
        await TryAsync(() => elm.SendCommandAsync("ATZ", 3000)).ConfigureAwait(false);

        await elm.SendCommandUntilOkAsync("ATE0").ConfigureAwait(false);
        await elm.SendCommandUntilOkAsync("ATH0").ConfigureAwait(false);
        await elm.SendCommandUntilOkAsync("ATSP5").ConfigureAwait(false);
        await elm.SendCommandUntilOkAsync("ATSH8101F1").ConfigureAwait(false);

        await Task.Delay(SettleDelayMs).ConfigureAwait(false);

        var ok = await SendTestPingAsync().ConfigureAwait(false);
        if (!ok)
        {
            throw new InvalidOperationException(
                "KwpSession: init sequence finished but test ping \"22111F\" got no positive response");
        }
    }

    /// <summary>§3.2/§3.3 connectivity check: success = response contains "62".</summary>
    private async Task<bool> SendTestPingAsync()
    {
        try
        {
            var raw = await RequireElm()
                .SendCommandAsync(Protocol.BuildReadByCidRequest(KnownPids.TestPingCid), 1000)
                .ConfigureAwait(false);
            return Protocol.ContainsPositiveResponseMarker(raw);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Keep-alive recovery ladder per §3.3: re-ping -&gt; force fast init -&gt; full restart.</summary>
    private async Task<bool> RecoverAsync()
    {
        if (await SendTestPingAsync().ConfigureAwait(false)) return true;

        await TryAsync(() => RequireElm().SendCommandAsync("ATFI", 3000)).ConfigureAwait(false);
        if (await SendTestPingAsync().ConfigureAwait(false)) return true;

        try
        {
            var elm = RequireElm();
            await elm.SendCommandUntilOkAsync("ATSP5").ConfigureAwait(false);
            await elm.SendCommandUntilOkAsync("ATSH8101F1").ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        return await SendTestPingAsync().ConfigureAwait(false);
    }

    private void StartPolling()
    {
        StopPollingSync();
        var cts = new CancellationTokenSource();
        _pollCts = cts;
        _pollTask = Task.Run(() => PollLoopAsync(cts.Token));
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollIntervalMs));
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await PollOnceAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on disconnect / recovery failure
        }
    }

    private async Task StopPollingAsync()
    {
        if (_pollCts is { } cts)
        {
            cts.Cancel();
            if (_pollTask is { } task)
            {
                await task.ConfigureAwait(false);
            }
            cts.Dispose();
        }
        _pollCts = null;
        _pollTask = null;
    }

    private void StopPollingSync()
    {
        _pollCts?.Cancel();
        _pollCts = null;
        _pollTask = null;
    }

    private async Task PollOnceAsync()
    {
        if (_polling || _state != ConnectionState.Connected || _pidReader is null) return;
        _polling = true;
        try
        {
            var readings = await _pidReader.ReadAllAsync(KnownPids.All).ConfigureAwait(false);
            PidsUpdated?.Invoke(readings);

            var sessionDropped = readings.All(reading => reading.Error is not null);
            if (sessionDropped)
            {
                var recovered = await RecoverAsync().ConfigureAwait(false);
                if (!recovered)
                {
                    StopPollingSync();
                    SetState(ConnectionState.Error, "Lost KWP session and recovery (§3.3) failed");
                }
            }
        }
        finally
        {
            _polling = false;
        }
    }

    private ElmClient RequireElm() => _elm ?? throw new InvalidOperationException("KwpSession: not connected");

    private void SetState(ConnectionState state, string? detail = null)
    {
        _state = state;
        StateChanged?.Invoke(state, detail);
    }

    private static async Task TryAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch
        {
            // swallow, mirrors the TS `.catch(() => undefined)` pattern
        }
    }

    private static async Task SafeCloseAsync(SerialTransport transport)
    {
        try
        {
            await transport.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // mirrors `.catch(() => undefined)`
        }
    }

    public void Dispose()
    {
        StopPollingSync();
        _transport?.Dispose();
    }
}
