using System.Globalization;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public sealed class Sprint4B6CTests
{
    [Fact]
    public void WidthsMatchIndependentlySuppliedWorkbookVectorAndHtmlRatios()
    {
        double[] expected=[6.140625,13,13,13,13,13,13,13,13,6.5703125,13,5.5703125,13,4.85546875];
        Assert.Equal(expected,AsNzWorkbookCellLayout.Widths);
        string html=new AsNz2021WorkbookPdfRenderer().BuildHtml(Sprint4B6BTests.Document(AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison())));
        foreach(double width in expected)Assert.Contains($"width:{(100*width/expected.Sum()).ToString("F4",CultureInfo.InvariantCulture)}%",html);
    }
    [Theory]
    [InlineData(0,3.8,0,"Monoslope",14)]
    [InlineData(-1,3.8,0,"Monoslope",14)]
    [InlineData(3.2,3.1,0,"Monoslope",14)]
    [InlineData(3.2,3.8,0,"Flat",14)]
    public void ImpossibleOrUnsupportedGeometryBlocksAllSignedPressures(double eave,double ridge,double ground,string roof,double pitch)
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison() with {EaveHeightM=eave,RidgeHeightM=ridge,GroundElevationM=ground,RoofForm=roof,RoofSlopeDegrees=pitch});
        Assert.False(run.NumericalComputationPossible);
        Assert.Equal(320,run.AsNz2021Cases.Length);
        Assert.All(run.AsNz2021Cases,c=>{Assert.Null(c.ExternalPa);Assert.Null(c.InternalPa);Assert.Null(c.NetPa);});
        Assert.Contains("BLOCKED",run.QualificationStatus);
    }
    [Fact]
    public void AuditIdentityPrecedesEvidenceRatherThanStrandingLastPage()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison());
        string html=new AsNz2021WorkbookPdfRenderer().BuildAuditHtml(Sprint4B6BTests.Document(run));
        Assert.True(html.IndexOf("Geometry and identity",StringComparison.Ordinal)<html.IndexOf("All direction / limit-state",StringComparison.Ordinal));
    }
    [Fact]
    public void ValidOwnerExampleKeepsReviewWarningAndNonfinitePolicy()
    {
        var request=AsNzMatrixFixture.LegacyWorkbookComparison();
        var run=AsNz2021ReviewCalculation.Execute(request);
        Assert.True(run.NumericalComputationPossible);
        Assert.Contains(run.Issues,i=>i.Code=="GEOMETRY_REVIEW_REQUIRED");
        Assert.Throws<ArgumentException>(()=>AsNz2021ReviewCalculation.Execute(request with {GroundElevationM=double.NaN}));
        var zero=AsNz2021ReviewCalculation.Execute(request with {BuildingWidthM=0});
        Assert.False(zero.NumericalComputationPossible);Assert.All(zero.AsNz2021Cases,c=>Assert.Null(c.NetPa));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CandidateHelperRejectsNonfiniteOrDegenerateSelectedRoof(bool nonfinite)
    {
        var model=DemoFixtures.Model();
        var roof=new StructuralLoadAssistant.Domain.ShellGeometry("roof","invalid",[new(0,0,3),new(1,1,3),new(nonfinite?double.NaN:2,2,3)],new(0,0,1),new(0,0,1));
        var result=StructuralLoadAssistant.Application.AsNzReadOnlyGeometryReview.FromSnapshot(model with {Shells=[roof]},0,"datum","selected roof");
        Assert.Null(result.PitchDegrees);Assert.Null(result.SlopeBearingDegrees);Assert.False(result.AutomaticGeometryQualified);
    }
    [Fact]
    public void RepresentativeSourceMergesRetainEffectiveSpans()
    {
        var cells=new AsNz2021WorkbookPdfRenderer().ProjectCells(Sprint4B6BTests.Document(AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.LegacyWorkbookComparison())));
        foreach(var (address,span) in new[]{("A5",9),("J5",2),("L5",3),("A6",2),("C6",7),("F55",3),("I55",3),("F57",3),("I57",3),("A64",11),("A65",5),("F65",3),("I65",3),("A108",11),("F111",3),("I111",3),("A117",11),("F120",3),("I120",3)})
            Assert.Equal(span,cells.Single(c=>c.Address==address).ColumnSpan);
    }
}
