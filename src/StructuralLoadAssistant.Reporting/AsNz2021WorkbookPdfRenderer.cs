using System.Globalization;
using System.Net;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Reporting;
/// <summary>Complete opt-in audit; main worksheet is rendered by the cell projection partial.</summary>
public sealed partial class AsNz2021WorkbookPdfRenderer
{
    static string E(string? value)=>WebUtility.HtmlEncode(value??"");
    static string MathLabel(string label) => E(label).Replace("C_shp (C_fig in owner workbook)", "C<sub>shp</sub> (C<sub>fig</sub> in owner workbook)").Replace("Cp,e", "C<sub>p,e</sub>").Replace("Cdyn", "C<sub>dyn</sub>").Replace("VRu", "V<sub>Ru</sub>").Replace("VRs", "V<sub>Rs</sub>").Replace("Vsit,βu", "V<sub>sit,βu</sub>").Replace("Vsit,βs", "V<sub>sit,βs</sub>").Replace("Vdes,ø", "V<sub>des,θ</sub>").Replace("pair", "ρ<sub>air</sub>").Replace("kN/m2", "kN/m²");
    static string N(double? value,int digits=3)=>value.HasValue&&double.IsFinite(value.Value)?value.Value.ToString("F"+digits,CultureInfo.InvariantCulture):"NOT CALCULATED";
    public string BuildAuditHtml(WindPdfReportDocument document)
    {
        var run=document.CalculationRun??throw new ArgumentException("Calculation run required.");
        var req=run.Request;
        var sb=new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><style>@page{size:A4 portrait;margin:10mm}body{font:9pt Arial;color:#111}table{table-layout:fixed;width:100%;border-collapse:collapse;margin-bottom:8px}td,th{border-bottom:0.3pt solid #aaa;padding:2px 3px;vertical-align:top;overflow-wrap:anywhere}th{color:#164d94;text-align:left}td.value{color:#174f9b;text-align:right}tr{break-inside:avoid}tr.section{color:#164d94;font-weight:bold;border-top:1pt solid #164d94}thead{display:table-header-group}.section{color:#164d94;font-weight:bold}.appendix{break-before:page}.small{font-size:7pt;overflow-wrap:anywhere}h1{font-size:12pt}h2{font-size:10pt;color:#164d94}</style></head><body>");
        sb.Append($"<h1>LOAD CALCULATION — {E(document.Project.Name)}</h1><p>{E(run.QualificationStatus)} · AS/NZS 1170.2:2021 · metric · NO CSI WRITES</p>");
        sb.Append("<h2>All direction / limit-state / internal-sign cases</h2><table class='small'><thead><tr><th>Stable case ID</th><th>Vdes m/s</th><th>pe kN/m²</th><th>pi kN/m²</th><th>pn kN/m²</th><th>Status</th></tr></thead><tbody>");
        foreach(var r in run.AsNz2021Cases)sb.Append($"<tr><td>{E(r.Id)}</td><td>{N(r.DesignSpeed)}</td><td>{N(r.ExternalPa/1000)}</td><td>{N(r.InternalPa/1000)}</td><td>{N(r.NetPa/1000)}</td><td>{E(r.Status)}</td></tr>");
        sb.Append("</tbody></table><h2>Case applicability and factor evidence</h2><table class='small'><colgroup><col style='width:35%'><col style='width:65%'></colgroup><thead><tr><th>Case / decision</th><th>Factor, value, source class, document / selector</th></tr></thead><tbody>");
        foreach(var r in run.AsNz2021Cases.Where(r=>r.LimitState==LimitState.Uls&&r.Id.EndsWith("Positive"))) {
            sb.Append($"<tr><td>{E(r.Id)}<br>{E(r.MethodDecision?.Status)}<br>{E(string.Join("; ",r.MethodDecision?.Conditions??[]))}<br>q basis {N(r.PressureBasisPa)} Pa</td><td>");
            foreach(var f in r.FactorEvidence)sb.Append($"{E(f.Key)} = {f.Value.Value:R}; {E(f.Value.Provenance.ToString())}; {E(f.Value.Document)} / {E(f.Value.ClauseOrTable)}; {E(f.Value.SourceNotes)}<br>");
            sb.Append("</td></tr>");
        }
        sb.Append("</tbody></table><h2>Full precision calculation trace and input provenance</h2><table class='small'><colgroup><col style='width:22%'><col style='width:50%'><col style='width:28%'></colgroup><thead><tr><th>ID / reference</th><th>Formula / substituted values</th><th>Result / source</th></tr></thead><tbody>");
        foreach(var t in run.TraceNodes)sb.Append($"<tr><td>{E(t.NodeId)}<br>{E(t.SourceReference)}</td><td>{E(t.SymbolicEquation)}<br>{E(t.SubstitutedEquation)}<br>Dependencies: {E(string.Join(", ",t.Dependencies))}</td><td>{E(t.ResultWithUnit)}<br>{E(t.ExplanatoryNote)}</td></tr>");
        sb.Append("</tbody></table><div style='break-inside:avoid'><h2>Geometry and identity</h2>");
        sb.Append($"<p class='small'>Procedure {E(req.ProcedureName)}; applied amendments: {E(string.Join(", ", req.SelectedAmendments))} (adoption / source verification pending).<br>Geometry: {E(req.Options.GetValueOrDefault("GeometrySource","OWNER_INPUT"))}; {E(req.Options.GetValueOrDefault("GeometryOverrideReason","No override reason supplied"))}; {E(req.Options.GetValueOrDefault("ReadOnlyScanEvidence","No read-only scan geometry evidence attached"))}. Automatic envelope qualification remains blocked. Ground datum {N(req.GroundElevationM)} m; eave {N(req.EaveHeightM)} m; roof slope {N(req.RoofSlopeDegrees)}°. Model fingerprint: {E(document.ModelSnapshot.GeometryFingerprint)}. Run hash: {E(run.ContentHash)}. Document hash: {E(document.ReportDocumentHash)}.</p>");
        foreach(var i in run.Issues)sb.Append($"<p>{E(i.Code)}: {E(i.Message)}</p>");
        return sb.Append("</div></body></html>").ToString();
    }
}
