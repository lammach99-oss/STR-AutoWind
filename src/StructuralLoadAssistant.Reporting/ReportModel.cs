using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Reporting;

public sealed record FacadeReportRow(string Panel, string Frame, string Method, double StartM, double EndM, double LineLoadNPerM);
public sealed record WallReportRow(string Frame, string Pattern, string WallType, double HeightM, double GravityNPerM);
public sealed record ReportDefinition(string Id, bool IncludeFacade = true, bool IncludeRoof = true, bool IncludeWall = true);
public sealed record ReportModel(Project Project, WindCalculationRun Calculation, StandardManifest Manifest,
    CsiModelSnapshot Model, ImmutableArray<FacadeReportRow> Facade, ImmutableArray<WallReportRow> Walls,
    RoofMappingApproval? Approval, AssignmentRun? Assignment, ReadbackVerification? Readback, ReportDefinition Definition,
    FacadeSimulationRun? FacadeAssignment=null,FacadeReadbackVerification? FacadeReadback=null);
public static class ReportSnapshotValidator
{
    public static void Validate(ReportModel m)
    {
        if(m.Calculation.ManifestHash!=m.Manifest.Hash) throw new InvalidOperationException("Report standard manifest differs from calculation source");
        if(m.Approval is { } a && (SnapshotJson.Hash(a.Calculation)!=SnapshotJson.Hash(m.Calculation)||
            SnapshotJson.Hash(a.Model)!=SnapshotJson.Hash(m.Model)||a.ContentHash!=RoofApprovalValidator.ComputeHash(a)))
            throw new InvalidOperationException("Report approval source snapshots differ");
        if(m.Assignment is { } run && (m.Approval is null || run.ApprovalId!=m.Approval.Id || run.ContentHash!=m.Approval.ContentHash ||
            run.CalculationRunId!=m.Calculation.Id || run.ModelSnapshotId!=m.Model.Id || !run.IsSimulation ||
            SnapshotJson.Serialize(run.AreaLoads)!=SnapshotJson.Serialize(m.Approval.Proposals)))
            throw new InvalidOperationException("Report assignment is not linked to approved simulation");
        if(m.Readback is { } readback && (m.Assignment is null || readback.AssignmentId!=m.Assignment.Id))
            throw new InvalidOperationException("Report readback is not linked to assignment");
        if(m.FacadeAssignment is { } f && (SnapshotJson.Hash(f.Approval.Preview.Calculation)!=SnapshotJson.Hash(m.Calculation) || !f.IsSimulation ||
            f.Approval.Preview.Model.ModelId!=m.Model.ModelId || f.Approval.Preview.Model.GeometryFingerprint!=m.Model.GeometryFingerprint))
            throw new InvalidOperationException("Report facade assignment source differs");
        if(m.FacadeReadback is { } fr && (m.FacadeAssignment is null || fr.RunId!=m.FacadeAssignment.Id))
            throw new InvalidOperationException("Report facade readback is not linked");
        if(m.FacadeAssignment is { } fa)
        {
            if(SnapshotJson.Serialize(fa.FrameLoads)!=SnapshotJson.Serialize(fa.Approval.Preview.FrameLoads))
                throw new InvalidOperationException("Reported facade loads differ from approval");
            foreach(var row in m.Facade)
            {
                if(!fa.FrameLoads.Any(p=>p.Segment.FrameId==row.Frame && p.Segment.LoadNPerM==row.LineLoadNPerM && fa.Approval.Preview.Method.ToString()==row.Method))
                    throw new InvalidOperationException("Facade summary differs from approved values");
            }
        }
    }
}
public interface IReportExporter { string Export(ReportModel model); }
public sealed class MarkdownReportExporter : IReportExporter
{
    public string Export(ReportModel m)
    {
        ReportSnapshotValidator.Validate(m);
        var s = new System.Text.StringBuilder();
        void Line(string value = "") => s.Append(value).Append('\n');
        string N(double v) => v.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        Line("# Structural Load Assistant");
        Line("DEMONSTRATION DATA - NOT FOR ENGINEERING DESIGN");
        Line("SIMULATION ONLY — REAL CSI WRITES DISABLED");
        Line($"Project: {m.Project.Name} | {m.Project.Id} | Units: {m.Project.UnitSystem}");
        Line($"Calculation: {m.Calculation.Id} | {m.Calculation.CreatedAt:O} | {m.Calculation.Provenance}");
        Line($"Standard: {m.Manifest.Edition} | {m.Manifest.Status} | Hash: {m.Manifest.Hash}");
        foreach(var revision in m.Manifest.Revisions) Line($"Revision: {revision}");
        foreach(var reference in m.Manifest.References) Line($"Reference: {reference.Document} / {reference.Clause} / {reference.Source}");
        Line($"State: {m.Calculation.State.Id}");
        Line("## Inputs and pressure profile");
        foreach(var p in m.Calculation.Profile) Line($"z={N(p.ElevationM)} m; p_net={N(p.PressurePa)} Pa (positive inward)");
        Line("## Calculation trace / missing data");
        foreach(var t in m.Calculation.Trace) Line($"{t.FormulaId}: {t.InputsWithUnits} -> {t.ResultWithUnits}; {t.Reference}; {t.Warning}");
        Line("Verified normative datasets and engineering calculation are unavailable.");
        Line($"CSI: {m.Model.Product} {m.Model.Version} | {m.Model.ModelId} | Scan: {m.Model.Id}");
        Line($"Geometry: {m.Model.GeometryFingerprint} | Existing loads: {m.Model.ExistingLoadsFingerprint}");
        Line($"Patterns: {string.Join(", ",m.Model.Patterns)}");
        Line($"Cases: {string.Join(", ",m.Model.Cases)} | Combinations: {string.Join(", ",m.Model.Combinations)}");
        if(m.Definition.IncludeFacade)
        {
            Line("## Frame line loads (N/m, net inward before global-axis conversion)");
            Line("Panel | Frame | Method | Start m | End m | N/m");Line("--- | --- | --- | --- | --- | ---");
            foreach(var f in m.Facade) Line($"{f.Panel} | {f.Frame} | {f.Method} | {N(f.StartM)} | {N(f.EndM)} | {N(f.LineLoadNPerM)}");
            if(m.FacadeAssignment is { } facade)
            {
                Line($"Facade simulation: {facade.Id} | {facade.Approval.User} | {facade.Approval.ApprovedAt:O} | {facade.Approval.ContentHash}");
                foreach(var p in facade.FrameLoads) Line($"{p.Segment.FrameId} | {p.Segment.Pattern} | signed global N/m = ({N(p.GlobalLoadNPerM.X)}, {N(p.GlobalLoadNPerM.Y)}, {N(p.GlobalLoadNPerM.Z)})");
                Line($"Facade readback: {m.FacadeReadback?.Matches}; manual loads preserved: {facade.PreservedManualLoads.Length}");
            }
        }
        if(m.Definition.IncludeRoof)
        {
            Line("## Roof mapping / Local 3 schedule");
            Line("Projected XY diagram is included in the HTML export. Polygon vertices (m):");
            foreach(var shell in m.Model.Shells) Line($"{shell.Id}: {string.Join("; ",shell.Vertices.Select(v=>$"({N(v.X)},{N(v.Y)})"))}");
            if(m.Approval is { } a)
            {
                Line($"Simulation approval: {a.Id} | {a.User} | {a.ApprovedAt:O} | {a.ContentHash}");
                foreach(var p in a.Proposals) Line($"{p.ShellId} | {p.ZoneId} | {p.StateId} | {p.Pattern} | net={N(p.NetPressurePa)} Pa | {p.ManualLocal3Direction} Local3 | assigned={N(p.SignedCsiAreaLoadPa)} Pa");
                foreach(var e in a.Exclusions) Line($"Excluded: {e.ShellId}; {e.User}; {e.Reason}; {e.CreatedAt:O}");
                foreach(var o in a.Overrides) Line($"Override: {o.ObjectId}/{o.Field}: {o.OldValue} -> {o.NewValue}; {o.User}; {o.CreatedAt:O}; {o.Reason}");
            }
            else Line("No approved mapping; no assignment authorized.");
        }
        if(m.Definition.IncludeWall)
        {
            Line("## Wall gravity loads (Global -Z)");
            foreach(var w in m.Walls) Line($"{w.Frame} | {w.Pattern} | {w.WallType} | independent height={N(w.HeightM)} m | signed={N(w.GravityNPerM)} N/m");
        }
        Line(m.Assignment is { } run ? $"Assignment: {run.Id}; IsSimulation={run.IsSimulation}; preserved manual loads={run.PreservedManualLoadCount}" : "No simulated assignment yet.");
        if(m.Readback is { } readback) Line($"Readback: {readback.Matches}; tolerance={N(readback.TolerancePa)} Pa; {string.Join("; ",readback.Issues)}");
        return s.ToString();
    }
}
public sealed class HtmlReportExporter : IReportExporter
{
    public string Export(ReportModel model)
    {
        string E(string v) => System.Net.WebUtility.HtmlEncode(v);
        string N(double v) => v.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
        var svg = new System.Text.StringBuilder("<svg viewBox='-1 -1 13 14' width='500' height='500' role='img' aria-label='Projected XY roof shells in metres'>");
        if(model.Definition.IncludeRoof)
            foreach(var shell in model.Model.Shells)
            {
                var zone = model.Approval?.Proposals.FirstOrDefault(p=>p.ShellId==shell.Id)?.ZoneId;
                string color = zone == "Edge" ? "#75c6bd" : zone == "Interior" ? "#a8b9e8" : "#efbe88";
                svg.Append($"<polygon points='{string.Join(" ",shell.Vertices.Select(v=>$"{N(v.X)},{N(v.Y)}"))}' fill='{color}' stroke='#163345' stroke-width='.04'><title>{E(shell.Id)} — {E(zone??"Unapproved/excluded")}</title></polygon>");
                svg.Append($"<text x='{N(shell.Vertices[0].X+.2)}' y='{N(shell.Vertices[0].Y+.6)}' font-size='.35'>{E(shell.Id)}</text>");
            }
        svg.Append("</svg>");
        return "<!doctype html><html lang='en'><meta charset='utf-8'><title>Structural Load Assistant — Simulation</title><style>body{font:15px system-ui;margin:36px;color:#183345}pre{white-space:pre-wrap;line-height:1.5}svg{background:#f3f6fa}</style><h1>DEMONSTRATION DATA — NOT FOR ENGINEERING DESIGN</h1>" + svg + "<pre>" + E(new MarkdownReportExporter().Export(model)) + "</pre></html>";
    }
}
