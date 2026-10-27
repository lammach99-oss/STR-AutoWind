using System.Collections.Immutable;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNzAutoLookupTests
{
    [Theory]
    [InlineData(2,1)]
    [InlineData(4,1.125)]
    [InlineData(6,1.25)]
    public void OrderedTerrainGridInterpolatesOnlyWithinDeclaredRange(double height,double expected)
    {
        var source=AsNzAutoLookupFixture.Source();var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mz");
        var value=new AsNz2021LocalAutoLookupService().Select(source,"Mz","Mz",row.CategoricalConditions,ImmutableDictionary<string,double>.Empty.Add("Height",height));
        Assert.Equal(AutoLookupState.Resolved,value.State);Assert.Equal(expected,value.Value!.Value,8);
    }
    [Fact]
    public void BlockedWorksheetDoesNotDumpHundredsOfMissingFactorsInMainPdf()
    {
        var r=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {Region=""},AsNzAutoLookupFixture.Source());
        var run=AsNz2021ReviewCalculation.Execute(r.Request);
        var doc=WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Synthetic auto review","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("auto")),run);
        string html=new AsNz2021WorkbookPdfRenderer().BuildHtml(doc);
        Assert.Contains("See detailed audit for",html);
        Assert.DoesNotContain("MISSING-Uls_PositiveX_Roof_2_4_Kv",html);
    }
    [Fact]
    public void DifferentWallSamplingHeightRequiresSeparateInternalPressureBasis()
    {
        var r=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {WindwardHeightM=2},AsNzAutoLookupFixture.Source());
        Assert.False(r.NumericalReviewPossible);
        Assert.Contains(r.Values,v=>v.Key=="WallHeightBasis");
    }
    [Fact]
    public void LookupAuditUsesRowsInsteadOfOneUnbreakableJsonParagraph()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzAutoLookupFixture.Resolve().Request);
        var doc=WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Synthetic auto review","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("auto")),run);
        var html=new AsNz2021WorkbookPdfRenderer().BuildAuditHtml(doc);
        Assert.Contains("Lookup key / state",html);
        Assert.DoesNotContain("&quot;RecordIds&quot;",html);
    }
    [Fact]
    public void TerrainGridRejectsExtrapolationAndDuplicateRows()
    {
        var source=AsNzAutoLookupFixture.Source();var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mz");var service=new AsNz2021LocalAutoLookupService();
        Assert.Equal(AutoLookupState.OutsideDomain,service.Select(source,"Mz","Mz",row.CategoricalConditions,ImmutableDictionary<string,double>.Empty.Add("Height",7)).State);
        var duplicate=source with {Bundle=new(source.Bundle.Manifest,source.Bundle.Records.Add(row))};
        Assert.Equal(AutoLookupState.Ambiguous,service.Select(duplicate,"Mz","Mz",row.CategoricalConditions,row.NumericalConditions).State);
    }
    [Theory]
    [InlineData(0,30)]
    [InlineData(360,30)]
    [InlineData(-360,30)]
    [InlineData(45,40)]
    [InlineData(-45,20)]
    public void DirectionSectorsIncludeBoundariesAndWrap(double bearing,double expected)
    {
        Assert.Equal(expected,AsNz2021MatrixEngine.DesignSpeed(new double[]{20,30,40,10,10,10,10,10},bearing,LimitState.Sls),8);
    }
    [Fact]
    public void NumericallyInconsistentFlatGeometryBlocksSelector()
    {
        var r=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry() with {RidgeHeightM=9},AsNzAutoLookupFixture.Site(),AsNzAutoLookupFixture.Source());
        Assert.False(r.NumericalReviewPossible);
    }
    [Fact]
    public void PrivateLoaderRequiresRightsFileAndDoesNotLoadPublicSyntheticBundle()
    {
        Assert.ThrowsAny<IOException>(()=>AsNzPrivateAutoSourceLoader.Load(Path.Combine(AppContext.BaseDirectory,"../../../../../fixtures/standards/asce7-22-synthetic")));
    }
    [Fact]
    public void AutoWorksheetUsesStateScopedInputsAndCurrentPitch()
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzAutoLookupFixture.Resolve().Request);
        var doc=WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Synthetic auto review","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("auto")),run);
        var renderer=new AsNz2021WorkbookPdfRenderer();var cells=renderer.ProjectCells(doc);
        Assert.Equal("1.125",cells.Single(c=>c.Address=="K28").Html);
        Assert.Equal("1.215",cells.Single(c=>c.Address=="K34").Html);
        Assert.DoesNotContain("14�",renderer.BuildHtml(doc));
    }
    [Theory]
    [InlineData("AU","A0")]
    [InlineData("NZ","SYNTHETIC")]
    [InlineData("AU","C")]
    [InlineData("AU","")]
    public void UnsupportedSiteCannotReuseSyntheticRows(string country,string region)
    {
        var r=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {Country=country,Region=region},AsNzAutoLookupFixture.Source());
        Assert.False(r.NumericalReviewPossible);
    }
    [Fact]
    public void InvalidHeightDoesNotCreateCalculatedResult()
    {
        var r=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {WindwardHeightM=double.NaN},AsNzAutoLookupFixture.Source());
        Assert.Contains(r.Values,v=>v.Key=="AreaHeight"&&v.State!=AutoLookupState.Resolved);
        Assert.False(r.NumericalReviewPossible);
    }
    [Fact]
    public void ChangingSiteEvidenceChangesImmutableRequestIdentity()
    {
        var service=new AsNz2021LocalAutoLookupService();var g=AsNzAutoLookupFixture.Geometry();var source=AsNzAutoLookupFixture.Source();var site=AsNzAutoLookupFixture.Site();
        var a=service.Resolve(g,site,source);var b=service.Resolve(g,site with {GeometryReason="Other engineer decision"},source);
        Assert.NotEqual(StructuralLoadAssistant.Domain.SnapshotJson.Hash(a.Request),StructuralLoadAssistant.Domain.SnapshotJson.Hash(b.Request));
    }
    [Fact]
    public void SyntheticSourceResolvesCompleteFlatMatrixWithSeparateLimitStates()
    {
        var result=AsNzAutoLookupFixture.Resolve();
        Assert.True(result.NumericalReviewPossible,string.Join(";",result.Values.Where(v=>v.Value==null).Select(v=>v.Key+":"+v.Reason)));
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.True(run.NumericalComputationPossible);
        Assert.False(result.ProductionNormativeQualified);

        Assert.Equal(320,run.AsNz2021Cases.Length);
        Assert.Contains("source-selected",run.TraceNodes.First(t=>t.NodeId=="Vdes_Uls_PositiveX").SymbolicEquation);
        Assert.Equal(45,run.ComputedQuantities["Vdes_Uls_PositiveX"],8);
        Assert.Equal(22.5,run.ComputedQuantities["Vdes_Sls_PositiveX"],8);
        var row=run.AsNz2021Cases.Single(c=>c.Id=="Uls_PositiveX_Windward_0_0_Positive");
        Assert.Equal(607.5,row.NetPa!.Value,8);
    }
    [Fact]
    public void AbsentPrivateSourceProducesDiagnosticRequestWithoutInheritedCoefficients()
    {
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzMatrixFixture.Request(),new("AU",""),null);
        Assert.False(result.NumericalReviewPossible);Assert.DoesNotContain(result.Request.ExplicitCoefficients.Keys,k=>k.Contains("Cpe")||k.StartsWith("VR_"));
        Assert.Contains("LOCAL SOURCE DATA UNAVAILABLE",result.Status);
        Assert.False(AsNz2021ReviewCalculation.Execute(result.Request).NumericalComputationPossible);
    }
    [Fact]
    public void PublicSelectionRejectsRightsMissingEvenWhenExactRowExists()
    {
        var row=new CoefficientRecord("SYNTHETIC-ONLY","Synthetic grid","Test clause","Mz",ImmutableDictionary<string,string>.Empty,ImmutableDictionary<string,double>.Empty,7,"dimensionless","positive",InterpolationPolicy.None);
        var template=StandardsDataBundle.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory,"../../../../../fixtures/standards/asce7-22-synthetic"));
        var source=new AsNzAutoSource(new(template.Manifest,[row]),"","",ImmutableDictionary<string,string>.Empty.Add("Synthetic grid","test only"),false);
        var result=new AsNz2021LocalAutoLookupService().Select(source,"Mz","Mz",ImmutableDictionary<string,string>.Empty,ImmutableDictionary<string,double>.Empty);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,result.State);
        Assert.Null(result.Value);
    }
    [Fact]
    public void FlatSourceNeverAcceptsFourteenDegreeMonopitch()
    {
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzMatrixFixture.Request(),new("AU","TEST"),null);
        Assert.False(result.NumericalReviewPossible);Assert.Contains(result.Values,v=>v.Key=="RoofMethod"&&v.State==AutoLookupState.OutsideDomain);
    }
}
