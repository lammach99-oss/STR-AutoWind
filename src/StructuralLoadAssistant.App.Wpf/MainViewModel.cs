using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
    public FacadeMethod Method { get => Session.Method; set { if (value != Session.Method) Run(() => Session.RecalculateFacade(value)); } }
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
    public string ProjectSummary => $"{Session.Project.Name}\nID: {Session.Project.Id}  |  Units: {Session.Project.UnitSystem}\nSQLite: {DatabasePath}\nData: SyntheticDemoFixture";
    public string ManifestSummary => $"{Session.Manifest.Edition} | Status: {Session.Manifest.Status}\n{string.Join("\n", Session.Manifest.Revisions)}\nMissing: normative velocity / terrain tables, aerodynamic coefficients, reference heights, reviewed regression fixtures.\n{Session.Calculation.State.Id}";
    public string CsiSummary => $"{Session.Model.Product} | {Session.Model.Version}\nModel: {Session.Model.ModelId}\nFrames: {Session.Model.Frames.Length} | Roof shells: {Session.Model.Shells.Length}\nReal CSI connection not enabled.";
    public string RoofSummary => $"Workflow: {Session.Roof.State} | Approval: {Session.Roof.Approval?.Id ?? "none"}\nAssignment: {Session.Assignment?.Id ?? "none"} | Readback: {Session.Readback?.Matches.ToString() ?? "not performed"}";
    public string FacadeSummary => $"Preview: {(Session.FacadePreview is null ? "not prepared" : "prepared")} | Approval: {(Session.FacadeApproval is null ? "none" : Session.FacadeApproval.User)}\nSimulated assignment: {Session.FacadeAssignment?.Id ?? "none"} | Readback: {Session.FacadeReadback?.Matches.ToString() ?? "not performed"}";
    public string ShellSummary
    {
        get
        {
            var shell = Session.Model.Shells.First(s => s.Id == SelectedShell);
            var mapping = Session.Roof.Mappings.FirstOrDefault(m => m.ShellId == SelectedShell);
            var zone = Session.Zones.FirstOrDefault(z => z.Id == mapping?.ZoneId);
            var sign = mapping?.ManualDirection;
            var signed = sign == ManualLocal3Direction.Unacknowledged ? "unacknowledged" : ((sign == ManualLocal3Direction.Positive ? 1 : -1) * Math.Abs(zone?.NetPressurePa ?? 0)).ToString("G6", CultureInfo.InvariantCulture);
            return $"{shell.Id} | {mapping?.Classification}\nZone: {mapping?.ZoneId ?? "unmapped"}\np_net: {zone?.NetPressurePa.ToString(CultureInfo.InvariantCulture) ?? "unmapped"} Pa (positive inward)\nSigned CSI Local 3: {signed} Pa\nLocal 3: {shell.Local3}\nOutward: {shell.OutwardNormal}";
        }
    }
    public MainViewModel(DemoSession session, ILogger<MainViewModel> logger)
    {
        this.logger = logger;
        Session = session; DatabasePath = session.DatabasePath;
        Session.LoadDemoFixture(); Refresh();
    }
    public ICommand Command(Action action) => new RelayCommand(() => Run(action));
    public void Run(Action action)
    {
        try { action(); Status = "Completed. Simulation only; real CSI writes disabled."; }
        catch (Exception e) { logger.LogError(e, "Desktop action failed"); Status = e.Message; }
        Refresh();
    }
    public async Task RunAsync(Func<Task> action)
    {
        try { await action(); Status = "Completed. Simulation only; real CSI writes disabled."; }
        catch (Exception e) { logger.LogError(e, "Desktop async action failed"); Status = e.Message; }
        Refresh();
    }
    public void Refresh()
    {
        ReportIds = Session.SavedReports();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); Refreshed?.Invoke();
    }
    public void SelectShell(string shell)
    {
        SelectedShell = shell;
        var mapping = Session.Roof.Mappings.First(m => m.ShellId == shell);
        SelectedZone = mapping.ZoneId ?? Session.Zones[0].Id; SelectedSign = mapping.ManualDirection;
        AcknowledgeMismatch = mapping.MismatchAcknowledged; Refresh();
    }
    public void OpenProject() { var opened = new DemoSession(DatabasePath); opened.LoadDemoFixture(Edition); Session = opened; SelectedShell = "R1"; }
    public void LoadFixture(bool crossing = true) { Session.LoadDemoFixture(Edition, crossing); SelectedShell = "R1"; Issues = []; ProposedLoads = []; }
    public void LoadSelectedState() { Session.LoadDemoFixture(Edition, true, new(Direction, InternalPressure, RoofCase, LimitState.Uls)); SelectedShell = "R1"; Issues = []; ProposedLoads = []; }
    public void EditZone() => Session.SetZone(SelectedShell, SelectedZone, User, Reason);
    public void EditSign() => Session.SetSign(SelectedShell, SelectedSign, AcknowledgeMismatch, User, Reason);
    public void Pattern() => Session.SetPattern(SelectedPattern);
    public void Exclude() => Session.ExcludeShell(SelectedShell, User, Reason);
    public void Validate() => Issues = Session.ValidateRoof().Cast<object>().ToArray();
    public void Approve() => Session.ApproveRoof(User);
    public void Preview() => ProposedLoads = Session.PreviewAssignment().ProposedLoads.Cast<object>().ToArray();
    public Task Apply() => RunAsync(async () => { await Session.ApplyAsync(); });
    public Task Verify() => RunAsync(async () => { await Session.VerifyAsync(); });
    public Task PreviewFacade() => RunAsync(async () => { await Session.PreviewFacadeAsync(); });
    public Task ApproveFacade() => RunAsync(async () => { await Session.ApproveFacadeAsync(User); });
    public Task ApplyFacade() => RunAsync(async () => { await Session.ApplyFacadeAsync(); });
    public Task VerifyFacade() => RunAsync(async () => { await Session.VerifyFacadeAsync(); });
    public void PreviewWall() => Session.PreviewWall(SelectedWall, double.Parse(WallHeight, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(WallOverride) ? null : double.Parse(WallOverride, CultureInfo.InvariantCulture));
    private ReportDefinition Definition() => new("desktop-report", IncludeFacade, IncludeRoof, IncludeWall);
    public void PreviewReport() => ReportText = new MarkdownReportExporter().Export(Session.BuildReport(Definition()));
    public void SaveReport() { SelectedReport = Session.SaveReport(Definition()); PreviewReport(); }
    public void OpenReport() { if (SelectedReport is null) throw new InvalidOperationException("Select a stored report ID."); ReportText = new MarkdownReportExporter().Export(Session.OpenReport(SelectedReport)); }
    public void ExportReport(string path) => Session.ExportReport(path, Definition());
    public async Task SmokeAsync()
    {
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
}
