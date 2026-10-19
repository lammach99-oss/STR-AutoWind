using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.RoofMapping;

public sealed class RoofMappingWorkflow
{
    private WindCalculationRun calculation;
    private CsiModelSnapshot model;
    private ImmutableArray<RoofPressureZone> zones;
    private readonly bool simulation;
    private readonly StandardManifest? manifest;
    private readonly Dictionary<string,string> patterns=new(StringComparer.Ordinal);
    private readonly List<EngineeringOverride> overrides=[];
    private readonly List<RoofScopeExclusion> exclusions=[];
    private bool scanned;
    private int revision=1;
    public RoofWorkflowState State {get;private set;}=RoofWorkflowState.Draft;
    public RoofMappingApproval? Approval {get;private set;}
    public ImmutableArray<RoofShellMapping> Mappings {get;private set;}=[];
    public RoofMappingWorkflow(WindCalculationRun calculation,CsiModelSnapshot model,IEnumerable<RoofPressureZone> zones,bool simulation=true,StandardManifest? manifest=null)
    {
        this.calculation=calculation;this.model=model;this.zones=zones.ToImmutableArray();this.simulation=simulation;this.manifest=manifest;
    }
    public void Scan(){Invalidate();scanned=true;Mappings=[];State=RoofWorkflowState.Scanned;}
    public void Map()
    {
        if(!scanned)throw new InvalidOperationException("Scan model geometry before mapping.");
        var rows=new List<RoofShellMapping>();
        foreach(var state in zones.Select(z=>z.State.Id).Distinct().Order(StringComparer.Ordinal))foreach(var shell in model.Shells.OrderBy(s=>s.Id,StringComparer.Ordinal))
        {
            var candidates=zones.Where(z=>z.State.Id==state).ToArray();var classification=RoofGeometry.Classify(shell,candidates,model.Shells);
            var inside=candidates.Where(z=>RoofGeometry.Classify(shell,[z])==ShellZoneClassification.FullyInsideZone).ToArray();var old=Mappings.FirstOrDefault(m=>m.ShellId==shell.Id&&m.StateId==state);
            var zone=old?.ZoneId??(inside.Length==1?inside[0].Id:null);
            rows.Add(new(shell.Id,state,zone,classification,old?.ManualDirection??ManualLocal3Direction.Unacknowledged,old?.MismatchAcknowledged??false));
        }
        Invalidate();Mappings=rows.ToImmutableArray();State=RoofWorkflowState.Mapped;
    }
    private void Invalidate(){Approval=null;State=RoofWorkflowState.Draft;revision++;}
    private static void AuditIdentity(string user,string reason){if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(reason))throw new ArgumentException("User and engineering review reason are required.");}
    private void Edit(string shellId,string stateId,Func<RoofShellMapping,RoofShellMapping> edit,string field,string newValue,string user,string reason)
    {
        AuditIdentity(user,reason);var index=-1;
        for(var i=0;i<Mappings.Length;i++)if(Mappings[i].ShellId==shellId&&Mappings[i].StateId==stateId){index=i;break;}
        if(index<0)throw new ArgumentException("Unknown shell / wind state.");var old=Mappings[index];Mappings=Mappings.SetItem(index,edit(old));
        overrides.Add(new(shellId,field+":"+stateId,field=="Zone"?old.ZoneId??"":old.ManualDirection.ToString(),newValue,user,DateTimeOffset.UtcNow,reason));Invalidate();
    }
    public void SetZone(string shellId,string stateId,string zoneId,string user,string reason)
    {
        if(!zones.Any(z=>z.Id==zoneId&&z.State.Id==stateId))throw new ArgumentException("Zone must belong to selected wind state.");
        Edit(shellId,stateId,m=>m with{ZoneId=zoneId},"Zone",zoneId,user,reason);
    }
    public void SetSign(string shellId,string stateId,ManualLocal3Direction direction,bool acknowledgeMismatch,string user,string reason)
    {
        if(!Enum.IsDefined(direction))throw new ArgumentException("Unknown Local 3 direction.");
        Edit(shellId,stateId,m=>m with{ManualDirection=direction,MismatchAcknowledged=acknowledgeMismatch},"Local3Sign",direction.ToString(),user,reason);
    }
    public void SetPattern(string stateId,string pattern){if(!zones.Any(z=>z.State.Id==stateId))throw new ArgumentException("Unknown wind state.");patterns[stateId]=pattern;Invalidate();}
    public void Exclude(string shellId,string reason,string user)
    {
        AuditIdentity(user,reason);if(!model.Shells.Any(s=>s.Id==shellId))throw new ArgumentException("Unknown shell.");
        exclusions.RemoveAll(e=>e.ShellId==shellId);exclusions.Add(new(shellId,user,reason,DateTimeOffset.UtcNow));Invalidate();
    }
    public void UpdateSources(WindCalculationRun calculation,CsiModelSnapshot model,IEnumerable<RoofPressureZone> zones)
    {
        this.calculation=calculation;this.model=model;this.zones=zones.ToImmutableArray();Mappings=[];patterns.Clear();exclusions.Clear();scanned=false;Invalidate();
    }
    public ImmutableArray<RoofValidationIssue> Validate()
    {
        var issues=ValidateData(calculation,manifest,model,zones,Mappings,patterns.ToImmutableDictionary(StringComparer.Ordinal),exclusions.ToImmutableArray(),overrides.ToImmutableArray(),simulation);
        if(!scanned)issues=issues.Add(new("ScanRequired","","Rescan sources after any source/model change."));
        if(!issues.Any(i=>i.Blocking))State=RoofWorkflowState.Validated;else{Approval=null;State=RoofWorkflowState.Mapped;}
        return issues;
    }
    internal static ImmutableArray<RoofValidationIssue> ValidateData(WindCalculationRun calculation,StandardManifest? manifest,CsiModelSnapshot model,
        ImmutableArray<RoofPressureZone> zones,ImmutableArray<RoofShellMapping> mappings,ImmutableDictionary<string,string> patterns,
        ImmutableArray<RoofScopeExclusion> exclusions,ImmutableArray<EngineeringOverride> overrides,bool simulation)
    {
        var issues=new List<RoofValidationIssue>();void Block(string code,string message,string shell="")=>issues.Add(new(code,shell,message));
        if(simulation)
        {
            if(calculation.Provenance!=DataProvenance.SyntheticDemoFixture||zones.Any(z=>z.Provenance!=DataProvenance.SyntheticDemoFixture)||!model.Product.Contains("Fake",StringComparison.OrdinalIgnoreCase))Block("SimulationGate","Simulation requires synthetic calculation/zones and a fake model.");
        }
        else Block("StandardsGate","Engineering approval is disabled: no qualified normative dataset is installed. Metadata verification labels cannot authorize engineering assignments.");
        if(string.IsNullOrWhiteSpace(calculation.Id)||string.IsNullOrWhiteSpace(calculation.ManifestHash)||calculation.Profile.IsDefaultOrEmpty||calculation.Profile.Any(p=>!double.IsFinite(p.PressurePa)||!double.IsFinite(p.ElevationM)))Block("CalculationInvalid","Calculation identity, manifest, and finite source pressures are required.");
        if(zones.IsDefaultOrEmpty||model.Shells.IsDefaultOrEmpty)Block("EmptyScope","No roof shells or pressure zones.");
        if(zones.Any(z=>!RoofGeometry.IsValidZone(z)||string.IsNullOrWhiteSpace(z.Id)||string.IsNullOrWhiteSpace(z.RoofPlaneId)))Block("ZoneInvalid","Invalid pressure zone geometry or pressure.");
        if(zones.GroupBy(z=>(z.State.Id,z.Id)).Any(g=>g.Count()!=1)||model.Shells.GroupBy(s=>s.Id).Any(g=>g.Count()!=1))Block("DuplicateId","Duplicate shell or zone identifiers.");
        if(exclusions.Any(e=>string.IsNullOrWhiteSpace(e.User)||string.IsNullOrWhiteSpace(e.Reason)||!model.Shells.Any(s=>s.Id==e.ShellId))||exclusions.GroupBy(e=>e.ShellId).Any(g=>g.Count()!=1))Block("InvalidExclusion","Exclusions require a selected shell, engineer and reason.");
        if(overrides.Any(o=>string.IsNullOrWhiteSpace(o.User)||string.IsNullOrWhiteSpace(o.Reason)))Block("InvalidAudit","Engineering overrides need user and reason.");
        var expected=model.Shells.Length*zones.Select(z=>z.State.Id).Distinct().Count();
        if(mappings.Length!=expected||mappings.GroupBy(m=>(m.ShellId,m.StateId)).Any(g=>g.Count()!=1))Block("MappingIncomplete","Each shell and each independent wind state needs exactly one mapping.");
        var windStates=zones.Select(z=>z.State.Id).ToHashSet(StringComparer.Ordinal);
        if(patterns.Where(p=>windStates.Contains(p.Key)).GroupBy(p=>p.Value,StringComparer.Ordinal).Any(g=>g.Count()>1))
            Block("AlternativePatternConflict","Independent wind states require distinct target load patterns; alternative cases must never be added together.");
        foreach(var state in zones.Select(z=>z.State.Id).Distinct())
        {
            if(!patterns.TryGetValue(state,out var pattern)||!model.Patterns.Contains(pattern,StringComparer.Ordinal))Block("MissingPattern","Map each wind state to an existing load pattern.");
            foreach(var shell in model.Shells)
            {
                var row=mappings.FirstOrDefault(m=>m.ShellId==shell.Id&&m.StateId==state);if(row is null){Block("MappingIncomplete","Missing mapping.",shell.Id);continue;}
                if(exclusions.Any(e=>e.ShellId==shell.Id))continue;
                var classification=RoofGeometry.Classify(shell,zones.Where(z=>z.State.Id==state),model.Shells.Where(s=>!exclusions.Any(e=>e.ShellId==s.Id)));
                if(classification!=ShellZoneClassification.FullyInsideZone){Block(classification.ToString(),"Resolve geometry / partition shell in CSI or explicitly exclude it.",shell.Id);continue;}
                var zone=zones.FirstOrDefault(z=>z.State.Id==state&&z.Id==row.ZoneId);
                if(zone is null||RoofGeometry.Classify(shell,[zone])!=ShellZoneClassification.FullyInsideZone){Block("Unmapped","Selected zone must fully contain this shell.",shell.Id);continue;}
                if(row.ManualDirection==ManualLocal3Direction.Unacknowledged||!Enum.IsDefined(row.ManualDirection)){Block("SignUnacknowledged","Select and acknowledge Local 3 sign.",shell.Id);continue;}
                var calculated=RoofGeometry.Local3Load(zone.NetPressurePa,shell.OutwardNormal,shell.Local3);var manual=row.ManualDirection==ManualLocal3Direction.Positive?1:-1;
                if(calculated!=0&&Math.Sign(calculated)!=manual)
                {
                    var audited=overrides.Any(o=>o.ObjectId==shell.Id&&o.Field=="Local3Sign:"+state&&o.NewValue==row.ManualDirection.ToString());
                    issues.Add(new("SignMismatch",shell.Id,"Manual sign differs from the physical outward-normal recommendation.",!row.MismatchAcknowledged||!audited));
                }
            }
        }
        if(model.Shells.All(s=>exclusions.Any(e=>e.ShellId==s.Id)))Block("EmptyScope","Cannot approve an entirely excluded roof.");
        return issues.ToImmutableArray();
    }
    internal static ImmutableArray<AreaLoadProposal> BuildProposals(CsiModelSnapshot model,ImmutableArray<RoofPressureZone> zones,ImmutableArray<RoofShellMapping> mappings,ImmutableDictionary<string,string> patterns,ImmutableArray<RoofScopeExclusion> exclusions)
    {
        return mappings.Where(m=>!exclusions.Any(e=>e.ShellId==m.ShellId)).OrderBy(m=>m.StateId,StringComparer.Ordinal).ThenBy(m=>m.ShellId,StringComparer.Ordinal).Select(m=>
        {
            var shell=model.Shells.Single(s=>s.Id==m.ShellId);var zone=zones.Single(z=>z.Id==m.ZoneId&&z.State.Id==m.StateId);
            return new AreaLoadProposal(shell.Id,m.StateId,patterns[m.StateId],zone.Id,zone.NetPressurePa,m.ManualDirection,shell.Local3,shell.OutwardNormal,Math.Abs(zone.NetPressurePa)*(m.ManualDirection==ManualLocal3Direction.Positive?1:-1));
        }).ToImmutableArray();
    }
    public RoofMappingApproval Approve(string user,DateTimeOffset timestamp)
    {
        if(State!=RoofWorkflowState.Validated||string.IsNullOrWhiteSpace(user))throw new InvalidOperationException("Validate mapping and supply approver before approval.");
        var patternSnapshot=patterns.ToImmutableDictionary(StringComparer.Ordinal);var excluded=exclusions.ToImmutableArray();
        var approval=new RoofMappingApproval(Guid.NewGuid().ToString("N"),revision,calculation,manifest,model,zones,Mappings,patternSnapshot,BuildProposals(model,zones,Mappings,patternSnapshot,excluded),overrides.ToImmutableArray(),excluded,user,timestamp,simulation,"");
        Approval=approval with{ContentHash=RoofApprovalValidator.ComputeHash(approval)};State=RoofWorkflowState.Approved;return Approval;
    }
    public void MarkApplied(){if(State!=RoofWorkflowState.Approved)throw new InvalidOperationException("Approve before apply.");State=RoofWorkflowState.Applied;}
    public void MarkVerified(){if(State!=RoofWorkflowState.Applied)throw new InvalidOperationException("Apply before readback verification.");State=RoofWorkflowState.Verified;}
}
public static class RoofApprovalValidator
{
    public static string ComputeHash(RoofMappingApproval a)=>SnapshotJson.Hash(new{a.Id,a.Revision,a.Calculation,a.Manifest,a.Model,a.Zones,a.Mappings,Patterns=a.Patterns.OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray(),a.Proposals,a.Overrides,a.Exclusions,a.User,a.ApprovedAt,a.Simulation});
    public static void Validate(RoofMappingApproval approval,CsiModelSnapshot currentModel,WindCalculationRun? currentCalculation=null)
    {
        if(approval.ContentHash!=ComputeHash(approval)||string.IsNullOrWhiteSpace(approval.User)||string.IsNullOrWhiteSpace(approval.Id))throw new InvalidOperationException("Approval content integrity failure.");
        if(SnapshotJson.Hash(approval.Model)!=SnapshotJson.Hash(currentModel))throw new InvalidOperationException("Model identity, geometry, patterns or existing loads changed; rescan/reapprove.");
        if(currentCalculation is not null&&SnapshotJson.Hash(approval.Calculation)!=SnapshotJson.Hash(currentCalculation))throw new InvalidOperationException("Calculation changed; reapprove.");
        var issues=RoofMappingWorkflow.ValidateData(approval.Calculation,approval.Manifest,currentModel,approval.Zones,approval.Mappings,approval.Patterns,approval.Exclusions,approval.Overrides,approval.Simulation);
        if(issues.Any(i=>i.Blocking))throw new InvalidOperationException("Approved mapping failed validation: "+string.Join(", ",issues.Where(i=>i.Blocking).Select(i=>i.Code)));
        var expected=RoofMappingWorkflow.BuildProposals(currentModel,approval.Zones,approval.Mappings,approval.Patterns,approval.Exclusions);
        if(SnapshotJson.Serialize(expected)!=SnapshotJson.Serialize(approval.Proposals))throw new InvalidOperationException("Approval proposals do not match reviewed mapping.");
    }
}
