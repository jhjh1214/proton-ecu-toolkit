using System.Globalization;
using System.Text.RegularExpressions;

namespace ProtonEcuToolkit.Core.Kwp;

public sealed record ParsedReadByCidResponse(bool Positive, byte[] DataBytes, byte? Nrc = null);

/// <summary>
/// Plain hex string encode/decode for KWP2000 Service 0x22
/// (ReadDataByCommonIdentifier). Assumes ATH0 (headers off) and ATE0 (echo
/// off) are already in effect, so there's no header/checksum byte-math to
/// do here (HANDOVER.md §3.1) - just ASCII hex.
/// </summary>
public static class Protocol
{
    private const byte PositiveResponseSid = 0x62; // 0x22 + 0x40
    private const byte NegativeResponseSid = 0x7f;

    public static string BuildReadByCidRequest(string cid) => $"22{cid.ToUpperInvariant()}";

    public static ParsedReadByCidResponse? ParseReadByCidResponse(string raw, string cid)
    {
        var hex = StripNonHex(raw);
        if (hex.Length < 2) return null;

        var sid = Convert.ToByte(hex[..2], 16);

        if (sid == NegativeResponseSid)
        {
            byte? nrc = null;
            if (hex.Length >= 6 && byte.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var parsedNrc))
            {
                nrc = parsedNrc;
            }
            return new ParsedReadByCidResponse(false, [], nrc);
        }

        if (sid != PositiveResponseSid) return null;
        if (hex.Length < 6) return null;

        var echoedCid = hex[2..6];
        if (!string.Equals(echoedCid, cid, StringComparison.OrdinalIgnoreCase)) return null;

        var dataBytes = HexToBytes(hex[6..]);
        return new ParsedReadByCidResponse(true, dataBytes);
    }

    /// <summary>Cheap check used for the test ping (§3.2): "success = response contains 62".</summary>
    public static bool ContainsPositiveResponseMarker(string raw) =>
        StripNonHex(raw).Contains("62", StringComparison.OrdinalIgnoreCase);

    internal static string StripNonHex(string raw) =>
        Regex.Replace(raw, "[^0-9a-fA-F]", "").ToUpperInvariant();

    internal static byte[] HexToBytes(string hex)
    {
        var bytes = new List<byte>();
        for (var i = 0; i + 1 < hex.Length; i += 2)
        {
            bytes.Add(Convert.ToByte(hex.Substring(i, 2), 16));
        }
        return bytes.ToArray();
    }
}
