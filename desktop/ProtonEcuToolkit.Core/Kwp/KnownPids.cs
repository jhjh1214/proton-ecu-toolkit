namespace ProtonEcuToolkit.Core.Kwp;

/// <summary>
/// The 5 PIDs the original ProtonOBDFree app reads, per HANDOVER.md §3.4.
/// All are Service 0x22 (ReadDataByCommonIdentifier) requests; Id is the
/// already-offset (+0x57) 2-byte CID actually sent on the wire.
/// </summary>
public static class KnownPids
{
    /// <summary>Service 0x22 CID used as a connectivity/keep-alive ping (§3.2, §3.3). Not a telemetry PID.</summary>
    public const string TestPingCid = "111F";

    public static readonly IReadOnlyList<PidDefinition> All = new List<PidDefinition>
    {
        new("1101", "Coolant temp", "°C", 1, bytes => bytes[0] - 60),
        new("1104", "RPM", "rpm", 2, bytes => bytes[1] * 255 + bytes[0]),
        new("110A", "TPS", "%", 1, bytes => bytes[0] * 0.39216),
        new("1110", "Battery voltage", "V", 1, bytes => bytes[0] * 0.078431),
        new("1113", "Vehicle speed", "km/h", 1, bytes => bytes[0] * 1.2),
    };
}
