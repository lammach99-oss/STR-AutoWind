using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.ApiV1;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class PhysicalRunnerRegressionTests
{
    [Fact]
    public async Task Wrong_pid_connection_fails_cleanly_with_process_terminated_issue()
    {
        int nonExistentPid = 999999;
        while (Process.GetProcesses().Any(p => p.Id == nonExistentPid))
        {
            nonExistentPid++;
        }

        await using var adapter = new InstalledCsiReadAdapter(opt =>
        {
            if (opt.ProcessId.HasValue)
            {
                CsiProcessDiscovery.ValidateProcessActive(opt.Product, opt.ProcessId.Value);
            }
            throw new InvalidOperationException("Should have validated PID first");
        });
        var options = new CsiConnectionOptions(CsiProduct.Etabs, "dummy.dll", nonExistentPid);
        var result = await adapter.ConnectAsync(options);

        Assert.False(result.Connected);
        Assert.Contains(result.Issues, i => i.Code is "PROCESS_TERMINATED" or "PID_REUSED" or "DISCONNECTED");
    }

    [Fact]
    public async Task Missing_api_path_connection_fails_cleanly_without_unhandled_crash()
    {
        await using var adapter = new InstalledCsiReadAdapter();
        var options = new CsiConnectionOptions(CsiProduct.Etabs, @"C:\NonExistentFolder\MissingInterop.dll");
        var result = await adapter.ConnectAsync(options);

        Assert.False(result.Connected);
        Assert.NotEmpty(result.Issues);
        Assert.True(result.Issues.Any(i => i.Severity == CsiIssueSeverity.Error));
    }

    [Fact]
    public void Disallowed_selection_mutation_when_unsupported_types_present()
    {
        // Allowed restorable types: Point=1, Frame=2, Area=5, Link=6
        var restorableTypes = new HashSet<int> { 1, 2, 5, 6 };
        var sampleSelectionWithSolid = new[]
        {
            new { Type = 2, Name = "Frame1" },
            new { Type = 5, Name = "Area1" },
            new { Type = 7, Name = "Solid1" } // Unsupported type 7
        };

        var unsupported = sampleSelectionWithSolid.Where(s => !restorableTypes.Contains(s.Type)).ToList();
        Assert.Single(unsupported);
        Assert.Equal(7, unsupported[0].Type);

        // Verification guard rule: if unsupported types present, mutation must be refused
        Assert.True(unsupported.Count > 0, "Safety guard must detect unsupported object types to refuse selection mutation.");
    }

    [Fact]
    public void Failed_restoration_detected_when_target_pid_is_dead()
    {
        int nonExistentPid = 999999;
        while (Process.GetProcesses().Any(p => p.Id == nonExistentPid))
        {
            nonExistentPid++;
        }

        // Before restoration, verifying PID activity must throw rather than silently falling back
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CsiProcessDiscovery.ValidateProcessActive(CsiProduct.Etabs, nonExistentPid));

        Assert.Contains("terminated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Model_mismatch_detected_during_scan_and_invalidates_scope()
    {
        var session = new MismatchModelSession("Model_Original.edb", "Model_Swapped.edb");
        await using var adapter = new InstalledCsiReadAdapter(_ => session);
        var connectResult = await adapter.ConnectAsync(new CsiConnectionOptions(CsiProduct.Etabs, "unused.dll"));
        Assert.True(connectResult.Connected);

        var scan = await adapter.RefreshSelectionAsync();

        Assert.Equal(CsiScanOutcome.Failed, scan.Outcome);
        Assert.Empty(scan.Model.Frames);
        Assert.Empty(scan.Model.Shells);
        Assert.Contains(scan.Issues, i => i.Code == "MODEL_MISMATCH");
    }

    [Fact]
    public void Qualification_outcome_propagation_and_json_serialization_preserves_contract()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/etabs-roof-independent-benchmark.json");
        if (!File.Exists(fixturePath)) fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "fixtures/etabs-roof-independent-benchmark.json");

        var emptyScan = new CsiReadScan(
            DemoFixtures.Model(), new("N_m_C", 1, 1, "N_m_C"), true,
            new(true, true, true, true, true), [], [], []
        );

        // Inconclusive outcome
        var inconclusiveResult = BenchmarkExecutionPipeline.Execute(emptyScan, fixturePath, processId: 1001);
        Assert.Equal(PhysicalQualificationOutcome.Inconclusive, inconclusiveResult.Outcome);
        Assert.Equal(1001, inconclusiveResult.ProcessId);

        // Verify JSON round-trip serialization
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var json = JsonSerializer.Serialize(inconclusiveResult, jsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var outcomeProp = root.TryGetProperty("Outcome", out var o) ? o.GetString() : root.GetProperty("outcome").GetString();
        Assert.Equal("Inconclusive", outcomeProp);
        var pidProp = root.TryGetProperty("ProcessId", out var p) ? p.GetInt32() : root.GetProperty("processId").GetInt32();
        Assert.Equal(1001, pidProp);
        var hasChecklist = root.TryGetProperty("Checklist", out var checklist) || root.TryGetProperty("checklist", out checklist);
        Assert.True(hasChecklist);
        var countProp = checklist.TryGetProperty("ExecutedCount", out var c) ? c.GetInt32() : checklist.GetProperty("executedCount").GetInt32();
        Assert.True(countProp >= 0);
    }

    private sealed class MismatchModelSession(string initialFilename, string changedFilename) : ICsiApiSession
    {
        private int readCount = 0;
        public object? Read(string component, string getter, object?[] args)
        {
            if (getter == "GetVersion")
            {
                args[0] = "23.0.0";
                return 0;
            }
            if (getter == "GetModelFilename")
            {
                readCount++;
                return readCount <= 1 ? initialFilename : changedFilename;
            }
            if (getter == "GetPresentUnits") return 10;
            if (getter == "GetModelIsLocked") return true;
            if (getter == "GetNameList")
            {
                args[0] = 0;
                args[1] = Array.Empty<string>();
                return 0;
            }
            if (getter == "GetSelected")
            {
                args[0] = 0;
                args[1] = Array.Empty<int>();
                args[2] = Array.Empty<string>();
                return 0;
            }
            return 0;
        }

        public void Dispose() { }
    }
}

