using System.Collections.Immutable;
using System.IO;
using System.Linq;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class IndependentGeometryBenchmarkTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/etabs-roof-independent-benchmark.json");

    private static CsiReadScan CreateBenchmarkScan(
        double vertexXOffset = 0.0,
        double surfaceAreaOffset = 0.0,
        bool invertLocal3 = false)
    {
        var l3West = new Vec3(-0.4472135955, 0.0, 0.8944271910);
        var l3WestInverted = new Vec3(0.4472135955, 0.0, -0.8944271910);
        var l3East = new Vec3(0.4472135955, 0.0, 0.8944271910);
        var l3EastInverted = new Vec3(-0.4472135955, 0.0, -0.8944271910);

        var shells = ImmutableArray.Create(
            new ShellGeometry("101", "Slope-West", [
                new(0.0 + vertexXOffset, 0.0, 6.0),
                new(2.0, 0.0, 7.0),
                new(2.0, 4.0, 7.0),
                new(0.0, 4.0, 6.0)
            ], invertLocal3 ? l3WestInverted : l3West, default),
            new ShellGeometry("102", "Slope-West", [
                new(2.0, 0.0, 7.0),
                new(4.0, 0.0, 8.0),
                new(4.0, 4.0, 8.0),
                new(2.0, 4.0, 7.0)
            ], l3WestInverted, default),
            new ShellGeometry("201", "Slope-East", [
                new(4.0, 0.0, 8.0),
                new(6.0, 0.0, 7.0),
                new(6.0, 4.0, 7.0),
                new(4.0, 4.0, 8.0)
            ], l3East, default),
            new ShellGeometry("202", "Slope-East", [
                new(6.0, 0.0, 7.0),
                new(8.0, 0.0, 6.0),
                new(8.0, 4.0, 6.0),
                new(6.0, 4.0, 7.0)
            ], l3EastInverted, default)
        );

        var frames = ImmutableArray.Create(
            new FrameGeometry("101", "Eaves", new(0.0, 0.0, 6.0), new(0.0, 4.0, 6.0))
        );

        var loads = ImmutableArray.Create(
            new ExistingLoad("101", "W_Uniform", "FrameDistributed", 1500.0, "Unknown"),
            new ExistingLoad("101", "W_Suction", "AreaUniform", -1200.0, "Unknown")
        );

        var model = new CsiModelSnapshot(
            "benchmark-hash", "ETABS-BENCHMARK.EDB", "Etabs", "23.2.0",
            "geom-hash", "loads-hash", ["W_Uniform", "W_Suction"], ["DEAD"], ["COMB1"],
            frames, shells, loads
        );

        return new CsiReadScan(
            model, new("9", 0.001, 1.0, "N_mm_C"), true,
            new(true, true, true, true, true), [], [], []
        );
    }

    private static ImmutableArray<RoofShell3D> CreateBenchmarkShells3D(double surfaceAreaOffset = 0.0)
    {
        var scan = CreateBenchmarkScan();
        return scan.Model.Shells.Select(s => {
            var processed = Roof3DGeometryProcessor.Process(s, "benchmark-model");
            return surfaceAreaOffset != 0.0 && s.Id == "101"
                ? processed with { SurfaceAreaM2 = processed.SurfaceAreaM2 + surfaceAreaOffset }
                : processed;
        }).ToImmutableArray();
    }

    [Fact]
    public void Missing_fixture_file_returns_inconclusive()
    {
        var scan = CreateBenchmarkScan();
        var result = IndependentGeometryBenchmarkValidator.Validate(
            scan, CreateBenchmarkShells3D(), [], "fixtures/non_existent_fixture.json");

        Assert.Equal(PhysicalQualificationOutcome.Inconclusive, result.Outcome);
        Assert.Contains("missing independent reference fixture", result.Summary);
    }

    [Fact]
    public void Missing_benchmark_elements_in_selection_returns_inconclusive()
    {
        var resolvedFixture = File.Exists(FixturePath) ? FixturePath : "fixtures/etabs-roof-independent-benchmark.json";
        var emptyScan = new CsiReadScan(
            DemoFixtures.Model(), new("N_m_C", 1, 1, "N_m_C"), true,
            new(true, true, true, true, true), [], [], []
        );

        var result = IndependentGeometryBenchmarkValidator.Validate(
            emptyScan, [], [], resolvedFixture);

        Assert.Equal(PhysicalQualificationOutcome.Inconclusive, result.Outcome);
        Assert.Contains("does not contain required benchmark elements", result.Summary);
    }

    [Fact]
    public void Exact_benchmark_match_passes_all_comparisons()
    {
        var resolvedFixture = File.Exists(FixturePath) ? FixturePath : "fixtures/etabs-roof-independent-benchmark.json";
        var scan = CreateBenchmarkScan();
        var shells3D = CreateBenchmarkShells3D();

        var intersections = ImmutableArray.Create(
            new ShellZoneIntersection("101", GeometryClassification.FullyInsideZone, 8.94427191, [
                new("Zone-A1-Full", 8.94427191, 100.0),
                new("Zone-A1-Half", 4.472135955, 50.0)
            ], false, "Fully inside"),
            new ShellZoneIntersection("102", GeometryClassification.Unmapped, 8.94427191, [], false, "Unmapped")
        );

        var result = IndependentGeometryBenchmarkValidator.Validate(
            scan, shells3D, [], resolvedFixture, intersections);

        Assert.True(result.Outcome == PhysicalQualificationOutcome.Passed, string.Join("; ", result.Issues));
        Assert.Equal(PhysicalQualificationOutcome.Passed, result.Outcome);
        Assert.Empty(result.Issues);
        Assert.Contains("PASSED", result.Summary);
        Assert.True(result.Comparisons.Length >= 15);
        Assert.All(result.Comparisons, c => Assert.True(c.Passed, $"Comparison failed: {c.Category} {c.ElementId} {c.Property}"));
    }

    [Fact]
    public void Vertex_coordinate_deviation_exceeding_tolerance_fails()
    {
        var resolvedFixture = File.Exists(FixturePath) ? FixturePath : "fixtures/etabs-roof-independent-benchmark.json";
        var scan = CreateBenchmarkScan(vertexXOffset: 0.010);
        var shells3D = CreateBenchmarkShells3D();

        var result = IndependentGeometryBenchmarkValidator.Validate(
            scan, shells3D, [], resolvedFixture);

        Assert.Equal(PhysicalQualificationOutcome.Failed, result.Outcome);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Comparisons, c => !c.Passed && c.Category == "Global XYZ Coordinates");
    }

    [Fact]
    public void Surface_area_deviation_exceeding_tolerance_fails()
    {
        var resolvedFixture = File.Exists(FixturePath) ? FixturePath : "fixtures/etabs-roof-independent-benchmark.json";
        var scan = CreateBenchmarkScan();
        var shells3D = CreateBenchmarkShells3D(surfaceAreaOffset: 0.200);
        var result = IndependentGeometryBenchmarkValidator.Validate(
            scan, shells3D, [], resolvedFixture);

        Assert.Equal(PhysicalQualificationOutcome.Failed, result.Outcome);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Comparisons, c => !c.Passed && c.Category == "3D Surface Areas");
    }

    [Fact]
    public void Opposite_local3_inversion_fails()
    {
        var resolvedFixture = File.Exists(FixturePath) ? FixturePath : "fixtures/etabs-roof-independent-benchmark.json";
        var scan = CreateBenchmarkScan(invertLocal3: true);
        var shells3D = CreateBenchmarkShells3D();

        var result = IndependentGeometryBenchmarkValidator.Validate(
            scan, shells3D, [], resolvedFixture);

        Assert.Equal(PhysicalQualificationOutcome.Failed, result.Outcome);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Comparisons, c => !c.Passed && c.Category == "Local 3 Vectors");
    }

    [Fact]
    public void Roof_pressure_prerequisites_block_unreviewed_or_invalid_mappings()
    {
        var validRow = new RoofShellMapping("101", "WX", "Zone1", ShellZoneClassification.FullyInsideZone,
            ManualDirection: ManualLocal3Direction.Positive, MappingContractVersion: 2,
            ZoneCoverages: [new("Zone1", 8.94427191, 100.0)], ShellAreaM2: 8.94427191);

        var crossingRow = new RoofShellMapping("102", "WX", "Zone1", ShellZoneClassification.CrossesZoneBoundary,
            ManualDirection: ManualLocal3Direction.Positive, MappingContractVersion: 2,
            ZoneCoverages: [new("Zone1", 4.47, 50.0)], ShellAreaM2: 8.94427191);

        var issuesDraft = RoofPressurePrerequisites.Validate([validRow], [], GeometryReviewStatus.GeometryDraft);
        Assert.NotEmpty(issuesDraft);
        Assert.Contains("GeometryReviewed", issuesDraft[0]);

        var issuesCrossing = RoofPressurePrerequisites.Validate([validRow, crossingRow], [], GeometryReviewStatus.GeometryReviewed);
        Assert.NotEmpty(issuesCrossing);
        Assert.Contains("blocks pressure mapping", issuesCrossing[0]);

        var issuesExcluded = RoofPressurePrerequisites.Validate([validRow, crossingRow], [new("102", "Engineer", "Tested exclusion", DateTimeOffset.UtcNow)], GeometryReviewStatus.GeometryReviewed);
        Assert.Empty(issuesExcluded);
    }
}
