using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNzWorksheetTests
{
    static WindPdfReportDocument Document(EngineeringCalculationRun run) => WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Synthetic worksheet review","SI"), DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("matrix-report")),run);
    [Fact]
    public void AllWorksheetRowsAndMatrixValuesAreRendered()
    {
        var run = AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request());
        string html = new WindWorkbookHtmlReportBuilder().BuildHtml(Document(run));
        for(int row=5;row<=124;row++) Assert.Contains($"id='Flat-{row}'",html);
        Assert.Contains("0.672",html);
        Assert.Contains("data-cell='J46'",html);
        Assert.DoesNotContain("metric-grid",html);
    }
    [Fact]
    public void GenerateCalculatedAndBlockedSamples()
    {
        Assert.True(WindPdfRenderService.IsRendererAvailable,"Edge required for actual PDF acceptance");
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/4b6c"));
        Directory.CreateDirectory(root);
        var request=AsNzMatrixFixture.LegacyWorkbookComparison();
        foreach(var (name,fixture) in new[]{("ASNZ_2021_Explicit_Review",AsNzMatrixFixture.Request(true)),("ASNZ_2021_Varied_Directions",AsNzMatrixFixture.VariedExplicitReview())})
        {
            var fixtureRun=AsNz2021ReviewCalculation.Execute(fixture);
            var fixtureDoc=Document(fixtureRun);
            File.WriteAllText(Path.Combine(root,name+".json"),SnapshotJson.Serialize(fixtureRun));
            new WindPdfRenderService().RenderHtmlToPdf(new AsNz2021WorkbookPdfRenderer().BuildHtml(fixtureDoc),Path.Combine(root,name+"_MAIN.pdf"));
        }
        foreach(bool blocked in new[]{false,true})
        {
            var run=AsNz2021ReviewCalculation.Execute(blocked?request with {ExplicitCoefficients=request.ExplicitCoefficients.Remove("Mc")}:request);
            string stem=blocked?"ASNZ_2021_Blocked_Diagnostic":"ASNZ_2021_FlatWorkbook_Review";
            var document=Document(run);var renderer=new AsNz2021WorkbookPdfRenderer();
            File.WriteAllText(Path.Combine(root,stem+".json"),SnapshotJson.Serialize(run));
            File.WriteAllText(Path.Combine(root,stem+"_cells.json"),SnapshotJson.Serialize(renderer.ProjectCells(document)));
            foreach(bool audit in new[]{false,true})
            {
                string html=audit?renderer.BuildAuditHtml(document):renderer.BuildHtml(document);
                string name=stem+(audit?"_Detailed_Audit":"_MAIN");
                File.WriteAllText(Path.Combine(root,name+".html"),html);
                new WindPdfRenderService().RenderHtmlToPdf(html,Path.Combine(root,name+".pdf"));
            }
        }
    }
}
