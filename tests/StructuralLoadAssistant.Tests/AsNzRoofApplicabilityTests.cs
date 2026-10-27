using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;

// Synthetic software routing assertions, not independent numerical standards benchmarks.
public sealed class AsNzRoofApplicabilityTests
{
    [Theory]
    [InlineData("Gable",9.999999,AsNzRoofFace.Upwind,"Table 5.3(A)")]
    [InlineData("Gable",10,AsNzRoofFace.Upwind,"Table 5.3(B)")]
    [InlineData("Gable",14,AsNzRoofFace.Upwind,"Table 5.3(B)")]
    [InlineData("Gable",9.999999,AsNzRoofFace.Downwind,"Table 5.3(A)")]
    [InlineData("Gable",10,AsNzRoofFace.Downwind,"Table 5.3(C)")]
    [InlineData("Gable",25,AsNzRoofFace.Crosswind,"Table 5.3(A)")]
    [InlineData("Hip",10,AsNzRoofFace.Crosswind,"Table 5.3(C)")]
    [InlineData("Hip",9.999999,AsNzRoofFace.Crosswind,"Table 5.3(A)")]
    [InlineData("Monoslope",14,AsNzRoofFace.Downwind,"Table 5.3(A)")]
    [InlineData("Monoslope",14,AsNzRoofFace.Upwind,"Table 5.3(B)")]
    [InlineData("Flat",0,AsNzRoofFace.Upwind,"Table 5.3(A)")]
    [InlineData("LowSlope",7,AsNzRoofFace.Upwind,"Table 5.3(A)")]
    public void FormFaceAndExactPitchSelectDistinctSourceRoute(string form,double pitch,AsNzRoofFace face,string table)
    {
        var result=AsNzRoofApplicability.Evaluate(form,pitch,face);
        Assert.True(result.Supported,result.Reason);Assert.Equal(table,result.Table);
    }
    [Theory]
    [InlineData("Flat",10,AsNzRoofFace.Upwind)]
    [InlineData("LowSlope",10,AsNzRoofFace.Upwind)]
    [InlineData("Monoslope",14,AsNzRoofFace.Crosswind)]
    [InlineData("Barrel",0,AsNzRoofFace.Upwind)]
    [InlineData("Gable",90,AsNzRoofFace.Downwind)]
    public void ContradictoryOrUnsupportedGeometryCannotReuseFlatRoute(string form,double pitch,AsNzRoofFace face)
    {
        var result=AsNzRoofApplicability.Evaluate(form,pitch,face);
        Assert.False(result.Supported);Assert.Empty(result.Table);
    }
}
