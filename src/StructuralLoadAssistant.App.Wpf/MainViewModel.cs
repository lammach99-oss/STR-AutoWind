using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StructuralLoadAssistant.Csi.Abstractions;
using Microsoft.Extensions.Logging;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.App.Wpf;

public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    public enum WorkspaceMode { Demo, RealReadOnly }
    private WorkspaceMode mode;
    private readonly CsiInspectionSession inspection;
    private FacadeMethod realMethod;
    private int modeRevision;
    public WorkspaceMode Mode
    {
        get => mode;
        set
        {
            if (mode == value) return;
            mode = value; modeRevision++; inspection.Clear(); Session.LoadDemoFixture(Edition); SelectedShell = IsDemo ? "R1" : "";
            SelectedZone = IsDemo ? "Edge" : "Unmapped"; SelectedPattern = IsDemo ? "WX_POS_IP_POS" : "";
            ReportText = IsDemo ? "Preview the current synthetic calculation." : EngineeringBlock; SelectedReport = null;
            Issues = []; ProposedLoads = []; FacadePairRows = []; LowerFrame = ""; UpperFrame = "";
            Status = IsDemo ? "Demo restored. Synthetic fixtures; simulation only." : "Real CSI Read-Only. Choose installed API and attach to an already running application.";
            Refresh();
        }
    }
    public bool IsDemo => Mode == WorkspaceMode.Demo;
    public bool IsReal => !IsDemo;
    public bool IsMockAdapter => !IsDemo && (inspection.Connection?.Product.StartsWith("MOCK", StringComparison.OrdinalIgnoreCase) == true || inspection.Scan?.Model.Product.StartsWith("MOCK", StringComparison.OrdinalIgnoreCase) == true);
    public string ModeBanner => IsDemo ? "DEMO / FAKE CSI • SYNTHETIC PRESSURES • NOT FOR ENGINEERING DESIGN" : (IsMockAdapter ? "MOCK LIVE ADAPTER / NOT ACTUAL CSI • READ-ONLY • APPROVAL / APPLY DISABLED" : "REAL CSI READ-ONLY • NO PRESSURE DATASET • APPROVAL / APPLY DISABLED");
    public CsiProduct Product { get; set; } = CsiProduct.Etabs;
    public string AssemblyPath { get; set; } = "";
    public string LowerFrame { get; set; } = "";
    public string UpperFrame { get; set; } = "";
    public string FacadeGroup { get; set; } = "Reviewed facade group";
    public object[] FacadePairRows { get; private set; } = [];
    public string[] FrameIds => (IsDemo ? Session.Model.Frames : inspection.Scan?.Model.Frames ?? []).Select(f => f.Id).ToArray();
    public object[] FrameRows => (IsDemo ? Session.Model.Frames : inspection.Facade?.Frames ?? []).Select(f => (object)new { f.Id, f.FacadeId, StartX = f.Start.X, StartY = f.Start.Y, StartZ = f.Start.Z, EndX = f.End.X, EndY = f.End.Y, EndZ = f.End.Z, LocalAxes = "Not read / not inferred; global geometry only" }).ToArray();
    private static string XYZ(Vec3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:G6}, {v.Y:G6}, {v.Z:G6})");
    public object[] ShellRows => (IsDemo ? Session.Model.Shells : inspection.Scan?.Model.Shells ?? []).Select(s => (object)new { s.Id, s.RoofPlaneId, VerticesXYZ = string.Join("; ", s.Vertices.Select(XYZ)), Local3X = s.Local3.Length > .5 ? (double?)s.Local3.X : null, Local3Y = s.Local3.Length > .5 ? (double?)s.Local3.Y : null, Local3Z = s.Local3.Length > .5 ? (double?)s.Local3.Z : null, AxisStatus = s.Local3.Length > .5 ? "Read" : "Unknown / unavailable", Normal = IsDemo ? XYZ(s.OutwardNormal) : inspection.Scan?.AreaMetadata.FirstOrDefault(a => a.AreaId == s.Id)?.NormalStatus ?? "Unknown" }).ToArray();
    public string[] Patterns => (IsDemo ? Session.Model.Patterns : inspection.Scan?.Model.Patterns ?? []).ToArray();
    public string[] ZoneIds => IsDemo ? Session.Zones.Select(z => z.Id).Distinct().ToArray() : ["Unmapped"];
    public string RoofLegend => IsDemo ? "Green = Edge; blue = Interior; amber = crossing/unmapped. Click a polygon to inspect. Pressure and Local 3 are independent quantities." : "Amber = Unmapped pressure. Click a selected CSI polygon for XYZ, Local 3 and unknown outward-normal metadata. No synthetic pressures are used.";
    public string[] Cases => (IsDemo ? Session.Model.Cases : inspection.Scan?.Model.Cases ?? []).ToArray();
    public string[] Combinations => (IsDemo ? Session.Model.Combinations : inspection.Scan?.Model.Combinations ?? []).ToArray();
    public object[] Stories => inspection.Scan?.Stories.Cast<object>().ToArray() ?? [];
    public object[] ExistingAssignments => IsDemo ? Session.Model.ExistingLoads.Cast<object>().ToArray() : inspection.Scan?.Model.ExistingLoads.Cast<object>().ToArray() ?? [];
    public object[] SelectedShellAssignments => ExistingAssignments.OfType<ExistingLoad>().Where(l => l.ObjectId == SelectedShell).Cast<object>().ToArray();
    public object[] ReadIssues => inspection.Issues.Cast<object>().ToArray();
    public object[] RoofReviewRows => IsDemo ? Session.Roof.Mappings.Cast<object>().ToArray() : inspection.RoofRows.Cast<object>().ToArray();
    public object[] Local3Reviews => inspection.Local3Reviews.Cast<object>().ToArray();
    public object[] FacadeGroupReviews => inspection.Facade?.GroupOverrides.Cast<object>().ToArray() ?? [];
    public string EngineeringBlock => "Real geometry has no verified normative pressure dataset. Pressure calculation, load proposals, engineering approval and all assignment operations are disabled. Local 3 sign is review metadata only.";
    public DemoSession Session { get; private set; }
    private readonly ILogger<MainViewModel> logger;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Refreshed;
    public string Status { get; private set; } = "Ready. Synthetic data; simulation only.";
    public string DatabasePath { get; set; }
    public string Edition { get; set; } = "ASCE/SEI 7-22";
    public string[] Editions { get; } = ["ASCE/SEI 7-22", "AS/NZS 1170.2:2021"];
    public WindDirection Direction { get; set; } = WindDirection.PositiveX;
    public string InternalPressure { get; set; } = "IP_POS";
    public string RoofCase { get; set; } = "RoofCase1";
    public string User { get; set; } = Environment.UserName;
    public string Reason { get; set; } = "Review synthetic roof mapping";
    public bool AcknowledgeMismatch { get; set; }
    public string SelectedZone { get; set; } = "Edge";
    public string SelectedPattern { get; set; } = "WX_POS_IP_POS";
    public ManualLocal3Direction SelectedSign { get; set; } = ManualLocal3Direction.Unacknowledged;
    public string SelectedShell { get; private set; } = "R1";
    public FacadeMethod Method { get => IsDemo ? Session.Method : realMethod; set { if (!IsDemo) { realMethod = value; Refresh(); } else if (value != Session.Method) Run(() => Session.RecalculateFacade(value)); } }
    public WallType[] WallTypes { get; } = WallLoads.DemoLibrary.ToArray();
    public WallType SelectedWall { get; set; } = WallLoads.DemoLibrary[0];
    public string WallHeight { get; set; } = "2.8";
    public string WallOverride { get; set; } = "";
    public bool IncludeFacade { get; set; } = true;
    public bool IncludeRoof { get; set; } = true;
    public bool IncludeWall { get; set; } = true;
    public string ReportText { get; private set; } = "Preview the current calculation, mapping and assignment.";
    public string? SelectedReport { get; set; }
    public IReadOnlyList<string> ReportIds { get; private set; } = [];
    public object[] Issues { get; private set; } = [];
    public object[] ProposedLoads { get; private set; } = [];
    public string ProjectSummary => IsDemo ? $"{Session.Project.Name}\nID: {Session.Project.Id}  |  Units: {Session.Project.UnitSystem}\nSQLite: {DatabasePath}\nData: SyntheticDemoFixture" : "Real CSI inspection — read-only session; no engineering run created.";
    public string ManifestSummary => IsDemo ? $"{Session.Manifest.Edition} | Status: {Session.Manifest.Status}\n{string.Join("\n", Session.Manifest.Revisions)}\nMissing: normative velocity / terrain tables, aerodynamic coefficients, reference heights, reviewed regression fixtures.\n{Session.Calculation.State.Id}" : EngineeringBlock;
    public string CsiSummary => IsDemo ? $"{Session.Model.Product} | {Session.Model.Version}\nModel: {Session.Model.ModelId}\nFrames: {Session.Model.Frames.Length} | Roof shells: {Session.Model.Shells.Length}\nDemo fixture; fake adapter." : inspection.Connection is null ? "Disconnected. Attach only to an existing ETABS / SAP2000 process. No application start or model open." : $"{(IsMockAdapter ? "[MOCK CONTRACT FIXTURE - NOT ACTUAL CSI DATA]\n" : "")}{inspection.Connection.Product} | {inspection.Connection.Version} | Connected: {inspection.Connection.Connected}\nModel: {inspection.Scan?.Model.ModelId ?? "not refreshed"}\nNative units: {inspection.Scan?.Units.Description ?? "unknown"}; SI length multiplier: {inspection.Scan?.Units.LengthToM}; SI force multiplier: {inspection.Scan?.Units.ForceToN}\nLocked: {inspection.Scan?.IsLocked.ToString() ?? "unknown"}\nCapabilities: {inspection.Scan?.Capabilities ?? inspection.Connection.Capabilities}\nSelected frames: {inspection.Scan?.Model.Frames.Length ?? 0}; selected shells: {inspection.Scan?.Model.Shells.Length ?? 0}; issues: {inspection.Issues.Length}";
    public string RoofSummary => IsDemo ? $"Workflow: {Session.Roof.State} | Approval: {Session.Roof.Approval?.Id ?? "none"}\nAssignment: {Session.Assignment?.Id ?? "none"} | Readback: {Session.Readback?.Matches.ToString() ?? "not performed"}" : $"Read-only selected shell geometry. Pressure mapping: Unmapped. Approval: unavailable.\n{EngineeringBlock}";
    public string FacadeSummary => IsDemo ? $"Preview: {(Session.FacadePreview is null ? "not prepared" : "prepared")} | Approval: {(Session.FacadeApproval is null ? "none" : Session.FacadeApproval.User)}\nSimulated assignment: {Session.FacadeAssignment?.Id ?? "none"} | Readback: {Session.FacadeReadback?.Matches.ToString() ?? "not performed"}" : $"Explicit selected frame pair geometry only. Method: {Method}.\n{EngineeringBlock}";
    public string ShellSummary
    {
        get
        {
            if (!IsDemo)
            {
                var live = inspection.Scan?.Model.Shells.FirstOrDefault(s => s.Id == SelectedShell);
                if (live is null) return "Select a CSI area in the running model, refresh selection, then click its polygon.";
                var metadata = inspection.Scan?.AreaMetadata.FirstOrDefault(a => a.AreaId == live.Id);
                var review = inspection.RoofRows.FirstOrDefault(r => r.ShellId == live.Id);
                return $"{live.Id} | Plane: {live.RoofPlaneId}\nPressure mapping: Unmapped; p_net: unavailable\nLocal 3 XYZ: {(live.Local3.Length > .5 ? XYZ(live.Local3) : "Unknown / unavailable")}\nOutward normal: {metadata?.NormalStatus ?? "Unknown"}\nVerified outward normal: {(metadata?.VerifiedOutwardNormal is Vec3 normal ? XYZ(normal) : "Unknown")}\nManual Local 3 review: {review?.ManualDirection}; no load assigned\nVertices XYZ [m]: {string.Join("; ", live.Vertices.Select(XYZ))}";
            }
            var shell = Session.Model.Shells.First(s => s.Id == SelectedShell);
            var mapping = Session.Roof.Mappings.FirstOrDefault(m => m.ShellId == SelectedShell);
            var zone = Session.Zones.FirstOrDefault(z => z.Id == mapping?.ZoneId);
            var sign = mapping?.ManualDirection;
            var signed = sign == ManualLocal3Direction.Unacknowledged ? "unacknowledged" : ((sign == ManualLocal3Direction.Positive ? 1 : -1) * Math.Abs(zone?.NetPressurePa ?? 0)).ToString("G6", CultureInfo.InvariantCulture);
            return $"{shell.Id} | {mapping?.Classification}\nZone: {mapping?.ZoneId ?? "unmapped"}\np_net: {zone?.NetPressurePa.ToString(CultureInfo.InvariantCulture) ?? "unmapped"} Pa (positive inward)\nSigned CSI Local 3: {signed} Pa\nLocal 3: {shell.Local3}\nOutward: {shell.OutwardNormal}";
        }
    }
    public MainViewModel(DemoSession session, ILogger<MainViewModel> logger, CsiInspectionSession inspection)
    {
        this.inspection = inspection;
        this.logger = logger;
        Session = session; DatabasePath = session.DatabasePath;
        Session.LoadDemoFixture(); Refresh();
    }
    public ICommand Command(Action action) => new RelayCommand(() => Run(action));
    public void Run(Action action)
    {
        try { action(); Status = IsDemo ? "Completed. Simulation only; real CSI writes disabled." : "Completed read-only inspection. No engineering loads or approvals."; }
        catch (Exception e) { logger.LogError(e, "Desktop action failed"); Status = e.Message; }
        Refresh();
    }
    public async Task RunAsync(Func<Task> action)
    {
        try { await action(); Status = IsDemo ? "Completed. Simulation only; real CSI writes disabled." : "Completed read-only inspection. No engineering loads or approvals."; }
        catch (Exception e) { logger.LogError(e, "Desktop async action failed"); Status = e.Message; }
        Refresh();
    }
    public void Refresh()
    {
        ReportIds = IsDemo ? Session.SavedReports() : [];
        if (IsDemo) FacadePairRows = Session.FacadePairs.Cast<object>().ToArray();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); Refreshed?.Invoke();
    }
    public void SelectShell(string shell)
    {
        SelectedShell = shell;
        if (!IsDemo) { SelectedSign = inspection.RoofRows.FirstOrDefault(r => r.ShellId == shell)?.ManualDirection ?? ManualLocal3Direction.Unacknowledged; Refresh(); return; }
        var mapping = Session.Roof.Mappings.First(m => m.ShellId == shell);
        SelectedZone = mapping.ZoneId ?? Session.Zones[0].Id; SelectedSign = mapping.ManualDirection;
        AcknowledgeMismatch = mapping.MismatchAcknowledged; Refresh();
    }
    public IReadOnlyList<ShellGeometry> DisplayShells => (IsDemo ? Session.Model.Shells : inspection.Scan?.Model.Shells ?? []).ToArray();
    public async Task ConnectReal()
    {
        if (IsDemo) throw new InvalidOperationException("Choose Real Read-Only mode first.");
        var revision = modeRevision;
        await inspection.ConnectAsync(new(Product, AssemblyPath));
        if (revision != modeRevision) return;
        if (inspection.Connection?.Connected != true) throw new InvalidOperationException("CSI attach failed. " + string.Join("; ", inspection.Issues.Select(i => i.Message)));
        await RefreshReal();
        Refresh();
    }
    public async Task RefreshReal()
    {
        if (IsDemo) throw new InvalidOperationException("Choose Real Read-Only mode first.");
        var revision = modeRevision;
        await inspection.RefreshAsync();
        if (revision != modeRevision) return;
        SelectedShell = inspection.Scan?.Model.Shells.FirstOrDefault()?.Id ?? "";
        SelectedSign = ManualLocal3Direction.Unacknowledged; SelectedZone = "Unmapped";
        ProposedLoads = []; Issues = []; FacadePairRows = []; LowerFrame = ""; UpperFrame = "";
        Status = IsMockAdapter
            ? "MOCK FIXTURE SCAN • Contract-test geometry only • NOT actual CSI scan"
            : inspection.Outcome switch
            {
                CsiScanOutcome.Succeeded => $"LIVE CSI SCAN SUCCEEDED • {inspection.Connection?.Product} {inspection.Connection?.Version} • {inspection.Scan?.Model.Frames.Length ?? 0} frames, {inspection.Scan?.Model.Shells.Length ?? 0} shells selected",
                CsiScanOutcome.EmptySelection => $"EMPTY CSI SELECTION • {inspection.Connection?.Product} {inspection.Connection?.Version} • 0 frames, 0 shells selected • Select elements in CSI and refresh",
                CsiScanOutcome.PartialScan => $"PARTIAL CSI SCAN • {inspection.Connection?.Product} {inspection.Connection?.Version} • {inspection.Scan?.Model.Frames.Length ?? 0} frames, {inspection.Scan?.Model.Shells.Length ?? 0} shells selected • Issues: {inspection.Issues.Length}",
                CsiScanOutcome.Failed => $"CSI SCAN FAILED • {inspection.Connection?.Product} {inspection.Connection?.Version} • {string.Join("; ", inspection.Issues.Select(i => i.Message))}",
                CsiScanOutcome.Scanning => $"SCANNING CSI MODEL • {inspection.Connection?.Product} {inspection.Connection?.Version}...",
                _ => "DISCONNECTED • Connect to a running CSI instance first"
            };
        Refresh();
    }
    public void ReviewLocal3()
    {
        if (IsDemo) { EditSign(); return; }
        inspection.SetLocal3Review(SelectedShell, SelectedSign, User, Reason); Refresh();
    }
    public void SelectFacadePair()
    {
        if (IsDemo) { Session.SelectFacadePair(LowerFrame, UpperFrame); FacadePairRows = Session.FacadePairs.Cast<object>().ToArray(); Refresh(); return; }
        var selector = inspection.Facade ?? throw new InvalidOperationException("Refresh selected CSI frames first.");
        selector.SelectPair(LowerFrame, UpperFrame);
        FacadePairRows = selector.Pairs.Cast<object>().ToArray(); Refresh();
    }
    public void ClearFacadePairs()
    {
        if (IsDemo) Session.ClearFacadePairs(); else inspection.Facade?.ClearPairs();
        FacadePairRows = []; Refresh();
    }
    public void ReviewFacadeGroup()
    {
        if (IsDemo) throw new InvalidOperationException("Demo frames already share their fixture facade group.");
        var selector = inspection.Facade ?? throw new InvalidOperationException("Refresh selected CSI frames first.");
        if (string.IsNullOrWhiteSpace(FacadeGroup) || string.IsNullOrWhiteSpace(User) || string.IsNullOrWhiteSpace(Reason) || LowerFrame == UpperFrame || !selector.Frames.Any(f => f.Id == LowerFrame) || !selector.Frames.Any(f => f.Id == UpperFrame))
            throw new ArgumentException("Choose two selected frames, a physical facade group, reviewer and reason before editing groups.");
        selector.OverrideGroup(LowerFrame, FacadeGroup, User, Reason); selector.OverrideGroup(UpperFrame, FacadeGroup, User, Reason);
        FacadePairRows = selector.Pairs.Cast<object>().ToArray(); Refresh();
    }
    private void RequireDemo()
    {
        if (!IsDemo) throw new InvalidOperationException("Real CSI Read-Only blocks all demo calculations, approvals and assignments. " + EngineeringBlock);
    }
    public void OpenProject() { RequireDemo(); var opened = new DemoSession(DatabasePath); opened.LoadDemoFixture(Edition); Session = opened; SelectedShell = "R1"; }
    public void LoadFixture(bool crossing = true) { RequireDemo(); Session.LoadDemoFixture(Edition, crossing); SelectedShell = "R1"; Issues = []; ProposedLoads = []; }
    public void LoadSelectedState() { RequireDemo(); Session.LoadDemoFixture(Edition, true, new(Direction, InternalPressure, RoofCase, LimitState.Uls)); SelectedShell = "R1"; Issues = []; ProposedLoads = []; }
    public void EditZone() { RequireDemo(); Session.SetZone(SelectedShell, SelectedZone, User, Reason); }
    public void EditSign() { RequireDemo(); Session.SetSign(SelectedShell, SelectedSign, AcknowledgeMismatch, User, Reason); }
    public void Pattern() { RequireDemo(); Session.SetPattern(SelectedPattern); }
    public void Exclude() { RequireDemo(); Session.ExcludeShell(SelectedShell, User, Reason); }
    public void Validate() { RequireDemo(); Issues = Session.ValidateRoof().Cast<object>().ToArray(); }
    public void Approve() { RequireDemo(); Session.ApproveRoof(User); }
    public void Preview() { RequireDemo(); ProposedLoads = Session.PreviewAssignment().ProposedLoads.Cast<object>().ToArray(); }
    public Task Apply() { RequireDemo(); return RunAsync(async () => { await Session.ApplyAsync(); }); }
    public Task Verify() { RequireDemo(); return RunAsync(async () => { await Session.VerifyAsync(); }); }
    public Task PreviewFacade() { RequireDemo(); return RunAsync(async () => { await Session.PreviewFacadeAsync(); }); }
    public Task ApproveFacade() { RequireDemo(); return RunAsync(async () => { await Session.ApproveFacadeAsync(User); }); }
    public Task ApplyFacade() { RequireDemo(); return RunAsync(async () => { await Session.ApplyFacadeAsync(); }); }
    public Task VerifyFacade() { RequireDemo(); return RunAsync(async () => { await Session.VerifyFacadeAsync(); }); }
    public void PreviewWall() { RequireDemo(); Session.PreviewWall(SelectedWall, double.Parse(WallHeight, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(WallOverride) ? null : double.Parse(WallOverride, CultureInfo.InvariantCulture)); }
    private ReportDefinition Definition() => new("desktop-report", IncludeFacade, IncludeRoof, IncludeWall);
    public void PreviewReport() { RequireDemo(); ReportText = new MarkdownReportExporter().Export(Session.BuildReport(Definition())); }
    public void SaveReport() { RequireDemo(); SelectedReport = Session.SaveReport(Definition()); PreviewReport(); }
    public void OpenReport() { RequireDemo(); if (SelectedReport is null) throw new InvalidOperationException("Select a stored report ID."); ReportText = new MarkdownReportExporter().Export(Session.OpenReport(SelectedReport)); }
    public void ExportReport(string path) { RequireDemo(); Session.ExportReport(path, Definition()); }
    public async Task SmokeAsync()
    {
        RequireDemo();
        LoadFixture(false);
        var equalMiddle = Session.FacadeRows.Where(r => r.Frame == "B2").Sum(r => r.LineLoadNPerM);
        Session.RecalculateFacade(FacadeMethod.StaticallyEquivalent);
        var staticMiddle = Session.FacadeRows.Where(r => r.Frame == "B2").Sum(r => r.LineLoadNPerM);
        if (Math.Abs(equalMiddle - 4650) > 1e-6 || Math.Abs(staticMiddle - 4700) > 1e-6) throw new InvalidOperationException("Facade method change did not preserve expected fixture accumulation.");
        foreach (var shell in Session.Model.Shells)
        {
            var map = Session.Roof.Mappings.Single(m => m.ShellId == shell.Id);
            var zone = Session.Zones.Single(z => z.Id == map.ZoneId);
            var signed = RoofGeometry.Local3Load(zone.NetPressurePa, shell.OutwardNormal, shell.Local3);
            Session.SetSign(shell.Id, signed >= 0 ? ManualLocal3Direction.Positive : ManualLocal3Direction.Negative, false, User, "Automated synthetic desktop smoke");
        }
        Pattern(); var issues = Session.ValidateRoof();
        if (issues.Any(i => i.Blocking)) throw new InvalidOperationException("Smoke mapping blocked: " + string.Join(", ", issues.Select(i => i.Code)));
        Approve();
        Session.SetSign("R1", ManualLocal3Direction.Positive, false, User, "Smoke checks edit invalidates immutable approval");
        if (Session.Roof.Approval is not null) throw new InvalidOperationException("Mapping edit did not invalidate approval.");
        Validate(); Approve(); Preview(); await Session.ApplyAsync(); var readback = await Session.VerifyAsync();
        if (!readback.Matches) throw new InvalidOperationException("Smoke readback differs.");
        await Session.PreviewFacadeAsync(); await Session.ApproveFacadeAsync(User); await Session.ApplyFacadeAsync();
        if (!(await Session.VerifyFacadeAsync()).Matches) throw new InvalidOperationException("Facade smoke readback differs.");
        SaveReport(); SelectShell("R1"); Refresh();
    }
    public async Task SmokeReadOnlyAsync()
    {
        await SmokeAsync();
        Mode = WorkspaceMode.RealReadOnly;
        if (Session.Roof.Approval is not null || Session.FacadeApproval is not null || Session.Assignment is not null || SelectedReport is not null) throw new InvalidOperationException("Mode switch retained an approved demo operation or report selection.");
        await ConnectReal();
        if (inspection.Connection?.Connected != true || DisplayShells.Count == 0 || FrameIds.Length == 0) throw new InvalidOperationException("Mock read-only connection did not populate selected geometry.");
        if (inspection.RoofRows.Any(r => r.ZoneId is not null) || ProposedLoads.Length != 0) throw new InvalidOperationException("Real read-only mode must leave pressure unmapped.");
          if (IsMockAdapter && (Status.Contains("LIVE CSI SCAN SUCCESSFUL", StringComparison.Ordinal) || Status.Contains("LIVE CSI SCAN SUCCEEDED", StringComparison.Ordinal))) throw new InvalidOperationException("A mock adapter was reported as a live CSI scan.");
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Real CSI Read-Only blocks", StringComparison.Ordinal)) { return; }
            throw new InvalidOperationException("A real-mode demo action was not rejected.");
        }
        Reject(Approve); Reject(() => _ = Apply()); Reject(() => _ = ApproveFacade()); Reject(() => _ = ApplyFacade()); Reject(Preview); Reject(() => _ = PreviewFacade()); Reject(PreviewWall); Reject(() => LoadFixture()); Reject(PreviewReport);
        if (Session.Assignment is not null || Session.FacadeAssignment is not null) throw new InvalidOperationException("Read-only smoke changed hidden demo assignments.");
        LowerFrame = FrameIds[0]; UpperFrame = FrameIds[1]; SelectFacadePair();
        SelectedSign = ManualLocal3Direction.Negative; Reason = "MOCK smoke sign review only"; ReviewLocal3();
        if (!ShellSummary.Contains("Unmapped", StringComparison.Ordinal) || ShellSummary.Contains("1200", StringComparison.Ordinal)) throw new InvalidOperationException("Real shell summary leaked synthetic pressure.");
        Mode = WorkspaceMode.Demo;
        if (inspection.Scan is not null || inspection.Connection is not null || ProposedLoads.Length != 0) throw new InvalidOperationException("Mode switch retained real context.");
        Mode = WorkspaceMode.RealReadOnly; await ConnectReal();
        LowerFrame = FrameIds[0]; UpperFrame = FrameIds[1]; SelectFacadePair();
        SelectedSign = ManualLocal3Direction.Positive; Reason = "MOCK sign review; no generated load"; ReviewLocal3();
        Status = "MOCK LIVE ADAPTER / contract-test geometry • read-only UI and guard assertions passed • not actual CSI"; Refresh();
    }
}
