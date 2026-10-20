using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.RoofMapping;

public sealed record RoofPlaneCoordinateSystem(string Id, Vec3 Origin, Vec3 UAxis, Vec3 VAxis,
    Vec3 Normal, double SlopeDegrees, double AzimuthDegrees, string GeometryFingerprint,
    ImmutableArray<string> Diagnostics)
{
    /// <summary>Creates a geometric basis independent of shell winding and CSI Local 3.
    /// The canonical normal points upward, or toward positive X/Y for vertical planes.
    /// Azimuth is the normal's horizontal bearing, clockwise from global +Y; flat is zero.</summary>
    public static RoofPlaneCoordinateSystem Create(string id, IEnumerable<Vec3> vertices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(vertices);
        var points = vertices.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).ToArray();
        if (points.Length < 3 || points.Any(p => !Finite(p)))
            throw new ArgumentException("A plane needs at least three distinct finite vertices.", nameof(vertices));
        var first = points[0];
        var farthest = points.MaxBy(p => (p-first).Length);
        var edge = farthest-first;
        var widest = points.MaxBy(p => edge.Cross(p-first).Length);
        var cross = edge.Cross(widest-first);
        if (!double.IsFinite(cross.Length) || cross.Length <= 1e-12)
            throw new ArgumentException("Plane vertices are collinear or degenerate.", nameof(vertices));
        var normal = cross.Unit();
        if (normal.Z < -1e-12 || Math.Abs(normal.Z) <= 1e-12 &&
            (normal.X < -1e-12 || Math.Abs(normal.X) <= 1e-12 && normal.Y < 0)) normal *= -1;
        var distance = normal.Dot(first);
        if (points.Any(p => Math.Abs(normal.Dot(p-first)) > 1e-6))
            throw new ArgumentException("Vertices are nonplanar (distance exceeds 0.000001 m).", nameof(vertices));
        // The nearest global-origin point does not move when selected shells change.
        var origin = normal * distance;
        // Rotate the global +Z frame onto the canonical normal. This closed-form
        // basis is continuous throughout the upward hemisphere, including vertical
        // roofs; 1 + normal.Z >= 1 - 1e-12 under the canonical-normal tolerance.
        // No reference-axis threshold can rotate saved UV polygons on a scope change.
        var denominator = 1+normal.Z;
        var u = new Vec3(1-normal.X*normal.X/denominator,
            -normal.X*normal.Y/denominator,-normal.X).Unit();
        var v = normal.Cross(u).Unit();
        var fingerprintText = string.Join(";", points.Select(p => string.Join(",",
            p.X.ToString("R",CultureInfo.InvariantCulture), p.Y.ToString("R",CultureInfo.InvariantCulture),
            p.Z.ToString("R",CultureInfo.InvariantCulture))));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintText)));
        var slope = Math.Acos(Math.Clamp(Math.Abs(normal.Z),0,1))*180/Math.PI;
        var azimuth = Math.Abs(normal.X)+Math.Abs(normal.Y) < 1e-12 ? 0 :
            (Math.Atan2(normal.X,normal.Y)*180/Math.PI+360)%360;
        return new(id,origin,u,v,normal,slope,azimuth,fingerprint,[]);
    }

    public UvPoint Project(Vec3 point)
    {
        if (!Finite(point)) throw new ArgumentException("Point must be finite.", nameof(point));
        var offset = point-Origin;
        return new(offset.Dot(UAxis),offset.Dot(VAxis));
    }

    public Vec3 Unproject(UvPoint point)
    {
        if (!double.IsFinite(point.U) || !double.IsFinite(point.V))
            throw new ArgumentException("Point must be finite.", nameof(point));
        var u = UAxis*point.U; var v = VAxis*point.V;
        return new(Origin.X+u.X+v.X,Origin.Y+u.Y+v.Y,Origin.Z+u.Z+v.Z);
    }

    private static bool Finite(Vec3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
}
