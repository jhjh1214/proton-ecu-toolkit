using ProtonEcuToolkit.Core.Scanning;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class PidScannerTests
{
    [Fact]
    public async Task RunAsync_LogsOneClassifiedEntryPerCandidateAndReportsProgress()
    {
        var responses = new Dictionary<string, string>
        {
            ["221101"] = "6211013E",
            ["229999"] = "7F2231",
        };

        var scanner = new PidScanner(
            sendRaw: (hex, _) => Task.FromResult(responses[hex]),
            ensureAlive: () => Task.FromResult(true),
            keepAliveInterval: TimeSpan.MaxValue);

        var logged = new List<ScanResultEntry>();
        var progress = new List<(int Done, int Total)>();
        scanner.ResultLogged += e => logged.Add(e);
        scanner.ProgressChanged += (done, total) => progress.Add((done, total));

        await scanner.RunAsync(0x22, [0x1101, 0x9999], timeoutMs: 250, CancellationToken.None);

        Assert.Equal(2, logged.Count);
        Assert.Equal(ResponseClassification.Positive, logged[0].Classification);
        Assert.Equal("221101", logged[0].RequestHex);
        Assert.Equal(ResponseClassification.Negative, logged[1].Classification);
        Assert.Equal((byte)0x31, logged[1].Nrc);

        Assert.Equal([(1, 2), (2, 2)], progress);
    }

    [Fact]
    public async Task RunAsync_RecordsNoResponseWhenSendThrows()
    {
        var scanner = new PidScanner(
            sendRaw: (_, _) => throw new TimeoutException("no reply"),
            ensureAlive: () => Task.FromResult(true),
            keepAliveInterval: TimeSpan.MaxValue);

        ScanResultEntry? logged = null;
        scanner.ResultLogged += e => logged = e;

        await scanner.RunAsync(0x22, [0x1101], timeoutMs: 250, CancellationToken.None);

        Assert.NotNull(logged);
        Assert.Equal(ResponseClassification.NoResponse, logged!.Classification);
    }

    [Fact]
    public async Task RunAsync_StopsEarlyWhenKeepAliveFails()
    {
        var scanner = new PidScanner(
            sendRaw: (_, _) => Task.FromResult("7F2231"),
            ensureAlive: () => Task.FromResult(false),
            keepAliveInterval: TimeSpan.Zero); // triggers the keep-alive check after every candidate

        var logged = new List<ScanResultEntry>();
        var statusMessages = new List<string>();
        scanner.ResultLogged += e => logged.Add(e);
        scanner.StatusChanged += s => statusMessages.Add(s);

        await scanner.RunAsync(0x22, [0x1101, 0x1102, 0x1103], timeoutMs: 250, CancellationToken.None);

        Assert.Single(logged); // stopped after the first candidate's failed keep-alive check
        Assert.Contains(statusMessages, m => m.Contains("recovery failed"));
    }

    [Fact]
    public async Task RunAsync_StopsImmediatelyWhenAlreadyCancelled()
    {
        var scanner = new PidScanner(
            sendRaw: (_, _) => Task.FromResult("7F2231"),
            ensureAlive: () => Task.FromResult(true));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var logged = new List<ScanResultEntry>();
        scanner.ResultLogged += e => logged.Add(e);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scanner.RunAsync(0x22, [0x1101], timeoutMs: 250, cts.Token));

        Assert.Empty(logged);
    }
}
