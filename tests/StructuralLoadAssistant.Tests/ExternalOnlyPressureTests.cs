using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;
public class ExternalOnlyPressureTests
{
    [Fact]
    public void ExternalLookupDoesNotSelectAnyInternalRows()
    {
        var geometry=AsNzAutoLookupFixture.Geometry();
        var r=new AsNz2021LocalAutoLookupService().ResolveExternalOnly(geometry,AsNzAutoLookupFixture.Site() with {Openings="",Debris=""},AsNzAutoLookupFixture.Source());
        Assert.DoesNotContain(r.Values,v=>v.Key.Contains("Cpi")||v.Key.EndsWith("_Kci")||v.Key.EndsWith("_Kv")||v.Key=="Enclosure");
        Assert.Equal("ExternalOnly",r.Request.Options["PressureMode"]);
        Assert.True(AsNz2021LocalAutoLookupService.HasAuthenticatedLookup(r.Request));
    }
    [Fact]
    public void ExplicitSurfaceInputsHaveIndependentPartialResultsAndNoInternalContract()
    {
        var input=ExternalWindFixture.Request();var run=AsNzExternalPressureService.Execute(input);
        Assert.Contains(run.Cases,c=>c.PressurePa.HasValue);
        Assert.Equal("EngineerControlledSeparate",run.InternalPressureMode);
        Assert.False(run.NetPressureCalculated);
        var wall=run.Cases.First(c=>c.SurfaceId=="wall"&&c.State==LimitState.Uls&&c.WindFrom==WindDirection.PositiveX);
        Assert.Equal(672,wall.PressurePa!.Value,8);Assert.Equal(672,wall.TractionPa!.Value.Y,8);
    }
    [Fact]
    public void ExternalMatrixDoesNotRequireInternalInputsOrInventNetPressure()
    {
        var original=AsNzMatrixFixture.Request();
        var request=original with {Options=original.Options.SetItem("PressureMode","ExternalOnly"),ExplicitCoefficients=original.ExplicitCoefficients.RemoveRange(original.ExplicitCoefficients.Keys.Where(k=>k.Contains("Cpi")||k.EndsWith("_Kci")||k.EndsWith("_Kv")))};
        var run=AsNz2021ReviewCalculation.Execute(request);
        Assert.True(run.NumericalComputationPossible);
        Assert.Equal(160,run.AsNz2021Cases.Length);
        Assert.All(run.AsNz2021Cases,r=>{Assert.Null(r.NetPa);Assert.Null(r.InternalPa);Assert.Null(r.InternalShape);});
        Assert.DoesNotContain(run.Issues,i=>i.Code.Contains("Cpi")||i.Code.Contains("Kci")||i.Code.Contains("Kv"));
        var wall=run.AsNz2021Cases.First(r=>r.Surface=="Windward"&&r.LimitState==LimitState.Uls);
        Assert.Equal(672,wall.ExternalPa!.Value,8);Assert.NotNull(wall.PressureVectorPa);
        Assert.Contains("EXTERNAL",run.QualificationStatus);
    }
}
