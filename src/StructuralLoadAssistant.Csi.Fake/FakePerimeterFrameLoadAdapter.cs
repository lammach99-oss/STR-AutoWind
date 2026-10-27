using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Csi.Fake;
/// <summary>In-memory simulation only. This type has no live CSI adapter or COM reference.</summary>
public sealed class FakePerimeterFrameLoadAdapter(CsiModelSnapshot initial)
{
    private readonly string initialHash=SnapshotJson.Hash(initial);
    private readonly Dictionary<string,ImmutableArray<FrameDistributedLoadSegment>> loads=new();
    public ImmutableArray<ExistingLoad> ManualLoads=>initial.ExistingLoads;
    public void Apply(FrameLoadPlan plan,CsiModelSnapshot current)
    {
        if(initial.Product is not ("Fake" or "FAKE CSI"))throw new ArgumentException("Fake assignment refuses real connected model snapshots");
        if(SnapshotJson.Hash(current)!=initialHash)throw new ArgumentException("Fake snapshot changed");
        PerimeterFrameLoadPlanner.Validate(plan,current);
        if(loads.ContainsKey(plan.Pattern))throw new ArgumentException("Pattern already assigned; no implicit replace");
        loads.Add(plan.Pattern,plan.Segments);
    }
    public ImmutableArray<FrameDistributedLoadSegment> Readback(string pattern)=>loads.TryGetValue(pattern,out var rows)?rows:[];
    public bool Verify(FrameLoadPlan plan)=>Readback(plan.Pattern).SequenceEqual(plan.Segments);
}
