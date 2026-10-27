using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;

namespace StructuralLoadAssistant.Tests;

public sealed class AsNzRoofWorksheetTests
{
    static WindPdfReportDocument Document(EngineeringCalculationRun run) => WindPdfReportDocumentBuilder.Build(
        new ReportModel(new Project(run.Request.ProjectId,"Synthetic roof worksheet","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),
            DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("roof-worksheet")),run);

    [Fact]
    public void FullFaceSlopedSourceIsNotLabeledAsLowSlopeDistanceBand()
    {
        var result=AsNzSlopedAutoLookupFixture.Resolve();
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        var cells=new AsNz2021WorkbookPdfRenderer().ProjectCells(Document(run));
        Assert.Equal("Whole roof face",cells.Single(c=>c.Address=="A67").Html);
        Assert.Equal("0.000",cells.Single(c=>c.Address=="D67").Html);
        Assert.Equal("30.000",cells.Single(c=>c.Address=="E67").Html);
        Assert.Equal("N/A",cells.Single(c=>c.Address=="A68").Html);
    }

    [Fact]
    public void MonoslopeLabelNeverConflatesRoofWithFlatForm()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request());
        var cells=new AsNz2021WorkbookPdfRenderer().ProjectCells(Document(run));
        Assert.Equal("Monoslope",cells.Single(c=>c.Address=="J14").Html);
    }

    [Fact]
    public void MainWorksheetDisplaysHeightOperandsAndSourceRevisionLimitations()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request());
        string html=new AsNz2021WorkbookPdfRenderer().BuildHtml(Document(run));
        Assert.Contains("Mean roof-height input",html);
        Assert.Contains("eave",html);
        Assert.Contains("AMENDMENT APPLICABILITY UNCONFIRMED",html);
    }

    [Fact]
    public void ClippedOutBandsHaveNoNumericZeroWidthRangeInWorksheet()
    {
        var result=AsNzAutoLookupFixture.Resolve();
        var request=result.Request with {BuildingLengthM=2};
        // Explicit review mode isolates rendering of clipped physical bands from automatic trust.
        request=request with {Options=request.Options.Remove("AutoLookup").Remove("AutoLookupComplete")};
        var run=AsNz2021ReviewCalculation.Execute(request);
        var cells=new AsNz2021WorkbookPdfRenderer().ProjectCells(Document(run));
        Assert.Equal("N/A",cells.Single(c=>c.Address=="D59").Html);
        Assert.Equal("N/A",cells.Single(c=>c.Address=="E59").Html);
    }
}
