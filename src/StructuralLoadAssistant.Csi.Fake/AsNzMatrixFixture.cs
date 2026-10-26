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
}

