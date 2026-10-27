using System.Collections.Immutable;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;
namespace StructuralLoadAssistant.Tests;
public class ExternalFrameMappingTests
{
    [Fact]
    public void ZoneIntersectionClipsPhysicalPolygonBeforeTransfer()
    {
        var p=new PressureZonePolygon("roof",[new(0,0,0),new(4,0,0),new(4,3,0),new(0,3,0)],new(0,0,1),"E","Plane");
        var pieces=PressureSurfaceTributaryMapper.Intersect(p,[new(2,0,0),new(5,0,0),new(5,3,0),new(2,3,0)]);
        Assert.Single(pieces);Assert.Equal(6,ZonePolygonEngine.Area(pieces[0].Vertices.Select(v=>new UvPoint(v.X,v.Y)).ToImmutableArray()),8);
    }
    [Fact]
    public void RoofUsesTrueInclinedAreaAndSignedGlobalComponents()
    {
        double a=14*Math.PI/180;var v=new Vec3(0,Math.Cos(a)*3,Math.Sin(a)*3);Vec3 Add(Vec3 p,Vec3 q)=>new(p.X+q.X,p.Y+q.Y,p.Z+q.Z);
        var panel=new PressureZonePolygon("roof",[new(0,0,0),new(2,0,0),Add(new(2,0,0),v),v],new(0,-Math.Sin(a),Math.Cos(a)),"E","Reviewed plane");
        var frames=ImmutableArray.Create(new FrameGeometry("a","F",new(0,0,0),new(2,0,0)),new FrameGeometry("b","F",v,Add(v,new(2,0,0))));
        var mapped=PressureSurfaceTributaryMapper.Map(panel,-1000,frames,new("roof",["a"],["b"],"E","Roof spans between these actual beams",true));
        Assert.Equal(6,mapped.AreaM2,7);Assert.True(mapped.Check.Passed);
        Assert.Equal(6000*Math.Cos(a),mapped.Check.FrameForceN.Z,6);
        Assert.Equal(-6000*Math.Sin(a),mapped.Check.FrameForceN.Y,6);
    }
    [Fact]
    public void MissingSupportAndUnreviewedTransferAreBlocked()
    {
        var panel=new PressureZonePolygon("p",[new(0,0,0),new(2,0,0),new(2,0,3),new(0,0,3)],new(0,1,0),"E","Wall");
        Assert.Throws<ArgumentException>(()=>PressureSurfaceTributaryMapper.Map(panel,1000,[],new("p",["missing"],["other"],"E","review",true)));
    }
    [Fact]
    public void WallPressureTransfersToReviewedTopBottomBeamsWithForceAndMomentBalance()
    {
        var panel=new PressureZonePolygon("wall",[new(0,0,0),new(2,0,0),new(2,0,3),new(0,0,3)],new(0,-1,0),"Engineer","Outward wall normal / vertical panel supports");
        var frames=ImmutableArray.Create(new FrameGeometry("bottom","F",new(0,0,0),new(2,0,0)),new FrameGeometry("top","F",new(2,0,3),new(0,0,3)));
        var mapped=PressureSurfaceTributaryMapper.Map(panel,1000,frames,new("wall",["bottom"],["top"],"Engineer","Simply supported vertical wall",true));
        Assert.True(mapped.Check.Passed);
        Assert.Equal(6000,mapped.Check.SurfaceForceN.Y,7);
        Assert.Equal(-9000,mapped.Check.SurfaceMomentNm.X,7);
        Assert.Equal(2,mapped.Loads.Length);
        Assert.All(mapped.Loads,l=>{Assert.Equal(1500,l.StartNPerM.Y,7);Assert.Equal(0,l.StartFraction,7);Assert.Equal(1,l.EndFraction,7);});
    }
}
