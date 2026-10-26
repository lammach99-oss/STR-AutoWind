using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Csi.Fake;
public static class AsNzMatrixFixture
{
    public static EngineeringCalculationRequest Request(bool workbookComparison = false)
    {
        var b = ImmutableDictionary.CreateBuilder<string,CoefficientInputEvidence>();
        void Add(string key,double value) => b[key] = new(key,value,"SI",DataProvenance.SyntheticDemoFixture,"Synthetic independent arithmetic fixture","NOT A STANDARDS LOOKUP","NOT FOR DESIGN",DateTimeOffset.UnixEpoch);
        Add("Mz_cat",1); Add("Ms",1); Add("Mt",1); Add("Mc",1); Add("rho",1.2); Add("Cdyn",1); Add("SlopeBearing",0);
        Add("VR_Uls",40); Add("VR_Sls",20); Add("R_Uls",500); Add("R_Sls",25); Add("Cpi_Positive",0.2); Add("Cpi_Negative",-0.1);
        for(int i=0;i<8;i++) Add("Md_"+i*45,1);
        foreach(var dir in Enum.GetValues<WindDirection>())
        foreach(var surface in new[]{"Windward","Leeward","SideLeft","SideRight","Roof"})
        for(int c=surface=="Roof"?1:0;c<=(surface=="Roof"?2:0);c++)
        for(int z=0;z<(surface=="Roof"?5:surface.StartsWith("Side")?4:1);z++)
        {
            string prefix=$"{dir}_{surface}_{c}_{z}";
            Add(prefix+"_Cpe",surface=="Windward"?0.7:surface=="Roof"?(c==1?-0.6:0.15):-0.4);
            foreach(string factor in new[]{"Ka","Kce","Kl","Kp","Kci","Kv"}) Add(prefix+"_"+factor,1);
        }
        if (workbookComparison)
        {
            void Set(string key, double value) => b[key] = b[key] with { Value = value, SourceNotes = "Owner workbook arithmetic comparison; synthetic surface coefficients; NOT FOR DESIGN" };
            Set("VR_Uls",80); Set("VR_Sls",53); Set("Mz_cat",0.91); Set("Mc",1.05);
            for(int i=0;i<8;i++) Set("Md_"+i*45,0.9);
        }
        return new("matrix-project","AS/NZS 1170.2:2021",[],AsNz2021ReviewCalculation.Procedure,LimitState.Uls,WindDirection.PositiveX,0,"Monoslope","Enclosed",40,3.5,11.44,17.8,3.2,3.8,14,0,"TC2",b.ToImmutable(),new Dictionary<string,string> { ["Region"] = workbookComparison ? "D maximum — illustrative comparison; site applicability unverified" : "SYNTHETIC", ["ParapetHeightM"] = "0" }.ToImmutableDictionary());
    }

    // Values transcribed from OWNER workbook cached cells for arithmetic comparison only.
    // This is not a standards table, lookup or production default.
    public static EngineeringCalculationRequest LegacyWorkbookComparison()
    {
        var req=Request(true);var b=req.ExplicitCoefficients.ToBuilder();
        double[] side=[-0.65,-0.5,-0.3,-0.2],roof1=[-0.9,-0.9,-0.5,-0.3,-0.2],roof2=[-0.4,-0.4,0,0.1,0.2];
        foreach(var direction in Enum.GetValues<WindDirection>())
        {
            bool x=direction is WindDirection.PositiveX or WindDirection.NegativeX;
            void Set(string surface,int c,int z,double value,string cell)
            {
                string key=$"{direction}_{surface}_{c}_{z}_Cpe";
                b[key]=b[key] with {Value=value,Document="Owner AS.NZS_Wind load.xlsm / Flat roof",ClauseOrTable=cell,SourceNotes="LEGACY WORKBOOK COMPARISON / NOT A QUALIFIED STANDARDS LOOKUP"};
            }
            Set("Windward",0,0,0.8,x?"J44":"J88");Set("Leeward",0,0,x?-0.3:-0.3888111888111888,x?"J50":"J94");
            for(int z=0;z<4;z++)foreach(string face in new[]{"SideLeft","SideRight"})Set(face,0,z,side[z],"F"+((x?57:101)+z));
            for(int z=0;z<5;z++){Set("Roof",1,z,roof1[z],"F"+((x?67:111)+z));Set("Roof",2,z,roof2[z],"F"+((x?76:120)+z));}
        }
        return req with { ExplicitCoefficients=b.ToImmutable(),Options=req.Options.SetItem("FixturePurpose","LEGACY WORKBOOK COMPARISON / NOT A QUALIFIED STANDARDS LOOKUP").SetItem("RegionCode","D maximum (comparison)") };
    }
    public static EngineeringCalculationRequest VariedExplicitReview()
    {
        var req=Request(true);var b=req.ExplicitCoefficients.ToBuilder();
        foreach(var key in b.Keys.ToArray())
        {
            if(key.StartsWith("Md_")) {int bearing=int.Parse(key[3..]);b[key]=b[key] with {Value=0.72+bearing/900.0};}
            if(key.EndsWith("_Cpe")) {int zone=int.Parse(key.Split('_')[3]);double direction=key.StartsWith("Negative")?0.08:0; b[key]=b[key] with {Value=b[key].Value+0.025*zone+direction,SourceNotes="Independent arithmetic stress fixture; explicit synthetic selection; NOT NORMATIVE"};}
        }
        return req with {WindAngleDegrees=22.5,ExplicitCoefficients=b.ToImmutable(),Options=req.Options.SetItem("FixturePurpose","2021 EXPLICIT REVIEW / SYNTHETIC FACTORS / APPLICABILITY UNVERIFIED")};
    }
}

