using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

public class DemoFacadeSelectionTests
{
    [Fact] public async Task ExplicitPairsDriveProposalsAndInvalidateApproval()
    {
        var dir=Path.Combine(Path.GetTempPath(),"sla-pairs-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            var session=new DemoSession(Path.Combine(dir,"test.db"));session.LoadDemoFixture();
            Assert.Equal(2,session.FacadePairs.Length);
            await session.PreviewFacadeAsync();await session.ApproveFacadeAsync("Engineer");
            session.ClearFacadePairs();Assert.Null(session.FacadeApproval);Assert.Empty(session.FacadeRows);
            session.SelectFacadePair("B1","B2");
            Assert.Equal(2,session.PanelRows.Length);Assert.DoesNotContain(session.FacadeRows,r=>r.Frame=="B3");
            session.RecalculateFacade(FacadeMethod.StaticallyEquivalent);
            Assert.Equal(2000,session.FacadeRows.Single(r=>r.Frame=="B1").LineLoadNPerM,6);
            Assert.Throws<ArgumentException>(()=>session.SelectFacadePair("B1","B-invalid"));
            Assert.Single(session.FacadePairs);
        }
        finally {Directory.Delete(dir,true);}
    }
}
