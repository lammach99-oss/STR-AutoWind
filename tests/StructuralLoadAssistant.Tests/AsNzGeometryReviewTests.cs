using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Application;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNzGeometryReviewTests
{
    [Fact]
    public void SelectedRoofPlaneProducesSiCandidatesButNeverQualifiesUnreviewedEnvelope()
    {
        var model=StructuralLoadAssistant.Csi.Fake.DemoFixtures.Model();
        var roof=new ShellGeometry("roof","plane",[new(100,200,10),new(110,200,10),new(110,220,15),new(100,220,15)],new(0,0,1),new(0,0,1));
        var result=AsNzReadOnlyGeometryReview.FromSnapshot(model with {Shells=[roof],Frames=[]},5,"Owner supplied natural ground at roof footprint","Selected roof only");
        Assert.Equal(10,result.LengthM);Assert.Equal(20,result.WidthM);
        Assert.Equal(10,result.PeakHeightM);Assert.Equal(7.5,result.AverageHeightM);
        Assert.Equal(14.0362434679,result.PitchDegrees!.Value,8);
        Assert.False(result.AutomaticGeometryQualified);
        Assert.Contains("MODEL_GEOMETRY_REVIEW_REQUIRED",result.Status);
    }
    [Fact]
    public void GroundDatumAndQualifiedSelectionAreNeverInvented()
    {
        var result=AsNzReadOnlyGeometryReview.FromSnapshot(StructuralLoadAssistant.Csi.Fake.DemoFixtures.Model(),null,"","");
        Assert.Null(result.PeakHeightM);Assert.Null(result.PitchDegrees);
        Assert.False(result.AutomaticGeometryQualified);
    }
}
