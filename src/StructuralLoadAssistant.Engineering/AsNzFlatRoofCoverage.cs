using System.Collections.Immutable;
using System.Text.Json;
namespace StructuralLoadAssistant.Engineering;
public sealed record AsNzFlatRoofScenario(string ScenarioId,EngineeringCalculationRequest Geometry,AsNzAutoSite Site);
public sealed record AsNzFlatRoofCoverageReport(string Status,bool ScenarioInputsResolved,bool Synthetic,string DatasetHash,
    ImmutableArray<AutoLookupValue> Lookups,ImmutableArray<string> Blockers)
{
    public AsNzFlatRoofScenario? Scenario {get;init;}
    public string DatasetId {get;init;}="";
    public string Scope=>"Explicit Flat Roof scenario selector inputs only; not full table coverage, legal acceptance or engineering qualification";
    public bool FullTableCoverageEstablished=>false;
    public bool IndependentEngineeringQualified=>false;
    public bool CanCalculateNormativePressures=>false;
    public bool CanMutateCsi=>false;
}
public static class AsNzFlatRoofCoverage
{
    public static AsNzFlatRoofCoverageReport Evaluate(AsNzFlatRoofScenario scenario,AsNzAutoSource? source)
    {
        AsNzFlatRoofCoverageReport Block(string reason)=>new("SCENARIO_BLOCKED",false,source?.Synthetic==true,source?.Bundle.Manifest.ContentHash??"",[],[reason]) {Scenario=scenario,DatasetId=source?.Bundle.Manifest.BundleId??""};
        if(scenario?.Geometry==null||scenario.Site==null||string.IsNullOrWhiteSpace(scenario.ScenarioId))return Block("Explicit scenario ID, geometry and site required");
        var geometry=scenario.Geometry;
        if(geometry.RoofForm!="Flat"||geometry.RoofSlopeDegrees!=0)return Block("Coverage assessment supports explicit flat roofs at zero pitch only; other workflows are not assessed");
        if(new[]{geometry.BuildingLengthM,geometry.BuildingWidthM,geometry.ReferenceHeightM,geometry.EaveHeightM,geometry.RidgeHeightM}.Any(v=>!double.IsFinite(v)||v<=0)||geometry.EaveHeightM!=geometry.RidgeHeightM)
            return Block("Finite positive, consistent flat-roof dimensions required");
        try
        {
            var selected=new AsNz2021LocalAutoLookupService().Resolve(geometry,scenario.Site,source);
            var blockers=selected.Values.Where(v=>v.State!=AutoLookupState.Resolved).Select(v=>v.Key+": "+v.State+" / "+v.Reason).ToImmutableArray();
            bool resolved=selected.NumericalReviewPossible&&selected.Values.Length>0&&blockers.Length==0;
            if(!resolved&&blockers.Length==0)blockers=["Scenario selector could not establish complete required inputs"];
            return new(resolved?"SCENARIO_INPUTS_RESOLVED_FOR_REVIEW":"SCENARIO_BLOCKED",resolved,source?.Synthetic==true,source?.Bundle.Manifest.ContentHash??"",selected.Values,blockers) {Scenario=scenario,DatasetId=source?.Bundle.Manifest.BundleId??""};
        }
        catch(Exception e)when(e is ArgumentException or InvalidOperationException or JsonException or NullReferenceException)
        {return Block("Scenario evidence invalid: "+e.Message);}
    }
    public static AsNzFlatRoofCoverageReport EvaluateFiles(string privatePackageDirectory,string scenarioFile)
    {
        try
        {
            var scenario=JsonSerializer.Deserialize<AsNzFlatRoofScenario>(File.ReadAllText(scenarioFile),AsNzPrivateStagingJson.Options)??throw new InvalidDataException("Null scenario");
            try{return Evaluate(scenario,AsNzPrivateAutoSourceLoader.Load(privatePackageDirectory));}
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or NullReferenceException)
            {return Evaluate(scenario,null) with {Blockers=["Private package unavailable or invalid: "+e.Message]};}
        }
        catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException)
        {return new("SCENARIO_BLOCKED",false,false,"",[],["Scenario file unavailable or invalid: "+e.Message]);}
    }
}
