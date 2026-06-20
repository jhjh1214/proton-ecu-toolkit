using ProtonEcuToolkit.Core.Scanning;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class ScanPlansTests
{
    [Fact]
    public void KnownCandidateIds_MatchesTheDocumentedSevenIdentifiers()
    {
        Assert.Equal([0x1147, 0x1148, 0x1149, 0x11CC, 0x11CD, 0x11CE, 0x11CF], ScanPlans.KnownCandidateIds);
    }

    [Fact]
    public void NearbyRange_Spans0x1000To0x12FFInclusive()
    {
        Assert.Equal(0x12FF - 0x1000 + 1, ScanPlans.NearbyRange.Count);
        Assert.Equal(0x1000, ScanPlans.NearbyRange[0]);
        Assert.Equal(0x12FF, ScanPlans.NearbyRange[^1]);
    }

    [Fact]
    public void WideRange_Spans0x0000To0x1FFFInclusive()
    {
        Assert.Equal(0x1FFF - 0x0000 + 1, ScanPlans.WideRange.Count);
        Assert.Equal(0x0000, ScanPlans.WideRange[0]);
        Assert.Equal(0x1FFF, ScanPlans.WideRange[^1]);
    }
}
