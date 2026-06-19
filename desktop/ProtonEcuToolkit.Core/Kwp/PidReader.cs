using ProtonEcuToolkit.Core.Elm;
using ProtonEcuToolkit.Core.Models;

namespace ProtonEcuToolkit.Core.Kwp;

/// <summary>Sends a known PID request, parses the response, and applies its formula.</summary>
public sealed class PidReader
{
    private const int DefaultTimeoutMs = 1000;

    private readonly ElmClient _elm;
    private readonly int _timeoutMs;

    public PidReader(ElmClient elm, int timeoutMs = DefaultTimeoutMs)
    {
        _elm = elm;
        _timeoutMs = timeoutMs;
    }

    public async Task<PidReading> ReadAsync(PidDefinition pid)
    {
        var timestamp = DateTimeOffset.UtcNow;
        string? rawHex = null;

        try
        {
            rawHex = await _elm.SendCommandAsync(Protocol.BuildReadByCidRequest(pid.Id), _timeoutMs)
                .ConfigureAwait(false);
            var parsed = Protocol.ParseReadByCidResponse(rawHex, pid.Id);

            if (parsed is null)
            {
                return Failure(pid, rawHex, timestamp, $"Unrecognized response: \"{rawHex}\"");
            }
            if (!parsed.Positive)
            {
                var nrc = parsed.Nrc is { } nrcValue ? $"0x{nrcValue:x}" : "unknown";
                return Failure(pid, rawHex, timestamp, $"Negative response, NRC={nrc}");
            }
            if (parsed.DataBytes.Length < pid.ByteLength)
            {
                return Failure(
                    pid, rawHex, timestamp,
                    $"Expected {pid.ByteLength} data byte(s), got {parsed.DataBytes.Length}");
            }

            return new PidReading(pid.Id, pid.Name, pid.Unit, pid.Decode(parsed.DataBytes), rawHex, timestamp);
        }
        catch (Exception err)
        {
            return Failure(pid, rawHex, timestamp, err.Message);
        }
    }

    public async Task<List<PidReading>> ReadAllAsync(IReadOnlyList<PidDefinition> pids)
    {
        var readings = new List<PidReading>(pids.Count);
        foreach (var pid in pids)
        {
            readings.Add(await ReadAsync(pid).ConfigureAwait(false));
        }
        return readings;
    }

    private static PidReading Failure(PidDefinition pid, string? rawHex, DateTimeOffset timestamp, string error) =>
        new(pid.Id, pid.Name, pid.Unit, null, rawHex, timestamp, error);
}
