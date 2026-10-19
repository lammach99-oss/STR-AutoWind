using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public sealed record FacadePairSelection(string LowerFrameId, string UpperFrameId,
    double LowerStartFraction, double LowerEndFraction, double UpperStartFraction, double UpperEndFraction,
    double LowerElevationM, double UpperElevationM);
public sealed record FacadeSelectionPreview(DataProvenance Provenance, ImmutableArray<FrameSegmentLoad> Loads);
/// <summary>Explicit geometry review only. Pressure preview requires an explicit caller-supplied function.</summary>
public sealed class FacadeSelection
{
    public ImmutableArray<FrameGeometry> Frames { get; private set; }
    public ImmutableArray<FacadePairSelection> Pairs { get; private set; } = [];
    public ImmutableArray<EngineeringOverride> GroupOverrides { get; private set; } = [];
    public FacadeSelection(IEnumerable<FrameGeometry> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);Frames=frames.ToImmutableArray();
        if(Frames.Any(f=>f is null||string.IsNullOrWhiteSpace(f.Id))||Frames.GroupBy(f=>f.Id,StringComparer.Ordinal).Any(g=>g.Count()>1))
            throw new ArgumentException("Selected frames require unique nonempty identifiers.",nameof(frames));
    }
    private FrameGeometry Find(string id) => Frames.FirstOrDefault(f=>f.Id==id) ?? throw new ArgumentException("Frame is not in the current selection.",nameof(id));
    public void SelectPair(string lowerId, string upperId)
    {
        var lower=Find(lowerId);var upper=Find(upperId);
        ValidateFrame(lower);ValidateFrame(upper);
        if(lowerId==upperId||lower.FacadeId!=upper.FacadeId||upper.Start.Z-lower.Start.Z<1e-8)
            throw new ArgumentException("NeedsManualMapping: incompatible facade or elevations.");
        var vector=lower.End-lower.Start;var direction=vector.Unit();var upperVector=upper.End-upper.Start;
        var offset=new Vec3(upper.Start.X-lower.Start.X,upper.Start.Y-lower.Start.Y,0);
        if(Math.Abs(direction.Dot(upperVector.Unit()))<1-1e-8||(offset-direction*offset.Dot(direction)).Length>1e-7)
            throw new ArgumentException("NeedsManualMapping: projected spans are not collinear.");
        double a=offset.Dot(direction), b=a+upperVector.Dot(direction);
        double start=Math.Max(0,Math.Min(a,b)),end=Math.Min(vector.Length,Math.Max(a,b));
        if(end-start<=1e-8)throw new ArgumentException("NeedsManualMapping: no genuine projected overlap.");
        var u1=(start-a)/(b-a);var u2=(end-a)/(b-a);
        var pair=new FacadePairSelection(lowerId,upperId,start/vector.Length,end/vector.Length,Math.Min(u1,u2),Math.Max(u1,u2),lower.Start.Z,upper.Start.Z);
        if(!Pairs.Any(p=>p.LowerFrameId==lowerId&&p.UpperFrameId==upperId))Pairs=Pairs.Add(pair);
    }
    private static void ValidateFrame(FrameGeometry frame)
    {
        Numeric.Required(frame.FacadeId);Numeric.Vector(frame.Start);Numeric.Vector(frame.End);
        if(Math.Abs(frame.Start.Z-frame.End.Z)>1e-8||(frame.End-frame.Start).Length<=1e-8)
            throw new ArgumentException("NeedsManualMapping: horizontal nondegenerate frames required.");
    }
    public void RemovePair(string lowerId, string upperId) => Pairs=Pairs.RemoveAll(p=>p.LowerFrameId==lowerId&&p.UpperFrameId==upperId);
    public void ClearPairs() => Pairs=[];
    public void OverrideGroup(string frameId,string groupId,string user,string reason)
    {
        Numeric.Required(groupId);Numeric.Required(user);Numeric.Required(reason);var frame=Find(frameId);
        var audit=new EngineeringOverride(frameId,"FacadeId",frame.FacadeId,groupId,user,DateTimeOffset.UtcNow,reason);
        Frames=Frames.SetItem(Frames.IndexOf(frame),frame with{FacadeId=groupId});GroupOverrides=GroupOverrides.Add(audit);ClearPairs();
    }
    public FacadeSelectionPreview Preview(Func<double,double> pressurePa,double groundDatumM,WindState state,string pattern,FacadeMethod method)
    {
        ArgumentNullException.ThrowIfNull(pressurePa);Numeric.Finite(groundDatumM);ArgumentNullException.ThrowIfNull(state);Numeric.Required(pattern);
        if(!Enum.IsDefined(method))throw new ArgumentException("Unknown facade method.",nameof(method));
        var loads=Pairs.SelectMany(p=>FacadeMapper.MapPair(Find(p.LowerFrameId),Find(p.UpperFrameId),pressurePa,groundDatumM,state,pattern,method));
        return new(DataProvenance.Unverified,FacadeMapper.Accumulate(loads));
    }
}
