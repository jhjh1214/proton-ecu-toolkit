using ProtonEcuToolkit.Core.Kwp;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class ProtocolTests
{
    [Fact]
    public void BuildReadByCidRequest_PrefixesWithService22()
    {
        Assert.Equal("221101", Protocol.BuildReadByCidRequest("1101"));
        Assert.Equal("22111F", Protocol.BuildReadByCidRequest("111f"));
    }

    [Fact]
    public void ParseReadByCidResponse_Parses1BytePositiveResponse_CoolantTempExampleFromHandover()
    {
        var result = Protocol.ParseReadByCidResponse("6211013E", "1101");
        Assert.NotNull(result);
        Assert.True(result!.Positive);
        Assert.Equal(new byte[] { 0x3e }, result.DataBytes);
    }

    [Fact]
    public void ParseReadByCidResponse_Parses2BytePositiveResponse_Rpm()
    {
        var result = Protocol.ParseReadByCidResponse("62110AB204", "110A");
        Assert.NotNull(result);
        Assert.True(result!.Positive);
        Assert.Equal(new byte[] { 0xb2, 0x04 }, result.DataBytes);
    }

    [Fact]
    public void ParseReadByCidResponse_ToleratesWhitespaceFromMultiLineOutput()
    {
        var result = Protocol.ParseReadByCidResponse("62 1101 3E\r\n", "1101");
        Assert.NotNull(result);
        Assert.True(result!.Positive);
        Assert.Equal(new byte[] { 0x3e }, result.DataBytes);
    }

    [Fact]
    public void ParseReadByCidResponse_ReturnsNegativeResponseWithNrc()
    {
        var result = Protocol.ParseReadByCidResponse("7F2231", "1101");
        Assert.NotNull(result);
        Assert.False(result!.Positive);
        Assert.Empty(result.DataBytes);
        Assert.Equal((byte)0x31, result.Nrc);
    }

    [Fact]
    public void ParseReadByCidResponse_ReturnsNullWhenCidEchoDoesNotMatch()
    {
        Assert.Null(Protocol.ParseReadByCidResponse("6211043E", "1101"));
    }

    [Fact]
    public void ParseReadByCidResponse_ReturnsNullForUnrecognizedOrGarbageResponses()
    {
        Assert.Null(Protocol.ParseReadByCidResponse("NODATA", "1101"));
        Assert.Null(Protocol.ParseReadByCidResponse("", "1101"));
    }

    [Fact]
    public void ContainsPositiveResponseMarker_MatchesTestPingExample()
    {
        Assert.True(Protocol.ContainsPositiveResponseMarker("62111F00"));
    }

    [Fact]
    public void ContainsPositiveResponseMarker_RejectsResponsesWithout62()
    {
        Assert.False(Protocol.ContainsPositiveResponseMarker("NO DATA"));
        Assert.False(Protocol.ContainsPositiveResponseMarker("7F2211"));
    }
}
