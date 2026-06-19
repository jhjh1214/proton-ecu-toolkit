namespace ProtonEcuToolkit.Core.Models;

public sealed record DtcActionResult(
    bool Positive,
    string RawHex,
    byte? Nrc,
    DateTimeOffset Timestamp,
    string? Error = null);
