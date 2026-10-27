using System.Collections.Immutable;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.App.Wpf;
public sealed partial class MainWindow
{
    private UIElement ExternalInputEditor()
    {
        var p=Stack();var fields=new Dictionary<string,TextBox>();
        TextBox Field(string name,int width=220){var t=new TextBox{Width=width,Margin=new Thickness(3),Padding=new Thickness(4)};fields[name]=t;t.TextChanged+=(_,_)=>{vm.InvalidateExternalReview();vm.Refresh();};p.Children.Add(Row(Text(name+(name.Contains(" N NE")?" (comma separated)":"")),t));return t;}
        p.Children.Add(Text("Enter one reviewed physical pressure zone (SI). Replace current request, or import a multi-face request. Values have no normative defaults."));
        Field("Project / jurisdiction / region / adoption decision",600);
        var country=ComboBoxOf(new[]{"AU","NZ"});var state=ComboBoxOf(Enum.GetNames<LimitState>());var wind=ComboBoxOf(Enum.GetNames<WindDirection>());
        var surface=ComboBoxOf(new[]{"Windward","Leeward","Side","Roof"});var form=ComboBoxOf(new[]{"Flat","Monoslope","Gable","Hip"});var face=ComboBoxOf(Enum.GetNames<AsNzRoofFace>());
        p.Children.Add(Row(Text("Country"),country,Text("State"),state,Text("Wind FROM"),wind));p.Children.Add(Row(Text("Surface"),surface,Text("Roof form"),form,Text("Face"),face));
        foreach(var combo in new[]{country,state,wind,surface,form,face})combo.SelectionChanged+=(_,_)=>{vm.InvalidateExternalReview();vm.Refresh();};
        foreach(string name in new[]{"Zone ID","Alternative ID (keep coupled roof cases consistent)","Pitch deg","Model +X wind-from bearing deg","VR m/s","Mc","rho kg/m3","Cdyn","Cpe","Ka","Kce","Kl","Kp","Participating area m2"})Field(name);
        foreach(string name in new[]{"Md N NE E SE S SW W NW","Mz N NE E SE S SW W NW","Ms N NE E SE S SW W NW","Mt N NE E SE S SW W NW"})Field(name,650);
        Field("Vertices x,y,z; x,y,z; ... (m)",700);Field("Physical outward unit normal x,y,z",400);
        Field("First selected support chain IDs comma separated",650);Field("Second selected support chain IDs comma separated",650);
        Field("Engineer / source document / printed-PDF page-row-column",700);Field("Applicability and structural load-path review reason",700);
        var spans=new CheckBox{Content="I reviewed one-way simply-supported action onto these actual supports",Margin=new Thickness(4)};p.Children.Add(spans);
        p.Children.Add(Button("Use entered face / reviewed supports",()=>vm.Run(()=>
        {
            string T(string k)=>fields[k].Text.Trim();double D(string k)=>double.Parse(T(k),NumberStyles.Float,CultureInfo.InvariantCulture);
            string source=T("Engineer / source document / printed-PDF page-row-column"),reason=T("Applicability and structural load-path review reason");if(string.IsNullOrWhiteSpace(source)||string.IsNullOrWhiteSpace(reason)||spans.IsChecked!=true)throw new ArgumentException("Source and explicit one-way support review required");
            string Pick(ComboBox c)=>c.SelectedItem?.ToString()??throw new ArgumentException("Select all scope fields");
            var st=Enum.Parse<LimitState>(Pick(state));var wd=Enum.Parse<WindDirection>(Pick(wind));var rf=Enum.Parse<AsNzRoofFace>(Pick(face));double pitch=D("Pitch deg");
            string table=Pick(surface) switch{"Windward"=>"Table 5.2(A)","Leeward"=>"Table 5.2(B)","Side"=>"Table 5.2(C)",_=>AsNzRoofApplicability.Evaluate(Pick(form),pitch,rf).Table};
            CoefficientInputEvidence Operand(string key,string unit="dimensionless")=>new(key,D(key),unit,DataProvenance.Unverified,source,key=="Cpe"?table:"Engineer supplied factor",reason,DateTimeOffset.UtcNow);
            ImmutableArray<CoefficientInputEvidence> Eight(string key)=>T(key+" N NE E SE S SW W NW").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select((s,i)=>new CoefficientInputEvidence(key+i,double.Parse(s,CultureInfo.InvariantCulture),"dimensionless",DataProvenance.Unverified,source,"Engineer supplied cardinal factor",reason,DateTimeOffset.UtcNow)).ToImmutableArray();
            Vec3 Vector(string s){var a=s.Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray();if(a.Length!=3)throw new ArgumentException("Three XYZ values required");return new(a[0],a[1],a[2]);}
            string id=T("Zone ID");var polygon=new PressureZonePolygon(id,T("Vertices x,y,z; x,y,z; ... (m)").Split(';',StringSplitOptions.RemoveEmptyEntries).Select(Vector).ToImmutableArray(),Vector(T("Physical outward unit normal x,y,z")),source,reason);
            var speed=new ExternalWindSpeedInput(st,Operand("VR m/s","m/s"),Operand("Mc"),Eight("Md"),Eight("Mz"),Eight("Ms"),Eight("Mt"),Operand("rho kg/m3","kg/m3"),Operand("Cdyn"));
            var s=new ExternalSurfaceInput(id,Pick(surface),Pick(form),pitch,rf,polygon,wd,st,T("Alternative ID (keep coupled roof cases consistent)"),reason,Operand("Cpe"),Operand("Ka"),Operand("Kce"),Operand("Kl"),Operand("Kp"),D("Participating area m2"),"MWFRS");
            var r=new ExternalWindRequest(T("Project / jurisdiction / region / adoption decision"),Pick(country),T("Project / jurisdiction / region / adoption decision"),T("Project / jurisdiction / region / adoption decision"),D("Model +X wind-from bearing deg"),[speed],[s]);
            var support=new TributarySupportReview(id,T("First selected support chain IDs comma separated").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToImmutableArray(),T("Second selected support chain IDs comma separated").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToImmutableArray(),source,reason,true);
            vm.SetExternalInput(new(r,[new($"{st}_{wd}_{id}_{s.Alternative}",support)]));
        }),false));
        return new Expander{Header="Engineer source-labeled face editor (no JSON dataset required)",Content=p};
    }
    private static ComboBox ComboBoxOf(IEnumerable<string> values)=>new(){ItemsSource=values,Width=145,Margin=new Thickness(3)};
}
