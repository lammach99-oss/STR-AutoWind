using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;
public class ExternalReviewAuditTests
{
    [Fact]
    public async Task CommonAndAlternativeCannotDoubleCountOnePhysicalPanel()
    {
        var r=ExternalWindFixture.Request();var a=r.Surfaces[0];var b=a with{Id="copy",Alternative="A",Polygon=a.Polygon with{Id="copy"}};
        r=r with{Surfaces=[a,b]};
        var p=await ExternalWindReviewCoordinator.PreviewAsync(r,ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support()),new("Uls_PositiveX_copy_A",ExternalWindFixture.Support() with{PanelId="copy"})]);
        Assert.Empty(p.Plans);Assert.Contains(p.Blockers,b=>b.Contains("OVERLAPPING_SOURCE"));
    }    [Fact]
    public async Task CommonWallContributionIsIncludedInEachRoofAlternative()
    {
        var r=ExternalWindFixture.Request();r=r with{Surfaces=r.Surfaces.Where(s=>s.State==LimitState.Uls&&s.WindFrom==WindDirection.PositiveX).ToImmutableArray()};
        var roof=r.Surfaces.First(s=>s.Id=="roof").Polygon;
        var model=ExternalWindFixture.Model() with{Frames=ExternalWindFixture.Model().Frames.Add(new("eave","",roof.Vertices[0],roof.Vertices[1])).Add(new("upper","",roof.Vertices[3],roof.Vertices[2]))};
        var t=r.Surfaces.Select(s=>new ExternalTransferSelection($"{s.State}_{s.WindFrom}_{s.Id}_{s.Alternative}",s.Id=="wall"?ExternalWindFixture.Support():new("roof",["eave"],["upper"],"E","One way roof",true))).ToImmutableArray();
        var p=await ExternalWindReviewCoordinator.PreviewAsync(r,model,t);
        Assert.Equal(2,p.Plans.Length);Assert.All(p.Plans,plan=>Assert.Contains(plan.Segments,s=>s.FrameId=="bottom"));
        model=model with{ExistingLoads=[new("bottom",p.Plans[0].Pattern,"manual",1,"owner")]};
        var conflicted=await ExternalWindReviewCoordinator.PreviewAsync(r,model,t);
        Assert.Single(conflicted.Plans);Assert.Contains(conflicted.Blockers,b=>b.Contains("TRANSFER_UNSUPPORTED"));
    }    [Fact]
    public async Task DuplicatePhysicalPanelsInOneScenarioFailClosed()
    {
        var r=ExternalWindFixture.Request();var a=r.Surfaces[0];var b=a with{Id="duplicate",Polygon=a.Polygon with{Id="duplicate"}};
        r=r with{Surfaces=[a,b]};
        var result=await ExternalWindReviewCoordinator.PreviewAsync(r,ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support()),new("Uls_PositiveX_duplicate_COMMON",ExternalWindFixture.Support() with{PanelId="duplicate"})]);
        Assert.Empty(result.Plans);Assert.Contains(result.Blockers,b=>b.Contains("OVERLAPPING_SOURCE"));
    }
    [Fact]
    public void GeographicPositiveYUsesCounterclockwiseModelRotation()
    {
        var r=ExternalWindFixture.Request();var speeds=r.Speeds.Select(s=>s with{Directional=Enumerable.Range(0,8).Select(i=>ExternalWindFixture.Input("Md",i==0?2:1)).ToImmutableArray()}).ToImmutableArray();
        var c=AsNzExternalPressureService.Execute(r with{ModelXWindFromBearingDegrees=90,Speeds=speeds}).Cases.First(c=>c.State==LimitState.Uls&&c.WindFrom==WindDirection.PositiveY);
        Assert.Equal(80,c.DesignSpeed);
    }

    [Fact]
    public void PositivePressureCannotUsePermeabilityReductionAndNullOperandIsLocalBlock()
    {
        var r=ExternalWindFixture.Request();var s=r.Surfaces[0];
        var run=AsNzExternalPressureService.Execute(r with{Surfaces=[s with{Kp=s.Kp with{Value=.5}},r.Surfaces[1]]});
        Assert.Null(run.Cases[0].PressurePa);Assert.NotNull(run.Cases[1].PressurePa);
        run=AsNzExternalPressureService.Execute(r with{Surfaces=[s with{Cpe=null!},r.Surfaces[1]]});
        Assert.Null(run.Cases[0].PressurePa);Assert.NotNull(run.Cases[1].PressurePa);
    }

    [Fact]
    public async Task SupportReviewEvidenceChangesImmutablePreviewIdentity()
    {
        var a=await ExternalWindReviewCoordinator.PreviewAsync(ExternalWindFixture.Request(),ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support())]);
        var b=await ExternalWindReviewCoordinator.PreviewAsync(ExternalWindFixture.Request(),ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support() with{Reviewer="Other"})]);
        Assert.NotEqual(a.ContentHash,b.ContentHash);Assert.NotEqual(a.Plans[0].ContentHash,b.Plans[0].ContentHash);
    }
}
