using ProtonEcuToolkit.Core.Kwp;
using Xunit;

namespace ProtonEcuToolkit.Core.Tests;

public class KnownPidsTests
{
    private static PidDefinition Pid(string id) =>
        KnownPids.All.FirstOrDefault(p => p.Id == id)
        ?? throw new InvalidOperationException($"no known PID with id {id}");

    [Fact]
    public void CoolantTemp_MatchesHandoverWorkedExample_ColdEngineReading()
    {
        Assert.Equal(2, Pid("1101").Decode([0x3e]));
    }

    [Fact]
    public void CoolantTemp_HandlesZeroCrossing()
    {
        Assert.Equal(0, Pid("1101").Decode([60]));
        Assert.Equal(-60, Pid("1101").Decode([0]));
    }

    [Fact]
    public void Rpm_DecodesRepresentativeIdleReading()
    {
        Assert.Equal(750, Pid("1104").Decode([0xf0, 0x02]));
    }

    [Fact]
    public void Rpm_DecodesZero()
    {
        Assert.Equal(0, Pid("1104").Decode([0, 0]));
    }

    [Fact]
    public void Tps_DecodesFullScaleToApprox100Percent()
    {
        Assert.Equal(100, Pid("110A").Decode([0xff]), precision: 0);
    }

    [Fact]
    public void Tps_DecodesClosedThrottleToZeroPercent()
    {
        Assert.Equal(0, Pid("110A").Decode([0]));
    }

    [Fact]
    public void BatteryVoltage_DecodesPlausibleRunningVoltageReading()
    {
        Assert.Equal(12.78, Pid("1110").Decode([163]), precision: 1);
    }

    [Fact]
    public void VehicleSpeed_DecodesRepresentativeReading()
    {
        Assert.Equal(60, Pid("1113").Decode([50]));
    }

    [Fact]
    public void VehicleSpeed_DecodesStandstillToZero()
    {
        Assert.Equal(0, Pid("1113").Decode([0]));
    }
}
