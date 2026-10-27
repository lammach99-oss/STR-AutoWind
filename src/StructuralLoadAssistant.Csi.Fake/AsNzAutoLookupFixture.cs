using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Csi.Fake;
/// <summary>Invented test numbers only. Never a standards dataset.</summary>
public static class AsNzAutoLookupFixture
{
    public static EngineeringCalculationRequest Geometry()=>AsNzMatrixFixture.Request() with {RoofForm="Flat",RoofSlopeDegrees=0,EaveHeightM=4,RidgeHeightM=4,ReferenceHeightM=4,BuildingLengthM=30,BuildingWidthM=20};
    public static AsNzAutoSite Site()=>new("AU","SYNTHETIC",UniformTerrain:true,NoTopographicAmplification:true,GeometryReviewed:true,GeometryReason:"Synthetic rectangular test geometry",Enclosure:"Enclosed",Openings:"TEST",Debris:"TEST",Dynamics:"RigidAssessed",TributaryAreaM2:10,WindwardHeightM:4,TrueNorthReviewed:true);
    public static AsNzAutoResult Resolve()=>new AsNz2021LocalAutoLookupService().Resolve(Geometry(),Site(),Source());
    public static AsNzAutoSource Source()
    {
        var records=ImmutableArray.CreateBuilder<CoefficientRecord>();
        foreach(var state in new[]{LimitState.Uls,LimitState.Sls})
        {
            var common=new Dictionary<string,string>{{"Country","AU"},{"Region","SYNTHETIC"},{"System","MWFRS"},{"ProcedureId",AsNz2021LocalAutoLookupService.Method},{"Method","FlatRoof"},{"RoofForm","Flat"},{"LimitState",state.ToString()}};
            void Add(string category,double value,string unit="dimensionless",Dictionary<string,string>? extra=null,Dictionary<string,double>? nums=null,InterpolationPolicy policy=InterpolationPolicy.None)
            {
                var cat=common.ToImmutableDictionary();if(extra!=null)cat=cat.SetItems(extra);
                records.Add(new("SYNTHETIC-"+records.Count,"Synthetic "+category,"Test arithmetic only",category,cat,(nums??new()).ToImmutableDictionary(),value,unit,"signed",policy));
            }
            Add("VR",state==LimitState.Uls?40:20,"m/s",nums:new(){{"R",state==LimitState.Uls?500:25}});
            Add("Mc",1);Add("Cdyn",1,extra:new(){{"Dynamics","RigidAssessed"}});Add("rho",1.2,"kg/m3");
            if(state==LimitState.Uls)Add("MinimumSpeed",0,"m/s");
            foreach(double height in new[]{2.0,6})Add("Mz",height==2?1:1.25,extra:new(){{"Terrain","2"}},nums:new(){{"Height",height}},policy:InterpolationPolicy.Linear1D);
            for(int bearing=0;bearing<360;bearing+=45)Add("Md",1,nums:new(){{"Bearing",bearing}});
            foreach(string sign in new[]{"Positive","Negative"})Add("Cpi",sign=="Positive"?0.2:-0.1,extra:new(){{"Enclosure","Enclosed"},{"Openings","TEST"},{"Debris","TEST"},{"InternalBranch","General"},{"Sign",sign}},nums:new(){{"Height",4}});
            foreach(string surface in new[]{"Windward","Leeward","SideLeft","SideRight","Roof"})
            for(int c=surface=="Roof"?1:0;c<=(surface=="Roof"?2:0);c++)
            for(int z=0;z<(surface=="Roof"?5:surface.StartsWith("Side")?4:1);z++)
            {
                var extra=new Dictionary<string,string>{{"Surface",surface},{"Case",c.ToString()},{"Zone",z.ToString()}};
                double[] ratios=surface=="Leeward"?new[]{1.5,20.0/30}:surface=="Roof"?new[]{4.0/30,0.2}:new[]{4.0};
                foreach(double ratio in ratios)Add("Cpe",surface=="Windward"?0.7:surface=="Roof"?(c==1?-0.6:0.15):-0.4,extra:extra,nums:surface=="Windward"?new(){{"Height",ratio}}:surface=="Leeward"?new(){{"db",ratio}}:surface=="Roof"?new(){{"hd",ratio}}:null);
                foreach(string factor in new[]{"Ka","Kce","Kl","Kp","Kci","Kv"})Add(factor,1,extra:extra,nums:factor=="Ka"?new(){{"Area",10}}:factor=="Kl"?new(){{"Parapet",0}}:null);
            }
        }
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(records.ToImmutable());
        var review=new EngineeringReviewTrace("Synthetic test","Test",DateTimeOffset.UnixEpoch,"","",null,"NOT REVIEWED","","",false);
        var manifest=new StandardsDataBundleManifest("SYNTHETIC-AUTO-ONLY","1.0",StandardFamily.AsNz1170_2,"AS/NZS 1170.2:2021",[],DataRightsClassification.SyntheticTestData,"Invented test numbers", "",ImmutableDictionary<string,string>.Empty.Add("records.json",Convert.ToHexString(SHA256.HashData(bytes))),review,[],"Synthetic",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"3.0");
        var raw=ImmutableDictionary<string,ImmutableArray<byte>>.Empty.Add("records.json",bytes.ToImmutableArray());
        var bundle=new StandardsDataBundle(manifest,records.ToImmutable(),rawComponentBytes:raw,isBytesVerified:true);
        bundle=new(manifest with {ContentHash=bundle.ComputeCanonicalHash()},bundle.Records,rawComponentBytes:raw,isBytesVerified:true);
        return new(bundle,"Synthetic rights-cleared","Test only",ImmutableDictionary<string,string>.Empty,true,true);
    }
}
