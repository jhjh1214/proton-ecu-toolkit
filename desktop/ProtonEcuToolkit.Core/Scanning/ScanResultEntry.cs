namespace ProtonEcuToolkit.Core.Scanning;

/// <summary>One scan attempt - the raw evidence trail. Logged for every candidate, not just hits.</summary>
public sealed record ScanResultEntry(
    DateTimeOffset Timestamp,
    byte Sid,
    string IdentifierHex,
    string RequestHex,
    string? ResponseHex,
    ResponseClassification Classification,
    byte? Nrc,
    int LatencyMs);
