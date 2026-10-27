using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public class ExternalReportTests
{
    [Fact]
    public void ExistingWorksheetProjectsExternalOnlyRows()
    {
        var r=AsNzMatrixFixture.Request();r=r with{Options=r.Options.SetItem("PressureMode","ExternalOnly")};
        var run=StructuralLoadAssistant.Engineering.AsNz2021ReviewCalculation.Execute(r);
        var model=new ReportModel(new StructuralLoadAssistant.Domain.Project(r.ProjectId,"Review","SI"),DemoFixtures.Calculation(r.StandardEdition),DemoFixtures.Manifest(r.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("external"));
        var html=new AsNz2021WorkbookPdfRenderer().BuildHtml(WindPdfReportDocumentBuilder.Build(model,run));
        Assert.Contains("0.672",html);Assert.Contains("ENGINEER CONTROLLED SEPARATELY",html);
    }
    [Fact]
    public async Task WorksheetAndAuditUseSameRunWithoutInternalPressure()
    {
        var p=await ExternalWindReviewCoordinator.PreviewAsync(ExternalWindFixture.Request(),ExternalWindFixture.Model(),[new("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support())]);
        var main=ExternalWindReportBuilder.BuildHtml(p.Run,p.Model,p.Plans,p.Blockers,false);
        var audit=ExternalWindReportBuilder.BuildHtml(p.Run,p.Model,p.Plans,p.Blockers,true);
        Assert.Contains("EXTERNAL ONLY",main);Assert.Contains("ENGINEER CONTROLLED SEPARATELY",main);
        Assert.Contains("672",main);Assert.Contains("bottom",audit);Assert.Contains(p.Run.ContentHash,audit);
        Assert.All(p.Run.Cases,c=>Assert.Contains(c.Id,audit));
        Assert.DoesNotContain("INPUT REQUIRED: Cpi",main);
        var csv=ExternalWindReportBuilder.ScheduleCsv(p.Run,p.Model,p.Plans);Assert.Contains("EXTERNAL_ONLY_REVIEW_NOT_FOR_DESIGN",csv);Assert.Contains("GlobalX",csv);Assert.Contains("bottom",csv);
    }
}
