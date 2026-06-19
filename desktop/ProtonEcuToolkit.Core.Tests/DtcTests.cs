using ProtonEcuToolkit.Core.Kwp;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class DtcTests
{
    [Fact]
    public void BuildRequests_MatchTheRequestBytesFromHandover()
    {
        Assert.Equal("18020000", Dtc.BuildDtcScanRequest());
        Assert.Equal("140000", Dtc.BuildDtcClearRequest());
    }

    [Fact]
    public void ParseDtcScanResponse_ParsesPositiveResponseAsUndecodedHexBlob()
    {
        var result = Dtc.ParseDtcScanResponse("580243910071");
        Assert.Equal(new DtcResponse(true, "0243910071"), result);
    }

    [Fact]
    public void ParseDtcScanResponse_ToleratesWhitespaceFromMultiLineOutput()
    {
        var result = Dtc.ParseDtcScanResponse("58 02 43 91\r\n");
        Assert.Equal(new DtcResponse(true, "024391"), result);
    }

    [Fact]
    public void ParseDtcScanResponse_ParsesNegativeResponseWithNrc()
    {
        var result = Dtc.ParseDtcScanResponse("7F1822");
        Assert.Equal(new DtcResponse(false, "", 0x22), result);
    }

    [Fact]
    public void ParseDtcScanResponse_ReturnsNullForUnrecognizedOrGarbageResponses()
    {
        Assert.Null(Dtc.ParseDtcScanResponse("NO DATA"));
        Assert.Null(Dtc.ParseDtcScanResponse(""));
        Assert.Null(Dtc.ParseDtcScanResponse("621101")); // a PID response, not a DTC one
    }

    [Fact]
    public void ParseDtcClearResponse_ParsesPositiveClearResponse()
    {
        var result = Dtc.ParseDtcClearResponse("54");
        Assert.Equal(new DtcResponse(true, ""), result);
    }

    [Fact]
    public void ParseDtcClearResponse_ParsesNegativeClearResponseWithNrc()
    {
        var result = Dtc.ParseDtcClearResponse("7F1431");
        Assert.Equal(new DtcResponse(false, "", 0x31), result);
    }
}
