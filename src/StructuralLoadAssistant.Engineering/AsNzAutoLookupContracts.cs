using System.Collections.Immutable;
namespace StructuralLoadAssistant.Engineering;
public enum AutoLookupState { Resolved,InputRequired,NotApplicable,Ambiguous,OutsideDomain,RightsOrEditionBlocked }
public sealed record AutoLookupValue(string Key,AutoLookupState State,double? Value,string Unit,string Reason,ImmutableArray<string> RecordIds,string Source,string Selection);
public sealed record AsNzAutoSite(string Country,string Region,double Terrain=2,double UlsYears=500,double SlsYears=25,
    bool UniformTerrain=false,bool NoShieldingBenefit=true,bool NoTopographicAmplification=false,bool GeometryReviewed=false,
    string GeometryReason="",string Enclosure="",string Openings="",string Debris="",string Dynamics="",double? CoastDistanceKm=null,
    string System="MWFRS",double TributaryAreaM2=0,double WindwardHeightM=0,double ParapetHeightM=0);
public sealed record AsNzAutoSource(StandardsDataBundle Bundle,string RightsReference,string AdoptionReference,
    ImmutableDictionary<string,string> TableDecisions,bool SoftwareUseAuthorized,bool Synthetic=false);
public sealed record AsNzAutoResult(EngineeringCalculationRequest Request,ImmutableArray<AutoLookupValue> Values,bool NumericalReviewPossible,string Status)
{
    public bool ProductionNormativeQualified=>false;
}
