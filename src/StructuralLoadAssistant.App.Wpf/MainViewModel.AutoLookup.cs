using System.Collections.Immutable;
using System.Globalization;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.App.Wpf;
public sealed partial class MainViewModel
{
    private AsNzAutoSource? autoSource;
    private string autoCountry="AU",autoRegion="",autoTerrain="2",autoUls="500",autoSls="25",autoRoof="Flat",autoBearing="0",autoLength="",autoWidth="",autoHeight="",autoGround="0",autoPitch="0",autoReason="",autoOpenings="",autoDebris="",autoArea="",autoWallHeight="",autoParapet="0",autoCoast="";
    private bool autoUniform,autoTopo,autoRigid,autoGeometry;
    void AutoChanged(){SetCalculationRequest(null);Refresh();}
    void AutoText(ref string field,string value){if(field==value)return;field=value;AutoChanged();}
    void AutoBool(ref bool field,bool value){if(field==value)return;field=value;AutoChanged();}
    public string AutoCountry {get=>autoCountry;set{AutoText(ref autoCountry,value);autoRegion="";Refresh();}}
    public string[] AutoRegions=>autoSource?.Bundle.Records.Where(r=>r.CategoricalConditions.GetValueOrDefault("Country")==AutoCountry).Select(r=>r.CategoricalConditions.GetValueOrDefault("Region","")).Where(s=>s!="").Distinct().Order().ToArray()??[];
    public string AutoRegion {get=>autoRegion;set=>AutoText(ref autoRegion,value);}
    public string AutoTerrain {get=>autoTerrain;set=>AutoText(ref autoTerrain,value);}
    public string AutoUls {get=>autoUls;set=>AutoText(ref autoUls,value);}
    public string AutoSls {get=>autoSls;set=>AutoText(ref autoSls,value);}
    public string AutoRoof {get=>autoRoof;set=>AutoText(ref autoRoof,value);}
    public string AutoBearing {get=>autoBearing;set=>AutoText(ref autoBearing,value);}
    public string AutoLength {get=>autoLength;set=>AutoText(ref autoLength,value);}
    public string AutoWidth {get=>autoWidth;set=>AutoText(ref autoWidth,value);}
    public string AutoHeight {get=>autoHeight;set=>AutoText(ref autoHeight,value);}
    public string AutoGround {get=>autoGround;set=>AutoText(ref autoGround,value);}
    public string AutoPitch {get=>autoPitch;set=>AutoText(ref autoPitch,value);}
    public string AutoReason {get=>autoReason;set=>AutoText(ref autoReason,value);}
    public string AutoOpenings {get=>autoOpenings;set=>AutoText(ref autoOpenings,value);}
    public string AutoDebris {get=>autoDebris;set=>AutoText(ref autoDebris,value);}
    public string AutoArea {get=>autoArea;set=>AutoText(ref autoArea,value);}
    public string AutoWallHeight {get=>autoWallHeight;set=>AutoText(ref autoWallHeight,value);}
    public string AutoParapet {get=>autoParapet;set=>AutoText(ref autoParapet,value);}
    public string AutoCoast {get=>autoCoast;set=>AutoText(ref autoCoast,value);}
    public bool AutoUniform {get=>autoUniform;set=>AutoBool(ref autoUniform,value);}
    public bool AutoTopo {get=>autoTopo;set=>AutoBool(ref autoTopo,value);}
    public bool AutoRigid {get=>autoRigid;set=>AutoBool(ref autoRigid,value);}
    public bool AutoGeometry {get=>autoGeometry;set=>AutoBool(ref autoGeometry,value);}
    public string AutoSourceStatus {get;private set;}="LOCAL SOURCE DATA UNAVAILABLE / OWNER RIGHTS OR AMENDMENT EVIDENCE REQUIRED";
    public void InstallPrivateAutoSource(string folder){autoSource=null;AutoChanged();autoSource=AsNzPrivateAutoSourceLoader.Load(folder);AutoSourceStatus="PRIVATE REVIEW / NOT QUALIFIED FOR DESIGN / "+autoSource.Bundle.Manifest.BundleId;Refresh();}
    public void InstallSyntheticAutoSmokeSource(){autoSource=StructuralLoadAssistant.Csi.Fake.AsNzAutoLookupFixture.Source();AutoSourceStatus="SYNTHETIC / NOT FOR DESIGN";AutoChanged();}
    public async Task CalculateAutoWindAsync(CancellationToken ct=default)
    {
        RequireDemo();
        double Read(string v)=>double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:throw new InvalidOperationException("Finite metric inputs required");
        double h=Read(AutoHeight),ground=Read(AutoGround);
        var request=new EngineeringCalculationRequest(Session.Project.Id,"AS/NZS 1170.2:2021",autoSource?.Bundle.Manifest.Amendments??[],AsNz2021ReviewCalculation.Procedure,LimitState.Uls,WindDirection.PositiveX,Read(AutoBearing),AutoRoof,"Enclosed",0,h,Read(AutoLength),Read(AutoWidth),ground+h,ground+h,Read(AutoPitch),ground,"TC"+AutoTerrain,ImmutableDictionary<string,CoefficientInputEvidence>.Empty,ImmutableDictionary<string,string>.Empty);
        var site=new AsNzAutoSite(AutoCountry,AutoRegion,Read(AutoTerrain),Read(AutoUls),Read(AutoSls),AutoUniform,true,AutoTopo,AutoGeometry,AutoReason,"Enclosed",AutoOpenings,AutoDebris,AutoRigid?"RigidAssessed":"",string.IsNullOrWhiteSpace(AutoCoast)?null:Read(AutoCoast),TributaryAreaM2:Read(AutoArea),WindwardHeightM:Read(AutoWallHeight),ParapetHeightM:Read(AutoParapet));
        var source=autoSource;
        // Yield before lookup; edits invalidate the generation token while work runs.
        long generation=System.Threading.Interlocked.Read(ref _currentRequestId);
        var resolved=await Task.Run(()=>new AsNz2021LocalAutoLookupService().Resolve(request,site,source),ct);
        if(generation!=System.Threading.Interlocked.Read(ref _currentRequestId))return;
        AutoSourceStatus=resolved.Status;SetCalculationRequest(resolved.Request);
        await CalculateEngineeringRunAsync(ct);
    }
}
