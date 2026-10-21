using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        ImmutableArray<ShellZoneIntersection>? intersections = null,
        int? processId = null)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var timestamp = DateTimeOffset.UtcNow;
        var product = scan.Model.Product;
        var version = scan.Model.Version;
        var modelId = scan.Model.ModelId;
        var scanFingerprint = scan.Model.GeometryFingerprint;

        if (string.IsNullOrWhiteSpace(fixturePath) || !File.Exists(fixturePath))
        {
            var missingChecklist = new BenchmarkChecklist(1, 0, 0, 0, ["Benchmark fixture file not found."], []);
            return new PhysicalQualificationResult(
                PhysicalQualificationOutcome.Inconclusive,
                product, version, processId, modelId, timestamp,
                "Independent Benchmark", [],
                [$"Independent benchmark fixture file not found at '{fixturePath}'."],
                "Physical qualification is Inconclusive: missing independent reference fixture.",
                missingChecklist, scanFingerprint, null);
        }

        string fixtureText;
        string fixtureHash;
        JsonDocument doc;
        try
        {
            fixtureText = File.ReadAllText(fixturePath);
            using var sha = SHA256.Create();
            fixtureHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(fixtureText)));
            doc = JsonDocument.Parse(fixtureText);
        }
        catch (Exception ex)
        {
            var parseChecklist = new BenchmarkChecklist(1, 0, 0, 0, [$"Failed to parse benchmark fixture: {ex.Message}"], []);
            return new PhysicalQualificationResult(
                PhysicalQualificationOutcome.Inconclusive,
                product, version, processId, modelId, timestamp,
                "Independent Benchmark", [],
                [$"Failed to parse benchmark fixture: {ex.Message}"],
                "Physical qualification is Inconclusive: invalid benchmark fixture JSON.",
                parseChecklist, scanFingerprint, null);
        }

        using (doc)
        {
            var root = doc.RootElement;
            var benchmarkName = root.TryGetProperty("benchmarkName", out var bn) ? bn.GetString() ?? "Benchmark" : "Benchmark";

            // Strict check: reject incomplete fixtures missing mandatory sections
            if (!root.TryGetProperty("benchmarkPlanes", out _) ||
                !root.TryGetProperty("benchmarkShells", out _) ||
                !root.TryGetProperty("benchmarkFrames", out _) ||
                !root.TryGetProperty("benchmarkLoads", out _) ||
                !root.TryGetProperty("benchmarkTestZones", out _))
            {
                var incompleteChecklist = new BenchmarkChecklist(1, 0, 0, 0, ["Benchmark fixture missing one or more mandatory sections."], []);
                return new PhysicalQualificationResult(
                    PhysicalQualificationOutcome.Inconclusive,
                    product, version, processId, modelId, timestamp,
                    benchmarkName, [],
                    ["Benchmark fixture is incomplete: missing required specification sections."],
                    "Physical qualification is Inconclusive: incomplete benchmark fixture.",
                    incompleteChecklist, scanFingerprint, fixtureHash);
            }

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
            var missingChecks = new List<string>();
            var failedChecks = new List<string>();
            int expectedChecksCount = 0;

            void AddCheck(string category, string elementId, string property, double expected, double actual, double absTol, double? relTol, bool passed, string details)
            {
                expectedChecksCount++;
                var comp = new BenchmarkComparison(category, elementId, property, expected, actual, absTol, relTol, passed, details);
                comparisons.Add(comp);
                if (!passed) failedChecks.Add($"{category} on {elementId} {property}: {details}");
            }

            void AddMissing(string category, string elementId, string property, string reason)
            {
                expectedChecksCount++;
                missingChecks.Add($"{category} on {elementId} {property}: {reason}");
            }

            // Verify tolerances section declared
            if (!root.TryGetProperty("tolerances", out _))
            {
                AddMissing("Tolerances", "Global", "TolerancesDeclared", "Fixture missing explicit tolerances section");
            }
            else
            {
                AddCheck("Tolerances", "Global", "TolerancesDeclared", 1, 1, 0, null, true, "Explicit tolerances declared in fixture");
            }

            // 1. Software / Platform checks
            if (root.TryGetProperty("software", out var sw))
            {
                if (sw.TryGetProperty("product", out var expProd))
                {
                    var ep = expProd.GetString()!;
                    bool pass = string.Equals(scan.Model.Product, ep, StringComparison.OrdinalIgnoreCase);
                    AddCheck("Software", "CSI", "Product", 1, pass ? 1 : 0, 0, null, pass, $"Expected {ep}, Actual {scan.Model.Product}");
                }
                if (sw.TryGetProperty("version", out var expVer))
                {
                    var ev = expVer.GetString()!;
                    bool pass = string.Equals(scan.Model.Version, ev, StringComparison.OrdinalIgnoreCase);
                    AddCheck("Software", "CSI", "Version", 1, pass ? 1 : 0, 0, null, pass, $"Expected version '{ev}', Actual '{scan.Model.Version}'");
                }
                if (sw.TryGetProperty("major", out var expMajor))
                {
                    var em = expMajor.GetInt32();
                    bool pass = scan.Model.Version.StartsWith($"{em}.", StringComparison.Ordinal);
                    AddCheck("Software", "CSI", "MajorVersion", em, pass ? em : 0, 0, null, pass, $"Expected major {em}, Actual {scan.Model.Version}");
                }
                if (sw.TryGetProperty("isLocked", out var expLocked))
                {
                    bool el = expLocked.GetBoolean();
                    bool pass = scan.IsLocked == el;
                    AddCheck("Software", "CSI", "IsLocked", el ? 1 : 0, scan.IsLocked ? 1 : 0, 0, null, pass, $"Expected IsLocked {el}, Actual {scan.IsLocked}");
                }
                if (sw.TryGetProperty("nativeUnits", out var nu))
                {
                    if (nu.TryGetProperty("id", out var nuId))
                    {
                        var expId = nuId.GetString()!;
                        bool passId = string.Equals(scan.Units.NativeUnitId, expId, StringComparison.OrdinalIgnoreCase);
                        AddCheck("Software", "CSI", "NativeUnitId", 1, passId ? 1 : 0, 0, null, passId, $"Expected NativeUnitId '{expId}', Actual '{scan.Units.NativeUnitId}'");
                    }
                    if (nu.TryGetProperty("name", out var nuName))
                    {
                        var expNu = nuName.GetString()!;
                        bool passNu = string.Equals(scan.Units.Description, expNu, StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(scan.Units.NativeUnitId, expNu, StringComparison.OrdinalIgnoreCase);
                        AddCheck("Software", "CSI", "NativeUnits", 1, passNu ? 1 : 0, 0, null, passNu, $"Expected units {expNu}, actual {scan.Units.Description}");
                    }
                    if (nu.TryGetProperty("lengthToM", out var l2m))
                    {
                        var expL2m = l2m.GetDouble();
                        bool passL2m = Math.Abs(scan.Units.LengthToM - expL2m) <= 1e-6;
                        AddCheck("Software", "CSI", "LengthToM", expL2m, scan.Units.LengthToM, 1e-6, null, passL2m, $"Expected LengthToM {expL2m}, actual {scan.Units.LengthToM}");
                    }
                    if (nu.TryGetProperty("forceToN", out var f2n))
                    {
                        var expF2n = f2n.GetDouble();
                        bool passF2n = Math.Abs(scan.Units.ForceToN - expF2n) <= 1e-6;
                        AddCheck("Software", "CSI", "ForceToN", expF2n, scan.Units.ForceToN, 1e-6, null, passF2n, $"Expected ForceToN {expF2n}, actual {scan.Units.ForceToN}");
                    }
                }
            }

            // 2. Check required benchmark frames (coordinates & length)
            if (root.TryGetProperty("benchmarkFrames", out var expectedFrames))
            {
                foreach (var ef in expectedFrames.EnumerateArray())
                {
                    var fId = ef.GetProperty("id").GetString()!;
                    var frame = scan.Model.Frames.FirstOrDefault(f => f.Id == fId);
                    if (frame == null)
                    {
                        AddMissing("Frames", fId, "Existence", "Frame not present in CSI selection");
                    }
                    else
                    {
                        if (ef.TryGetProperty("startX", out var sx) && ef.TryGetProperty("startY", out var sy) && ef.TryGetProperty("startZ", out var sz))
                        {
                            var expStartX = sx.GetDouble(); var expStartY = sy.GetDouble(); var expStartZ = sz.GetDouble();
                            AddCheck("Frames", fId, "Start.X", expStartX, frame.Start.X, coordTol, null, Math.Abs(frame.Start.X - expStartX) <= coordTol, $"Start.X: expected {expStartX:F4}, actual {frame.Start.X:F4}");
                            AddCheck("Frames", fId, "Start.Y", expStartY, frame.Start.Y, coordTol, null, Math.Abs(frame.Start.Y - expStartY) <= coordTol, $"Start.Y: expected {expStartY:F4}, actual {frame.Start.Y:F4}");
                            AddCheck("Frames", fId, "Start.Z", expStartZ, frame.Start.Z, coordTol, null, Math.Abs(frame.Start.Z - expStartZ) <= coordTol, $"Start.Z: expected {expStartZ:F4}, actual {frame.Start.Z:F4}");
                        }
                        if (ef.TryGetProperty("endX", out var ex) && ef.TryGetProperty("endY", out var ey) && ef.TryGetProperty("endZ", out var ez))
                        {
                            var expEndX = ex.GetDouble(); var expEndY = ey.GetDouble(); var expEndZ = ez.GetDouble();
                            AddCheck("Frames", fId, "End.X", expEndX, frame.End.X, coordTol, null, Math.Abs(frame.End.X - expEndX) <= coordTol, $"End.X: expected {expEndX:F4}, actual {frame.End.X:F4}");
                            AddCheck("Frames", fId, "End.Y", expEndY, frame.End.Y, coordTol, null, Math.Abs(frame.End.Y - expEndY) <= coordTol, $"End.Y: expected {expEndY:F4}, actual {frame.End.Y:F4}");
                            AddCheck("Frames", fId, "End.Z", expEndZ, frame.End.Z, coordTol, null, Math.Abs(frame.End.Z - expEndZ) <= coordTol, $"End.Z: expected {expEndZ:F4}, actual {frame.End.Z:F4}");
                        }
                        if (ef.TryGetProperty("lengthM", out var expLen))
                        {
                            var el = expLen.GetDouble();
                            var actLen = (frame.End - frame.Start).Length;
                            AddCheck("Frames", fId, "LengthM", el, actLen, coordTol, null, Math.Abs(actLen - el) <= coordTol, $"Length expected {el:F4} m, actual {actLen:F4} m");
                        }
                    }
                }
            }

            // 3. Check required benchmark shells
            if (root.TryGetProperty("benchmarkShells", out var expectedShells))
            {
                var shellArray = expectedShells.EnumerateArray().ToArray();
                var missingShells = shellArray
                    .Select(e => e.GetProperty("id").GetString()!)
                    .Where(id => !scan.Model.Shells.Any(s => s.Id == id))
                    .ToArray();

                if (missingShells.Length > 0)
                {
                    var incChecklist = new BenchmarkChecklist(shellArray.Length, 0, 0, 0, missingShells.Select(s => $"Shell {s} missing from selection").ToImmutableArray(), []);
                    return new PhysicalQualificationResult(
                        PhysicalQualificationOutcome.Inconclusive,
                        product, version, processId, modelId, timestamp,
                        benchmarkName, [],
                        [$"Required benchmark shells missing from CSI selection: {string.Join(", ", missingShells)}."],
                        $"Physical qualification is Inconclusive: CSI selection does not contain required benchmark elements ({string.Join(", ", missingShells)}).",
                        incChecklist, scanFingerprint, fixtureHash);
                }

                foreach (var expected in shellArray)
                {
                    var id = expected.GetProperty("id").GetString()!;
                    var actualShell = scan.Model.Shells.FirstOrDefault(s => s.Id == id);
                    if (actualShell == null)
                    {
                        AddMissing("Shells", id, "Existence", "Shell not present in CSI selection");
                        continue;
                    }

                    var actual3D = shells3D.IsDefault ? null : shells3D.FirstOrDefault(s => s.ShellId == id);

                    // Verify Local 3
                    if (expected.TryGetProperty("local3", out var expL3))
                    {
                        var ex = expL3.GetProperty("x").GetDouble();
                        var ey = expL3.GetProperty("y").GetDouble();
                        var ez = expL3.GetProperty("z").GetDouble();

                        AddCheck("Local 3 Vectors", id, "Local3.X", ex, actualShell.Local3.X, l3Tol, null, Math.Abs(actualShell.Local3.X - ex) <= l3Tol, $"Expected {ex:F6}, Actual {actualShell.Local3.X:F6}");
                        AddCheck("Local 3 Vectors", id, "Local3.Y", ey, actualShell.Local3.Y, l3Tol, null, Math.Abs(actualShell.Local3.Y - ey) <= l3Tol, $"Expected {ey:F6}, Actual {actualShell.Local3.Y:F6}");
                        AddCheck("Local 3 Vectors", id, "Local3.Z", ez, actualShell.Local3.Z, l3Tol, null, Math.Abs(actualShell.Local3.Z - ez) <= l3Tol, $"Expected {ez:F6}, Actual {actualShell.Local3.Z:F6}");
                        AddCheck("Local 3 Vectors", id, "Local3.UnitLength", 1.0, actualShell.Local3.Length, 1e-4, null, Math.Abs(actualShell.Local3.Length - 1.0) <= 1e-4, $"Unit length {actualShell.Local3.Length:F6}");
                    }

                    // Verify 3D Surface Area
                    if (expected.TryGetProperty("surfaceAreaM2", out var expArea))
                    {
                        var ea = expArea.GetDouble();
                        if (actual3D == null)
                        {
                            AddMissing("3D Surface Areas", id, "SurfaceAreaM2", "Shell 3D geometry not processed");
                        }
                        else
                        {
                            var diff = Math.Abs(actual3D.SurfaceAreaM2 - ea);
                            bool pass = diff <= areaTol || (ea > 0 && diff / ea <= 1e-4);
                            AddCheck("3D Surface Areas", id, "SurfaceAreaM2", ea, actual3D.SurfaceAreaM2, areaTol, 1e-4, pass, $"Expected {ea:F4} m², Actual {actual3D.SurfaceAreaM2:F4} m²");
                        }
                    }

                    // Verify Projected Area
                    if (expected.TryGetProperty("projectedAreaM2", out var expProj))
                    {
                        var ep = expProj.GetDouble();
                        if (actual3D == null)
                        {
                            AddMissing("Projected Areas", id, "ProjectedAreaM2", "Shell 3D geometry not processed");
                        }
                        else
                        {
                            var diff = Math.Abs(actual3D.ProjectedAreaM2 - ep);
                            bool pass = diff <= areaTol || (ep > 0 && diff / ep <= 1e-4);
                            AddCheck("Projected Areas", id, "ProjectedAreaM2", ep, actual3D.ProjectedAreaM2, areaTol, 1e-4, pass, $"Expected {ep:F4} m², Actual {actual3D.ProjectedAreaM2:F4} m²");
                        }
                    }

                    // Verify Global Vertices
                    if (expected.TryGetProperty("verticesXYZ", out var expVerts))
                    {
                        var evList = expVerts.EnumerateArray().ToArray();
                        AddCheck("Global XYZ Coordinates", id, "VertexCount", evList.Length, actualShell.Vertices.Length, 0, null, actualShell.Vertices.Length == evList.Length, $"Vertex count: expected {evList.Length}, actual {actualShell.Vertices.Length}");
                        for (int i = 0; i < Math.Min(evList.Length, actualShell.Vertices.Length); i++)
                        {
                            var vx = evList[i].GetProperty("x").GetDouble();
                            var vy = evList[i].GetProperty("y").GetDouble();
                            var vz = evList[i].GetProperty("z").GetDouble();
                            var act = actualShell.Vertices[i];

                            AddCheck("Global XYZ Coordinates", id, $"V[{i}].X", vx, act.X, coordTol, null, Math.Abs(act.X - vx) <= coordTol, $"Vertex {i} X");
                            AddCheck("Global XYZ Coordinates", id, $"V[{i}].Y", vy, act.Y, coordTol, null, Math.Abs(act.Y - vy) <= coordTol, $"Vertex {i} Y");
                            AddCheck("Global XYZ Coordinates", id, $"V[{i}].Z", vz, act.Z, coordTol, null, Math.Abs(act.Z - vz) <= coordTol, $"Vertex {i} Z");
                        }
                        if (actualShell.Vertices.Length < evList.Length)
                        {
                            for (int i = actualShell.Vertices.Length; i < evList.Length; i++)
                                AddMissing("Global XYZ Coordinates", id, $"V[{i}]", "Vertex missing from actual shell");
                        }
                    }
                }
            }

            // 4. Roof Planes verification (plane count, normals, slopes, shell membership)
            if (root.TryGetProperty("benchmarkPlanes", out var expectedPlanes))
            {
                var expPlanesList = expectedPlanes.EnumerateArray().ToArray();
                if (planes == null || planes.Count == 0)
                {
                    AddMissing("Roof Planes", "All", "PlaneCount", $"Expected {expPlanesList.Length} reconstructed roof planes; input was empty.");
                }
                else
                {
                    AddCheck("Roof Planes", "All", "PlaneCount", expPlanesList.Length, planes.Count, 0, null, planes.Count >= expPlanesList.Length, $"Plane count: expected {expPlanesList.Length}, actual {planes.Count}");
                    foreach (var ep in expPlanesList)
                    {
                        var pId = ep.GetProperty("planeId").GetString()!;
                        var normObj = ep.GetProperty("normal");
                        var expNorm = new Vec3(normObj.GetProperty("x").GetDouble(), normObj.GetProperty("y").GetDouble(), normObj.GetProperty("z").GetDouble());
                        var expSlope = ep.TryGetProperty("slopeDegrees", out var sd) ? sd.GetDouble() : 0.0;

                        var matchedPlane = planes.FirstOrDefault(p => Math.Abs(Math.Abs(p.Normal.Dot(expNorm)) - 1.0) < 0.05);
                        if (matchedPlane == null)
                        {
                            AddMissing("Roof Planes", pId, "Reconstruction", $"Reconstructed plane for {pId} with normal {expNorm} not found");
                        }
                        else
                        {
                            var planeSlope = Math.Acos(Math.Clamp(Math.Abs(matchedPlane.Normal.Z), -1.0, 1.0)) * 180.0 / Math.PI;
                            AddCheck("Roof Planes", pId, "SlopeDegrees", expSlope, planeSlope, 0.5, null, Math.Abs(planeSlope - expSlope) <= 0.5, $"Plane {pId} slope: expected {expSlope:F2}°, actual {planeSlope:F2}°");

                            // Verify shell membership per plane
                            if (ep.TryGetProperty("shells", out var epShells))
                            {
                                foreach (var shElem in epShells.EnumerateArray())
                                {
                                    var shId = shElem.GetString()!;
                                    var sh = scan.Model.Shells.FirstOrDefault(s => s.Id == shId);
                                    if (sh == null)
                                    {
                                        AddMissing("Roof Planes", pId, $"ShellMember:{shId}", $"Shell {shId} missing from selection");
                                    }
                                    else
                                    {
                                        bool allCoplanar = sh.Vertices.All(v => Math.Abs(matchedPlane.Normal.Dot(v - matchedPlane.Origin)) <= coordTol);
                                        AddCheck("Roof Planes", pId, $"ShellMember:{shId}", 1, allCoplanar ? 1 : 0, 0, null, allCoplanar, $"Shell {shId} coplanar with plane {pId}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 5. Test Zones & Intersections verification
            if (root.TryGetProperty("benchmarkTestZones", out var testZones))
            {
                foreach (var tz in testZones.EnumerateArray())
                {
                    var targetShellId = tz.GetProperty("targetShellId").GetString()!;
                    var zoneId = tz.GetProperty("zoneId").GetString()!;

                    if (!intersections.HasValue || intersections.Value.IsDefaultOrEmpty)
                    {
                        AddMissing("Zone Intersections", $"{targetShellId}:{zoneId}", "Coverage", "Intersection results not provided");
                        continue;
                    }

                    var match = intersections.Value.FirstOrDefault(i => i.ShellId == targetShellId);
                    if (match == null)
                    {
                        AddMissing("Zone Intersections", $"{targetShellId}:{zoneId}", "Classification", $"No intersection record for shell {targetShellId}");
                        continue;
                    }

                    if (tz.TryGetProperty("expectedClassification", out var expClass))
                    {
                        var ec = expClass.GetString()!;
                        bool pass = string.Equals(match.Classification.ToString(), ec, StringComparison.Ordinal);
                        AddCheck("Zone Intersections", targetShellId, "Classification", 1, pass ? 1 : 0, 0, null, pass, $"Expected classification {ec}, actual {match.Classification}");
                    }

                    if (tz.TryGetProperty("needsSplitInCSI", out var expSplit))
                    {
                        var es = expSplit.GetBoolean();
                        bool pass = match.NeedsSplitInCSI == es;
                        AddCheck("Zone Intersections", targetShellId, "NeedsSplitInCSI", es ? 1 : 0, match.NeedsSplitInCSI ? 1 : 0, 0, null, pass, $"Expected NeedsSplitInCSI {es}, actual {match.NeedsSplitInCSI}");
                    }

                    if (tz.TryGetProperty("expectedIntersectionAreaM2", out var expIa))
                    {
                        var expectedArea = expIa.GetDouble();
                        var actualCov = match.Coverages.FirstOrDefault(c => c.ZoneId == zoneId);
                        if (actualCov == null)
                        {
                            AddMissing("Zone Intersections", $"{targetShellId}:{zoneId}", "IntersectionAreaM2", $"No coverage recorded for zone {zoneId}");
                        }
                        else
                        {
                            bool pass = Math.Abs(actualCov.IntersectionAreaM2 - expectedArea) <= areaTol;
                            AddCheck("Zone Intersections", $"{targetShellId}:{zoneId}", "IntersectionAreaM2", expectedArea, actualCov.IntersectionAreaM2, areaTol, null, pass, $"Coverage area: expected {expectedArea:F4} m², actual {actualCov.IntersectionAreaM2:F4} m²");
                        }
                    }

                    if (tz.TryGetProperty("expectedCoveragePercent", out var expCp))
                    {
                        var expectedPct = expCp.GetDouble();
                        var actualCov = match.Coverages.FirstOrDefault(c => c.ZoneId == zoneId);
                        if (actualCov != null)
                        {
                            bool pass = Math.Abs(actualCov.CoveragePercent - expectedPct) <= covTol;
                            AddCheck("Coverage Percentages", $"{targetShellId}:{zoneId}", "CoveragePercent", expectedPct, actualCov.CoveragePercent, covTol, null, pass, $"Coverage %: expected {expectedPct:F2}%, actual {actualCov.CoveragePercent:F2}%");
                        }
                    }
                }
            }

            // 6. Existing Loads verification
            if (root.TryGetProperty("benchmarkLoads", out var expectedLoads))
            {
                foreach (var el in expectedLoads.EnumerateArray())
                {
                    var objId = el.GetProperty("objectId").GetString()!;
                    var pattern = el.GetProperty("pattern").GetString()!;
                    var expVal = el.GetProperty("valueSI").GetDouble();
                    var matchedLoad = scan.Model.ExistingLoads.FirstOrDefault(l => l.ObjectId == objId && l.Pattern == pattern);
                    if (matchedLoad == null)
                    {
                        AddMissing("Existing Loads", objId, $"LoadSI:{pattern}", $"Expected load for pattern '{pattern}' on object '{objId}' missing from scan");
                    }
                    else
                    {
                        if (el.TryGetProperty("loadType", out var expType))
                        {
                            var et = expType.GetString()!;
                            bool passType = matchedLoad.Kind.Contains(et, StringComparison.OrdinalIgnoreCase);
                            AddCheck("Existing Loads", objId, $"LoadKind:{pattern}", 1, passType ? 1 : 0, 0, null, passType, $"Expected load kind containing '{et}', actual '{matchedLoad.Kind}'");
                        }
                        if (el.TryGetProperty("direction", out var expDir))
                        {
                            var ed = expDir.GetString()!;
                            bool passDir = matchedLoad.Kind.Contains(ed, StringComparison.OrdinalIgnoreCase) ||
                                           (ed == "Gravity" && matchedLoad.Kind.Contains("dir=10")) ||
                                           (ed == "Local3" && (matchedLoad.Kind.Contains("dir=3") || matchedLoad.Kind.Contains("Local")));
                            AddCheck("Existing Loads", objId, $"LoadDirection:{pattern}", 1, passDir ? 1 : 0, 0, null, passDir, $"Expected direction '{ed}', actual '{matchedLoad.Kind}'");
                        }
                        bool passVal = Math.Abs(matchedLoad.ValueSI - expVal) <= loadTol;
                        AddCheck("Existing Loads", objId, $"LoadSI:{pattern}", expVal, matchedLoad.ValueSI, loadTol, null, passVal, $"Expected {expVal:F3}, actual {matchedLoad.ValueSI:F3}");
                    }
                }
            }

            int passedChecksCount = comparisons.Count(c => c.Passed);
            int executedChecksCount = comparisons.Count;
            var checklist = new BenchmarkChecklist(
                expectedChecksCount, executedChecksCount, passedChecksCount, failedChecks.Count,
                missingChecks.ToImmutableArray(), failedChecks.ToImmutableArray());

            // Strict fail-closed decision:
            // Missing mandatory checks or failed comparisons MUST prevent Passed status!
            PhysicalQualificationOutcome outcome;
            string summary;
            if (executedChecksCount == 0)
            {
                outcome = PhysicalQualificationOutcome.Inconclusive;
                summary = "Physical qualification is Inconclusive: no benchmark comparisons could be executed.";
            }
            else if (missingChecks.Count > 0)
            {
                outcome = PhysicalQualificationOutcome.Failed;
                summary = $"Physical Qualification FAILED • {missingChecks.Count} mandatory benchmark checks were missing or incomplete.";
            }
            else if (failedChecks.Count > 0)
            {
                outcome = PhysicalQualificationOutcome.Failed;
                summary = $"Physical Qualification FAILED • {failedChecks.Count} of {executedChecksCount} benchmark checks exceeded tolerance.";
            }
            else
            {
                outcome = PhysicalQualificationOutcome.Passed;
                summary = $"Physical Qualification PASSED • All {executedChecksCount} independent benchmark checks confirmed within tolerances.";
            }

            return new PhysicalQualificationResult(
                outcome, product, version, processId, modelId, timestamp,
                benchmarkName, comparisons.ToImmutableArray(),
                failedChecks.Concat(missingChecks).ToImmutableArray(),
                summary,
                checklist,
                scanFingerprint,
                fixtureHash);
        }
    }
}
