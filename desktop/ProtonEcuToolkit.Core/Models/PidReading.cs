namespace ProtonEcuToolkit.Core.Models;

public sealed record PidReading(
    string Id,
    string Name,
    string Unit,
    double? Value,
    string? RawHex,
    DateTimeOffset Timestamp,
    string? Error = null);
