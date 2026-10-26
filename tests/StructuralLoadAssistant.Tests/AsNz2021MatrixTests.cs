using StructuralLoadAssistant.Csi.Fake;
using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNz2021MatrixTests
{
    [Fact]
    public void DirectionSectorInterpolatesAcrossNorthAndAppliesOnlyUlsFloor()
    {
        double[] speeds = [20, 24, 40, 22, 18, 16, 14, 12];
        Assert.Equal(32, AsNz2021MatrixEngine.DesignSpeed(speeds, 22.5, LimitState.Sls), 8);
        Assert.Equal(30, AsNz2021MatrixEngine.DesignSpeed(speeds, 270, LimitState.Uls), 8);
        Assert.Equal(16, AsNz2021MatrixEngine.DesignSpeed(speeds, 270, LimitState.Sls), 8);
    }
    [Fact]
    public void ExplicitShapeFactorsKeepInternalAndExternalSeparate()
    {
        var p = AsNz2021MatrixEngine.Pressure(40, 1.2, 1.1, 0.7, 0.8, 0.9, 1.2, 0.95, -0.2, 0.85, 0.75);
        Assert.Equal(606.73536, p.ExternalPa, 6);
        Assert.Equal(-134.64, p.InternalPa, 6);
        Assert.Equal(741.37536, p.NetPa, 6);
    }
    [Fact]
    public void MatrixPreserves320CasesAndSignedPressuresWithoutQualification()
    {
        var run = new AsNz1170_2_2021CalculationStrategy().Execute(AsNzMatrixFixture.Request());
        Assert.True(run.NumericalComputationPossible);
        Assert.Equal(320, run.AsNz2021Cases.Length);
        var wall = Assert.Single(run.AsNz2021Cases.Where(c => c.Id == "Uls_PositiveX_Windward_0_0_Positive"));
        Assert.Equal(672, wall.ExternalPa); Assert.Equal(192,wall.InternalPa); Assert.Equal(480,wall.NetPa);
        Assert.Equal(480,wall.PressureVectorPa!.Value.X);
        Assert.False(run.StandardsDataIntegrityVerified);
        Assert.Equal(64,run.ContentHash.Length);
    }
    [Fact]
    public void MissingFactorsBlockAndSnapshotRemainsSerializable()
    {
        var req = AsNzMatrixFixture.Request();
        var run = AsNz2021ReviewCalculation.Execute(req with { ExplicitCoefficients = req.ExplicitCoefficients.Remove("Mc") });
        Assert.False(run.NumericalComputationPossible);
        Assert.All(run.AsNz2021Cases,c=>Assert.Null(c.NetPa));
        Assert.Equal(64,run.ContentHash.Length);
    }    [Theory]
    [InlineData(0)] [InlineData(9.9)] [InlineData(10)] [InlineData(14)] [InlineData(25)]
    public void ExplicitPitchIsPreservedWithoutSelectingUnverifiedCoefficientMethods(double pitch)
    {
        var run=AsNz2021ReviewCalculation.Execute(AsNzMatrixFixture.Request() with { RoofSlopeDegrees=pitch });
        Assert.True(run.NumericalComputationPossible);
        Assert.Equal(pitch,run.Request.RoofSlopeDegrees);
        var roof=run.AsNz2021Cases.First(c=>c.Surface=="Roof");
        Assert.Equal(Math.Cos(pitch*Math.PI/180),roof.OutwardNormal!.Value.Z,10);
        Assert.False(run.PrimarySourceApplicabilityVerified);
    }
    [Theory]
    [InlineData(0.2499)] [InlineData(0.25)] [InlineData(0.5)] [InlineData(1)]
    public void PhysicalBandsConserveDepthAndNeverProduceNegativeExtent(double ratio)
    {
        var request=AsNzMatrixFixture.Request();
        double depth=request.ReferenceHeightM/ratio;
        var run=AsNz2021ReviewCalculation.Execute(request with { BuildingLengthM=depth });
        var rows=run.AsNz2021Cases.Where(c=>c.Direction==WindDirection.PositiveX&&c.LimitState==LimitState.Uls&&c.Id.EndsWith("Positive"));
        foreach(var group in rows.Where(c=>c.Surface=="SideLeft"||c.Surface=="Roof").GroupBy(c=>(c.Surface,c.RoofCase)))
        {
            Assert.All(group,c=>Assert.True(c.ToM>=c.FromM));
            Assert.Equal(depth,group.Sum(c=>c.ToM!.Value-c.FromM!.Value),9);
        }
    }
    [Fact]
    public void WorkbookExampleArithmeticIsReproducedOnlyWithExplicitInputs()
    {
        var req=AsNzMatrixFixture.Request();
        var b=req.ExplicitCoefficients.ToBuilder();
        void Set(string key,double v)=>b[key]=b[key] with {Value=v,Document="Owner workbook comparison example",SourceNotes="Illustrative cached example; not a standards lookup"};
        Set("VR_Uls",80);Set("VR_Sls",53);Set("Mz_cat",0.91);Set("Mc",1.05);
        for(int i=0;i<8;i++)Set("Md_"+i*45,0.9);
        var run=AsNz2021ReviewCalculation.Execute(req with {ExplicitCoefficients=b.ToImmutable()});
        Assert.Equal(68.796,run.ComputedQuantities["Vdes_Uls_PositiveX"],8);
        var wall=run.AsNz2021Cases.First(c=>c.Id=="Uls_PositiveX_Windward_0_0_Positive");
        Assert.Equal(1987.81363872,wall.ExternalPa!.Value,7);
        // Workbook uses Cpe=0.8: 2271.78701568 Pa. Explicit 0.7 produces the documented delta.
        Assert.Equal(-283.97337696,wall.ExternalPa.Value-2271.78701568,7);
    }}



