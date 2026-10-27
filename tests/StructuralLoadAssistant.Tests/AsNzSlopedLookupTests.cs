using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;

public sealed class AsNzSlopedLookupTests
{
    [Fact]
    public void UnsupportedMonoslopeLeewardDoesNotSuppressEvidencedExternalRoofReview()
    {
        var result=AsNzSlopedAutoLookupFixture.Resolve();
        Assert.False(result.NumericalReviewPossible);
        Assert.Contains(result.Values,v=>v.Key.Contains("Leeward")&&v.Reason.Contains("5.2(B)"));
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.False(run.NumericalComputationPossible);
        Assert.Contains(run.AsNz2021Cases,c=>c.Surface=="Roof"&&c.Direction==WindDirection.PositiveX&&c.ExternalPa.HasValue);
        Assert.All(run.AsNz2021Cases.Where(c=>c.Surface=="Leeward"),c=>Assert.Null(c.NetPa));
    }
    [Fact]
    public void MissingEnclosureLeavesExternalReviewButNoNetPressure()
    {
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {Openings=""},AsNzAutoLookupFixture.Source());
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.False(result.NumericalReviewPossible);
        Assert.Contains(run.AsNz2021Cases,c=>c.ExternalPa.HasValue);
        Assert.All(run.AsNz2021Cases,c=>Assert.Null(c.NetPa));
    }
    [Fact]
    public void UnreviewedNorthCannotProduceDirectionalPressures()
    {
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {TrueNorthReviewed=false},AsNzAutoLookupFixture.Source());
        Assert.False(result.NumericalReviewPossible);
        Assert.All(AsNz2021ReviewCalculation.Execute(result.Request).AsNz2021Cases,c=>Assert.Null(c.ExternalPa));
    }
    [Theory]
    [InlineData(0,0,AsNzRoofFace.Upwind)]
    [InlineData(0,180,AsNzRoofFace.Downwind)]
    [InlineData(90,0,AsNzRoofFace.Crosswind)]
    [InlineData(360,0,AsNzRoofFace.Upwind)]
    public void UphillBearingSeparatesWindIncidence(double slope,double wind,AsNzRoofFace face)=>Assert.Equal(face,AsNzRoofApplicability.FaceForWind(slope,wind));
    [Fact]
    public void ObliqueIncidenceDoesNotRoundIntoAnEligibleTable()=>Assert.Null(AsNzRoofApplicability.FaceForWind(0,9.99999));
}
