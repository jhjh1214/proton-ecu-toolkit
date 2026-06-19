using System.Text.Json;
using ProtonEcuToolkit.Core.Transport;

namespace ProtonEcuToolkit.Core.Elm;

/// <summary>
/// ELM327 AT command/response layer. Knows the chip's own text conventions
/// (command echo, the "OK" success marker) but nothing about what any given
/// command string means in KWP2000 terms - that's the kwp layer's job.
/// </summary>
public sealed class ElmClient
{
    private const int DefaultTimeoutMs = 2000;
    private const int DefaultMaxAttempts = 5;
    private const int DefaultRetryDelayMs = 300;

    private readonly SerialTransport _transport;

    public ElmClient(SerialTransport transport)
    {
        _transport = transport;
    }

    /// <summary>Sends one line, returns the cleaned response text (command echo stripped if present).</summary>
    public async Task<string> SendCommandAsync(string command, int timeoutMs = DefaultTimeoutMs)
    {
        var raw = await _transport.SendAsync(command, timeoutMs).ConfigureAwait(false);
        return Clean(raw, command);
    }

    /// <summary>Repeats a command until its response contains "OK", or gives up after maxAttempts.</summary>
    public async Task<string> SendCommandUntilOkAsync(
        string command,
        int timeoutMs = DefaultTimeoutMs,
        int maxAttempts = DefaultMaxAttempts,
        int retryDelayMs = DefaultRetryDelayMs)
    {
        var lastResponse = "";
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                lastResponse = await SendCommandAsync(command, timeoutMs).ConfigureAwait(false);
                if (lastResponse.Contains("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return lastResponse;
                }
            }
            catch (Exception err)
            {
                lastError = err;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(retryDelayMs).ConfigureAwait(false);
            }
        }

        var errSuffix = lastError is not null ? $", last error: {lastError}" : "";
        throw new InvalidOperationException(
            $"ElmClient: \"{command}\" did not return OK after {maxAttempts} attempts " +
            $"(last response: {JsonSerializer.Serialize(lastResponse)}{errSuffix})");
    }

    private static string Clean(string raw, string command)
    {
        var text = raw.TrimStart();
        if (text.StartsWith(command, StringComparison.OrdinalIgnoreCase))
        {
            text = text[command.Length..];
        }

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

        return string.Join("\n", lines);
    }
}
