using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

public sealed class FacadeSimulationTests
{
    [Fact] public async Task Facade_RunRoundTripsWithFrozenApprovalAndHash()
    {
        var service=new FakeFacadeAssignmentService(new FakeCsiAdapter());
        var preview=await service.PreviewAsync(Calculation,Loads,new(1,0,0),FacadeMethod.EqualSplit);
        var approval=await service.ApproveAsync(preview,"Engineer",DateTimeOffset.UnixEpoch);
        var run=await service.ApplyAsync(approval,Calculation);
        var json=SnapshotJson.Serialize(run);
        var loaded=System.Text.Json.JsonSerializer.Deserialize<FacadeSimulationRun>(json);
        Assert.NotNull(loaded);
        Assert.Equal(json,SnapshotJson.Serialize(loaded));
        Assert.Equal(approval.ContentHash,loaded!.Approval.ContentHash);
        Assert.Equal(approval.ApprovedAt,loaded.Approval.ApprovedAt);
        Assert.Equal(run.FrameLoads,loaded.FrameLoads);
        Assert.True((await service.VerifyAsync(loaded.Id)).Matches);
    }
    private static WindCalculationRun Calculation => new("facade-calc",DateTimeOffset.UnixEpoch,"synthetic-manifest",DataProvenance.SyntheticDemoFixture,new(WindDirection.PositiveX,"IP+","wall",LimitState.Uls),[new(3,1200),new(6,1600)],[]);
    private static FrameSegmentLoad[] Loads => [new("B1",Calculation.State,"WX_POS_IP_POS",0,1,2100),new("B2",Calculation.State,"WX_POS_IP_POS",0,1,2100)];
    [Fact] public async Task Facade_SimulationApproveApplyReadbackPreservesManualAndSignedForce()
    {
        var adapter=new FakeCsiAdapter(); var service=new FakeFacadeAssignmentService(adapter);
        var original=await adapter.GetExistingLoadsAsync();
        var preview=await service.PreviewAsync(Calculation,Loads,new(1,0,0),FacadeMethod.EqualSplit);
        var approval=await service.ApproveAsync(preview,"Engineer",DateTimeOffset.UnixEpoch);
        var run=await service.ApplyAsync(approval,Calculation);
        Assert.True(run.IsSimulation); Assert.Equal(new Vec3(-2100,0,0),run.FrameLoads[0].GlobalLoadNPerM);
        Assert.Equal(original.Where(x=>x.Owner=="Manual"),run.PreservedManualLoads);
        Assert.Equal(original,await adapter.GetExistingLoadsAsync());
        Assert.True((await service.VerifyAsync(run.Id)).Matches);
        Assert.Equal(run.Id,(await service.ApplyAsync(approval,Calculation)).Id);
    }
    [Fact] public async Task Facade_RejectsMissingPatternCaseNameAndUnknownFrame()
    {
        var service=new FakeFacadeAssignmentService(new FakeCsiAdapter());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewAsync(Calculation,[Loads[0] with {Pattern="not-a-pattern"}],new(1,0,0),FacadeMethod.EqualSplit));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewAsync(Calculation,[Loads[0] with {FrameId="missing"}],new(1,0,0),FacadeMethod.EqualSplit));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewAsync(Calculation,[Loads[0] with {StartFraction=-0.1}],new(1,0,0),FacadeMethod.EqualSplit));
    }
    [Fact] public async Task Facade_EditsRequireNewApprovalAndStaleGeometryOrCalculationRejects()
    {
        var adapter=new FakeCsiAdapter(); var service=new FakeFacadeAssignmentService(adapter);
        var preview=await service.PreviewAsync(Calculation,Loads,new(1,0,0),FacadeMethod.EqualSplit);
        var approval=await service.ApproveAsync(preview,"Engineer",DateTimeOffset.UnixEpoch);
        var second=await service.ApproveAsync(preview with {Method=FacadeMethod.StaticallyEquivalent},"Engineer",DateTimeOffset.UnixEpoch);
        Assert.NotEqual(approval.ContentHash,second.ContentHash); Assert.Equal(FacadeMethod.EqualSplit,approval.Preview.Method);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyAsync(approval,Calculation with {Id="new-calc"}));
        adapter.ChangeGeometryForTest();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyAsync(approval,Calculation));
    }
    [Fact] public async Task Facade_AnotherApprovalOnOwnedScopeConflictsAndNonSyntheticBlocks()
    {
        var service=new FakeFacadeAssignmentService(new FakeCsiAdapter());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewAsync(Calculation with {Provenance=DataProvenance.Verified},Loads,new(1,0,0),FacadeMethod.EqualSplit));
        var preview=await service.PreviewAsync(Calculation,Loads,new(1,0,0),FacadeMethod.EqualSplit);
        var a=await service.ApproveAsync(preview,"A",DateTimeOffset.UnixEpoch);
        var b=await service.ApproveAsync(preview,"B",DateTimeOffset.UnixEpoch);
        await service.ApplyAsync(a,Calculation);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyAsync(b,Calculation));
    }
}
