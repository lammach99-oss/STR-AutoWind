using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

public class FacadeSelectionTests
{
    private static FrameGeometry Frame(string id,double z,double y=0,string group="A") => new(id,group,new(0,y,z),new(10,y,z));
    private static WindState State => new(WindDirection.PositiveX,"review","review",LimitState.Uls);
    [Theory]
    [InlineData(FacadeMethod.EqualSplit,2100,2100)]
    [InlineData(FacadeMethod.StaticallyEquivalent,2000,2200)]
    public void ExplicitPair_PreservesDistributionMethods(FacadeMethod method,double lower,double upper)
    {
        var selection=new FacadeSelection([Frame("L",0),Frame("U",3)]);
        Assert.Empty(selection.Pairs);
        selection.SelectPair("L","U");
        var preview=selection.Preview(z=>1200+z*400/3,0,State,"W",method);
        Assert.Equal(DataProvenance.Unverified,preview.Provenance);
        Assert.Equal(lower,preview.Loads.Single(x=>x.FrameId=="L").LoadNPerM,5);
        Assert.Equal(upper,preview.Loads.Single(x=>x.FrameId=="U").LoadNPerM,5);
    }
    [Fact] public void RejectedPairs_DoNotMutateSelection()
    {
        var selection=new FacadeSelection([Frame("L",0),Frame("U",3),Frame("Offset",6,1),Frame("Other",9,0,"B")]);
        selection.SelectPair("L","U");
        foreach(var pair in new[]{("U","L"),("L","Offset"),("L","Other"),("L","missing"),("L","L")})
            Assert.Throws<ArgumentException>(()=>selection.SelectPair(pair.Item1,pair.Item2));
        Assert.Single(selection.Pairs);
    }
    [Fact] public void GroupOverride_RequiresAuditAndClearsPairs()
    {
        var selection=new FacadeSelection([Frame("L",0),Frame("U",3)]);selection.SelectPair("L","U");
        Assert.Throws<ArgumentException>(()=>selection.OverrideGroup("L","B","",""));
        Assert.Single(selection.Pairs);Assert.Empty(selection.GroupOverrides);
        selection.OverrideGroup("L","B","Engineer","Reviewed facade membership");
        Assert.Empty(selection.Pairs);Assert.Single(selection.GroupOverrides);
        Assert.Throws<ArgumentException>(()=>selection.SelectPair("L","U"));
    }
    [Fact] public void PartialReversedOverlap_UsesExistingMapperFractions()
    {
        var selection=new FacadeSelection([Frame("L",0),new("U","A",new(15,0,3),new(5,0,3))]);
        selection.SelectPair("L","U");var pair=Assert.Single(selection.Pairs);
        Assert.Equal(0.5,pair.LowerStartFraction);Assert.Equal(1,pair.LowerEndFraction);
        Assert.Equal(0.5,pair.UpperStartFraction);Assert.Equal(1,pair.UpperEndFraction);
        Assert.Equal(0,pair.LowerElevationM);Assert.Equal(3,pair.UpperElevationM);
        var loads=selection.Preview(_=>100,0,State,"W",FacadeMethod.EqualSplit).Loads;
        Assert.All(loads,x=>Assert.Equal(0.5,x.StartFraction));Assert.All(loads,x=>Assert.Equal(1,x.EndFraction));
    }
    [Fact] public void InvalidFrames_RequireManualMappingWithoutAddingPairs()
    {
        var selection=new FacadeSelection([Frame("L",0),new("Slope","A",new(0,0,3),new(10,0,4)),
            new("Zero","A",new(0,0,3),new(0,0,3)),new("NaN","A",new(double.NaN,0,3),new(10,0,3)),
            new("Separate","A",new(11,0,3),new(21,0,3))]);
        foreach(var id in new[]{"Slope","Zero","NaN","Separate"})Assert.Throws<ArgumentException>(()=>selection.SelectPair("L",id));
        Assert.Empty(selection.Pairs);
    }
    [Fact] public void DuplicateIds_AreRejectedAndRemovingPairsClearsOnlyRequestedPair()
    {
        Assert.Throws<ArgumentException>(()=>new FacadeSelection([Frame("L",0),Frame("L",3)]));
        var selection=new FacadeSelection([Frame("L",0),Frame("M",3),Frame("U",6)]);
        selection.SelectPair("L","M");selection.SelectPair("M","U");selection.SelectPair("L","M");Assert.Equal(2,selection.Pairs.Length);
        selection.RemovePair("L","M");Assert.Equal("M",Assert.Single(selection.Pairs).LowerFrameId);selection.ClearPairs();Assert.Empty(selection.Pairs);
    }
}
