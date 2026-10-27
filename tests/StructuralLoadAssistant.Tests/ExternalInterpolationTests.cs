using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;
public class ExternalInterpolationTests
{
    [Fact]
    public void SameAlternativeInterpolationRetainsEndpointsAndRejectsSignCrossing()
    {
        var a=ExternalWindFixture.Input("Cpe",-.8,"Table 5.3(A) / A");var b=a with{Value=-1.2};
        var r=ExternalPressureInterpolation.Interpolate(.75,.5,1,a,b,"Case1","Case1",true);
        Assert.Equal(-1,r.Value,8);Assert.Contains("weight=0.5",r.SourceNotes);
        Assert.Throws<ArgumentException>(()=>ExternalPressureInterpolation.Interpolate(.75,.5,1,a,b with{Value=.2},"Case1","Case1",true));
        Assert.Throws<ArgumentException>(()=>ExternalPressureInterpolation.Interpolate(.75,.5,1,a,b,"Case1","Case2",true));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ExternalPressureInterpolation.Interpolate(2,.5,1,a,b,"Case1","Case1",true));
    }
}
