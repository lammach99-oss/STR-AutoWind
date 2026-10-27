using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.RoofMapping;
namespace StructuralLoadAssistant.Application;
public sealed record ExternalTransferSelection(string CaseId,TributarySupportReview Supports);
public sealed record ExternalWindPreview(ExternalWindRun Run,CsiModelSnapshot Model,ImmutableArray<FrameLoadPlan> Plans,ImmutableArray<string> Blockers)
{
    public string ContentHash=>SnapshotJson.Hash(new{Run=Run.ContentHash,Model,Plans,Blockers});
    public bool CanMutateCsi=>false;
}
public static class ExternalWindReviewCoordinator
{
    public static double NativeLineIntensity(double nPerM,StructuralLoadAssistant.Csi.Abstractions.CsiUnits units)
    {
        if(!double.IsFinite(nPerM)||!double.IsFinite(units.LengthToM)||!double.IsFinite(units.ForceToN)||units.LengthToM<=0||units.ForceToN<=0)throw new ArgumentException("Verified native force/length units required");
        return nPerM*units.LengthToM/units.ForceToN;
    }
    public static Task<ExternalWindPreview> PreviewReadOnlyAsync(ExternalWindRequest request,StructuralLoadAssistant.Csi.Abstractions.CsiReadScan scan,ImmutableArray<ExternalTransferSelection> transfers,CancellationToken ct=default)
    {
        if(scan.Outcome!=StructuralLoadAssistant.Csi.Abstractions.CsiScanOutcome.Succeeded||!scan.Capabilities.IsReadOnly||!scan.Capabilities.FrameDistributedLoads||scan.Issues.Any(i=>i.Severity==StructuralLoadAssistant.Csi.Abstractions.CsiIssueSeverity.Error))throw new ArgumentException("Complete read-only selection and existing frame-load inspection required");
        _=NativeLineIntensity(1,scan.Units);
        return PreviewAsync(request,scan.Model,transfers,ct);
    }
    public static Task<ExternalWindPreview> PreviewAsync(ExternalWindRequest request,CsiModelSnapshot model,ImmutableArray<ExternalTransferSelection> transfers,CancellationToken ct=default)=>Task.Run(()=>
    {
        var run=AsNzExternalPressureService.Execute(request,ct);
        var plans=ImmutableArray.CreateBuilder<FrameLoadPlan>();var blockers=ImmutableArray.CreateBuilder<string>();
        if(transfers.Select(t=>t.CaseId).Distinct().Count()!=transfers.Length)throw new ArgumentException("Duplicate case transfer");
        var overlap=new HashSet<string>();
        for(int i=0;i<request.Surfaces.Length;i++)for(int j=i+1;j<request.Surfaces.Length;j++)
        {
            var a=request.Surfaces[i];var b=request.Surfaces[j];
            if(a.State!=b.State||a.WindFrom!=b.WindFrom||(a.Alternative!=b.Alternative&&a.Alternative!="COMMON"&&b.Alternative!="COMMON")||Math.Abs(a.Polygon.OutwardNormal.Dot(b.Polygon.OutwardNormal))<1-1e-7)continue;
            if(a.Polygon.Vertices.IsDefaultOrEmpty||b.Polygon.Vertices.IsDefaultOrEmpty||Math.Abs((a.Polygon.Vertices[0]-b.Polygon.Vertices[0]).Dot(a.Polygon.OutwardNormal))>1e-6)continue;
            try{if(PressureSurfaceTributaryMapper.Intersect(a.Polygon,b.Polygon.Vertices).Length>0){overlap.Add($"{a.State}_{a.WindFrom}_{a.Id}_{a.Alternative}");overlap.Add($"{b.State}_{b.WindFrom}_{b.Id}_{b.Alternative}");}}catch(ArgumentException){overlap.Add($"{a.State}_{a.WindFrom}_{a.Id}_{a.Alternative}");}
        }
        foreach(var transfer in transfers)
        {
            ct.ThrowIfCancellationRequested();
            var c=run.Cases.SingleOrDefault(c=>c.Id==transfer.CaseId)??throw new ArgumentException("Unknown calculation case");
            if(overlap.Contains(c.Id)){blockers.Add(c.Id+": OVERLAPPING_SOURCE / transfer blocked");continue;}
            if(c.PressurePa is not double p){blockers.Add(c.Id+": pressure blocked");continue;}
            try
            {
                var s=request.Surfaces.Single(s=>$"{s.State}_{s.WindFrom}_{s.Id}_{s.Alternative}"==c.Id);
                var mapped=PressureSurfaceTributaryMapper.Map(s.Polygon,p,model.Frames,transfer.Supports);
                var plan=new FrameLoadPlan(SnapshotJson.Hash(model),run.ContentHash,"EXT_"+SnapshotJson.Hash(c.Id)[..16],c.Id,mapped.Loads,[mapped.Check]){SupportReviews=[transfer.Supports]};
                PerimeterFrameLoadPlanner.Validate(plan,model);plans.Add(plan);
            }
            catch(ArgumentException e){blockers.Add(c.Id+": TRANSFER_UNSUPPORTED: "+e.Message);}
        }
        foreach(var c in run.Cases.Where(c=>!transfers.Any(t=>t.CaseId==c.Id)))blockers.Add(c.Id+": UNASSIGNED / no reviewed transfer");
        var expanded=plans.SelectMany(p=>{var c=run.Cases.Single(c=>c.Id==p.ScenarioId);var alternatives=run.Cases.Where(o=>o.State==c.State&&o.WindFrom==c.WindFrom&&o.Alternative!="COMMON").Select(o=>o.Alternative).Distinct().DefaultIfEmpty("COMMON").ToArray();return c.Alternative=="COMMON"?alternatives.Select(a=>(Plan:p,Key:$"{c.State}_{c.WindFrom}_{a}")):new[]{(Plan:p,Key:$"{c.State}_{c.WindFrom}_{c.Alternative}")};}).ToArray();
        var grouped=ImmutableArray.CreateBuilder<FrameLoadPlan>();
        foreach(var group in expanded.GroupBy(p=>p.Key,p=>p.Plan))
        {
            var segments=ImmutableArray.CreateBuilder<FrameDistributedLoadSegment>();
            foreach(var frameGroup in group.SelectMany(p=>p.Segments).GroupBy(s=>s.FrameId))
            {
                var knots=frameGroup.SelectMany(s=>new[]{s.StartFraction,s.EndFraction}).Distinct().Order().ToArray();
                for(int i=0;i<knots.Length-1;i++)
                {
                    double a=knots[i],b=knots[i+1];var active=frameGroup.Where(s=>s.StartFraction<b-1e-10&&s.EndFraction>a+1e-10).ToArray();if(active.Length==0)continue;
                    Vec3 At(double x){Vec3 sum=default;foreach(var s in active){double t=(x-s.StartFraction)/(s.EndFraction-s.StartFraction);var v=new Vec3(s.StartNPerM.X+(s.EndNPerM.X-s.StartNPerM.X)*t,s.StartNPerM.Y+(s.EndNPerM.Y-s.StartNPerM.Y)*t,s.StartNPerM.Z+(s.EndNPerM.Z-s.StartNPerM.Z)*t);sum=new(sum.X+v.X,sum.Y+v.Y,sum.Z+v.Z);}return sum;}
                    segments.Add(new(frameGroup.Key,a,b,At(a),At(b),string.Join("+",active.Select(s=>s.ZoneId).Distinct())));
                }
            }
            var combined=new FrameLoadPlan(SnapshotJson.Hash(model),run.ContentHash,"EXT_"+SnapshotJson.Hash(group.Key)[..16],group.Key,segments.ToImmutable(),group.SelectMany(p=>p.Checks).ToImmutableArray()){SupportReviews=group.SelectMany(p=>p.SupportReviews).ToImmutableArray()};
            try{PerimeterFrameLoadPlanner.Validate(combined,model);grouped.Add(combined);}catch(ArgumentException e){blockers.Add(group.Key+": TRANSFER_UNSUPPORTED: "+e.Message);}
        }
        return new ExternalWindPreview(run,model,grouped.ToImmutable(),blockers.ToImmutable());
    },ct);
}
