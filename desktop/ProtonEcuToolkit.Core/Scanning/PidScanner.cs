using System.Diagnostics;

namespace ProtonEcuToolkit.Core.Scanning;

/// <summary>
/// Sequentially requests each candidate identifier under one service,
/// classifying and logging every attempt (not just hits). Interleaves a
/// keep-alive check every ~2.5s so a long scan doesn't let the KWP2000
/// session drop (HANDOVER.md/README.md §3.3's P3 timeout). Takes plain
/// delegates rather than a KwpSession reference directly, so it has no
/// idea what "connected" or "WPF" mean - just how to send a request and
/// check the session is still alive.
/// </summary>
public sealed class PidScanner
{
    private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(2.5);

    private readonly Func<string, int, Task<string>> _sendRaw;
    private readonly Func<Task<bool>> _ensureAlive;
    private readonly TimeSpan _keepAliveInterval;

    public PidScanner(
        Func<string, int, Task<string>> sendRaw,
        Func<Task<bool>> ensureAlive,
        TimeSpan? keepAliveInterval = null)
    {
        _sendRaw = sendRaw;
        _ensureAlive = ensureAlive;
        _keepAliveInterval = keepAliveInterval ?? DefaultKeepAliveInterval;
    }

    public event Action<ScanResultEntry>? ResultLogged;
    public event Action<int, int>? ProgressChanged;
    public event Action<string>? StatusChanged;

    public async Task RunAsync(byte sid, IReadOnlyList<int> candidateIds, int timeoutMs, CancellationToken token)
    {
        var lastKeepAlive = DateTimeOffset.UtcNow;

        for (var i = 0; i < candidateIds.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            await ScanOneAsync(sid, candidateIds[i], timeoutMs).ConfigureAwait(false);
            ProgressChanged?.Invoke(i + 1, candidateIds.Count);

            if (DateTimeOffset.UtcNow - lastKeepAlive >= _keepAliveInterval)
            {
                lastKeepAlive = DateTimeOffset.UtcNow;
                var alive = await _ensureAlive().ConfigureAwait(false);
                if (!alive)
                {
                    StatusChanged?.Invoke("Lost KWP session and recovery failed - scan stopped.");
                    return;
                }
            }
        }
    }

    private async Task ScanOneAsync(byte sid, int id, int timeoutMs)
    {
        var idHex = id.ToString("X4");
        var requestHex = $"{sid:X2}{idHex}";

        var stopwatch = Stopwatch.StartNew();
        string? response;
        try
        {
            response = await _sendRaw(requestHex, timeoutMs).ConfigureAwait(false);
        }
        catch
        {
            response = null;
        }
        stopwatch.Stop();

        var classified = ResponseClassifier.Classify(sid, response);
        var entry = new ScanResultEntry(
            DateTimeOffset.UtcNow,
            sid,
            idHex,
            requestHex,
            classified.ResponseHex,
            classified.Classification,
            classified.Nrc,
            (int)stopwatch.ElapsedMilliseconds);

        ResultLogged?.Invoke(entry);
    }
}
