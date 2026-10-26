using System.Collections.Immutable;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Reporting;

public sealed record WorksheetReportCell(string Address,int Row,int Column,int ColumnSpan,string Html,string Unit,string Origin,string TraceSource,string State,string CssClass);
public sealed partial class AsNz2021WorkbookPdfRenderer
{
    public ImmutableArray<WorksheetReportCell> ProjectCells(WindPdfReportDocument doc,WindDirection x=WindDirection.PositiveX,WindDirection y=WindDirection.PositiveY)
    {
        var run=doc.CalculationRun??throw new ArgumentException("Calculation run required");var req=run.Request;
        var cells=new Dictionary<string,WorksheetReportCell>();
        void Put(string address,string html,int span=1,string origin="CALCULATED",string trace="",string state="VALUE",string css="")
        {
            int row=int.Parse(new string(address.Where(char.IsDigit).ToArray()));int col=address[0]-'A'+1;
            string unit=AsNzWorkbookCellLayout.Cells.FirstOrDefault(c=>c.Address==address)?.Label??"";
            cells[address]=new(address,row,col,span,html,unit,origin,trace,state,css);
        }
        void Text(string address,string text,int span=1,string css="")=>Put(address,E(text),span,"WORKBOOK_LAYOUT","", "LABEL",css);
        void Number(string address,double? value,string unit="",int span=1,string origin="CALCULATED",string trace="",string css="number")
        {Put(address,value.HasValue?N(value):"N/C",span,origin,trace,value.HasValue?"VALUE":"INPUT_REQUIRED",css);}
        double? Input(string k)=>req.ExplicitCoefficients.TryGetValue(k,out var e)?e.Value:null;
        void Factor(string address,string k,int span=1)=>Number(address,Input(k),span:span,origin:req.ExplicitCoefficients.TryGetValue(k,out var e)?e.Provenance.ToString():"INPUT_REQUIRED",trace:"INPUT-"+k);
        AsNz2021Case? Result(WindDirection d,LimitState state,string surface,int c=0,int z=0)=>run.AsNz2021Cases.FirstOrDefault(r=>r.Direction==d&&r.LimitState==state&&r.Surface==surface&&r.RoofCase==c&&r.Zone==z&&r.Id.EndsWith("Positive"));
        string Sym(string value)=>MathLabel(value).Replace("Mz_cat","M<sub>z,cat</sub>").Replace("Mz,cat","M<sub>z,cat</sub>").Replace("Md","M<sub>d</sub>").Replace("Mc","M<sub>c</sub>").Replace("Ms","M<sub>s</sub>").Replace("Mt","M<sub>t</sub>").Replace("Kl","K<sub>ℓ</sub>").Replace("Ka","K<sub>a</sub>").Replace("Kc","K<sub>c,e</sub>").Replace("Kp","K<sub>p</sub>");
        Put("A5","LOAD CALCULATION",9,"WORKBOOK_LAYOUT",css:"title");Text("J5","Project Code:",2);Text("L5",doc.Project.Id,3);
        Text("A6","Project:",2);Text("C6",doc.Project.Name,7);Text("J6","Calc. by:",2);Text("L6",req.Options.GetValueOrDefault("Author","Not supplied"),2);Text("N6","Rev",css:"small");
        Text("A7","Address:",2);Text("C7",req.Options.GetValueOrDefault("SiteAddress","Site / jurisdiction pending"),7);Text("J7","Checked by:",2);Text("L7","NOT APPROVED",2);Text("N7",req.Options.GetValueOrDefault("Revision","—"));
        Text("A8","Item:",2);Text("C8","Wind load to building calculation",7);Text("J8","Date:",2);Text("L8",run.CreatedAt.ToString("yyyy-MM-dd"),3);
        Text("A10","Design code & reference",14,"section");Text("A11",req.StandardEdition+" — Structural design actions, Part 2: Wind actions",14);
        Text("A13","1. Basic parameters",14,"section");Text("A14","Roof type",9);Text("J14",req.RoofForm=="Monoslope"?"Monopitch & flat roof":req.RoofForm,5);
        foreach(var (row,label,value,unit) in new[]{(15,"Width (Y)",req.BuildingWidthM,"m"),(16,"Length (X)",req.BuildingLengthM,"m"),(17,"Roof pitch (α)",req.RoofSlopeDegrees,"°"),(18,"Peak roof height (hroof)",req.RidgeHeightM-req.GroundElevationM,"m"),(19,"Average roof height (h)",req.ReferenceHeightM,"m")})
        {Text("A"+row,label,9);Number("K"+row,value,origin:"OWNER_INPUT / GEOMETRY REVIEW REQUIRED");Text("L"+row,unit,2);}
        Text("A20","Region",9);Text("K20",req.Options.GetValueOrDefault("RegionCode","INPUT REQUIRED"),4);Text("A21","Terrain Category",9);Text("K21",req.ExposureOrTerrain,3);
        foreach(var (row,state,label) in new[]{(22,LimitState.Uls,"Ultimate, V"),(23,LimitState.Sls,"Serviceability, V")})
        {Put("A"+row,label+"<sub>R"+(state==LimitState.Uls?"u":"s")+"</sub> =",4,"WORKBOOK_LAYOUT");Factor("E"+row,"R_"+state);Text("F"+row,"Years",3);Factor("K"+row,"VR_"+state);Text("L"+row,"m/s",2);}
        string[] factorKeys=["Md_0","Mc","Ms","Mt","Mz_cat"];
        string[] factorLabels=["Wind direction multiplier","Climate change multiplier","Shielding multiplier","Topographic multiplier","Terrain/Height multiplier"];
        for(int i=0;i<5;i++){int row=24+i;Text("A"+row,factorLabels[i],8);Put("I"+row,Sym(i==0?"Md =":factorKeys[i]+" ="),2,"WORKBOOK_LAYOUT");Factor("K"+row,factorKeys[i]);Text("L"+row,i==0?"β=0° input":i==4?"Table 4.1":"",3);}
        foreach(var (row,state) in new[]{(29,LimitState.Uls),(30,LimitState.Sls)})
        {Text("A"+row,"Site wind speed — "+state.ToString().ToUpperInvariant(),6);Put("G"+row,"V<sub>sit,β</sub> = V<sub>R</sub>M<sub>d</sub>M<sub>z,cat</sub>M<sub>s</sub>M<sub>t</sub>M<sub>c</sub>",4,"CALCULATED");Number("K"+row,run.ComputedQuantities.TryGetValue($"Vsit_{state}_0",out var v)?v:null,trace:$"Vsit_{state}_0");Text("L"+row,"m/s");Text("M"+row,"β=0°",2);}
        foreach(var (row,key,label) in new[]{(31,"Cdyn","Dynamic response factor, Cdyn ="),(32,"rho","Density of air, pair ="),(33,"Kp","Porous cladding factor, Kp =")})
        {Put("A"+row,Sym(label),9,"WORKBOOK_LAYOUT");Factor("K"+row,key=="Kp"?$"{x}_Windward_0_0_Kp":key);Text("L"+row,key=="rho"?"kg/m³":"",3);}
        foreach(var (row,state) in new[]{(34,LimitState.Uls),(35,LimitState.Sls)})
        {Put("A"+row,$"q<sub>{state.ToString().ToUpperInvariant()}</sub> = ½ρ<sub>air</sub>V<sub>des,θ</sub>²C<sub>dyn</sub>",10,"CALCULATED",trace:"Eq 2.4(1), Cshp excluded");var r=Result(x,state,"Windward");Number("K"+row,r?.DesignSpeed is double v&&Input("rho") is double rho&&Input("Cdyn") is double cdyn?0.5*rho*v*v*cdyn/1000:null);Text("L"+row,"kN/m²",2);}
        Text("A37","2. Wind load calculation",14,"section");
        void Axis(int start,WindDirection direction,string axis)
        {
            int Offset(int original)=>original+(start-38);
            string A(int r)=>"A"+Offset(r);
            Text(A(38),$"2.1. Wind {axis} calculation — {direction} (θ={(direction==WindDirection.PositiveX?"0":direction==WindDirection.NegativeX?"180":direction==WindDirection.PositiveY?"90":"270")}°)",14,"section");
            foreach(var (row,key,label) in new[]{(39,"Kl","Local pressure factor, Kl ="),(40,"Ka","Area reduction factor, Ka ="),(41,"Kce","Combination factor, Kc =")})
            {Put(A(row),Sym(label),9,"WORKBOOK_LAYOUT");Factor("J"+Offset(row),$"{direction}_Windward_0_0_{key}",2);Text("L"+Offset(row),"windward input",3);}
            Text(A(42),"Parapet height, hₚ =",9);Put("J"+Offset(42),E(req.Options.GetValueOrDefault("ParapetHeightM","INPUT REQUIRED")),1,"OWNER_INPUT");Text("K"+Offset(42),"m",2);
            void Wall(int row,string surface,string title)
            {
                Text(A(row),title,14,"section");int cp=row==43?44:50;
                if(row==48){Text(A(49),"Ratio d/b =",9);Number("J"+Offset(49),direction is WindDirection.PositiveX or WindDirection.NegativeX?req.BuildingLengthM/req.BuildingWidthM:req.BuildingWidthM/req.BuildingLengthM,span:2);}
                Put(A(cp),"External pressure coefficient, C<sub>p,e</sub> =",9,"WORKBOOK_LAYOUT");Number("J"+Offset(cp),Result(direction,LimitState.Uls,surface)?.Cpe,span:2,trace:$"INPUT-{direction}_{surface}_0_0_Cpe");Text("L"+Offset(cp),"EXPLICIT¹",3);
                Put(A(cp+1),"C<sub>shp,e</sub> = C<sub>p,e</sub>K<sub>a</sub>K<sub>c,e</sub>K<sub>ℓ</sub>K<sub>p</sub>",9,"CALCULATED");Number("J"+Offset(cp+1),Result(direction,LimitState.Uls,surface)?.ExternalShape,span:2);Text("L"+Offset(cp+1),"Eq 5.2(2)",3);
                foreach(var state in new[]{LimitState.Uls,LimitState.Sls}){int r=Offset(cp+(state==LimitState.Uls?2:3));Put("A"+r,"p<sub>e</sub> = ½ρ<sub>air</sub>V<sub>des,θ</sub>²C<sub>shp,e</sub>C<sub>dyn</sub>",9,"CALCULATED");Number("J"+r,Result(direction,state,surface)?.ExternalPa/1000,span:2,trace:Result(direction,state,surface)?.Id??"");Text("L"+r,"kN/m²",css:"unit");Text("M"+r,state.ToString().ToUpperInvariant(),2);}
            }
            Wall(43,"Windward","2.1.1. Windward wall");Wall(48,"Leeward","2.1.2. Leeward wall");
            Text(A(54),"2.1.3. Sideward wall",14,"section");
            void Zone(int header,int first,int count,string surface,int roofCase)
            {
                int r=Offset(header);Text("A"+r,"Horizontal distance from windward edge",5,"zone-head");Text("F"+r,"External pressure coefficient",3,"zone-head");Text("I"+r,"Wind pressure",3,"zone-head");
                r++;Text("A"+r,"",3,"zone");Text("D"+r,"From",1,"zone-head");Text("E"+r,"To",1,"zone-head");Put("F"+r,"C<sub>p,e</sub>",3,"WORKBOOK_LAYOUT",css:"zone-head");Text("I"+r,"W (kN/m²)",3,"zone-head");
                string[] labels=surface=="Roof"?["0–0.5h","0.5h–h","h–2h","2h–3h",">3h"]:["0–h","h–2h","2h–3h",">3h"];
                for(int z=0;z<count;z++){r=Offset(first+z);var result=Result(direction,LimitState.Uls,surface,roofCase,z);Text("A"+r,labels[z],3,"zone");Number("D"+r,result?.FromM,css:"zone number");Number("E"+r,result?.ToM,css:"zone number");Number("F"+r,result?.Cpe,span:3,trace:$"INPUT-{direction}_{surface}_{roofCase}_{z}_Cpe",css:"zone number");Put("I"+r,result?.Status=="NOT_APPLICABLE"?"N/A":result?.ExternalPa is double p?N(p/1000):"NOT CALCULATED",3,"CALCULATED",result?.Id??"",result?.Status??"INPUT_REQUIRED","zone number");}
            }
            Zone(55,57,4,"SideLeft",0);Text(A(62),"2.1.4. Roof wind",14,"section");Text(A(63),"Ratio h/d =",2);Number("C"+Offset(63),req.ReferenceHeightM/(direction is WindDirection.PositiveX or WindDirection.NegativeX?req.BuildingLengthM:req.BuildingWidthM));Text("F"+Offset(63),"Method applicability: UNVERIFIED¹",8);
            Text(A(64),"Case 1",11,"section");Zone(65,67,5,"Roof",1);Text(A(73),"Case 2",11,"section");Zone(74,76,5,"Roof",2);
        }
        Axis(38,x,"X");Axis(82,y,"Y");// Attach workbook source style/label metadata through the stable coordinate registry.
        return cells.Values.OrderBy(c=>c.Row).ThenBy(c=>c.Column).ToImmutableArray();
    }
    public string BuildHtml(WindPdfReportDocument document)
    {
        var run=document.CalculationRun??throw new ArgumentException("Run required");
        var sb=new StringBuilder("<!doctype html><html><head><meta charset='utf-8'><style>@page{size:A4;margin:9mm 10mm}body{font:9pt Arial;color:#111;margin:0}section{break-after:page}section:last-child{break-after:auto}table{width:100%;table-layout:fixed;border-collapse:collapse}td{height:4.0mm;padding:1px 2px;vertical-align:middle;overflow-wrap:break-word}sub{font-size:75%}.unit{white-space:nowrap}.number{color:#003f9c;text-align:right;white-space:nowrap}.section{color:#003b76;font-weight:bold}.title{font-size:14pt;font-weight:bold}.zone,.zone-head{border:0.5pt solid #222}.zone-head{text-align:center;font-weight:bold}.small{font-size:7.5pt;overflow-wrap:anywhere}footer{margin-top:3mm;border-top:0.5pt solid #888;font-size:7pt;overflow-wrap:anywhere}.status{font-size:8pt;color:#8a3100;margin:2mm 0}h2{font-size:10pt;color:#003b76;margin:2mm 0}</style></head><body>");
        void Sheet(WindDirection x,WindDirection y,int from,int to,string title)
        {
            var cells=ProjectCells(document,x,y);sb.Append($"<section><div class='status'>{E(run.QualificationStatus)} · NO CSI WRITES · {E(run.Request.Options.GetValueOrDefault("FixturePurpose","EXPLICIT-INPUT REVIEW MODE ONLY"))}</div><h2>{E(title)}</h2><table><colgroup>");
            double total=AsNzWorkbookCellLayout.Widths.Sum();foreach(var w in AsNzWorkbookCellLayout.Widths)sb.Append($"<col style='width:{(100*w/total).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)}%'>");sb.Append("</colgroup>");
            for(int row=from;row<=to;row++){sb.Append($"<tr id='Flat-{row}'>");int column=1;while(column<=14){var cell=cells.FirstOrDefault(c=>c.Row==row&&c.Column==column);if(cell==null){sb.Append("<td></td>");column++;continue;}sb.Append($"<td data-cell='{cell.Address}' data-origin='{E(cell.Origin)}' data-trace='{E(cell.TraceSource)}' data-state='{E(cell.State)}' class='{cell.CssClass}' colspan='{cell.ColumnSpan}'>{cell.Html}</td>");column+=cell.ColumnSpan;}sb.Append("</tr>");}
            sb.Append($"</table><p class='small'>¹ Explicit-input review arithmetic only. {E(run.Provenance.ToString())}. Normative lookup unavailable / source rights required. Geometry is supplied, not a proven full-span 14° roof. C<sub>shp</sub> is the 2021 notation (C<sub>fig</sub> in owner workbook). q is the pressure basis without C<sub>shp</sub>; surface p<sub>e</sub> includes it. Zone W is external ULS; signed internal/net and SLS cases are in the detailed calculation PDF.</p>");
            if(!run.NumericalComputationPossible)sb.Append($"<p class='small'>NOT CALCULATED / INPUT REQUIRED: {E(string.Join(", ",run.Issues.Select(i=>i.Code)))}. No pressure can be used for design.</p>");
            foreach(var d in new[]{x,y}){var r=run.AsNz2021Cases.FirstOrDefault(c=>c.Direction==d&&c.LimitState==LimitState.Uls);sb.Append($"<div class='small'>{d}: β sector {N(r?.BearingDegrees-45)}° to {N(r?.BearingDegrees+45)}°; V<sub>des,θ</sub> ULS {N(r?.DesignSpeed)} m/s; SLS {N(run.AsNz2021Cases.FirstOrDefault(c=>c.Direction==d&&c.LimitState==LimitState.Sls)?.DesignSpeed)} m/s. Cl 2.3, sector maximum with interpolation.</div>");}
            sb.Append(from==5 ? $"<footer>Run hash {E(run.ContentHash)}<br>Document {E(document.ReportDocumentHash)}<br>Model {E(document.ModelSnapshot.GeometryFingerprint)} · PDF byte hash in viewer/export sidecar. Integrity markers, not certification.</footer></section>" : $"<footer>Run {E(run.ContentHash[..12])} · document {E(document.ReportDocumentHash[..12])} · detailed calculation PDF contains all signed cases and source evidence. N/C = NOT CALCULATED; missing inputs remain blockers.</footer></section>");
        }
        Sheet(WindDirection.PositiveX,WindDirection.PositiveY,5,42,"Basic parameters / primary +X and +Y worksheet");
        Sheet(WindDirection.PositiveX,WindDirection.PositiveY,43,81,"Wind X — PositiveX / external pressure worksheet");
        Sheet(WindDirection.PositiveX,WindDirection.PositiveY,82,124,"Wind Y — PositiveY / external pressure worksheet");
        bool Different(WindDirection a,WindDirection b)=>run.AsNz2021Cases.Where(c=>c.Direction==a).Any(c=>run.AsNz2021Cases.FirstOrDefault(o=>o.Direction==b&&o.LimitState==c.LimitState&&o.Surface==c.Surface&&o.RoofCase==c.RoofCase&&o.Zone==c.Zone&&o.Id.EndsWith(c.Id.EndsWith("Positive")?"Positive":"Negative")) is var o && (c.ExternalPa!=o?.ExternalPa||c.InternalPa!=o?.InternalPa));
        if(Different(WindDirection.PositiveX,WindDirection.NegativeX))Sheet(WindDirection.NegativeX,WindDirection.PositiveY,38,80,"Wind X — NegativeX alternative / independent direction");
        if(Different(WindDirection.PositiveY,WindDirection.NegativeY))Sheet(WindDirection.PositiveX,WindDirection.NegativeY,82,124,"Wind Y — NegativeY alternative / independent direction");
        return sb.Append("</body></html>").ToString();
    }
}
