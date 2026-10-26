using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNzPrivateLookupTests
{
    [Fact]
    public void AuthorizedInputInterpolationUsesBothTerrainAndHeightBounds()
    {
        // Artificial grid deliberately unrelated to a standards table.
        var grid=new double[,]{{1,2},{3,4}};
        Assert.Equal(2.5,AsNz2021InputInterpolation.Terrain(7.5,2.5,[5,10],[2,3],grid),10);
        Assert.Throws<ArgumentOutOfRangeException>(()=>AsNz2021InputInterpolation.Terrain(11,2.5,[5,10],[2,3],grid));
    }
    [Fact]
    public void CoastalInterpolationRequiresDistanceBoundsAndDoesNotSilentlyUseMaximum()
    {
        Assert.Equal(55,AsNz2021InputInterpolation.Linear(25,0,100,50,70),10);
        Assert.Throws<ArgumentOutOfRangeException>(()=>AsNz2021InputInterpolation.Linear(-1,0,100,50,70));
    }
}
