using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;
public class ExternalSafetyTests
{
    [Fact]
    public void DuplicateCaseIdentityIsRejected()
    {
        var r=ExternalWindFixture.Request();
        Assert.Throws<ArgumentException>(()=>AsNzExternalPressureService.Execute(r with{Surfaces=r.Surfaces.Add(r.Surfaces[0])}));
    }
    [Fact]
    public void NonplanarPhysicalSurfaceDoesNotProduceNumericPressure()
    {
        var r=ExternalWindFixture.Request();var s=r.Surfaces[0];
        s=s with{Polygon=s.Polygon with{Vertices=s.Polygon.Vertices.SetItem(0,new(0,1,0))}};
        Assert.Null(AsNzExternalPressureService.Execute(r with{Surfaces=[s]}).Cases[0].PressurePa);
    }
    [Fact]
    public async Task MissingSupportsProduceBlockedTransferWithoutDroppingCase()
    {
        var p=await ExternalWindReviewCoordinator.PreviewAsync(ExternalWindFixture.Request(),ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support() with{FirstSupport=["missing"]})]);
        Assert.Empty(p.Plans);Assert.Contains(p.Blockers,b=>b.Contains("TRANSFER_UNSUPPORTED"));Assert.Equal(p.Run.Cases.Length,p.Blockers.Length);
    }
    [Fact]
    public async Task CancelledCalculationPublishesNothing()
    {
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ExternalWindReviewCoordinator.PreviewAsync(ExternalWindFixture.Request(),ExternalWindFixture.Model(),[],cts.Token));
    }
    [Fact]
    public void ReversedAndSplitSupportFramesPreserveStationsAndEquilibrium()
    {
        var panel=ExternalWindFixture.Request().Surfaces[0].Polygon;
        ImmutableArray<FrameGeometry> frames=[new("b1","",new(1,0,0),new(0,0,0)),new("b2","",new(2,0,0),new(1,0,0)),new("top","",new(0,0,3),new(2,0,3))];
        var p=StructuralLoadAssistant.RoofMapping.PressureSurfaceTributaryMapper.Map(panel,1000,frames,new("wall",["b1","b2"],["top"],"E","Split I/J beams",true));
        Assert.True(p.Check.Passed);Assert.Equal(6000,p.Check.FrameForceN.Y,6);Assert.Equal(-9000,p.Check.FrameMomentNm.X,6);
        Assert.All(p.Loads,l=>Assert.True(l.StartFraction>=0&&l.EndFraction<=1&&l.StartFraction<l.EndFraction));
    }
}
