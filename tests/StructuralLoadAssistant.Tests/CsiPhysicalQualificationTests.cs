using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.ApiV1;
using StructuralLoadAssistant.Domain;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class CsiPhysicalQualificationTests
{
    private const string EtabsDllPath = @"C:\Program Files\Computers and Structures\ETABS 23\ETABSv1.dll";
    private const string Sap2000DllPath = @"C:\Program Files\Computers and Structures\SAP2000 27\SAP2000v1.dll";

    [Fact]
    [Trait("Category", "PhysicalCsi")]
    public async Task Physical_ETABS_read_only_qualification_when_running()
    {
        if (Environment.GetEnvironmentVariable("CSI_PHYSICAL_QUALIFICATION") != "1")
        {
            throw new InvalidOperationException("Opt-in physical CSI qualification requires CSI_PHYSICAL_QUALIFICATION=1. Exclude this test using '--filter Category!=PhysicalCsi' or run scripts/qualify-csi.ps1.");
        }

        var envAssembly = Environment.GetEnvironmentVariable("CSI_ASSEMBLY_PATH");
        var assemblyPath = !string.IsNullOrWhiteSpace(envAssembly) && File.Exists(envAssembly) ? envAssembly : EtabsDllPath;

        if (!OperatingSystem.IsWindows() || !File.Exists(assemblyPath) || Process.GetProcessesByName("ETABS").Length == 0)
        {
            throw new InvalidOperationException($"ETABS 23 is not running or ETABSv1.dll was not found at '{assemblyPath}'. Physical qualification cannot proceed.");
        }

        await using var adapter = new InstalledCsiReadAdapter();
        var envPid = Environment.GetEnvironmentVariable("CSI_PROCESS_ID");
        int? targetPid = int.TryParse(envPid, out var p) && p > 0 ? p : null;
        var connectResult = await adapter.ConnectAsync(new CsiConnectionOptions(CsiProduct.Etabs, assemblyPath, targetPid));
        
        Assert.True(connectResult.Connected, "Should connect to running ETABS instance.");
        Assert.Equal("Etabs", connectResult.Product);
        Assert.StartsWith("23.", connectResult.Version);
        Assert.True(connectResult.Capabilities.IsReadOnly);

        var scan = await adapter.RefreshSelectionAsync();
        Assert.NotNull(scan);
        Assert.True(scan.Capabilities.IsReadOnly);
        Assert.False(string.IsNullOrWhiteSpace(scan.Model.ModelId));
        Assert.NotEqual("unknown", scan.Units.NativeUnitId);

        // Load reviewed physical baseline fixture
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/physical-csi-reviewed-baseline.json");
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "fixtures/physical-csi-reviewed-baseline.json");
        }
        Assert.True(File.Exists(fixturePath), $"Baseline fixture not found at {fixturePath}");
        var fixtureJson = await File.ReadAllTextAsync(fixturePath);
        using var doc = System.Text.Json.JsonDocument.Parse(fixtureJson);
        var root = doc.RootElement;

        // Strict physical qualification: selection must not be empty or partial
        Assert.NotEqual(CsiScanOutcome.Failed, scan.Outcome);
        Assert.NotEqual(CsiScanOutcome.EmptySelection, scan.Outcome);
        Assert.True(scan.Model.Frames.Length >= 2, $"Strict qualification requires at least 2 frames in selection, got {scan.Model.Frames.Length}");
        Assert.True(scan.Model.Shells.Length >= 2, $"Strict qualification requires at least 2 shells in selection, got {scan.Model.Shells.Length}");

            // Verify frames: coordinates in SI metres, valid nonzero length
            foreach (var frame in scan.Model.Frames)
            {
                Assert.False(string.IsNullOrWhiteSpace(frame.Id));
                Assert.True(double.IsFinite(frame.Start.X));
                Assert.True(double.IsFinite(frame.Start.Y));
                Assert.True(double.IsFinite(frame.Start.Z));
                Assert.True(double.IsFinite(frame.End.X));
                Assert.True(double.IsFinite(frame.End.Y));
                Assert.True(double.IsFinite(frame.End.Z));
                Assert.True((frame.End - frame.Start).Length > 1e-4, "Frame must have nonzero length.");
            }

            // Assert against expected frames from fixture
            if (root.TryGetProperty("expectedFrames", out var expectedFrames))
            {
                foreach (var expected in expectedFrames.EnumerateArray())
                {
                    var id = expected.GetProperty("id").GetString();
                    var matched = scan.Model.Frames.FirstOrDefault(f => f.Id == id);
                    if (matched != null)
                    {
                        var startY = expected.GetProperty("startY").GetDouble();
                        var endY = expected.GetProperty("endY").GetDouble();
                        var length = expected.GetProperty("lengthM").GetDouble();
                        var tol = expected.GetProperty("toleranceM").GetDouble();
                        Assert.True(Math.Abs(matched.Start.Y - startY) <= tol, $"Frame {id} Start.Y expected {startY} got {matched.Start.Y}");
                        Assert.True(Math.Abs(matched.End.Y - endY) <= tol, $"Frame {id} End.Y expected {endY} got {matched.End.Y}");
                        Assert.True(Math.Abs((matched.End - matched.Start).Length - length) <= tol, $"Frame {id} length expected {length} got {(matched.End - matched.Start).Length}");
                    }
                }
            }

            // Verify shells: vertices, right-hand rule, unit Local 3
            foreach (var shell in scan.Model.Shells)
            {
                Assert.False(string.IsNullOrWhiteSpace(shell.Id));
                Assert.True(shell.Vertices.Length >= 3, "Shell must have at least 3 vertices.");
                foreach (var v in shell.Vertices)
                {
                    Assert.True(double.IsFinite(v.X));
                    Assert.True(double.IsFinite(v.Y));
                    Assert.True(double.IsFinite(v.Z));
                }
                Assert.True(shell.Local3.Length > 0.5, "Local 3 must be available.");
                Assert.True(Math.Abs(shell.Local3.Length - 1.0) < 1e-4, "Local 3 must be a unit vector.");
            }

            // Assert against expected shells from fixture
            if (root.TryGetProperty("expectedShells", out var expectedShells))
            {
                foreach (var expected in expectedShells.EnumerateArray())
                {
                    var id = expected.GetProperty("id").GetString();
                    var matched = scan.Model.Shells.FirstOrDefault(s => s.Id == id);
                    if (matched != null)
                    {
                        var l3 = expected.GetProperty("local3");
                        var expL3 = new Vec3(l3.GetProperty("x").GetDouble(), l3.GetProperty("y").GetDouble(), l3.GetProperty("z").GetDouble());
                        var tol = expected.TryGetProperty("tolerance", out var t) ? t.GetDouble() : 0.05;
                        Assert.True(Math.Abs(matched.Local3.X - expL3.X) <= tol, $"Shell {id} Local3.X expected {expL3.X} got {matched.Local3.X}");
                        Assert.True(Math.Abs(matched.Local3.Y - expL3.Y) <= tol, $"Shell {id} Local3.Y expected {expL3.Y} got {matched.Local3.Y}");
                        Assert.True(Math.Abs(matched.Local3.Z - expL3.Z) <= tol, $"Shell {id} Local3.Z expected {expL3.Z} got {matched.Local3.Z}");
                    }
                }
            }

            // Verify opposite Local 3 vectors between horizontal slabs (Area 10 pointing +Z, Area 18 pointing -Z)
            var area10 = scan.Model.Shells.FirstOrDefault(s => s.Id == "10");
            var area18 = scan.Model.Shells.FirstOrDefault(s => s.Id == "18");
            if (area10 is not null && area18 is not null)
            {
                Assert.True(area10.Local3.Z > 0.9, $"Area 10 Local3.Z should be +1, got {area10.Local3.Z}");
                Assert.True(area18.Local3.Z < -0.9, $"Area 18 Local3.Z should be -1, got {area18.Local3.Z}");
            }

            // Verify rotated / sloped shells if selected (Area 64 or Area 84)
            var sloped = scan.Model.Shells.FirstOrDefault(s => s.Id is "64" or "84" or "15" or "29");
            if (sloped is not null)
            {
                Assert.True(Math.Abs(sloped.Local3.X) > 0.05 || Math.Abs(sloped.Local3.Y) > 0.05,
                    $"Expected sloped shell Local3 to have horizontal component, got ({sloped.Local3.X}, {sloped.Local3.Y}, {sloped.Local3.Z})");
            }

            // Verify existing loads readback
            Assert.NotEmpty(scan.Model.ExistingLoads);
            foreach (var load in scan.Model.ExistingLoads)
            {
                Assert.False(string.IsNullOrWhiteSpace(load.ObjectId));
                Assert.False(string.IsNullOrWhiteSpace(load.Pattern));
                Assert.False(string.IsNullOrWhiteSpace(load.Kind));
                Assert.True(double.IsFinite(load.ValueSI));
            }

            // Assert against expected loads from fixture
            if (root.TryGetProperty("expectedLoads", out var expectedLoads))
            {
                foreach (var expected in expectedLoads.EnumerateArray())
                {
                    var objId = expected.GetProperty("objectId").GetString();
                    var pattern = expected.GetProperty("pattern").GetString();
                    var valueSI = expected.GetProperty("valueSI").GetDouble();
                    var tolSI = expected.GetProperty("toleranceSI").GetDouble();
                    var matched = scan.Model.ExistingLoads.FirstOrDefault(l => l.ObjectId == objId && l.Pattern == pattern);
                    if (matched != null)
                    {
                        Assert.True(Math.Abs(matched.ValueSI - valueSI) <= tolSI, $"Load on {objId} pattern {pattern} expected {valueSI} got {matched.ValueSI}");
                    }
                }
            }
    }

    [Fact]
    public async Task Physical_Mock_adapter_distinguished_from_live_CSI()
    {
        await using var session = new CsiInspectionSession(new MockAdapter());
        await session.ConnectAsync(new CsiConnectionOptions(CsiProduct.Etabs, "mock.dll"));
        
        Assert.NotNull(session.Connection);
        Assert.True(session.Connection.Connected);
        Assert.StartsWith("MOCK", session.Connection.Product, StringComparison.OrdinalIgnoreCase);

        await session.RefreshAsync();
        Assert.NotNull(session.Scan);
        Assert.StartsWith("MOCK", session.Scan.Model.Product, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MOCK", session.Scan.Model.ModelId, StringComparison.OrdinalIgnoreCase);

        // RealReadOnly inspection ensures pressure mapping remains Unmapped
        Assert.NotEmpty(session.RoofRows);
        Assert.All(session.RoofRows, r => Assert.Null(r.ZoneId));
    }

    [Fact]
    public async Task Physical_SAP2000_reported_as_pending_when_disconnected()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Sap2000DllPath) || Process.GetProcessesByName("SAP2000").Length > 0)
        {
            // Only test disconnected behavior when SAP2000 is not running
            return;
        }

        await using var adapter = new InstalledCsiReadAdapter();
        var result = await adapter.ConnectAsync(new CsiConnectionOptions(CsiProduct.Sap2000, Sap2000DllPath));
        
        Assert.False(result.Connected);
        Assert.Contains(result.Issues, i => i.Code is "DISCONNECTED" or "ConnectionFailed");
    }

    [Fact]
    [Trait("Category", "PhysicalCsi")]
    public async Task Physical_ETABS_independent_benchmark_qualification_when_running()
    {
        if (Environment.GetEnvironmentVariable("CSI_PHYSICAL_QUALIFICATION") != "1")
        {
            throw new InvalidOperationException("Opt-in physical CSI qualification requires CSI_PHYSICAL_QUALIFICATION=1. Exclude this test using '--filter Category!=PhysicalCsi' or run scripts/qualify-csi.ps1.");
        }

        var envAssembly = Environment.GetEnvironmentVariable("CSI_ASSEMBLY_PATH");
        var assemblyPath = !string.IsNullOrWhiteSpace(envAssembly) && File.Exists(envAssembly) ? envAssembly : EtabsDllPath;

        if (!OperatingSystem.IsWindows() || !File.Exists(assemblyPath) || Process.GetProcessesByName("ETABS").Length == 0)
        {
            throw new InvalidOperationException($"ETABS 23 is not running or ETABSv1.dll was not found at '{assemblyPath}'. Physical qualification cannot proceed.");
        }

        await using var adapter = new InstalledCsiReadAdapter();
        var envPid = Environment.GetEnvironmentVariable("CSI_PROCESS_ID");
        int? targetPid = int.TryParse(envPid, out var p) && p > 0 ? p : null;
        var connectResult = await adapter.ConnectAsync(new CsiConnectionOptions(CsiProduct.Etabs, assemblyPath, targetPid));
        Assert.True(connectResult.Connected);

        var scan = await adapter.RefreshSelectionAsync();
        var envFixture = Environment.GetEnvironmentVariable("CSI_BENCHMARK_FIXTURE_PATH");
        var fixturePath = !string.IsNullOrWhiteSpace(envFixture) && File.Exists(envFixture)
            ? envFixture
            : Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/etabs-roof-independent-benchmark.json");
        if (!File.Exists(fixturePath)) fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "fixtures/etabs-roof-independent-benchmark.json");

        var envResultOutput = Environment.GetEnvironmentVariable("CSI_RESULT_OUTPUT_PATH");
        var resultOutputPath = !string.IsNullOrWhiteSpace(envResultOutput)
            ? envResultOutput
            : Path.Combine(Directory.GetCurrentDirectory(), "artifacts/physical-qualification-result.json");

        var result = BenchmarkExecutionPipeline.Execute(scan, fixturePath, targetPid);

        try
        {
            var dir = Path.GetDirectoryName(resultOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(result, jsonOptions);
            File.WriteAllText(resultOutputPath, json);
        }
        catch
        {
            // Non-fatal if filesystem prevents writing result artifact
        }

        if (result.Outcome != PhysicalQualificationOutcome.Passed)
        {
            throw new InvalidOperationException($"Physical benchmark qualification did not pass. Outcome: {result.Outcome}. Summary: {result.Summary}. Issues: {string.Join("; ", result.Issues)}");
        }

        Assert.Equal(PhysicalQualificationOutcome.Passed, result.Outcome);
    }

    private sealed class MockAdapter : ICsiSessionReadAdapter
    {
        private static readonly CsiCapabilities Capabilities = new(true, true, true, true);
        private static readonly CsiModelSnapshot Model = new("mock-scan", "MOCK LIVE ADAPTER / not actual CSI", "MOCK LIVE ADAPTER", "1.0", "mock-geom", "mock-loads",
            ["MOCK_PAT"], ["MOCK_CASE"], ["MOCK_COMBO"],
            [new("MOCK-F1", "MOCK-FACADE", new(0, 0, 3), new(8, 0, 3)), new("MOCK-F2", "MOCK-FACADE", new(1, 0, 6), new(7, 0, 6))],
            [new("MOCK-A1", "MOCK-ROOF", [new(0, 0, 6), new(5, 0, 6), new(5, 4, 7), new(0, 4, 7)], new(0, 0, 1), default)],
            []);

        public Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options, System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new CsiConnectionResult(true, "MOCK LIVE ADAPTER / NOT actual CSI", "1.0", Capabilities, []));

        public Task<CsiReadScan> RefreshSelectionAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(new CsiReadScan(Model, new("N_m_C", 1, 1, "MOCK units"), true, Capabilities, [], [], []));

        public Task<CsiModelSnapshot> ScanModelAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Model);
        public Task<System.Collections.Immutable.ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Model.ExistingLoads);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
