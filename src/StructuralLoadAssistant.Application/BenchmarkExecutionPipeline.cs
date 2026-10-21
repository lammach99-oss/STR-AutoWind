using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Application;

/// <summary>
/// Executes the physical qualification pipeline against an independently defined analytical benchmark.
/// </summary>
/// <remarks>
/// IMPORTANT: Reconstructed planes for benchmark qualification are fixture-directed groupings
/// based on the declared benchmark plane shell memberships. This deterministic grouping qualifies
/// coordinate transformations, local basis calculations, and polygon overlay classifications.
/// It does NOT claim to qualify automated unguided roof-plane discovery across arbitrary models.
/// </remarks>
public static class BenchmarkExecutionPipeline
{
    public static PhysicalQualificationResult Execute(
        CsiReadScan scan,
        string fixturePath,
        int? processId = null)
    {
        ArgumentNullException.ThrowIfNull(scan);

        if (string.IsNullOrWhiteSpace(fixturePath) || !File.Exists(fixturePath))
        {
            return IndependentGeometryBenchmarkValidator.Validate(
                scan, ImmutableArray<RoofShell3D>.Empty, [], fixturePath, null, processId);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        }
        catch
        {
            return IndependentGeometryBenchmarkValidator.Validate(
                scan, ImmutableArray<RoofShell3D>.Empty, [], fixturePath, null, processId);
        }

        using (doc)
        {
            var root = doc.RootElement;

            // 1. Process 3D geometry for all shells
            var shells3D = scan.Model.Shells
                .Select(s => Roof3DGeometryProcessor.Process(s, scan.Model.ModelId))
                .ToImmutableArray();

            // 2. Reconstruct roof planes from fixture-directed shell groupings
            var reconstructedPlanes = new List<RoofPlaneCoordinateSystem>();
            if (root.TryGetProperty("benchmarkPlanes", out var expPlanes))
            {
                foreach (var bp in expPlanes.EnumerateArray())
                {
                    var planeId = bp.GetProperty("planeId").GetString()!;
                    var planeShellIds = bp.TryGetProperty("shells", out var sList)
                        ? sList.EnumerateArray().Select(s => s.GetString()!).ToHashSet(StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal);

                    var matchingShells = scan.Model.Shells
                        .Where(s => planeShellIds.Contains(s.Id))
                        .ToArray();

                    var vertices = matchingShells
                        .SelectMany(s => s.Vertices)
                        .Distinct()
                        .ToArray();

                    if (vertices.Length >= 3)
                    {
                        try
                        {
                            var plane = RoofPlaneCoordinateSystem.Create(planeId, vertices);
                            reconstructedPlanes.Add(plane);
                        }
                        catch (ArgumentException)
                        {
                            // Vertices nonplanar or degenerate; plane cannot be reconstructed.
                        }
                    }
                }
            }

            // 3. Construct fixture test zones in the appropriate plane coordinate systems
            var testZones = new List<EditableRoofZone>();
            var now = DateTimeOffset.UtcNow;
            if (root.TryGetProperty("benchmarkTestZones", out var btz))
            {
                foreach (var tz in btz.EnumerateArray())
                {
                    var zoneId = tz.GetProperty("zoneId").GetString()!;
                    var planeId = tz.GetProperty("planeId").GetString()!;
                    var uvList = tz.GetProperty("verticesUV").EnumerateArray()
                        .Select(p => new UvPoint(p.GetProperty("u").GetDouble(), p.GetProperty("v").GetDouble()))
                        .ToImmutableArray();

                    var plane = reconstructedPlanes.FirstOrDefault(p => p.Id == planeId);
                    var fp = plane?.GeometryFingerprint ?? "benchmark-fp";

                    var zone = new EditableRoofZone(
                        zoneId,
                        zoneId,
                        planeId,
                        "BENCHMARK",
                        uvList,
                        null,
                        1,
                        fp,
                        now,
                        now,
                        ZoneGeometryStatus.Valid,
                        []);

                    testZones.Add(zone);
                }
            }

            // 4. Run production polygon intersection engine (ZonePolygonEngine.Classify)
            var intersections = new List<ShellZoneIntersection>();
            foreach (var shell in shells3D)
            {
                if (!shell.IsValid)
                {
                    intersections.Add(new ShellZoneIntersection(
                        shell.ShellId, GeometryClassification.InvalidGeometry,
                        shell.SurfaceAreaM2, [], false, shell.GeometryError ?? "Invalid source shell"));
                    continue;
                }

                var matchedPlane = reconstructedPlanes.FirstOrDefault(p =>
                    shell.Vertices.All(v => Math.Abs(p.Normal.Dot(v - p.Origin)) <= 1e-4));

                if (matchedPlane == null)
                {
                    intersections.Add(new ShellZoneIntersection(
                        shell.ShellId, GeometryClassification.InvalidGeometry,
                        shell.SurfaceAreaM2, [], false, "No coplanar reconstructed plane found"));
                    continue;
                }

                var footprint = shell.Vertices.Select(matchedPlane.Project).ToImmutableArray();
                var planeZones = testZones.Where(z => z.RoofPlaneId == matchedPlane.Id);
                var classification = ZonePolygonEngine.Classify(shell.ShellId, footprint, planeZones);
                intersections.Add(classification);
            }

            // 5. Delegate to fail-closed validator with complete real data
            return IndependentGeometryBenchmarkValidator.Validate(
                scan,
                shells3D,
                reconstructedPlanes,
                fixturePath,
                intersections.ToImmutableArray(),
                processId);
        }
    }
}
