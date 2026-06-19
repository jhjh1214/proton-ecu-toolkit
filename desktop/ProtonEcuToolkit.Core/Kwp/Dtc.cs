using System.Globalization;

namespace ProtonEcuToolkit.Core.Kwp;

public sealed record DtcResponse(bool Positive, string DataHex, byte? Nrc = null);

/// <summary>
/// Plain hex string encode/decode for the DTC scan/erase services from
/// HANDOVER.md §3.7 (non-CFE / Siemens branch):
///   18020000 -&gt; ReadDiagnosticTroubleCodesByStatus, positive resp contains "58"
///   140000   -&gt; ClearDiagnosticInformation, positive resp contains "54"
///
/// §3.7 only documents the request bytes and the positive-response SID - it
/// does NOT document how the returned DTC bytes are laid out (how many bytes
/// per code, status byte format, etc.). So scan responses are surfaced as a
/// raw, undecoded hex blob rather than split into individual fault codes
/// until that's reverse-engineered against a real ECU response.
/// </summary>
public static class Dtc
{
    private const string ScanRequest = "18020000";
    private const string ClearRequest = "140000";
    private const string ScanPositiveSid = "58";
    private const string ClearPositiveSid = "54";
    private const string NegativeSid = "7F";

    public static string BuildDtcScanRequest() => ScanRequest;

    public static string BuildDtcClearRequest() => ClearRequest;

    public static DtcResponse? ParseDtcScanResponse(string raw) => ParseDtcResponse(raw, ScanPositiveSid);

    public static DtcResponse? ParseDtcClearResponse(string raw) => ParseDtcResponse(raw, ClearPositiveSid);

    private static DtcResponse? ParseDtcResponse(string raw, string positiveSid)
    {
        var hex = Protocol.StripNonHex(raw);
        if (hex.Length < 2) return null;

        var sid = hex[..2];

        if (string.Equals(sid, NegativeSid, StringComparison.OrdinalIgnoreCase))
        {
            byte? nrc = null;
            if (hex.Length >= 6 && byte.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var parsedNrc))
            {
                nrc = parsedNrc;
            }
            return new DtcResponse(false, "", nrc);
        }

        if (!string.Equals(sid, positiveSid, StringComparison.OrdinalIgnoreCase)) return null;

        return new DtcResponse(true, hex[2..]);
    }
}
