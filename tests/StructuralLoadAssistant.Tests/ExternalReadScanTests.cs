using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.Fake;
namespace StructuralLoadAssistant.Tests;
public class ExternalReadScanTests
{
    [Fact]
    public async Task PartialScanAndUnknownUnitsCannotProduceSafeFramePlan()
    {
        var scan=new CsiReadScan(ExternalWindFixture.Model(),new("unknown",0,1,"unknown"),false,new(true,false,true,true),[],[],[],CsiScanOutcome.PartialScan);
        await Assert.ThrowsAsync<ArgumentException>(()=>ExternalWindReviewCoordinator.PreviewReadOnlyAsync(ExternalWindFixture.Request(),scan,[]));
    }
    [Fact]
    public void NativeLineLoadsRoundTripUsingForcePerLength()
    {
        var units=new CsiUnits("kN_mm",0.001,1000,"kN and mm");
        Assert.Equal(0.0015,ExternalWindReviewCoordinator.NativeLineIntensity(1500,units),10);
        Assert.Equal(1500,ExternalWindReviewCoordinator.NativeLineIntensity(1500,units)*units.ForceToN/units.LengthToM,8);
    }
}
