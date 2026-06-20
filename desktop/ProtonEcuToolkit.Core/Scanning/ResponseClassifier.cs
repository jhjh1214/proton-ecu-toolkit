using System.Globalization;
using ProtonEcuToolkit.Core.Kwp;

namespace ProtonEcuToolkit.Core.Scanning;

public sealed record ClassifiedResponse(ResponseClassification Classification, string? ResponseHex, byte? Nrc);

/// <summary>
/// Generic positive/negative/no-data/malformed classification for any
/// service's response - used by the scanner, which (unlike PidReader)
/// doesn't know in advance what a "correct" response for an untried
/// identifier looks like, just what KWP2000 response framing looks like.
/// </summary>
public static class ResponseClassifier
{
    private const byte NegativeResponseSid = 0x7f;

    /// <summary>
    /// Classifies a request's outcome. Pass null for <paramref name="rawResponse"/>
    /// when the request timed out / threw, rather than catching the exception here -
    /// the scanner already knows it failed, this just records that fact uniformly.
    /// </summary>
    public static ClassifiedResponse Classify(byte requestSid, string? rawResponse)
    {
        if (rawResponse is null)
        {
            return new ClassifiedResponse(ResponseClassification.NoResponse, null, null);
        }

        var hex = Protocol.StripNonHex(rawResponse);
        if (hex.Length < 2)
        {
            return new ClassifiedResponse(ResponseClassification.Malformed, rawResponse, null);
        }

        var responseSid = Convert.ToByte(hex[..2], 16);

        if (responseSid == NegativeResponseSid)
        {
            byte? nrc = null;
            if (hex.Length >= 6 && byte.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var parsedNrc))
            {
                nrc = parsedNrc;
            }
            return new ClassifiedResponse(ResponseClassification.Negative, rawResponse, nrc);
        }

        var positiveSid = (byte)(requestSid + 0x40);
        if (responseSid == positiveSid)
        {
            return new ClassifiedResponse(ResponseClassification.Positive, rawResponse, null);
        }

        return new ClassifiedResponse(ResponseClassification.Malformed, rawResponse, null);
    }
}
