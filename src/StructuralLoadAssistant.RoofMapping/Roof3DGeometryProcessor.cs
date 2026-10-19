 using System.Collections.Immutable;
 using System.Globalization;
 using StructuralLoadAssistant.Domain;
 
 namespace StructuralLoadAssistant.RoofMapping;
 
 public static class Roof3DGeometryProcessor
 {
     private const double Epsilon = 1e-6;
 
     public static RoofShell3D Process(
         ShellGeometry shell,
         string modelId,
         Vec3? verifiedOutward = null,
         OutwardNormalStatus outwardStatus = OutwardNormalStatus.Unknown)
     {
         ArgumentNullException.ThrowIfNull(shell);
         var vertices = shell.Vertices;
         if (vertices.IsDefaultOrEmpty || vertices.Length < 3)
         {
             return new RoofShell3D(
                 shell.Id, modelId, shell.RoofPlaneId, vertices, shell.Local3,
                 0, null, 0, 0, "", outwardStatus, verifiedOutward, [], false, true, false, 0, false, "Shell has fewer than 3 vertices.");
         }
 
         bool isPlanar = IsPlanar(vertices, out var planeNormal, out var planeD);
         double surfaceArea = Compute3DArea(vertices);
         double projectedArea = ComputeProjectedPlanArea(vertices);
         bool isDegenerate = surfaceArea < 1e-8 || !double.IsFinite(surfaceArea);
        bool isSelfIntersecting = HasSelfIntersections(vertices, planeNormal);
 
         // Slope: angle of planeNormal with vertical Z-axis (0 deg = horizontal, 90 deg = vertical)
         // If plane normal points downward, reflect to upper hemisphere to compute roof pitch
         var upNormal = planeNormal.Z < 0 ? planeNormal * -1 : planeNormal;
         double cosSlope = Math.Clamp(upNormal.Z, -1.0, 1.0);
         double slopeDegrees = Math.Acos(cosSlope) * 180.0 / Math.PI;
 
         // Azimuth: compass angle of maximum slope descent or horizontal projection
         double? azimuthDegrees = null;
         double horizLen = Math.Sqrt(upNormal.X * upNormal.X + upNormal.Y * upNormal.Y);
         if (horizLen > 1e-4)
         {
             // Azimuth in degrees from North (+Y = 0 deg, +X = 90 deg)
             double rad = Math.Atan2(upNormal.X, upNormal.Y);
             double deg = rad * 180.0 / Math.PI;
             if (deg < 0) deg += 360.0;
             azimuthDegrees = deg;
         }
 
         // Triangulate 3D polygon for rendering
         var triangles = (!isDegenerate && isPlanar && !isSelfIntersecting)
            ? Triangulate(vertices, planeNormal)
            : ImmutableArray<int>.Empty;

        double triangulatedArea = 0;
        if (triangles.Length >= 3)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var v1 = vertices[triangles[i]];
                var v2 = vertices[triangles[i + 1]];
                var v3 = vertices[triangles[i + 2]];
                triangulatedArea += (v2 - v1).Cross(v3 - v1).Length * 0.5;
            }
        }

        bool areaMismatch = Math.Abs(triangulatedArea - surfaceArea) > Math.Max(0.01, surfaceArea * 0.05);
        string? geomError = null;
        if (!isPlanar) geomError = "Nonplanar polygon vertices.";
        else if (isSelfIntersecting) geomError = "Self-intersecting polygon boundary.";
        else if (isDegenerate) geomError = "Degenerate zero-area polygon.";
        else if (areaMismatch) geomError = $"Triangulated area ({triangulatedArea:F3}) differs from polygon area ({surfaceArea:F3}).";

        bool isValid = geomError == null;
 
         var geomHash = SnapshotJson.Hash(new
         {
             shell.Id,
             Vertices = vertices.Select(v => new { v.X, v.Y, v.Z }),
             Local3 = new { shell.Local3.X, shell.Local3.Y, shell.Local3.Z }
         });
 
         var effectiveOutwardStatus = verifiedOutward.HasValue
             ? OutwardNormalStatus.EngineerVerified
             : (shell.OutwardNormal.Length > 0.5 ? OutwardNormalStatus.ReadFromCsi : outwardStatus);
 
         return new RoofShell3D(
             shell.Id,
             modelId,
             shell.RoofPlaneId,
             vertices,
             shell.Local3,
             slopeDegrees,
             azimuthDegrees,
             surfaceArea,
             projectedArea,
             geomHash,
             effectiveOutwardStatus,
             verifiedOutward,
             triangles,
            isPlanar,
            isDegenerate,
            isSelfIntersecting,
            triangulatedArea,
            isValid,
            geomError
        );
     }
 
     public static bool IsPlanar(ImmutableArray<Vec3> vertices, out Vec3 normal, out double d)
     {
         normal = ComputeNewellNormal(vertices);
         d = 0;
         if (normal.Length < Epsilon || !double.IsFinite(normal.Length))
        {
            // Newell normal can cancel out on self-intersecting polygons (e.g. bowtie).
            // Try to find plane from any 3 non-collinear vertices:
            bool foundPlane = false;
            for (int i = 0; i < vertices.Length && !foundPlane; i++)
            {
                for (int j = i + 1; j < vertices.Length && !foundPlane; j++)
                {
                    for (int k = j + 1; k < vertices.Length; k++)
                    {
                        var cross = (vertices[j] - vertices[i]).Cross(vertices[k] - vertices[i]);
                        if (cross.Length >= Epsilon && double.IsFinite(cross.Length))
                        {
                            normal = cross.Unit();
                            foundPlane = true;
                            break;
                        }
                    }
                }
            }
            if (!foundPlane) return false;
        }
        else
        {
            normal = normal.Unit();
        }
         d = normal.Dot(vertices[0]);
         for (int i = 1; i < vertices.Length; i++)
         {
             if (Math.Abs(normal.Dot(vertices[i]) - d) > 1e-4)
             {
                 return false;
             }
         }
         return true;
     }
 
     public static Vec3 ComputeNewellNormal(ImmutableArray<Vec3> vertices)
     {
         if (vertices.Length < 3) return default;
         double x = 0, y = 0, z = 0;
         for (int i = 0; i < vertices.Length; i++)
         {
             var cur = vertices[i];
             var next = vertices[(i + 1) % vertices.Length];
             x += (cur.Y - next.Y) * (cur.Z + next.Z);
             y += (cur.Z - next.Z) * (cur.X + next.X);
             z += (cur.X - next.X) * (cur.Y + next.Y);
         }
         return new Vec3(x, y, z);
     }
 
     public static double Compute3DArea(ImmutableArray<Vec3> vertices)
     {
         var normal = ComputeNewellNormal(vertices);
         return normal.Length * 0.5;
     }
 
     public static double ComputeProjectedPlanArea(ImmutableArray<Vec3> vertices)
     {
         if (vertices.Length < 3) return 0;
         double sum = 0;
         for (int i = 0; i < vertices.Length; i++)
         {
             var cur = vertices[i];
             var next = vertices[(i + 1) % vertices.Length];
             sum += cur.X * next.Y - next.X * cur.Y;
         }
         return Math.Abs(sum) * 0.5;
     }
 
     public static ImmutableArray<int> Triangulate(ImmutableArray<Vec3> vertices, Vec3 normal)
     {
         if (vertices.Length < 3) return [];
         if (vertices.Length == 3) return [0, 1, 2];
 
         // Project 3D vertices to 2D using largest normal component
         var absX = Math.Abs(normal.X);
         var absY = Math.Abs(normal.Y);
         var absZ = Math.Abs(normal.Z);
         int drop = (absX >= absY && absX >= absZ) ? 0 : (absY >= absZ ? 1 : 2);
 
         var pts = new List<Vec2D>(vertices.Length);
         for (int i = 0; i < vertices.Length; i++)
         {
             var v = vertices[i];
             pts.Add(drop switch
             {
                 0 => new Vec2D(v.Y, v.Z, i),
                 1 => new Vec2D(v.X, v.Z, i),
                 _ => new Vec2D(v.X, v.Y, i)
             });
         }
 
         // Ear clipping in 2D
         var remaining = new List<Vec2D>(pts);
         double signedArea = 0;
         for (int i = 0; i < remaining.Count; i++)
         {
             var a = remaining[i];
             var b = remaining[(i + 1) % remaining.Count];
             signedArea += a.X * b.Y - b.X * a.Y;
         }
         if (signedArea < 0)
         {
             // Reverse so polygon is counter-clockwise
             remaining.Reverse();
         }
 
         var result = new List<int>();
         int guard = remaining.Count * remaining.Count + 10;
         while (remaining.Count > 3 && guard-- > 0)
         {
             bool earFound = false;
             for (int i = 0; i < remaining.Count; i++)
             {
                 int prevIdx = (i + remaining.Count - 1) % remaining.Count;
                 int nextIdx = (i + 1) % remaining.Count;
                 var a = remaining[prevIdx];
                 var b = remaining[i];
                 var c = remaining[nextIdx];
 
                 // Check convexity
                 double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                 if (cross <= 1e-9) continue;
 
                 // Check no other point inside ear triangle
                 bool pointInside = false;
                 for (int j = 0; j < remaining.Count; j++)
                 {
                     if (j == prevIdx || j == i || j == nextIdx) continue;
                     var p = remaining[j];
                     if (IsPointInTriangle2D(p, a, b, c))
                     {
                         pointInside = true;
                         break;
                     }
                 }
                 if (!pointInside)
                 {
                     result.Add(a.OriginalIndex);
                     result.Add(b.OriginalIndex);
                     result.Add(c.OriginalIndex);
                     remaining.RemoveAt(i);
                     earFound = true;
                     break;
                 }
             }
             if (!earFound)
             {
                 // Fallback: fan triangulation if ear clipping fails on nearly degenerate vertex
                 break;
             }
         }
 
         if (remaining.Count == 3)
         {
             result.Add(remaining[0].OriginalIndex);
             result.Add(remaining[1].OriginalIndex);
             result.Add(remaining[2].OriginalIndex);
         }
         else if (remaining.Count > 3)
         {
             // Fan triangulation fallback
             for (int i = 1; i < remaining.Count - 1; i++)
             {
                 result.Add(remaining[0].OriginalIndex);
                 result.Add(remaining[i].OriginalIndex);
                 result.Add(remaining[i + 1].OriginalIndex);
             }
         }
 
         return result.ToImmutableArray();
     }
 
     private static bool IsPointInTriangle2D(Vec2D p, Vec2D a, Vec2D b, Vec2D c)
     {
         double cp1 = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
         double cp2 = (c.X - b.X) * (p.Y - b.Y) - (c.Y - b.Y) * (p.X - b.X);
         double cp3 = (a.X - c.X) * (p.Y - c.Y) - (a.Y - c.Y) * (p.X - c.X);
         return (cp1 >= -1e-9 && cp2 >= -1e-9 && cp3 >= -1e-9) || (cp1 <= 1e-9 && cp2 <= 1e-9 && cp3 <= 1e-9);
     }
 
     private readonly record struct Vec2D(double X, double Y, int OriginalIndex);
 
    public static bool HasSelfIntersections(ImmutableArray<Vec3> vertices, Vec3 normal)
    {
        if (vertices.Length < 4) return false;
        if (normal.Length < 1e-6)
        {
            IsPlanar(vertices, out normal, out _);
            if (normal.Length < 1e-6) normal = new Vec3(0, 0, 1);
        }
        var absX = Math.Abs(normal.X);
        var absY = Math.Abs(normal.Y);
        var absZ = Math.Abs(normal.Z);
        int drop = (absX >= absY && absX >= absZ) ? 0 : (absY >= absZ ? 1 : 2);
        var pts = new List<Vec2D>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            pts.Add(drop switch
            {
                0 => new Vec2D(v.Y, v.Z, i),
                1 => new Vec2D(v.X, v.Z, i),
                _ => new Vec2D(v.X, v.Y, i)
            });
        }
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var p3 = pts[j];
                var p4 = pts[(j + 1) % n];
                if (SegmentsCross(p1, p2, p3, p4)) return true;
            }
        }
        return false;
    }

    private static bool SegmentsCross(Vec2D a, Vec2D b, Vec2D c, Vec2D d)
    {
        static double Ccw(Vec2D p1, Vec2D p2, Vec2D p3) => (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
        double c1 = Ccw(a, b, c);
        double c2 = Ccw(a, b, d);
        double c3 = Ccw(c, d, a);
        double c4 = Ccw(c, d, b);
        return ((c1 > 1e-7 && c2 < -1e-7) || (c1 < -1e-7 && c2 > 1e-7)) &&
               ((c3 > 1e-7 && c4 < -1e-7) || (c3 < -1e-7 && c4 > 1e-7));
    }
}