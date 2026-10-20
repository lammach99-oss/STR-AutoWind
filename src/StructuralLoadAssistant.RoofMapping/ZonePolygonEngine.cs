using System.Collections.Immutable;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using NetTopologySuite.Operation.Valid;

namespace StructuralLoadAssistant.RoofMapping;

public static class ZonePolygonEngine
{
    private static readonly GeometryFactory Factory = new();

    public static string? Validate(ImmutableArray<UvPoint> vertices)
    {
        if (vertices.IsDefaultOrEmpty || vertices.Length < 3) return "A polygon needs at least three vertices.";
        if (vertices.Any(p => !Finite(p))) return "Polygon coordinates must be finite.";
        var count = vertices.Length > 3 && vertices[0] == vertices[^1] ? vertices.Length-1 : vertices.Length;
        if (count < 3) return "A polygon needs at least three distinct vertices.";
        for (var i=0; i<count; i++)
            if (vertices[i] == vertices[(i+1)%count]) return "Polygon has a zero-length edge.";
        var polygon = Polygon(vertices);
        var error = new IsValidOp(polygon).ValidationError;
        if (error is not null) return error.Message;
        if (!double.IsFinite(polygon.Area) || polygon.Area <= 1e-12) return "Polygon is degenerate or has no finite area.";
        return null;
    }

    public static double Area(ImmutableArray<UvPoint> vertices) => ValidPolygon(vertices).Area;

    /// <summary>Classifies one planar shell. The caller must supply zones for its plane and wind state.
    /// Invalid supplied polygons block validation even when outside the shell. Tolerances are in m²
    /// and a fraction of shell area; touching edges have zero coverage.</summary>
    public static ShellZoneIntersection Classify(string shellId, ImmutableArray<UvPoint> shell,
        IEnumerable<EditableRoofZone> zones, double absoluteAreaTolerance = 1e-6, double relativeAreaTolerance = 1e-7)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (!double.IsFinite(absoluteAreaTolerance) || absoluteAreaTolerance < 0 ||
            !double.IsFinite(relativeAreaTolerance) || relativeAreaTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteAreaTolerance),"Area tolerances must be finite and nonnegative.");
        var shellError = Validate(shell);
        if (shellError is not null) return new(shellId,GeometryClassification.InvalidGeometry,0,[],false,shellError);
        var surface = Polygon(shell); var area = surface.Area;
        var tolerance = Math.Max(absoluteAreaTolerance,relativeAreaTolerance*area);
        var intersections = new List<(string Id,Geometry Shape)>();
        try
        {
            foreach (var zone in zones)
            {
                var error = Validate(zone.Vertices);
                if (error is not null || zone.ValidationStatus == ZoneGeometryStatus.Invalid)
                    return new(shellId,GeometryClassification.InvalidGeometry,area,[],false,
                        $"Zone {zone.Id}: {error ?? "marked invalid"}");
                var clipped = Intersect(surface,Polygon(zone.Vertices));
                if (clipped.Area > tolerance) intersections.Add((zone.Id,clipped));
            }
            var coverages = intersections.Select(p => new ZoneCoverage(p.Id,p.Shape.Area,
                Math.Clamp(100*p.Shape.Area/area,0,100))).ToImmutableArray();
            for (var i=0; i<intersections.Count; i++)
                for (var j=i+1; j<intersections.Count; j++)
                    if (Intersect(intersections[i].Shape,intersections[j].Shape).Area > tolerance)
                        return new(shellId,GeometryClassification.OverlappingZones,area,coverages,true,
                            $"Overlapping zones {intersections[i].Id}, {intersections[j].Id} cover this shell; review polygons before splitting in CSI.");
            if (intersections.Count == 0)
                return new(shellId,GeometryClassification.Unmapped,area,coverages,false,"No zone covers the shell above area tolerance.");
            if (intersections.Count == 1 && area-intersections[0].Shape.Area <= tolerance)
                return new(shellId,GeometryClassification.FullyInsideZone,area,coverages,false,
                    $"Fully inside zone {intersections[0].Id} within area tolerance.");
            return new(shellId,GeometryClassification.CrossesZoneBoundary,area,coverages,true,
                $"Shell crosses zone boundaries: {string.Join(", ",intersections.Select(p=>p.Id))}. NeedsSplitInCSI; divide the shell in CSI.");
        }
        catch (TopologyException ex)
        {
            return new(shellId,GeometryClassification.InvalidGeometry,area,[],false,$"Polygon overlay failed: {ex.Message}");
        }
    }

    public static ImmutableArray<UvPoint> Union(IEnumerable<ImmutableArray<UvPoint>> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        var inputs = polygons.Select(ValidPolygon).ToArray();
        if (inputs.Length == 0) throw new ArgumentException("Select at least one polygon.",nameof(polygons));
        var result = OverlayNGRobust.Union(inputs.Cast<Geometry>(),Factory);
        if (result is not Polygon polygon || polygon.IsEmpty)
            throw new ArgumentException("Selected footprint is disconnected or has multiple components.",nameof(polygons));
        if (polygon.NumInteriorRings != 0)
            throw new ArgumentException("Selected footprint contains holes; a single editable polygon cannot represent it.",nameof(polygons));
        // Normalize only the valid output's winding/start vertex, never repair invalid inputs.
        polygon.Normalize();
        return polygon.ExteriorRing.Coordinates.SkipLast(1).Select(c=>new UvPoint(c.X,c.Y)).ToImmutableArray();
    }

    public static UvPoint Snap(UvPoint point, IEnumerable<ImmutableArray<UvPoint>> shellBoundaries,
        double toleranceM)
    {
        ArgumentNullException.ThrowIfNull(shellBoundaries);
        if (!Finite(point) || !double.IsFinite(toleranceM) || toleranceM < 0)
            throw new ArgumentException("Snap point and nonnegative meter tolerance must be finite.");
        var best = point; var bestDistance = toleranceM;
        foreach (var boundary in shellBoundaries)
        {
            if (boundary.IsDefaultOrEmpty) continue;
            if (boundary.Any(p=>!Finite(p))) throw new ArgumentException("Shell boundary must be finite.",nameof(shellBoundaries));
            for (var i=0; i<boundary.Length; i++)
            {
                var a=boundary[i]; var b=boundary[(i+1)%boundary.Length];
                var du=b.U-a.U; var dv=b.V-a.V; var lengthSquared=du*du+dv*dv;
                var t=lengthSquared==0 ? 0 : Math.Clamp(((point.U-a.U)*du+(point.V-a.V)*dv)/lengthSquared,0,1);
                var candidate=new UvPoint(a.U+t*du,a.V+t*dv);
                var distance=Math.Sqrt(Math.Pow(candidate.U-point.U,2)+Math.Pow(candidate.V-point.V,2));
                if (distance <= bestDistance)
                { best=candidate; bestDistance=distance; }
            }
        }
        return best;
    }

    private static bool Finite(UvPoint p) => double.IsFinite(p.U) && double.IsFinite(p.V);
    private static Polygon Polygon(ImmutableArray<UvPoint> points)
    {
        var coordinates=points.Select(p=>new Coordinate(p.U,p.V)).ToList();
        if (!coordinates[0].Equals2D(coordinates[^1])) coordinates.Add(coordinates[0].Copy());
        return Factory.CreatePolygon(coordinates.ToArray());
    }
    private static Polygon ValidPolygon(ImmutableArray<UvPoint> points)
    {
        var error=Validate(points);
        if (error is not null) throw new ArgumentException(error,nameof(points));
        return Polygon(points);
    }
    private static Geometry Intersect(Geometry a, Geometry b) =>
        OverlayNGRobust.Overlay(a,b,SpatialFunction.Intersection);
}
