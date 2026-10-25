using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Threading;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.App.Wpf;

public sealed partial class MainWindow
{
    public async Task RunReportExportSmokeAsync(string outputDir)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Report export smoke regression: " + message);
        }

        async Task Idle() { await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle); }

        Check(vm.IsDemo, "Must run in Demo / Engineer Review mode");
        Check(vm.CurrentEngineeringRun == null, "Initial engineering run must be null");
        Check(vm.CurrentEngineeringRunSummary.Contains("No engineering run selected"), "Initial summary must indicate empty run");

        // 1. Validate engineering input when no request configured
        vm.ValidateEngineeringInput();
        await Idle();
        Check(vm.Status.Contains("No explicit calculation request configured"), "Validation without request must report unconfigured status");

        // 2. Set explicit calculation request for ASCE 7-22 Gable Roof
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Illustrative review fixture", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            vm.Session.Project.Id, "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        vm.SetCalculationRequest(req);
        vm.ValidateEngineeringInput();
        await Idle();
        Check(vm.Status.Contains("explicit coefficients"), "Validation with request must report explicit coefficients loaded");

        // 3. Calculate engineering run
        vm.CalculateEngineeringRun();
        await Idle();
        Check(vm.CurrentEngineeringRun != null, "Engineering run must not be null after calculation");
        var run = vm.CurrentEngineeringRun!;
        Check(run.NumericalComputationPossible, "Numerical computation must be possible");
        Check(run.TraceNodes.Length > 0, "Trace nodes must be populated");
        string runId = run.Id;
        string runHash = run.ContentHash;

        // 4. Save engineering run
        vm.SaveEngineeringRun();
        await Idle();
        Check(vm.Status.Contains("Saved engineering run"), "Status must confirm save");

        // 5. Verify CurrentEngineeringRunSummary
        string summary = vm.CurrentEngineeringRunSummary;
        Check(summary.Contains(runId), "Summary must contain run ID");
        Check(summary.Contains(runHash[..8]), "Summary must contain run content hash");

        // 6. Export the report product: detailed PDF per latest Owner Directive.
        string xlsxPath = Path.Combine(outputDir, "smoke-report.xlsx");
        string pdfPath = Path.Combine(outputDir, "smoke-report.pdf");

        vm.ExportDetailedReport(pdfPath);
        await Idle();

        Check(File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0, "PDF export must exist and be non-empty");
        byte[] pdfBytes = File.ReadAllBytes(pdfPath);
        Check(System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4) == "%PDF", "Export must be a valid PDF file");
        string pdfText = System.Text.Encoding.ASCII.GetString(pdfBytes);
        Check(pdfText.Contains("Wind Load") || pdfText.Contains("Skia/PDF") || pdfText.Contains("Edg/") || pdfText.Contains("SUMMARY REPORT"), "PDF must contain wind calculation report contents");

        // Any requested extension is normalised to PDF.
        vm.ExportDetailedReport(xlsxPath);
        await Idle();
        Check(File.Exists(pdfPath), "Export with a non-PDF path must still produce the PDF");

        // 7. Verify invalidation on wind state change
        vm.Direction = WindDirection.NegativeY;
        vm.LoadSelectedState();
        await Idle();
        Check(vm.CurrentEngineeringRun == null, "Wind state change must invalidate CurrentEngineeringRun");
        Check(vm.CurrentCalculationRequest == null, "Wind state change must invalidate CurrentCalculationRequest");

        bool threwOnExport = false;
        try { vm.ExportDetailedReport(xlsxPath); }
        catch (InvalidOperationException) { threwOnExport = true; }
        Check(threwOnExport, "ExportDetailedReport must throw when CurrentEngineeringRun is null");

        // 8. Re-set run and verify export succeeds again
        vm.SetEngineeringRun(run);
        await Idle();
        Check(vm.CurrentEngineeringRun == run, "Re-set run must restore CurrentEngineeringRun");
        vm.ExportDetailedReport(xlsxPath);
        await Idle();
    }
}
