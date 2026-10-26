using System.Globalization;
using System.Net;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Reporting;
public sealed class AsNz2021WorkbookPdfRenderer
{
    static string E(string? value)=>WebUtility.HtmlEncode(value??"");
    static string MathLabel(string label) => E(label).Replace("C_shp (C_fig in owner workbook)", "C<sub>shp</sub> (C<sub>fig</sub> in owner workbook)").Replace("Cp,e", "C<sub>p,e</sub>").Replace("Cdyn", "C<sub>dyn</sub>").Replace("VRu", "V<sub>Ru</sub>").Replace("VRs", "V<sub>Rs</sub>").Replace("Vsit,βu", "V<sub>sit,βu</sub>").Replace("Vsit,βs", "V<sub>sit,βs</sub>").Replace("Vdes,ø", "V<sub>des,θ</sub>").Replace("pair", "ρ<sub>air</sub>").Replace("kN/m2", "kN/m²");
    static string N(double? value,int digits=3)=>value.HasValue&&double.IsFinite(value.Value)?value.Value.ToString("F"+digits,CultureInfo.InvariantCulture):"NOT CALCULATED";
    public string BuildHtml(WindPdfReportDocument document)
    {
        var run=document.CalculationRun??throw new ArgumentException("Calculation run required.");
        var req=run.Request;
        var sb=new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><style>@page{size:A4 portrait;margin:10mm}body{font:9pt Arial;color:#111}table{width:100%;border-collapse:collapse;margin-bottom:8px}td,th{border-bottom:0.3pt solid #aaa;padding:2px 3px;vertical-align:top}th{color:#164d94;text-align:left}td.value{color:#174f9b;text-align:right}tr{break-inside:avoid}tr.section{color:#164d94;font-weight:bold;border-top:1pt solid #164d94}thead{display:table-header-group}.section{color:#164d94;font-weight:bold}.appendix{break-before:page}.small{font-size:7pt;overflow-wrap:anywhere}h1{font-size:12pt}h2{font-size:10pt;color:#164d94}</style></head><body>");
        sb.Append($"<h1>LOAD CALCULATION — {E(document.Project.Name)}</h1><p>{E(run.QualificationStatus)} · AS/NZS 1170.2:2021 · metric · NO CSI WRITES</p>");
        double? Input(string key)=>req.ExplicitCoefficients.TryGetValue(key,out var e)?e.Value:null;
        string V(string key)=>N(Input(key));
        AsNz2021Case? Case(WindDirection d,LimitState state,string surface,int c=0,int z=0)=>run.AsNz2021Cases.FirstOrDefault(r=>r.Direction==d&&r.LimitState==state&&r.Surface==surface&&r.RoofCase==c&&r.Zone==z&&r.Id.EndsWith("Positive"));
        string Value(int row)
        {
            if(row==5)return "ENGINEER REVIEW / NOT APPROVED";
            if(row==6)return E(document.Project.Name);
            if(row==7)return "Jurisdiction / site address: OWNER CONFIRMATION REQUIRED";
            if(row==8)return E(run.Id);
            if(row==11)return E(req.StandardEdition);
            if(row==14)return E(req.RoofForm);
            if(row==15)return N(req.BuildingWidthM)+" m";
            if(row==16)return N(req.BuildingLengthM)+" m";
            if(row==17)return N(req.RoofSlopeDegrees)+"°";
            if(row==18)return N(req.RidgeHeightM-req.GroundElevationM)+" m";
            if(row==19)return N(req.ReferenceHeightM)+" m";
            if(row==20)return E(req.Options.GetValueOrDefault("Region","INPUT REQUIRED"));
            if(row==21)return E(req.ExposureOrTerrain);
            if(row==22)return $"R = {V("R_Uls")} years; V<sub>Ru</sub> = {V("VR_Uls")} m/s";
            if(row==23)return $"R = {V("R_Sls")} years; V<sub>Rs</sub> = {V("VR_Sls")} m/s";
            if(row==24)return "M<sub>d</sub>: eight-direction inputs; see appendix";
            string? key=row switch{25=>"Mc",26=>"Ms",27=>"Mt",28=>"Mz_cat",31=>"Cdyn",32=>"rho",_=>null};
            if(key!=null)return V(key)+(row==32?" kg/m³":"");
            if(row is 29 or 30) {string state=row==29?"Uls":"Sls";return run.ComputedQuantities.TryGetValue($"Vsit_{state}_0",out var speed)?N(speed)+" m/s (β=0°; Vdes separately below)":"NOT CALCULATED";}
            if(row is 34 or 35){var r=Case(WindDirection.PositiveX,row==34?LimitState.Uls:LimitState.Sls,"Windward");return N(r?.DesignSpeed is double v && Input("rho") is double rho && Input("Cdyn") is double cdyn ?0.5*rho*v*v*cdyn/1000:null)+" kN/m² (velocity basis)";}
            bool y=row>=82; int local=y?row-44:row; var direction=y?WindDirection.PositiveY:WindDirection.PositiveX;
            if(local is 39 or 40 or 41 || row==33) { string factor = row==33?"Kp":local==39?"Kl":local==40?"Ka":"Kce"; return V($"{direction}_Windward_0_0_{factor}")+" (windward; per-surface values in trace)"; }
            if(local==42)return req.Options.TryGetValue("ParapetHeightM",out var parapet)?E(parapet)+" m — owner input":"INPUT REQUIRED / method applicability unverified";
            if(local==49)return N((y?req.BuildingWidthM:req.BuildingLengthM)/(y?req.BuildingLengthM:req.BuildingWidthM));
            if(local==63)return N(req.ReferenceHeightM/(y?req.BuildingWidthM:req.BuildingLengthM));
            string? surface=local is >=44 and <=47?"Windward":local is >=50 and <=53?"Leeward":null;
            if(surface!=null){var r=Case(direction,local is 47 or 53?LimitState.Sls:LimitState.Uls,surface);return local is 44 or 50?N(r?.Cpe):local is 45 or 51?N(r?.ExternalShape):N(r?.ExternalPa/1000)+" kN/m² (external)";}
            int z=local is >=57 and <=60?local-57:local is >=67 and <=71?local-67:local is >=76 and <=80?local-76:-1;
            if(z>=0){var r=Case(direction,LimitState.Uls,local<=60?"SideLeft":"Roof",local<=60?0:local<=71?1:2,z);return r?.Status=="NOT_APPLICABLE"?"NOT APPLICABLE — beyond physical extent":$"{N(r?.FromM)}–{N(r?.ToM)} m | Cp,e {N(r?.Cpe)} | {N(r?.ExternalPa/1000)} kN/m²";}
            if(local is 56 or 66 or 75)return "From (m) | To (m) | Cp,e | W (kN/m²)";
            if(local is 55 or 65 or 74)return "From / To (m) | Cp,e | external pressure (ULS)";
            return "";
        }
        sb.Append("<table><thead><tr><th>Workbook row / parameter</th><th>Calculated value / equation</th></tr></thead><tbody>");
        foreach(var entry in AsNzWorkbookRows.Flat)
        {
            string label=entry.Value.Replace("Cfig","C_shp (C_fig in owner workbook)");
            sb.Append($"<tr class='{(entry.Key is 13 or 37 or 38 or 43 or 48 or 54 or 62 or 73 or 82 or 87 or 92 or 98 or 106 or 117 ? "section" : "")}' id='Flat-{entry.Key}'><td>{entry.Key}. {MathLabel(label)}</td><td class='value'>{Value(entry.Key)}</td></tr>");
        }
        sb.Append("</tbody></table><p>Primary worksheet columns show +X / +Y external pressures. Complete signed directions, ULS/SLS and internal/net cases follow. Legacy duplicate 2.1 numbering retained. Coefficient applicability, source rights, amendments and engineering sign-off remain unverified.</p><h2 class='appendix'>All direction / limit-state / internal-sign cases</h2><table class='small'><thead><tr><th>Stable case ID</th><th>Vdes m/s</th><th>pe kN/m²</th><th>pi kN/m²</th><th>pn kN/m²</th><th>Status</th></tr></thead><tbody>");
        foreach(var r in run.AsNz2021Cases)sb.Append($"<tr><td>{E(r.Id)}</td><td>{N(r.DesignSpeed)}</td><td>{N(r.ExternalPa/1000)}</td><td>{N(r.InternalPa/1000)}</td><td>{N(r.NetPa/1000)}</td><td>{E(r.Status)}</td></tr>");
        sb.Append("</tbody></table><h2>Full precision calculation trace and input provenance</h2><table class='small'><thead><tr><th>ID / reference</th><th>Formula / substituted values</th><th>Result / source</th></tr></thead><tbody>");
        foreach(var t in run.TraceNodes)sb.Append($"<tr><td>{E(t.NodeId)}<br>{E(t.SourceReference)}</td><td>{E(t.SymbolicEquation)}<br>{E(t.SubstitutedEquation)}</td><td>{E(t.ResultWithUnit)}<br>{E(t.ExplanatoryNote)}</td></tr>");
        sb.Append("</tbody></table><div style='break-inside:avoid'><h2>Geometry and identity</h2>");
        sb.Append($"<p class='small'>Procedure {E(req.ProcedureName)}; applied amendments: {E(string.Join(", ", req.SelectedAmendments))} (adoption / source verification pending).<br>Geometry: explicit engineer inputs; automatic CSI envelope/roof qualification NOT IMPLEMENTED in this path. Ground datum {N(req.GroundElevationM)} m; eave {N(req.EaveHeightM)} m; roof slope {N(req.RoofSlopeDegrees)}°. Model fingerprint: {E(document.ModelSnapshot.GeometryFingerprint)}. Run hash: {E(run.ContentHash)}. Document hash: {E(document.ReportDocumentHash)}.</p>");
        foreach(var i in run.Issues)sb.Append($"<p>{E(i.Code)}: {E(i.Message)}</p>");
        return sb.Append("</div></body></html>").ToString();
    }
}
