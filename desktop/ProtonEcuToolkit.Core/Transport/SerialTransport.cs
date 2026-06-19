using System.IO.Ports;
using System.Text;

namespace ProtonEcuToolkit.Core.Transport;

/// <summary>
/// Raw serial framing only. Writes a line, reads bytes until the ELM327's
/// '&gt;' prompt shows up, and hands back whatever text arrived in between.
/// Has no idea what an AT command or a KWP2000 service byte is - that
/// belongs to the layers above.
/// </summary>
public sealed class SerialTransport : IDisposable
{
    private const char Prompt = '>';
    private const int DefaultBaudRate = 38400;

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly StringBuilder _buffer = new();
    private readonly SemaphoreSlim _queue = new(1, 1);
    private SerialPort? _port;
    private PendingRequest? _pending;

    public SerialTransport(string portName, int baudRate = DefaultBaudRate)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public static string[] ListPortNames() => SerialPort.GetPortNames();

    public bool IsOpen => _port?.IsOpen ?? false;

    public Task OpenAsync()
    {
        var port = new SerialPort(_portName, _baudRate);
        port.DataReceived += OnDataReceived;
        port.ErrorReceived += (_, _) => FailPending(new IOException("SerialTransport: port error"));
        port.Open();
        _port = port;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (_port is { IsOpen: true } port)
        {
            port.DataReceived -= OnDataReceived;
            port.Close();
        }
        _port = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one line (CR-terminated) and resolves with everything received
    /// before the next '&gt;' prompt. Calls are serialized - only one in flight
    /// at a time, since the ELM327 is strictly half-duplex.
    /// </summary>
    public async Task<string> SendAsync(string line, int timeoutMs = 2000)
    {
        await _queue.WaitAsync().ConfigureAwait(false);
        try
        {
            return await SendNowAsync(line, timeoutMs).ConfigureAwait(false);
        }
        finally
        {
            _queue.Release();
        }
    }

    private Task<string> SendNowAsync(string line, int timeoutMs)
    {
        var port = _port;
        if (port is not { IsOpen: true })
        {
            throw new InvalidOperationException("SerialTransport: port is not open");
        }

        lock (_buffer)
        {
            _buffer.Clear();
        }

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource(timeoutMs);
        var pending = new PendingRequest(tcs, cts);
        cts.Token.Register(() =>
        {
            if (Interlocked.CompareExchange(ref _pending, null, pending) == pending)
            {
                tcs.TrySetException(
                    new TimeoutException($"SerialTransport: timed out waiting for response to \"{line}\""));
            }
        });

        _pending = pending;
        port.Write(line + "\r");
        return tcs.Task;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var port = (SerialPort)sender;
        string chunk;
        try
        {
            chunk = port.ReadExisting();
        }
        catch (Exception)
        {
            return;
        }

        string? response = null;
        lock (_buffer)
        {
            _buffer.Append(chunk);
            if (_pending is null) return;

            var text = _buffer.ToString();
            var promptIndex = text.IndexOf(Prompt);
            if (promptIndex == -1) return;

            response = text[..promptIndex];
            _buffer.Clear();
            _buffer.Append(text[(promptIndex + 1)..]);
        }

        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is null) return;
        pending.Cts.Dispose();
        pending.Tcs.TrySetResult(response);
    }

    private void FailPending(Exception ex)
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is null) return;
        pending.Cts.Dispose();
        pending.Tcs.TrySetException(ex);
    }

    public void Dispose()
    {
        if (_port is { IsOpen: true })
        {
            _port.Close();
        }
        _port?.Dispose();
        _queue.Dispose();
    }

    private sealed record PendingRequest(TaskCompletionSource<string> Tcs, CancellationTokenSource Cts);
}
