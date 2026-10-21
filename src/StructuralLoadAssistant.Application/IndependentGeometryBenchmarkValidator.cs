using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Application;

public static class IndependentGeometryBenchmarkValidator
{
    public static PhysicalQualificationResult Validate(
        CsiReadScan scan,
        ImmutableArray<RoofShell3D> shells3D,
        IReadOnlyList<RoofPlaneCoordinateSystem> planes,
        string fixturePath,
        ImmutableArray<ShellZoneIntersection>? intersections = null)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var timestamp = DateTimeOffset.UtcNow;
        var product = scan.Model.Product;
        var version = scan.Model.Version;
        var modelId = scan.Model.ModelId;

        if (string.IsNullOrWhiteSpace(fixturePath) || !File.Exists(fixturePath))
        {
            return new PhysicalQualificationResult(
                PhysicalQualificationOutcome.Inconclusive,
                product, version, null, modelId, timestamp,
                "Independent Benchmark", [],
                [$"Independent benchmark fixture file not found at '{fixturePath}'."],
                "Physical qualification is Inconclusive: missing independent reference fixture.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        }
        catch (Exception ex)
        {
            return new PhysicalQualificationResult(
                PhysicalQualificationOutcome.Inconclusive,
                product, version, null, modelId, timestamp,
                "Independent Benchmark", [],
                [$"Failed to parse benchmark fixture: {ex.Message}"],
                "Physical qualification is Inconclusive: invalid benchmark fixture JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var benchmarkName = root.TryGetProperty("benchmarkName", out var bn) ? bn.GetString() ?? "Benchmark" : "Benchmark";

            // Read tolerances
            double coordTol = 0.001;
            double areaTol = 0.001;
            double l3Tol = 0.001;
            double covTol = 0.01;
            double loadTol = 0.01;
            if (root.TryGetProperty("tolerances", out var tolElem))
            {
                if (tolElem.TryGetProperty("coordinateAbsM", out var ct)) coordTol = ct.GetDouble();
                if (tolElem.TryGetProperty("areaAbsM2", out var at)) areaTol = at.GetDouble();
                if (tolElem.TryGetProperty("local3ComponentAbs", out var lt)) l3Tol = lt.GetDouble();
                if (tolElem.TryGetProperty("coveragePercentAbs", out var cpt)) covTol = cpt.GetDouble();
                if (tolElem.TryGetProperty("loadValueAbs", out var lvt)) loadTol = lvt.GetDouble();
            }

            var comparisons = new List<BenchmarkComparison>();
            var issues = new List<string>();

            // Check required benchmark shells
            if (root.TryGetProperty("benchmarkShells", out var expectedShells))
            {
                var shellArray = expectedShells.EnumerateArray().ToArray();
                var missingShells = shellArray
                    .Select(e => e.GetProperty("id").GetString()!)
                    .Where(id => !scan.Model.Shells.Any(s => s.Id == id))
                    .ToArray();

                if (missingShells.Length > 0)
                {
                    return new PhysicalQualificationResult(
                        PhysicalQualificationOutcome.Inconclusive,
                        product, version, null, modelId, timestamp,
                        benchmarkName, [],
                        [$"Required benchmark shells missing from CSI selection: {string.Join(", ", missingShells)}."],
                        $"Physical qualification is Inconclusive: CSI selection does not contain required benchmark elements ({string.Join(", ", missingShells)}).");
                }

                foreach (var expected in shellArray)
                {
                    var id = expected.GetProperty("id").GetString()!;
                    var actualShell = scan.Model.Shells.Single(s => s.Id == id);
                    var actual3D = shells3D.FirstOrDefault(s => s.ShellId == id);

                    // 1. Verify Local 3
                    if (expected.TryGetProperty("local3", out var expL3))
                    {
                        var ex = expL3.GetProperty("x").GetDouble();
                        var ey = expL3.GetProperty("y").GetDouble();
                        var ez = expL3.GetProperty("z").GetDouble();

                        comparisons.Add(new("Local 3 Vectors", id, "Local3.X", ex, actualShell.Local3.X, l3Tol, null, Math.Abs(actualShell.Local3.X - ex) <= l3Tol, $"Expected {ex:F6}, Actual {actualShell.Local3.X:F6}"));
                        comparisons.Add(new("Local 3 Vectors", id, "Local3.Y", ey, actualShell.Local3.Y, l3Tol, null, Math.Abs(actualShell.Local3.Y - ey) <= l3Tol, $"Expected {ey:F6}, Actual {actualShell.Local3.Y:F6}"));
                        comparisons.Add(new("Local 3 Vectors", id, "Local3.Z", ez, actualShell.Local3.Z, l3Tol, null, Math.Abs(actualShell.Local3.Z - ez) <= l3Tol, $"Expected {ez:F6}, Actual {actualShell.Local3.Z:F6}"));
                        comparisons.Add(new("Local 3 Vectors", id, "Local3.UnitLength", 1.0, actualShell.Local3.Length, 1e-4, null, Math.Abs(actualShell.Local3.Length - 1.0) <= 1e-4, $"Unit length {actualShell.Local3.Length:F6}"));
                    }

                    // 2. Verify 3D Surface Area
                    if (expected.TryGetProperty("surfaceAreaM2", out var expArea) && actual3D != null)
                    {
                        var ea = expArea.GetDouble();
                        var diff = Math.Abs(actual3D.SurfaceAreaM2 - ea);
                        bool pass = diff <= areaTol || (ea > 0 && diff / ea <= 1e-4);
                        comparisons.Add(new("3D Surface Areas", id, "SurfaceAreaM2", ea, actual3D.SurfaceAreaM2, areaTol, 1e-4, pass, $"Expected {ea:F4} m², Actual {actual3D.SurfaceAreaM2:F4} m²"));
                    }

                    // 3. Verify Projected Area
                    if (expected.TryGetProperty("projectedAreaM2", out var expProj) && actual3D != null)
                    {
                        var ep = expProj.GetDouble();
                        var diff = Math.Abs(actual3D.ProjectedAreaM2 - ep);
                        bool pass = diff <= areaTol || (ep > 0 && diff / ep <= 1e-4);
                        comparisons.Add(new("Projected Areas", id, "ProjectedAreaM2", ep, actual3D.ProjectedAreaM2, areaTol, 1e-4, pass, $"Expected {ep:F4} m², Actual {actual3D.ProjectedAreaM2:F4} m²"));
                    }

                    // 4. Verify Global Vertices
                    if (expected.TryGetProperty("verticesXYZ", out var expVerts))
                    {
                        var evList = expVerts.EnumerateArray().ToArray();
                        for (int i = 0; i < Math.Min(evList.Length, actualShell.Vertices.Length); i++)
                        {
                            var vx = evList[i].GetProperty("x").GetDouble();
                            var vy = evList[i].GetProperty("y").GetDouble();
                            var vz = evList[i].GetProperty("z").GetDouble();
                            var act = actualShell.Vertices[i];

                            comparisons.Add(new("Global XYZ Coordinates", id, $"V[{i}].X", vx, act.X, coordTol, null, Math.Abs(act.X - vx) <= coordTol, $"Vertex {i} X"));
                            comparisons.Add(new("Global XYZ Coordinates", id, $"V[{i}].Y", vy, act.Y, coordTol, null, Math.Abs(act.Y - vy) <= coordTol, $"Vertex {i} Y"));
                            comparisons.Add(new("Global XYZ Coordinates", id, $"V[{i}].Z", vz, act.Z, coordTol, null, Math.Abs(act.Z - vz) <= coordTol, $"Vertex {i} Z"));
                        }
                    }
                }
            }

            // Check test zones & intersections
            if (intersections.HasValue && root.TryGetProperty("benchmarkTestZones", out var testZones))
            {
                foreach (var tz in testZones.EnumerateArray())
                {
                    var targetShellId = tz.GetProperty("targetShellId").GetString()!;
                    var zoneId = tz.GetProperty("zoneId").GetString()!;
                    var match = intersections.Value.FirstOrDefault(i => i.ShellId == targetShellId);
                    if (match != null)
                    {
                        var actualCov = match.Coverages.FirstOrDefault(c => c.ZoneId == zoneId);
                        if (actualCov == null) continue;

                        if (tz.TryGetProperty("expectedIntersectionAreaM2", out var expIa))
                        {
                            var expectedArea = expIa.GetDouble();
                            var actualArea = actualCov?.IntersectionAreaM2 ?? 0.0;
                            bool pass = Math.Abs(actualArea - expectedArea) <= areaTol;
                            comparisons.Add(new("Zone Intersections", $"{targetShellId}:{zoneId}", "IntersectionAreaM2", expectedArea, actualArea, areaTol, null, pass, $"Coverage area m²"));
                        }
                        if (tz.TryGetProperty("expectedCoveragePercent", out var expCp))
                        {
                            var expectedPct = expCp.GetDouble();
                            var actualPct = actualCov?.CoveragePercent ?? 0.0;
                            bool pass = Math.Abs(actualPct - expectedPct) <= covTol;
                            comparisons.Add(new("Coverage Percentages", $"{targetShellId}:{zoneId}", "CoveragePercent", expectedPct, actualPct, covTol, null, pass, $"Coverage %"));
                        }
                    }
                }
            }

            // Check existing loads
            if (root.TryGetProperty("benchmarkLoads", out var expectedLoads))
            {
                foreach (var el in expectedLoads.EnumerateArray())
                {
                    var objId = el.GetProperty("objectId").GetString()!;
                    var pattern = el.GetProperty("pattern").GetString()!;
                    var expVal = el.GetProperty("valueSI").GetDouble();
                    var matchedLoad = scan.Model.ExistingLoads.FirstOrDefault(l => l.ObjectId == objId && l.Pattern == pattern);
                    if (matchedLoad != null)
                    {
                        bool pass = Math.Abs(matchedLoad.ValueSI - expVal) <= loadTol;
                        comparisons.Add(new("Existing Loads", objId, $"LoadSI:{pattern}", expVal, matchedLoad.ValueSI, loadTol, null, pass, $"Pattern {pattern}"));
                    }
                }
            }

            if (comparisons.Count == 0)
            {
                return new PhysicalQualificationResult(
                    PhysicalQualificationOutcome.Inconclusive,
                    product, version, null, modelId, timestamp,
                    benchmarkName, [],
                    ["No benchmark comparisons could be evaluated."],
                    "Physical qualification is Inconclusive: no matching benchmark objects found.");
            }

            var failed = comparisons.Where(c => !c.Passed).ToArray();
            var outcome = failed.Length > 0 ? PhysicalQualificationOutcome.Failed : PhysicalQualificationOutcome.Passed;
            var summary = outcome == PhysicalQualificationOutcome.Passed
                ? $"Physical Qualification PASSED • {comparisons.Count} independent benchmark checks confirmed within tolerances."
                : $"Physical Qualification FAILED • {failed.Length} of {comparisons.Count} benchmark checks exceeded tolerance.";

            return new PhysicalQualificationResult(
                outcome, product, version, null, modelId, timestamp,
                benchmarkName, comparisons.ToImmutableArray(),
                failed.Select(f => $"{f.Category} on {f.ElementId} {f.Property}: {f.Details}").ToImmutableArray(),
                summary);
        }
    }
}
