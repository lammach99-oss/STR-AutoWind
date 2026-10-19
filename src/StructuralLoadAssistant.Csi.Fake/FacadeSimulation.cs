using System.Collections.Immutable;
using System.Text.Json.Serialization;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Csi.Fake;

public sealed record FacadeFrameProposal(FrameSegmentLoad Segment, Vec3 GlobalLoadNPerM);
public sealed record FacadeSimulationPreview(WindCalculationRun Calculation, CsiModelSnapshot Model,
    FacadeMethod Method, Vec3 OutwardNormal, ImmutableArray<FacadeFrameProposal> FrameLoads);
public sealed class FacadeSimulationApproval
{
    public FacadeSimulationPreview Preview { get; }
    public string User { get; }
    public DateTimeOffset ApprovedAt { get; }
    public string ContentHash { get; }
    [JsonConstructor]
    internal FacadeSimulationApproval(FacadeSimulationPreview preview,string user,DateTimeOffset approvedAt)
    { Preview=preview; User=user; ApprovedAt=approvedAt; ContentHash=SnapshotJson.Hash(new {Preview,User,ApprovedAt}); }
}
public sealed record FacadeSimulationRun(string Id, FacadeSimulationApproval Approval,
    ImmutableArray<FacadeFrameProposal> FrameLoads, ImmutableArray<ExistingLoad> PreservedManualLoads, bool IsSimulation);
public sealed record FacadeReadbackVerification(string RunId, bool Matches, double ToleranceNPerM, ImmutableArray<string> Issues);

public sealed class FakeFacadeAssignmentService(ICsiReadAdapter source)
{
    private readonly Dictionary<string,FacadeSimulationRun> _runs=new(StringComparer.Ordinal);
    private readonly Dictionary<string,ImmutableArray<FacadeFrameProposal>> _frameLoads=new(StringComparer.Ordinal);
    private readonly object _sync=new();
    public async Task<FacadeSimulationPreview> PreviewAsync(WindCalculationRun calculation,IEnumerable<FrameSegmentLoad> contributions,Vec3 outwardNormal,FacadeMethod method,CancellationToken cancellationToken=default)
    {
        var model=await _source.ScanModelAsync(cancellationToken);
        ImmutableArray<FrameSegmentLoad> segments;
        try { segments=FacadeMapper.Accumulate(contributions); }
        catch(ArgumentException ex) { throw new InvalidOperationException("Invalid facade contributions",ex); }
        var preview=new FacadeSimulationPreview(calculation,model,method,outwardNormal.Unit(),segments.Select(s=>new FacadeFrameProposal(s,EngineeringMath.GlobalForce(s.LoadNPerM,outwardNormal))).ToImmutableArray());
        Validate(preview,model); return preview;
    }
    public async Task<FacadeSimulationApproval> ApproveAsync(FacadeSimulationPreview preview,string user,DateTimeOffset? time=null,CancellationToken cancellationToken=default)
    {
        if(string.IsNullOrWhiteSpace(user)) throw new InvalidOperationException("Approval user required");
        var model=await _source.ScanModelAsync(cancellationToken);
        Validate(preview,model);
        return new(preview,user,time??DateTimeOffset.UtcNow);
    }
    public async Task<FacadeSimulationRun> ApplyAsync(FacadeSimulationApproval approval,WindCalculationRun currentCalculation,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var model=await _source.ScanModelAsync(cancellationToken);
        Validate(approval.Preview,model);
        if(SnapshotJson.Hash(currentCalculation)!=SnapshotJson.Hash(approval.Preview.Calculation)) throw new InvalidOperationException("Calculation changed; create a new approval");
        var id="facade-simulation-"+approval.ContentHash;
        lock(_sync)
        {
            if(_runs.TryGetValue(id,out var prior)) return prior;
            foreach(var proposal in approval.Preview.FrameLoads)
            {
                var p=proposal.Segment;
                if(_frameLoads.Values.SelectMany(x=>x).Any(x=>x.Segment.FrameId==p.FrameId && x.Segment.Pattern==p.Pattern && x.Segment.StartFraction<p.EndFraction && x.Segment.EndFraction>p.StartFraction))
                    throw new InvalidOperationException("An owned facade load occupies this frame/pattern/segment; explicit conflict resolution required");
            }
            var run=new FacadeSimulationRun(id,approval,approval.Preview.FrameLoads,model.ExistingLoads.Where(x=>x.Owner=="Manual").ToImmutableArray(),true);
            _frameLoads.Add(id,run.FrameLoads); _runs.Add(id,run); return run;
        }
    }
    public Task<FacadeReadbackVerification> VerifyAsync(string runId,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock(_sync)
        {
            if(!_runs.TryGetValue(runId,out var run) || !_frameLoads.TryGetValue(runId,out var stored)) throw new InvalidOperationException("Unknown facade simulation run");
            var issues=ImmutableArray.CreateBuilder<string>();
            if(stored.Length!=run.FrameLoads.Length) issues.Add("Frame load count mismatch");
            foreach(var p in run.FrameLoads)
            {
                var matches=stored.Where(x=>x.Segment.FrameId==p.Segment.FrameId && x.Segment.Pattern==p.Segment.Pattern && x.Segment.StartFraction==p.Segment.StartFraction && x.Segment.EndFraction==p.Segment.EndFraction).ToArray();
                if(matches.Length!=1 || (matches[0].GlobalLoadNPerM-p.GlobalLoadNPerM).Length>1e-6 || Math.Abs(matches[0].Segment.LoadNPerM-p.Segment.LoadNPerM)>1e-6) issues.Add("Readback mismatch: "+p.Segment.FrameId);
            }
            return Task.FromResult(new FacadeReadbackVerification(runId,issues.Count==0,1e-6,issues.ToImmutable()));
        }
    }
    private static void Validate(FacadeSimulationPreview preview,CsiModelSnapshot current)
    {
        if(current.Product!="FAKE CSI" || preview.Model.Product!="FAKE CSI") throw new InvalidOperationException("Fake models only");
        if(SnapshotJson.Hash(current)!=SnapshotJson.Hash(preview.Model)) throw new InvalidOperationException("Stale model snapshot; create a new preview and approval");
        var cal=preview.Calculation;
        if(cal.Provenance!=DataProvenance.SyntheticDemoFixture || string.IsNullOrWhiteSpace(cal.Id) || string.IsNullOrWhiteSpace(cal.ManifestHash) || cal.Profile.IsDefaultOrEmpty || cal.Profile.Any(x=>!double.IsFinite(x.ElevationM)||!double.IsFinite(x.PressurePa)))
            throw new InvalidOperationException("A valid, explicitly synthetic source calculation is required");
        if(!Enum.IsDefined(preview.Method) || preview.FrameLoads.IsDefaultOrEmpty || !double.IsFinite(preview.OutwardNormal.Length) || Math.Abs(preview.OutwardNormal.Length-1)>1e-8) throw new InvalidOperationException("Invalid facade method, normal or empty proposal");
        foreach(var proposal in preview.FrameLoads)
        {
            var p=proposal.Segment; var frame=current.Frames.SingleOrDefault(f=>f.Id==p.FrameId);
            if(frame is null || !current.Patterns.Contains(p.Pattern) || p.State!=cal.State || !double.IsFinite(p.LoadNPerM) || !double.IsFinite(p.StartFraction) || !double.IsFinite(p.EndFraction) || p.StartFraction<0 || p.EndFraction>1 || p.StartFraction>=p.EndFraction)
                throw new InvalidOperationException("Invalid frame, existing load pattern, wind state or segment");
            var delta=frame.End-frame.Start;
            if(!double.IsFinite(delta.Length)||delta.Length<1e-8||Math.Abs(delta.Z)>1e-8 || Math.Abs(delta.Unit().Dot(preview.OutwardNormal))>1e-8) throw new InvalidOperationException("Horizontal facade frame and perpendicular physical normal required");
            var expected=EngineeringMath.GlobalForce(p.LoadNPerM,preview.OutwardNormal);
            if(!double.IsFinite(proposal.GlobalLoadNPerM.Length) || (expected-proposal.GlobalLoadNPerM).Length>1e-6) throw new InvalidOperationException("Signed global force mismatch");
            if(current.ExistingLoads.Any(x=>x.ObjectId==p.FrameId&&x.Pattern==p.Pattern&&x.Owner!="Manual")) throw new InvalidOperationException("Existing owned frame load conflicts");
        }
        if(preview.FrameLoads.GroupBy(x=>(x.Segment.FrameId,x.Segment.Pattern)).Any(group=>group.Any(a=>group.Any(b=>!ReferenceEquals(a,b)&&a.Segment.StartFraction<b.Segment.EndFraction&&a.Segment.EndFraction>b.Segment.StartFraction))))
            throw new InvalidOperationException("Overlapping proposal segments require accumulation");
    }
    // Keep the injected read source explicit: no live write interface is accepted.
    private readonly ICsiReadAdapter _source=source;
}
