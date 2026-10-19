using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Persistence;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;
public class DemoSessionTests
{
    [Fact] public async Task FacadeSimulation_RequiresApprovalAndInvalidatesOnMethodEdit()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var s=new DemoSession(Path.Combine(dir,"project.db"));s.LoadDemoFixture();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>s.ApplyFacadeAsync());
            var preview=await s.PreviewFacadeAsync();Assert.All(preview.FrameLoads,l=>Assert.True(l.GlobalLoadNPerM.X<0));
            await s.ApproveFacadeAsync("Engineer");var a=await s.ApplyFacadeAsync();Assert.True((await s.VerifyFacadeAsync()).Matches);
            Assert.Equal(a.Id,(await s.ApplyFacadeAsync()).Id);
            s.RecalculateFacade(FacadeMethod.StaticallyEquivalent);Assert.Null(s.FacadeApproval);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>s.ApplyFacadeAsync());
        }
        finally{Directory.Delete(dir,true);}
    }
    [Fact] public void IndependentPressureState_CreatesDistinctCalculationAndUnapprovedMapping()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var s=new DemoSession(Path.Combine(dir,"project.db"));s.LoadDemoFixture();var first=s.Calculation.Id;
            s.LoadDemoFixture("ASCE/SEI 7-22",true,s.Calculation.State with {InternalPressure="IP_NEG",Direction=WindDirection.NegativeY,RoofCase="RoofCase2"});
            Assert.NotEqual(first,s.Calculation.Id);Assert.All(s.Roof.Mappings,m=>Assert.Equal(s.Calculation.State.Id,m.StateId));
            Assert.All(s.PanelRows,r=>Assert.Equal("WY_NEG_IP_NEG",s.FacadePattern));
            Assert.Null(s.Roof.Approval);Assert.Equal(DataProvenance.SyntheticDemoFixture,s.Calculation.Provenance);
        }
        finally{Directory.Delete(dir,true);}
    }
    [Fact] public void SwitchingStandardAndCleanFixture_PreservesPriorSnapshots()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var s=new DemoSession(Path.Combine(dir,"project.db"));s.LoadDemoFixture();var first=s.Calculation.Id;
            s.LoadDemoFixture("AS/NZS 1170.2:2021",false);var second=s.Calculation.Id;
            Assert.NotEqual(first,second);Assert.NotNull(new SqliteSnapshotStore(s.DatabasePath).Load<WindCalculationRun>(first));
            s.LoadDemoFixture();Assert.Equal(first,s.Calculation.Id);
        }
        finally {Directory.Delete(dir,true);}
    }
    [Fact] public async Task EndToEnd_PersistsSameCalculationApprovalAssignmentReport()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var path=Path.Combine(dir,"project.db");var s=new DemoSession(path);s.LoadDemoFixture();
            Assert.Equal(2100,s.FacadeRows.Single(r=>r.Frame=="B1").LineLoadNPerM,5);
            s.RecalculateFacade(FacadeMethod.StaticallyEquivalent);
            Assert.Equal(2000,s.FacadeRows.Single(r=>r.Frame=="B1").LineLoadNPerM,5);
            Assert.NotEmpty(s.ValidateRoof().Where(i=>i.Blocking));
            s.ExcludeShell("RX","Test Engineer","NeedsSplitInCSI; excluded from simulation scope");
            s.SetSign("R1",ManualLocal3Direction.Positive,false,"Engineer","Axis reviewed");
            s.SetSign("R2",ManualLocal3Direction.Negative,false,"Engineer","Axis reviewed");
            Assert.Empty(s.ValidateRoof().Where(i=>i.Blocking));var a=s.ApproveRoof("Engineer");
            Assert.Equal(2,s.PreviewAssignment().ProposedLoads.Length);
            await s.ApplyAsync();Assert.True((await s.VerifyAsync()).Matches);
            var assigned=s.Assignment!.Id;
            Assert.Equal(assigned,(await s.ApplyAsync()).Id);Assert.True((await s.VerifyAsync()).Matches);
            var id=s.SaveReport();s.ExportReport(Path.Combine(dir,"report.html"));
            var store=new SqliteSnapshotStore(path);
            Assert.Equal(a.ContentHash,store.Load<RoofMappingApproval>(a.Id)!.ContentHash);
            Assert.Equal(s.Calculation.Id,s.OpenReport(id).Calculation.Id);
            Assert.Contains("<svg",File.ReadAllText(Path.Combine(dir,"report.html")));
        }
        finally {Directory.Delete(dir,true);}
    }
    [Fact] public void EditingAfterApproval_LeavesStoredApprovalUnchanged()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var s=new DemoSession(Path.Combine(dir,"project.db"));s.LoadDemoFixture(includeCrossing:false);
            s.SetSign("R1",ManualLocal3Direction.Positive,false,"Engineer","Reviewed");
            s.SetSign("R2",ManualLocal3Direction.Negative,false,"Engineer","Reviewed");s.ValidateRoof();
            var a=s.ApproveRoof("Engineer");var before=SnapshotJson.Serialize(a);
            s.SetSign("R1",ManualLocal3Direction.Positive,false,"Engineer","Second reviewed revision");s.ValidateRoof();
            var revised=s.ApproveRoof("Engineer");Assert.NotEqual(a.Id,revised.Id);
            s.SetPattern("WX_POS_IP_NEG");Assert.Null(s.Roof.Approval);
            Assert.Equal(before,SnapshotJson.Serialize(new SqliteSnapshotStore(s.DatabasePath).Load<RoofMappingApproval>(a.Id)));
        }
        finally {Directory.Delete(dir,true);}
    }
}
