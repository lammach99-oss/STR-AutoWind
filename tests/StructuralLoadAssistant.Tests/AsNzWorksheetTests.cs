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
        Assert.Contains("Uls_PositiveX_Windward_0_0_Positive",html);
        Assert.DoesNotContain("metric-grid",html);
    }
    [Fact]
    public void GenerateCalculatedAndBlockedSamples()
    {
        if(!WindPdfRenderService.IsRendererAvailable) return;
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/4b6a"));
        Directory.CreateDirectory(root);
        var request=AsNzMatrixFixture.Request(workbookComparison:true);
        foreach(bool blocked in new[]{false,true})
        {
            var run=AsNz2021ReviewCalculation.Execute(blocked?request with {ExplicitCoefficients=request.ExplicitCoefficients.Remove("Mc")}:request);
            string stem=blocked?"ASNZ_2021_Blocked_Diagnostic":"ASNZ_2021_FlatWorkbook_Comparison_Review";
            var document=Document(run);
            File.WriteAllText(Path.Combine(root,stem+".json"),SnapshotJson.Serialize(run));
            File.WriteAllText(Path.Combine(root,stem+".html"),new WindWorkbookHtmlReportBuilder().BuildHtml(document));
            new WindPdfRenderService().RenderDocument(document,Path.Combine(root,stem+".pdf"));
        }
    }
}

