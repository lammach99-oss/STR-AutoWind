using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Reporting;
public static class ExternalWindReportBuilder
{
    static string E(object? v)=>WebUtility.HtmlEncode(Convert.ToString(v,CultureInfo.InvariantCulture)??"");
    static string V(Vec3 v)=>string.Join(", ",new[]{v.X,v.Y,v.Z}.Select(n=>n.ToString("G8",CultureInfo.InvariantCulture)));
    public static string BuildHtml(ExternalWindRun run,CsiModelSnapshot model,ImmutableArray<FrameLoadPlan> plans,ImmutableArray<string> blockers,bool audit)
    {
        var b=new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><style>@page{size:A4;margin:10mm}body{font:8pt Arial;color:#123}h1{font-size:14pt}h2{font-size:10pt;color:#164d94}table{width:100%;border-collapse:collapse;table-layout:fixed;margin:8px 0}td,th{border:0.4pt solid #aaa;padding:3px;overflow-wrap:anywhere}th{background:#edf3fa}tr{break-inside:avoid}thead{display:table-header-group}.identity{font-size:6pt;overflow-wrap:anywhere}.section{background:#dce8f5}</style></head><body>");
        b.Append("<h1>LOAD CALCULATION — EXTERNAL ONLY</h1><p>AS/NZS 1170.2:2021 · metric · EXTERNAL_ONLY_REVIEW_NOT_FOR_DESIGN<br>INTERNAL / NET: ENGINEER CONTROLLED SEPARATELY — NOT CALCULATED · NO CSI WRITES</p>");
        b.Append($"<p>{E(run.Status)}<br>{E(run.Request.ProjectId)} / {E(run.Request.Country)} / {E(run.Request.Region)} / {E(run.Request.RevisionDecision)}</p>");
        b.Append($"<p class='identity'>Run SHA-256 {E(run.ContentHash)}<br>Model {E(model.ModelId)} / snapshot SHA-256 {E(SnapshotJson.Hash(model))}</p>");
        b.Append("<h2>1. Basic parameters and signed external pressures</h2><p>q = ½ ρ Vdes² Cdyn; Cshp,e = Cpe Ka Kce Kl Kp; pe = q Cshp,e; traction = −pe n_out. Pressure positive inward, suction negative outward.</p><table><colgroup>");
        double sum=AsNzWorkbookCellLayout.Widths.Sum();foreach(double w in AsNzWorkbookCellLayout.Widths)b.Append($"<col style='width:{(100*w/sum).ToString("F4",CultureInfo.InvariantCulture)}%'>");
        b.Append("</colgroup><thead><tr><th colspan='4'>Wind FROM / state / face / alternative</th><th colspan='2'>Vdes m/s</th><th colspan='2'>q Pa</th><th colspan='2'>Cshp,e</th><th colspan='2'>pe Pa</th><th colspan='2'>Source route</th></tr></thead><tbody>");
        foreach(var c in run.Cases)b.Append($"<tr><td colspan='4'>{E(c.Id)}</td><td colspan='2'>{E(c.DesignSpeed)}</td><td colspan='2'>{E(c.BasisPa)}</td><td colspan='2'>{E(c.Shape)}</td><td colspan='2'>{E(c.PressurePa)}{(c.PressurePa.HasValue?"":"<br>"+E(c.Status))}</td><td colspan='2'>{E(c.Table)}</td></tr>");
        b.Append("</tbody></table><h2>2. Perimeter frame schedule — Global XYZ / I-end stations</h2><table><thead><tr><th>Case / frame / zone</th><th>From / To</th><th>Start kN/m XYZ</th><th>End kN/m XYZ</th></tr></thead><tbody>");
        foreach(var p in plans)foreach(var s in p.Segments)b.Append($"<tr><td>{E(p.ScenarioId)}<br>{E(s.FrameId)} / {E(s.ZoneId)}</td><td>{E(s.StartFraction)} / {E(s.EndFraction)}</td><td>{E(V(s.StartNPerM*0.001))}</td><td>{E(V(s.EndNPerM*0.001))}</td></tr>");
        b.Append("</tbody></table><h2>3. Transfer status / missing assignments</h2>");
        foreach(string blocker in blockers)b.Append($"<p>{E(blocker)}</p>");
        if(audit)
        {
            b.Append("<h2>4. Force and moment equilibrium about global origin</h2>");
            foreach(var p in plans){b.Append($"<p class='identity'>{E(p.Pattern)} / plan SHA-256 {E(p.ContentHash)}</p>");foreach(var review in p.SupportReviews)b.Append($"<p>Support review {E(review.PanelId)} / {E(review.Reviewer)} / {E(review.Reason)}; first {E(string.Join(",",review.FirstSupport))}; second {E(string.Join(",",review.SecondSupport))}</p>");foreach(var c in p.Checks)b.Append($"<p>Surface F N {E(c.SurfaceForceN)}; frame F N {E(c.FrameForceN)}<br>Surface M Nm {E(c.SurfaceMomentNm)}; frame M Nm {E(c.FrameMomentNm)}<br>Residuals {E(c.ForceResidualN)} N / {E(c.MomentResidualNm)} Nm; passed {E(c.Passed)}</p>");}
            b.Append("<h2>5. Reviewed physical surfaces</h2>");
            foreach(var s in run.Request.Surfaces)b.Append($"<p>{E(s.Id)} / {E(s.State)} / {E(s.WindFrom)} / {E(s.Alternative)}<br>normal {E(s.Polygon.OutwardNormal)}; participating area {E(s.ParticipatingAreaM2)} m²<br>Vertices {E(string.Join("; ",s.Polygon.Vertices))}<br>Review {E(s.Polygon.Reviewer)} / {E(s.Polygon.Reason)}; applicability {E(s.Applicability)}</p>");
            b.Append("<h2>6. Complete factor and substitution audit</h2><table><thead><tr><th>Case / reference</th><th>Formula / operands</th><th>Result / disclosure</th></tr></thead><tbody>");
            foreach(var c in run.Cases)foreach(var t in c.Trace)b.Append($"<tr><td>{E(c.Id)}<br>{E(t.SourceReference)}</td><td>{E(t.SymbolicEquation)}<br>{E(t.SubstitutedEquation)}</td><td>{E(t.ResultWithUnit)}<br>{E(t.ExplanatoryNote)}</td></tr>");
            b.Append("</tbody></table>");
        }
        return b.Append("</body></html>").ToString();
    }
    public static string ScheduleCsv(ExternalWindRun run,CsiModelSnapshot model,ImmutableArray<FrameLoadPlan> plans)
    {
        string Q(object? value){string text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";if(text.Length>0&&"=+@".Contains(text[0]))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
        var b=new StringBuilder("Status,RunHash,ModelHash,Pattern,Case,Frame,FromI,ToI,GlobalX_Start_NperM,GlobalY_Start_NperM,GlobalZ_Start_NperM,GlobalX_End_NperM,GlobalY_End_NperM,GlobalZ_End_NperM\r\n");
        foreach(var p in plans)foreach(var s in p.Segments)b.AppendLine(string.Join(",",new object[]{"EXTERNAL_ONLY_REVIEW_NOT_FOR_DESIGN",run.ContentHash,SnapshotJson.Hash(model),p.Pattern,p.ScenarioId,s.FrameId,s.StartFraction,s.EndFraction,s.StartNPerM.X,s.StartNPerM.Y,s.StartNPerM.Z,s.EndNPerM.X,s.EndNPerM.Y,s.EndNPerM.Z}.Select(Q)));
        return b.ToString();
    }
    public static string ScheduleJson(ExternalWindRun run,CsiModelSnapshot model,ImmutableArray<FrameLoadPlan> plans)=>SnapshotJson.Serialize(new{Status="EXTERNAL_ONLY_REVIEW_NOT_FOR_DESIGN",RunHash=run.ContentHash,ModelHash=SnapshotJson.Hash(model),Units="N, m, N/m; Global XYZ; stations from I end",Plans=plans});
}
