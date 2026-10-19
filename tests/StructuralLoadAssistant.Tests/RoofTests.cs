using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;
namespace StructuralLoadAssistant.Tests;

public class RoofTests
{
    public static WindState Wind(string internalPressure = "+") => new(WindDirection.PositiveX,internalPressure,"Case1",LimitState.Uls);
    public static ImmutableArray<Vec3> Box(double x1=0,double x2=1,double y1=0,double y2=1) => [new(x1,y1,0),new(x2,y1,0),new(x2,y2,0),new(x1,y2,0)];
    public static ShellGeometry Shell(string id="S1",double x1=0,double x2=1) => new(id,"P",Box(x1,x2),new(0,0,1),new(0,0,1));
    public static RoofPressureZone Zone(string id="Z",double x1=-1,double x2=2,WindState? state=null) => new(id,"P",state??Wind(),Box(x1,x2,-1,2),-1500);
    public static WindCalculationRun Calculation() => new("C",DateTimeOffset.UnixEpoch,"demo",DataProvenance.SyntheticDemoFixture,Wind(),[new(0,-1500)],[]);
    public static CsiModelSnapshot Model(params ShellGeometry[] shells) => new("snapshot","model","Fake","1","geo","loads",["WX","WX_ALT"],["CASE"],[],[],shells.ToImmutableArray(),[]);
    public static RoofMappingWorkflow Ready(CsiModelSnapshot? model=null,RoofPressureZone[]? zones=null)
    {
        var w=new RoofMappingWorkflow(Calculation(),model??Model(Shell()),zones??[Zone()]); w.Scan();w.Map();
        foreach(var state in (zones??[Zone()]).Select(z=>z.State.Id).Distinct()) w.SetPattern(state,state==Wind().Id?"WX":"WX_ALT");
        foreach(var row in w.Mappings) w.SetSign(row.ShellId,row.StateId,ManualLocal3Direction.Positive,false,"Engineer","review");
        return w;
    }
    [Theory][InlineData(-1500,1,1500)][InlineData(-1500,-1,-1500)][InlineData(1500,1,-1500)][InlineData(1500,-1,1500)]
    public void Roof_Local3PhysicalSigns(double pressure,double axis,double expected) => Assert.Equal(expected,RoofGeometry.Local3Load(pressure,new(0,0,1),new(0,0,axis)));
    [Fact] public void Roof_NonparallelAxisRejected() => Assert.Throws<ArgumentException>(()=>RoofGeometry.Local3Load(-1500,new(0,0,1),new(1,0,1)));
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)] public void Roof_NonfinitePressureRejected(double value) => Assert.Throws<ArgumentException>(()=>RoofGeometry.Local3Load(value,new(0,0,1),new(0,0,1)));
    [Fact] public void Roof_InsideZone() => Assert.Equal(ShellZoneClassification.FullyInsideZone,RoofGeometry.Classify(Shell(),[Zone()]));
    [Fact] public void Roof_CrossingZone() => Assert.Equal(ShellZoneClassification.CrossesZoneBoundary,RoofGeometry.Classify(Shell(),[Zone("A",-1,.5),Zone("B",.5,2)]));
    [Fact] public void Roof_Unmapped() => Assert.Equal(ShellZoneClassification.Unmapped,RoofGeometry.Classify(Shell(),[Zone("Z",3,4)]));
    [Fact] public void Roof_SelfIntersectingInvalid() => Assert.Equal(ShellZoneClassification.GeometryInvalid,RoofGeometry.Classify(Shell() with {Vertices=[new(0,0,0),new(1,1,0),new(0,1,0),new(1,0,0)]},[Zone()]));
    [Fact] public void Roof_DuplicateShellOverlap() => Assert.Equal(ShellZoneClassification.OverlapsAnotherShell,RoofGeometry.Classify(Shell(),[Zone()],[Shell("S2")]));
    [Fact] public void Roof_SharedEdgesAreNotOverlap() => Assert.Equal(ShellZoneClassification.FullyInsideZone,RoofGeometry.Classify(Shell(),[Zone()],[Shell("S2",1,2)]));
    [Fact] public void Roof_CoplanarityRequired() => Assert.Equal(ShellZoneClassification.GeometryInvalid,RoofGeometry.Classify(Shell() with {Vertices=[new(0,0,0),new(1,0,0),new(1,1,1),new(0,1,0)]},[Zone()]));
    [Fact] public void Roof_ConcaveZoneNotFilledByVertexShortcut()
    {
        var z=Zone() with {Vertices=[new(0,0,0),new(3,0,0),new(3,3,0),new(2,3,0),new(2,1,0),new(1,1,0),new(1,3,0),new(0,3,0)]};
        var s=Shell() with {Vertices=Box(.5,2.5,2,2.5)};
        Assert.Equal(ShellZoneClassification.CrossesZoneBoundary,RoofGeometry.Classify(s,[z]));
    }
    [Fact] public void Roof_UnmappedBlocksApproval(){var w=Ready(zones:[Zone("Z",3,4)]);Assert.Contains(w.Validate(),i=>i.Blocking);Assert.Throws<InvalidOperationException>(()=>w.Approve("E",DateTimeOffset.UtcNow));}
    [Fact] public void Roof_StraddlerRequiresExplicitScopeExclusion()
    {
        var w=Ready(Model(Shell(),Shell("cross",2,4)),[Zone("A",-1,3),Zone("B",3,5)]);
        Assert.Contains(w.Validate(),i=>i.Code=="CrossesZoneBoundary");w.Exclude("cross","Split in CSI pending","Engineer");
        Assert.DoesNotContain(w.Validate(),i=>i.Blocking);var a=w.Approve("Engineer",DateTimeOffset.UtcNow);Assert.Single(a.Exclusions);Assert.Single(a.Proposals);
    }
    [Fact] public void Roof_ManualSignAcknowledgementRequired()
    {
        var w=new RoofMappingWorkflow(Calculation(),Model(Shell()),[Zone()]);w.Scan();w.Map();w.SetPattern(Wind().Id,"WX");
        Assert.Contains(w.Validate(),i=>i.Code=="SignUnacknowledged");
    }
    [Fact] public void Roof_SignMismatchNeedsExplicitAcknowledgement()
    {
        var w=Ready();w.SetSign("S1",Wind().Id,ManualLocal3Direction.Negative,false,"E","reason");Assert.Contains(w.Validate(),i=>i.Code=="SignMismatch"&&i.Blocking);
        w.SetSign("S1",Wind().Id,ManualLocal3Direction.Negative,true,"E","reviewed mismatch");Assert.DoesNotContain(w.Validate(),i=>i.Blocking);
        Assert.Equal(-1500,w.Approve("E",DateTimeOffset.UtcNow).Proposals[0].SignedCsiAreaLoadPa);
    }
    [Fact] public void Roof_CaseNameCannotSubstituteForPattern(){var w=Ready();w.SetPattern(Wind().Id,"CASE");Assert.Contains(w.Validate(),i=>i.Code=="MissingPattern");}
    [Fact] public void Roof_DistinctInternalPressureStatesRetained(){var w=Ready(zones:[Zone(),Zone("Z-",state:Wind("-"))]);Assert.Empty(w.Validate());Assert.Equal(2,w.Approve("E",DateTimeOffset.UtcNow).Proposals.Length);}
    [Fact] public void Roof_ApprovalIsFrozenAndEditsInvalidate()
    {
        var w=Ready();Assert.Empty(w.Validate());var a=w.Approve("E",DateTimeOffset.UtcNow);var hash=a.ContentHash;
        w.SetSign("S1",Wind().Id,ManualLocal3Direction.Negative,true,"E","change");Assert.Null(w.Approval);Assert.Equal(RoofWorkflowState.Draft,w.State);Assert.Equal(1500,a.Proposals[0].SignedCsiAreaLoadPa);Assert.Equal(hash,RoofApprovalValidator.ComputeHash(a));
    }
    [Fact] public void Roof_ZoneEditInvalidatesApproval(){var w=Ready();w.Validate();w.Approve("E",DateTimeOffset.UtcNow);w.SetZone("S1",Wind().Id,"Z","E","zone review");Assert.Null(w.Approval);}
    [Fact] public void Roof_ApprovalIntegrityAndCurrentness()
    {
        var w=Ready();w.Validate();var a=w.Approve("E",DateTimeOffset.UtcNow);RoofApprovalValidator.Validate(a,a.Model,Calculation());
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(a,a.Model with {GeometryFingerprint="changed"}));
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(a,a.Model with {ExistingLoadsFingerprint="changed"}));
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(a,a.Model,Calculation() with {ManifestHash="changed"}));
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(a with {Proposals=[]},a.Model));
    }
    [Fact] public void Roof_SourceUpdatesInvalidateApproval(){var w=Ready();w.Validate();w.Approve("E",DateTimeOffset.UtcNow);w.UpdateSources(Calculation() with {Profile=[new(0,-2000)]},Model(Shell()),[Zone()]);Assert.Null(w.Approval);Assert.Equal(RoofWorkflowState.Draft,w.State);}
    [Fact] public void Roof_EngineeringModeRequiresVerifiedSources(){var w=new RoofMappingWorkflow(Calculation(),Model(Shell()),[Zone()],false);w.Scan();w.Map();Assert.Contains(w.Validate(),i=>i.Code=="StandardsGate");}
    [Fact] public void Roof_MetadataLabelsCannotUnlockEngineeringApproval()
    {
        var manifest=new StandardManifest("review-label","7-22",[],StandardDataVerificationStatus.VerifiedForUse,[new("ASCE","Clause","claimed review")]);
        var calculation=Calculation() with {ManifestHash=manifest.Hash,Provenance=DataProvenance.Verified};
        var w=new RoofMappingWorkflow(calculation,Model(Shell()),[Zone() with{Provenance=DataProvenance.Verified}],false,manifest);
        w.Scan();w.Map();w.SetPattern(Wind().Id,"WX");w.SetSign("S1",Wind().Id,ManualLocal3Direction.Positive,false,"E","reviewed");
        Assert.Contains(w.Validate(),i=>i.Code=="StandardsGate"&&i.Blocking);
        Assert.Throws<InvalidOperationException>(()=>w.Approve("E",DateTimeOffset.UtcNow));
    }
    [Fact] public void Roof_AlternativeWindStatesRequireDistinctTargetPatterns()
    {
        var w=Ready(zones:[Zone(),Zone("Z-",state:Wind("-"))]);w.SetPattern(Wind("-").Id,"WX");
        Assert.Contains(w.Validate(),i=>i.Code=="AlternativePatternConflict"&&i.Blocking);
        Assert.Throws<InvalidOperationException>(()=>w.Approve("E",DateTimeOffset.UtcNow));
    }
    [Fact] public void Roof_TransitionsRequireValidation(){var w=Ready();Assert.Throws<InvalidOperationException>(()=>w.Approve("E",DateTimeOffset.UtcNow));w.Validate();w.Approve("E",DateTimeOffset.UtcNow);w.MarkApplied();w.MarkVerified();Assert.Equal(RoofWorkflowState.Verified,w.State);}
    [Fact] public void Roof_OverrideAuditIdentifiesIndependentWindState()
    {
        var w=Ready(zones:[Zone(),Zone("Z-",state:Wind("-"))]);w.Validate();var a=w.Approve("E",DateTimeOffset.UtcNow);
        Assert.Contains(a.Overrides,o=>o.Field=="Local3Sign:"+Wind("-").Id);
        Assert.Contains(a.Overrides,o=>o.Field=="Local3Sign:"+Wind().Id);
    }
    [Fact] public void Roof_ZoneMappingUsesProjectedXYRegardlessOfZoneDisplayZ()
    {
        var unrelated=Zone("upper") with {Vertices=Zone().Vertices.Select(v=>v with{Z=3}).ToImmutableArray()};
        Assert.Equal(ShellZoneClassification.FullyInsideZone,RoofGeometry.Classify(Shell(),[unrelated]));
    }
    [Fact] public void Roof_ResealedForgedProposalCannotBypassMapping()
    {
        var w=Ready();w.Validate();var a=w.Approve("E",DateTimeOffset.UtcNow);
        var forged=a with {Proposals=a.Proposals.SetItem(0,a.Proposals[0] with{SignedCsiAreaLoadPa=99999})};
        forged=forged with {ContentHash=RoofApprovalValidator.ComputeHash(forged)};
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(forged,a.Model));
    }
    [Fact] public void Roof_ResealedSignMismatchCannotBypassStateSpecificAudit()
    {
        var w=Ready(zones:[Zone(),Zone("Z-",state:Wind("-"))]);
        w.SetSign("S1",Wind().Id,ManualLocal3Direction.Negative,true,"E","review only positive internal pressure state");
        Assert.DoesNotContain(w.Validate(),i=>i.Blocking);var a=w.Approve("E",DateTimeOffset.UtcNow);
        var index=Enumerable.Range(0,a.Mappings.Length).Single(i=>a.Mappings[i].StateId==Wind("-").Id);
        var forged=a with {Mappings=a.Mappings.SetItem(index,a.Mappings[index] with {ManualDirection=ManualLocal3Direction.Negative,MismatchAcknowledged=true})};
        var proposals=forged.Proposals.Select(p=>p.StateId==Wind("-").Id?p with {ManualLocal3Direction=ManualLocal3Direction.Negative,SignedCsiAreaLoadPa=-1500}:p).ToImmutableArray();
        forged=forged with{Proposals=proposals};forged=forged with{ContentHash=RoofApprovalValidator.ComputeHash(forged)};
        Assert.Throws<InvalidOperationException>(()=>RoofApprovalValidator.Validate(forged,a.Model));
    }
}
