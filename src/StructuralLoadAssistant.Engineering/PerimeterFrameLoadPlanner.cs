using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;
public static class PerimeterFrameLoadPlanner
{
    public static void Validate(FrameLoadPlan plan,CsiModelSnapshot model)
    {
        if(plan.ModelSnapshotHash!=SnapshotJson.Hash(model)||string.IsNullOrWhiteSpace(plan.CalculationHash)||string.IsNullOrWhiteSpace(plan.ScenarioId)||!plan.Pattern.StartsWith("EXT_",StringComparison.Ordinal)||plan.Pattern.Contains("NET",StringComparison.OrdinalIgnoreCase)||plan.Segments.IsDefaultOrEmpty||plan.Checks.IsDefaultOrEmpty||plan.Checks.Any(c=>new[]{c.SurfaceForceN.Length,c.SurfaceMomentNm.Length,c.FrameForceN.Length,c.FrameMomentNm.Length}.Any(v=>!double.IsFinite(v))||!c.Passed||!double.IsFinite(c.ForceResidualN)||!double.IsFinite(c.MomentResidualNm)||c.ForceResidualN>0.01||c.MomentResidualNm>0.01))throw new ArgumentException("Stale/incomplete/external-only plan or failed force/moment equilibrium");
        foreach(var s in plan.Segments)
        {
            var frame=model.Frames.SingleOrDefault(f=>f.Id==s.FrameId)??throw new ArgumentException("Frame not selected");
            if(new[]{s.StartFraction,s.EndFraction,s.StartNPerM.X,s.StartNPerM.Y,s.StartNPerM.Z,s.EndNPerM.X,s.EndNPerM.Y,s.EndNPerM.Z}.Any(v=>!double.IsFinite(v))||s.StartFraction< -1e-8||s.EndFraction>1+1e-8||s.EndFraction<=s.StartFraction||(frame.End-frame.Start).Length<=1e-8||model.ExistingLoads.Any(l=>l.ObjectId==s.FrameId&&l.Pattern==plan.Pattern))throw new ArgumentException("Invalid station/vector or existing-pattern conflict");
        }
        for(int i=0;i<plan.Segments.Length;i++)for(int j=i+1;j<plan.Segments.Length;j++)if(plan.Segments[i].FrameId==plan.Segments[j].FrameId&&Math.Min(plan.Segments[i].EndFraction,plan.Segments[j].EndFraction)-Math.Max(plan.Segments[i].StartFraction,plan.Segments[j].StartFraction)>1e-8)throw new ArgumentException("Overlapping intervals require explicit accumulation before export");
        Vec3 sumF=default,sumM=default;Vec3 Add(Vec3 a,Vec3 b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        foreach(var s in plan.Segments){var f=model.Frames.Single(f=>f.Id==s.FrameId);var delta=f.End-f.Start;double length=delta.Length*(s.EndFraction-s.StartFraction);var a=Add(f.Start,delta*s.StartFraction);var b=Add(f.Start,delta*s.EndFraction);sumF=Add(sumF,Add(s.StartNPerM,s.EndNPerM)*(length/2));sumM=Add(sumM,Add(a.Cross(Add(s.StartNPerM*2,s.EndNPerM)),b.Cross(Add(s.StartNPerM,s.EndNPerM*2)))*(length/6));}
        Vec3 expectedF=default,expectedM=default;foreach(var c in plan.Checks){expectedF=Add(expectedF,c.SurfaceForceN);expectedM=Add(expectedM,c.SurfaceMomentNm);}
        if((sumF-expectedF).Length>0.01||(sumM-expectedM).Length>0.01)throw new ArgumentException("Computed segment force/moment does not match declared panel integrals");
    }
}
