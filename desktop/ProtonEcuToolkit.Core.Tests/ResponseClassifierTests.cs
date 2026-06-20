using ProtonEcuToolkit.Core.Scanning;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class ResponseClassifierTests
{
    [Fact]
    public void Classify_RecognizesPositiveResponseForService0x22()
    {
        var result = ResponseClassifier.Classify(0x22, "6211013E");
        Assert.Equal(ResponseClassification.Positive, result.Classification);
        Assert.Null(result.Nrc);
    }

    [Fact]
    public void Classify_RecognizesPositiveResponseForADifferentService()
    {
        // §3.6: 1083 -> StartDiagnosticSession, positive response "50" (0x10 + 0x40)
        var result = ResponseClassifier.Classify(0x10, "5083");
        Assert.Equal(ResponseClassification.Positive, result.Classification);
    }

    [Fact]
    public void Classify_RecognizesNegativeResponseWithNrc()
    {
        var result = ResponseClassifier.Classify(0x22, "7F2231");
        Assert.Equal(ResponseClassification.Negative, result.Classification);
        Assert.Equal((byte)0x31, result.Nrc);
    }

    [Fact]
    public void Classify_TreatsNullResponseAsNoResponse()
    {
        var result = ResponseClassifier.Classify(0x22, null);
        Assert.Equal(ResponseClassification.NoResponse, result.Classification);
    }

    [Fact]
    public void Classify_TreatsGarbageAsMalformed()
    {
        Assert.Equal(ResponseClassification.Malformed, ResponseClassifier.Classify(0x22, "NO DATA").Classification);
        Assert.Equal(ResponseClassification.Malformed, ResponseClassifier.Classify(0x22, "").Classification);
    }

    [Fact]
    public void Classify_TreatsMismatchedSidAsMalformed()
    {
        // a 0x22 request that somehow gets back a 0x62-shaped response for a different request isn't
        // distinguishable here, but a response that's neither 7F nor (sid+0x40) is malformed.
        var result = ResponseClassifier.Classify(0x22, "9911013E");
        Assert.Equal(ResponseClassification.Malformed, result.Classification);
    }
}
