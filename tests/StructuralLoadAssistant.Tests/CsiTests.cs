using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public class CsiTests
{
    public static RoofMappingWorkflow Workflow(CsiModelSnapshot model)
    {
        var state = DemoFixtures.State;
        RoofPressureZone[] zones = [new("Edge","Roof",state,DemoFixtures.Rectangle(0,0,5,12),-1500),
            new("Interior","Roof",state,DemoFixtures.Rectangle(5,0,10,12),-1000)];
        var w = new RoofMappingWorkflow(DemoFixtures.Calculation(), model, zones, true, DemoFixtures.Manifest());
        w.Scan(); w.Map(); w.SetPattern(state.Id,"WX_POS_IP_POS");
        w.SetSign("R1",state.Id,ManualLocal3Direction.Positive,false,"Test Engineer","Outward axis confirmed");
        w.SetSign("R2",state.Id,ManualLocal3Direction.Negative,false,"Test Engineer","Inward axis confirmed");
        if (model.Shells.Any(s=>s.Id=="RX")) w.Exclude("RX","NeedsSplitInCSI: excluded from simulation scope","Test Engineer");
        return w;
    }
    [Fact] public async Task FakeScan_DistinguishesPatternsCasesCombinations()
    {
        var m=await new FakeCsiAdapter().ScanModelAsync();
        Assert.Contains("WX_POS_IP_POS",m.Patterns); Assert.Contains("CASE_WX",m.Cases);
        Assert.DoesNotContain("CASE_WX",m.Patterns); Assert.Equal(4,m.Frames.Length);
    }
    [Fact] public async Task FakeApply_ReadbackMixedSignsAndManualLoadsSurvive()
    {
        var fake=new FakeCsiAdapter(); var m=await fake.ScanModelAsync();
        var w=Workflow(m); Assert.Empty(w.Validate().Where(i=>i.Blocking));
        var approval=w.Approve("Test Engineer",DemoFixtures.FixtureTime);
        var preview=fake.Preview(approval); Assert.Equal(2,preview.ProposedLoads.Length);
        var run=await fake.ApplyAreaLoadsAsync(approval); Assert.True(run.IsSimulation);
        Assert.Equal(1500,run.AreaLoads.Single(l=>l.ShellId=="R1").SignedCsiAreaLoadPa);
        Assert.Equal(-1500,run.AreaLoads.Single(l=>l.ShellId=="R2").SignedCsiAreaLoadPa);
        Assert.True((await fake.VerifyReadbackAsync(run.Id)).Matches);
        Assert.Equal(2,(await fake.GetExistingLoadsAsync()).Count(l=>l.Owner=="Manual"));
    }
    [Fact] public async Task RepeatedApply_IsIdempotent()
    {
        var f=new FakeCsiAdapter(); var w=Workflow(await f.ScanModelAsync()); w.Validate();
        var a=w.Approve("Engineer",DemoFixtures.FixtureTime);
        var first=await f.ApplyAreaLoadsAsync(a); var second=await f.ApplyAreaLoadsAsync(a);
        Assert.Equal(first.Id,second.Id); Assert.Equal(4,(await f.GetExistingLoadsAsync()).Length);
    }
    [Fact] public async Task StaleGeometry_RejectsApply()
    {
        var f=new FakeCsiAdapter(); var w=Workflow(await f.ScanModelAsync()); w.Validate();
        var a=w.Approve("Engineer",DemoFixtures.FixtureTime); f.ChangeGeometryForTest();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.ApplyAreaLoadsAsync(a));
    }
    [Fact] public async Task MissingApproval_UnknownReadbackRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new FakeCsiAdapter().VerifyReadbackAsync("unknown"));
    }
    [Fact] public async Task LoadCaseCannotBeUsedAsPattern()
    {
        var f=new FakeCsiAdapter();var w=Workflow(await f.ScanModelAsync());
        w.SetPattern(DemoFixtures.State.Id,"CASE_WX");
        Assert.NotEmpty(w.Validate().Where(i=>i.Blocking));
        Assert.Throws<InvalidOperationException>(()=>w.Approve("Engineer",DemoFixtures.FixtureTime));
    }
    [Fact] public async Task CancelledScanDoesNotMutateModel()
    {
        var f=new FakeCsiAdapter(); using var ct=new CancellationTokenSource();ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.ScanModelAsync(ct.Token));
        Assert.Equal(2,(await f.GetExistingLoadsAsync()).Length);
    }
}
