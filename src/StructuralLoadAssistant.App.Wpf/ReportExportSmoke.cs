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
    public async Task RunAutoLookupSmokeAsync(string outputDir)
    {
        vm.Edition="AS/NZS 1170.2:2021";
        vm.InstallSyntheticAutoSmokeSource();
        vm.AutoCountry="AU";vm.AutoRegion="SYNTHETIC";
        vm.AutoLength="30";vm.AutoWidth="20";vm.AutoHeight="4";vm.AutoGround="0";vm.AutoPitch="0";
        vm.AutoReason="Synthetic smoke geometry / axes / datum";vm.AutoGeometry=true;vm.AutoTrueNorth=true;
        vm.AutoUniform=true;vm.AutoTopo=true;vm.AutoRigid=true;
        vm.AutoOpenings="TEST";vm.AutoDebris="TEST";vm.AutoArea="10";vm.AutoWallHeight="4";
        if(vm.AutoTerrain!="2"||vm.AutoUls!="500"||vm.AutoSls!="25")throw new InvalidOperationException("Guided defaults mismatch");
        await vm.CalculateAutoWindAsync();
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Ready||vm.CurrentEngineeringRun?.NumericalComputationPossible!=true)throw new InvalidOperationException("Synthetic automatic workflow failed: "+vm.CurrentReportError);
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_AUTO_SYNTHETIC_MAIN.pdf"));
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_AUTO_SYNTHETIC_AUDIT.pdf"),true);
        await File.WriteAllTextAsync(Path.Combine(outputDir,"auto-run.json"),SnapshotJson.Serialize(vm.CurrentEngineeringRun));
        var path=vm.CurrentPdfPath!;var bytes=File.ReadAllBytes(path);File.AppendAllText(path,"tampered");
        bool rejected=false;try{await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"tampered.pdf"));}catch(InvalidOperationException){rejected=true;}
        File.WriteAllBytes(path,bytes);if(!rejected)throw new InvalidOperationException("Tampered auto PDF exported");
        vm.AutoTerrain="3";
        if(vm.CurrentPdfPath!=null||vm.CurrentEngineeringRun!=null)throw new InvalidOperationException("Terrain change did not invalidate PDF");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await vm.CalculateAutoWindAsync(cancelled.Token);}catch(OperationCanceledException){}}
        if(vm.CurrentPdfPath!=null)throw new InvalidOperationException("Cancelled lookup published PDF");
        vm.AutoTerrain="2";await vm.CalculateAutoWindAsync();vm.AutoRegion="";
        if(vm.CurrentPdfPath!=null)throw new InvalidOperationException("Region change did not invalidate PDF");
        await vm.CalculateAutoWindAsync();
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Blocked)throw new InvalidOperationException("Missing region did not block");
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_AUTO_BLOCKED_MAIN.pdf"));
    }
    public async Task RunSlopedAutoLookupSmokeAsync(string outputDir)
    {
        vm.Edition="AS/NZS 1170.2:2021";vm.InstallSyntheticSlopedAutoSmokeSource();
        vm.AutoCountry="AU";vm.AutoRegion="SYNTHETIC";
        vm.AutoLength="30";vm.AutoWidth="20";vm.AutoHeight="4";vm.AutoGround="0";
        vm.AutoEave="3";vm.AutoPeak="5";vm.AutoPitch="14";vm.AutoSlopeBearing="0";
        vm.AutoReason="Synthetic sloped smoke / entered roof levels and model axes; not physical CSI evidence";
        vm.AutoGeometry=true;vm.AutoUniform=true;vm.AutoTopo=true;vm.AutoRigid=true;
        vm.AutoOpenings="TEST";vm.AutoDebris="TEST";vm.AutoArea="10";vm.AutoWallHeight="4";
        Tabs.SelectedIndex=1;UpdateLayout();
        static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
        {
            yield return root;
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
                foreach(var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))yield return child;
        }
        var controls=Descendants(this).OfType<System.Windows.FrameworkElement>().ToArray();
        var roof=controls.OfType<System.Windows.Controls.ComboBox>().Single(c=>System.Windows.Data.BindingOperations.GetBindingExpression(c,System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.ParentBinding.Path.Path=="AutoRoof");
        roof.SelectedItem="Monoslope";
        var north=controls.OfType<System.Windows.Controls.CheckBox>().Single(c=>System.Windows.Data.BindingOperations.GetBindingExpression(c,System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path=="AutoTrueNorth");
        north.IsChecked=true;
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(vm.AutoRoof!="Monoslope"||!vm.AutoTrueNorth)throw new InvalidOperationException("Real guided controls failed to update roof/north state");
        await vm.CalculateAutoWindAsync();
        var run=vm.CurrentEngineeringRun??throw new InvalidOperationException("Sloped lookup produced no diagnostic run");
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Blocked||run.NumericalComputationPossible)throw new InvalidOperationException("Incomplete monoslope source must remain blocked");
        if(!run.AsNz2021Cases.Any(c=>c.Surface=="Roof"&&c.ExternalPa.HasValue))throw new InvalidOperationException("Supported synthetic roof external results missing");
        if(run.PrimarySourceApplicabilityVerified||run.OwnerEngineerReviewed||run.IndependentEngineerReviewed)throw new InvalidOperationException("Sloped review safety gates changed");
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_AUTO_SLOPED_SYNTHETIC_MAIN.pdf"));
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_AUTO_SLOPED_SYNTHETIC_AUDIT.pdf"),true);
        await File.WriteAllTextAsync(Path.Combine(outputDir,"sloped-auto-run.json"),SnapshotJson.Serialize(run));
        north.IsChecked=false;
        await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        if(vm.CurrentPdfPath!=null||vm.CurrentEngineeringRun!=null)throw new InvalidOperationException("True-north control did not invalidate sloped PDF");
        north.IsChecked=true;await vm.CalculateAutoWindAsync();
    }
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
        await vm.CalculateEngineeringRunAsync();
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
    public async Task RunAsNzWorksheetSmokeAsync(string outputDir)
    {
        vm.Edition = "AS/NZS 1170.2:2021";
        var request = StructuralLoadAssistant.Csi.Fake.AsNzMatrixFixture.LegacyWorkbookComparison() with { ProjectId = vm.Session.Project.Id };
        vm.SetCalculationRequest(request);
        await vm.CalculateEngineeringRunAsync();
        if (vm.CurrentReportState != MainViewModel.WindReportState.Ready || vm.CurrentEngineeringRun?.AsNz2021Cases.Length != 320)
            throw new InvalidOperationException("AS/NZS worksheet calculation/render failed: " + vm.CurrentReportError);
        if (vm.CurrentPdfDocument?.CalculationRun?.ContentHash != vm.CurrentEngineeringRun.ContentHash)
            throw new InvalidOperationException("AS/NZS PDF provenance mismatch");
        File.Copy(vm.CurrentPdfPath!, Path.Combine(outputDir,"ASNZ_2021_FlatWorkbook_Comparison_Review.pdf"));
        if(vm.CurrentAuditPdfPath==null||!File.Exists(vm.CurrentAuditPdfPath))throw new InvalidOperationException("Separate audit PDF missing");
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_2021_FlatWorkbook_Review_MAIN.pdf"));
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_2021_FlatWorkbook_Review_Detailed_Audit.pdf"),audit:true);
        string failureTarget=Path.Combine(outputDir,"sidecar-failure.pdf");
        byte[] priorPdf=System.Text.Encoding.ASCII.GetBytes("%PDF-prior-valid-output");
        await File.WriteAllBytesAsync(failureTarget,priorPdf);
        Directory.CreateDirectory(failureTarget+".integrity.json");
        bool sidecarFailed=false;
        try {await vm.ExportWorksheetPdfAsync(failureTarget);}catch(IOException){sidecarFailed=true;}catch(UnauthorizedAccessException){sidecarFailed=true;}
        if(!sidecarFailed||!File.ReadAllBytes(failureTarget).SequenceEqual(priorPdf))
            throw new InvalidOperationException("Sidecar publication failure replaced the prior PDF");
        string originalPdf=vm.CurrentPdfPath!;
        byte[] originalBytes=await File.ReadAllBytesAsync(originalPdf);
        await File.AppendAllTextAsync(originalPdf,"tampered");
        bool tamperedBlocked=false;
        try { await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"tampered.pdf")); }
        catch(InvalidOperationException){tamperedBlocked=true;}
        await File.WriteAllBytesAsync(originalPdf,originalBytes);
        if(!tamperedBlocked)throw new InvalidOperationException("Changed PDF bytes exported without integrity rejection");
        vm.CurrentCalculationRequest=request with {BuildingWidthM=19};
        bool staleBlocked=false;try{await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"stale.pdf"));}catch(InvalidOperationException){staleBlocked=true;}
        if(!staleBlocked)throw new InvalidOperationException("Stale request exported a PDF");
        vm.SetCalculationRequest(request);await vm.CalculateEngineeringRunAsync();
        File.WriteAllText(Path.Combine(outputDir,"request.json"), SnapshotJson.Serialize(request));
        File.WriteAllText(Path.Combine(outputDir,"run.json"), SnapshotJson.Serialize(vm.CurrentEngineeringRun));
        vm.SetCalculationRequest(request with { BuildingLengthM = 12 });
        if(vm.CurrentPdfPath != null || vm.CurrentEngineeringRun != null) throw new InvalidOperationException("Changed request retained stale PDF");
        await vm.CalculateEngineeringRunAsync(new System.Threading.CancellationToken(true));
        if(vm.CurrentReportState != MainViewModel.WindReportState.Cancelled || vm.CurrentPdfPath != null) throw new InvalidOperationException("Cancelled calculation published PDF");
        vm.SetCalculationRequest(request);
        await vm.CalculateEngineeringRunAsync();
        await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
    }    public async Task RunAsNzBlockedSmokeAsync(string outputDir)
    {
        vm.Edition="AS/NZS 1170.2:2021";
        var req=StructuralLoadAssistant.Csi.Fake.AsNzMatrixFixture.Request(true) with {ProjectId=vm.Session.Project.Id};
        vm.SetCalculationRequest(req with {ExplicitCoefficients=req.ExplicitCoefficients.Remove("Mc")});
        await vm.CalculateEngineeringRunAsync();
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Blocked || vm.CurrentPdfPath==null)
            throw new InvalidOperationException("Blocked diagnostic must remain Blocked with available PDF; actual="+vm.CurrentReportState);
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_2021_Blocked_Diagnostic_MAIN.pdf"));
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Blocked)throw new InvalidOperationException("Diagnostic export changed blocked status");
        vm.SetCalculationRequest(req with {RoofForm="Flat",RoofSlopeDegrees=14});
        await vm.CalculateEngineeringRunAsync();
        if(vm.CurrentReportState!=MainViewModel.WindReportState.Blocked||vm.CurrentEngineeringRun!.AsNz2021Cases.Any(c=>c.NetPa.HasValue))
            throw new InvalidOperationException("Unsupported roof method published calculated pressures or Ready state");
        await vm.ExportWorksheetPdfAsync(Path.Combine(outputDir,"ASNZ_2021_Unsupported_Geometry.pdf"));
    }}
