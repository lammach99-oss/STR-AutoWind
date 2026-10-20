using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public sealed class EditableZoneGeometryTests
{
    private static ImmutableArray<UvPoint> Rect(double x, double y, double w, double h) =>
        [new(x,y), new(x+w,y), new(x+w,y+h), new(x,y+h)];
    private static EditableRoofZone Zone(string id, ImmutableArray<UvPoint> points) =>
        new(id,id,"plane","WX+",points,null,1,"source",DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,ZoneGeometryStatus.Valid,[]);

    [Theory]
    [InlineData(0)] [InlineData(0.5)] [InlineData(1)]
    public void Plane_round_trips_rotated_slopes_and_surface_area(double slope)
    {
        Vec3[] p = [new(10,20,8),new(12,22,8+2*slope),new(10,24,8+4*slope),new(8,22,8+2*slope)];
        var plane=RoofPlaneCoordinateSystem.Create("p",p);
        foreach(var v in p) Assert.True((plane.Unproject(plane.Project(v))-v).Length<1e-8);
        Assert.Equal(1,plane.Normal.Length,10);
        Assert.Equal(0,plane.UAxis.Dot(plane.VAxis),10);
        Assert.Equal(Math.Atan(slope)*180/Math.PI,plane.SlopeDegrees,8);
        Assert.Equal(8*Math.Sqrt(1+slope*slope),ZonePolygonEngine.Area(p.Select(plane.Project).ToImmutableArray()),8);
    }

    [Fact]
    public void Plane_axes_fingerprint_and_origin_ignore_vertex_order_and_scope()
    {
        Vec3[] p=[new(0,0,5),new(4,0,5),new(4,4,7),new(0,4,7)];
        var a=RoofPlaneCoordinateSystem.Create("a",p);
        var b=RoofPlaneCoordinateSystem.Create("a",p.Reverse());
        Assert.Equal(a.Normal,b.Normal); Assert.Equal(a.UAxis,b.UAxis); Assert.Equal(a.VAxis,b.VAxis);
        Assert.Equal(a.GeometryFingerprint,b.GeometryFingerprint);
        Assert.True(a.Origin.Cross(a.Normal).Length<1e-8);
        Vec3[] adjacent=[new(4,0,5),new(8,0,5),new(8,4,7),new(4,4,7)];
        var c=RoofPlaneCoordinateSystem.Create("a",adjacent);
        Assert.True((a.Origin-c.Origin).Length<1e-8);
        Assert.Equal(a.Project(p[1]),c.Project(p[1]));
    }

    [Fact]
    public void Coplanar_scope_changes_at_reference_axis_boundary_preserve_saved_zone_geometry()
    {
        var slope=-0.9/Math.Sqrt(0.19);
        Vec3[] Footprint(double width) => [new(0,0,0),new(width,0,slope*width),
            new(width,width,slope*width),new(0,width,0)];
        var original=RoofPlaneCoordinateSystem.Create("plane",Footprint(1));
        var changed=RoofPlaneCoordinateSystem.Create("plane",Footprint(3));
        var common=new Vec3(1,1,slope);
        var before=original.Project(common); var after=changed.Project(common);
        Assert.True(Math.Abs(before.U-after.U)<1e-8,$"U moved from {before.U:R} to {after.U:R}");
        Assert.True(Math.Abs(before.V-after.V)<1e-8,$"V moved from {before.V:R} to {after.V:R}");
        foreach(var savedPoint in Rect(0.2,0.2,0.5,0.5))
            Assert.True((original.Unproject(savedPoint)-changed.Unproject(savedPoint)).Length<1e-8);
    }

    [Theory]
    [InlineData(0.8999999999)] [InlineData(0.9000000001)]
    public void Roof_frame_varies_continuously_across_former_reference_boundary(double normalX)
    {
        RoofPlaneCoordinateSystem Plane(double nx)
        {
            var slope=-nx/Math.Sqrt(1-nx*nx);
            return RoofPlaneCoordinateSystem.Create("p",[new(0,0,5),new(2,0,5+2*slope),
                new(2,2,5+2*slope),new(0,2,5)]);
        }
        var baseline=Plane(0.8999999998); var nearby=Plane(normalX);
        Assert.True((baseline.UAxis-nearby.UAxis).Length<1e-8);
        Assert.True((baseline.VAxis-nearby.VAxis).Length<1e-8);
    }

    [Fact]
    public void Plane_rejects_nonplanar_degenerate_and_nonfinite_points()
    {
        Assert.Throws<ArgumentException>(()=>RoofPlaneCoordinateSystem.Create("p",[new(0,0,0),new(1,0,0),new(1,1,1),new(0,1,0)]));
        Assert.Throws<ArgumentException>(()=>RoofPlaneCoordinateSystem.Create("p",[new(0,0,0),new(1,0,0),new(2,0,0)]));
        Assert.Throws<ArgumentException>(()=>RoofPlaneCoordinateSystem.Create("p",[new(double.NaN,0,0),new(1,0,0),new(1,1,0)]));
    }

    [Fact]
    public void Full_containment_reports_actual_area_and_percentage()
    {
        var r=ZonePolygonEngine.Classify("s",Rect(0,0,2,2),[Zone("z",Rect(-1,-1,4,4))]);
        Assert.Equal(GeometryClassification.FullyInsideZone,r.Classification);
        Assert.Equal(4,r.ShellAreaM2); Assert.Equal(4,r.Coverages.Single().IntersectionAreaM2);
        Assert.Equal(100,r.Coverages.Single().CoveragePercent); Assert.False(r.NeedsSplitInCSI);
    }

    [Fact]
    public void Crossing_reports_conflicting_zone_and_split()
    {
        var r=ZonePolygonEngine.Classify("s",Rect(0,0,2,2),[Zone("left",Rect(0,0,1,2))]);
        Assert.Equal(GeometryClassification.CrossesZoneBoundary,r.Classification);
        Assert.Equal(50,r.Coverages.Single().CoveragePercent); Assert.True(r.NeedsSplitInCSI);
        Assert.Contains("left",r.Diagnostic);
    }

    [Fact]
    public void Adjacent_zone_partition_is_crossing_not_overlap()
    {
        var r=ZonePolygonEngine.Classify("s",Rect(0,0,2,2),[Zone("a",Rect(0,0,1,2)),Zone("b",Rect(1,0,1,2))]);
        Assert.Equal(GeometryClassification.CrossesZoneBoundary,r.Classification);
        Assert.Equal(2,r.Coverages.Length);
    }

    [Fact]
    public void Actual_overlap_inside_shell_is_blocking()
    {
        var r=ZonePolygonEngine.Classify("s",Rect(0,0,2,2),[Zone("a",Rect(0,0,1.5,2)),Zone("b",Rect(1,0,1,2))]);
        Assert.Equal(GeometryClassification.OverlappingZones,r.Classification);
    }

    [Fact]
    public void Zone_overlap_outside_shell_is_not_overlap_inside_shell()
    {
        var r=ZonePolygonEngine.Classify("s",Rect(0,0,1,1),[Zone("a",Rect(0,0,2,2)),Zone("b",Rect(1,0,2,2))]);
        Assert.Equal(GeometryClassification.FullyInsideZone,r.Classification);
    }

    [Fact]
    public void Boundary_touch_is_unmapped_and_small_area_gap_obeys_tolerance()
    {
        Assert.Equal(GeometryClassification.Unmapped,ZonePolygonEngine.Classify("s",Rect(0,0,1,1),[Zone("z",Rect(1,0,1,1))]).Classification);
        Assert.Equal(GeometryClassification.FullyInsideZone,ZonePolygonEngine.Classify("s",Rect(0,0,1,1),[Zone("z",Rect(0,0,1-1e-8,1))]).Classification);
        Assert.Equal(GeometryClassification.CrossesZoneBoundary,ZonePolygonEngine.Classify("s",Rect(0,0,1,1),[Zone("z",Rect(0,0,1-1e-4,1))]).Classification);
    }

    [Fact]
    public void Concave_polygon_uses_real_interior()
    {
        ImmutableArray<UvPoint> l=[new(0,0),new(3,0),new(3,1),new(1,1),new(1,3),new(0,3)];
        Assert.Null(ZonePolygonEngine.Validate(l)); Assert.Equal(5,ZonePolygonEngine.Area(l));
        Assert.Equal(GeometryClassification.Unmapped,ZonePolygonEngine.Classify("s",Rect(1.5,1.5,1,1),[Zone("l",l)]).Classification);
        Assert.Equal(GeometryClassification.FullyInsideZone,ZonePolygonEngine.Classify("s",Rect(0,0,1,2),[Zone("l",l)]).Classification);
    }

    [Fact]
    public void Invalid_zone_blocks_instead_of_repairing_or_ignoring()
    {
        ImmutableArray<UvPoint> bowtie=[new(0,0),new(2,2),new(0,2),new(2,0)];
        Assert.NotNull(ZonePolygonEngine.Validate(bowtie));
        Assert.NotNull(ZonePolygonEngine.Validate([new(0,0),new(1,0),new(2,0)]));
        Assert.NotNull(ZonePolygonEngine.Validate([new(0,0),new(double.PositiveInfinity,0),new(1,1)]));
        Assert.Equal(GeometryClassification.InvalidGeometry,ZonePolygonEngine.Classify("s",Rect(0,0,1,1),[Zone("bad",bowtie)]).Classification);
        Assert.Equal(GeometryClassification.InvalidGeometry,ZonePolygonEngine.Classify("s",bowtie,[]).Classification);
    }

    [Fact]
    public void Union_preserves_concavity_and_rejects_disconnected_and_holed_footprints()
    {
        var union=ZonePolygonEngine.Union([Rect(0,0,3,1),Rect(0,1,1,2)]);
        Assert.Equal(5,ZonePolygonEngine.Area(union));
        Assert.Throws<ArgumentException>(()=>ZonePolygonEngine.Union([Rect(0,0,1,1),Rect(2,0,1,1)]));
        Assert.Throws<ArgumentException>(()=>ZonePolygonEngine.Union([Rect(0,0,3,1),Rect(0,2,3,1),Rect(0,1,1,1),Rect(2,1,1,1)]));
        Assert.Throws<ArgumentException>(()=>ZonePolygonEngine.Union([]));
    }

    [Fact]
    public void Snap_uses_nearest_edge_or_vertex_and_respects_meter_tolerance()
    {
        var boundary=Rect(0,0,2,2);
        Assert.Equal(new UvPoint(1,0),ZonePolygonEngine.Snap(new(1,0.05),[boundary],0.1));
        Assert.Equal(new UvPoint(0,0),ZonePolygonEngine.Snap(new(-0.02,-0.02),[boundary],0.1));
        Assert.Equal(new UvPoint(1,0.5),ZonePolygonEngine.Snap(new(1,0.5),[boundary],0.1));
    }
}
