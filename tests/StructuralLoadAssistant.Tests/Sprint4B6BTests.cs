using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public sealed class Sprint4B6BTests
{
    public static WindPdfReportDocument Document(EngineeringCalculationRun run) => WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Worksheet review","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("4b6b")),run);
    [Fact]
    public void MainUsesSeparateWorkbookCellsWithoutLedgerOrVisibleRowIndices()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request(true));
        string html=new AsNz2021WorkbookPdfRenderer().BuildHtml(Document(run));
        foreach(string cell in new[]{"D57","E57","F57","I57","E22","F22","K22","L22","N46"}) Assert.Contains($"data-cell='{cell}'",html);
        Assert.DoesNotContain("Workbook row / parameter",html);
        Assert.DoesNotContain("57. From",html);
        Assert.DoesNotContain(" | ",html);
        Assert.DoesNotContain("Full precision calculation trace",html);
    }
    [Fact]
    public void VelocityPressureLabelMatchesActualUnshapedQuantity()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request(true));
        string html=new AsNz2021WorkbookPdfRenderer().BuildHtml(Document(run));
        Assert.Contains("q<sub>ULS</sub> = ½ρ<sub>air</sub>V<sub>des,θ</sub>²C<sub>dyn</sub>",html);
        Assert.DoesNotContain("puls",html);
    }
    [Fact]
    public void PressureTraceDeclaresEveryFactorDependency()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request());
        var trace=run.TraceNodes.Single(t=>t.NodeId=="Uls_PositiveX_Windward_0_0_Positive");
        Assert.Contains("INPUT-PositiveX_Windward_0_0_Kce",trace.Dependencies);
        Assert.Contains("INPUT-Cpi_Positive",trace.Dependencies);
        Assert.Contains("Vdes_Uls_PositiveX",trace.Dependencies);
    }

    [Fact]
    public void LegacyComparisonReproducesCachedPressureGroupsWithoutQualification()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison());
        double P(string id)=>run.AsNz2021Cases.Single(c=>c.Id==id).ExternalPa!.Value/1000;
        Assert.Equal(2.27178701568,P("Uls_PositiveX_Windward_0_0_Positive"),10);
        Assert.Equal(-0.85192013088,P("Uls_PositiveX_Leeward_0_0_Positive"),10);
        Assert.Equal(-1.84582695024,P("Uls_PositiveX_SideLeft_0_0_Positive"),10);
        Assert.Equal(-2.55576039264,P("Uls_PositiveX_Roof_1_0_Positive"),10);
        Assert.Equal(-1.13589350784,P("Uls_PositiveX_Roof_2_0_Positive"),10);
        Assert.False(run.StandardsDataIntegrityVerified);
    }
    [Fact]
    public void RotatedNonuniformDirectionsAndZonesRemainIndependent()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.VariedExplicitReview());
        Assert.True(run.NumericalComputationPossible);
        Assert.NotEqual(run.ComputedQuantities["Vdes_Uls_PositiveX"],run.ComputedQuantities["Vdes_Uls_NegativeX"]);
        Assert.NotEqual(run.ComputedQuantities["Vdes_Sls_PositiveY"],run.ComputedQuantities["Vdes_Sls_NegativeY"]);
        Assert.Contains("NegativeX alternative",new AsNz2021WorkbookPdfRenderer().BuildHtml(Document(run)));
        Assert.All(run.TraceNodes.SelectMany(t=>t.Dependencies),id=>Assert.Contains(run.TraceNodes,t=>t.NodeId==id));
    }

    [Fact]
    public void WorksheetRegistryHasNoOverlappingCellsAndAllZoneBoundsRemainSeparate()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison());
        var cells=new AsNz2021WorkbookPdfRenderer().ProjectCells(Document(run));
        foreach(var group in cells.GroupBy(c=>c.Row))
        {
            var occupied=group.SelectMany(c=>Enumerable.Range(c.Column,c.ColumnSpan)).ToArray();
            Assert.Equal(occupied.Length,occupied.Distinct().Count());
            Assert.All(occupied,c=>Assert.InRange(c,1,14));
        }
        foreach(int row in new[]{57,58,59,60,67,68,69,70,71,76,77,78,79,80,101,102,103,104,111,112,113,114,115,120,121,122,123,124})
        {
            foreach(string col in new[]{"D","E","F","I"})Assert.Contains(cells,c=>c.Address==col+row);
            Assert.Equal(3,cells.Single(c=>c.Address=="F"+row).ColumnSpan);
            Assert.Equal(3,cells.Single(c=>c.Address=="I"+row).ColumnSpan);
        }
    }
    [Fact]
    public void AuditContainsAllCasesAndTraceIdsWhileMainDoesNotCarryLedger()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison());
        var renderer=new AsNz2021WorkbookPdfRenderer();var doc=Document(run);
        string audit=renderer.BuildAuditHtml(doc);
        foreach(var c in run.AsNz2021Cases)Assert.Contains(c.Id,audit);
        foreach(var t in run.TraceNodes)Assert.Contains(t.NodeId,audit);
        Assert.DoesNotContain("All direction / limit-state",renderer.BuildHtml(doc));
    }
    [Fact]
    public void EveryNumericZoneCellHasIndividualBorder()
    {
        var renderer=new AsNz2021WorkbookPdfRenderer();
        var cells=renderer.ProjectCells(Document(AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison())));
        foreach(string address in new[]{"D57","E57","F57","I57","D124","E124","F124","I124"})
            Assert.Contains("zone",cells.Single(c=>c.Address==address).CssClass);
    }
}
