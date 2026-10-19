using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Persistence;
using StructuralLoadAssistant.Reporting;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Application;

public sealed class DemoSession
{
    public Project Project { get; private set; } = new("demo-project","Three-story demonstration","SI");
    public CsiModelSnapshot Model { get; private set; } = DemoFixtures.Model();
    public WindCalculationRun Calculation { get; private set; } = DemoFixtures.Calculation();
    public StandardManifest Manifest { get; private set; } = DemoFixtures.Manifest();
    public RoofMappingWorkflow Roof { get; private set; } = null!;
    public ImmutableArray<RoofPressureZone> Zones { get; private set; } = [];
    public ImmutableArray<FacadeReportRow> FacadeRows { get; private set; } = [];
    public ImmutableArray<FacadeReportRow> PanelRows { get; private set; } = [];
    public ImmutableArray<WallReportRow> Walls { get; private set; } = [];
    public FacadeMethod Method { get; private set; } = FacadeMethod.EqualSplit;
    public string FacadePattern => $"{(Calculation.State.Direction is WindDirection.PositiveX or WindDirection.NegativeX?"WX":"WY")}_{(Calculation.State.Direction is WindDirection.PositiveX or WindDirection.PositiveY?"POS":"NEG")}_{Calculation.State.InternalPressure}";
    public AssignmentRun? Assignment { get; private set; }
    public ReadbackVerification? Readback { get; private set; }
    public FacadeSimulationPreview? FacadePreview {get;private set;}
    public FacadeSimulationApproval? FacadeApproval {get;private set;}
    public FacadeSimulationRun? FacadeAssignment {get;private set;}
    public FacadeReadbackVerification? FacadeReadback {get;private set;}
    public string DatabasePath { get; }
    public ImmutableArray<FacadePairSelection> FacadePairs => _selection?.Pairs ?? [];
    private FacadeSelection? _selection;
    private readonly SqliteSnapshotStore _store;
    private FakeCsiAdapter _adapter = new();
    private FakeFacadeAssignmentService? _facadeAdapter;
    private ImmutableArray<FrameSegmentLoad> _facadeContributions=[];
    public DemoSession(string databasePath) { DatabasePath=databasePath; _store=new(databasePath); }
    private void Save<T>(string id,SnapshotKind kind,T value,string? approvalId = null)
    {
        bool linked = kind is not (SnapshotKind.Project or SnapshotKind.StandardsManifestSnapshot or SnapshotKind.ReportDefinition or SnapshotKind.CsiModelSnapshot or SnapshotKind.CalculationRunSnapshot);
        _store.Save(new(id,kind,Project.Id,linked?Calculation.Id:null,linked?Model.Id:null,approvalId),value);
    }
    public void LoadDemoFixture(string edition = "ASCE/SEI 7-22", bool includeCrossing = true)
        => LoadDemoFixture(edition,includeCrossing,DemoFixtures.State);
    public void LoadDemoFixture(string edition,bool includeCrossing,WindState state)
    {
        Manifest=DemoFixtures.Manifest(edition);Calculation=DemoFixtures.Calculation(edition);
        ArgumentNullException.ThrowIfNull(state);
        if(!Enum.IsDefined(state.Direction)||!Enum.IsDefined(state.LimitState)||state.InternalPressure is not ("IP_POS" or "IP_NEG")||state.RoofCase is not ("RoofCase1" or "RoofCase2"))
            throw new ArgumentException("Unsupported synthetic wind state");
        if(state!=DemoFixtures.State) Calculation=Calculation with {Id=Calculation.Id+"-"+SnapshotJson.Hash(state),State=state};
        Model=DemoFixtures.Model(includeCrossing);_adapter=new(Model);Assignment=null;Readback=null;
        _facadeAdapter=new(_adapter);
        _selection=new(Model.Frames);
        _selection.SelectPair("B1","B2");
        _selection.SelectPair("B2","B3");
        Zones=[new("Edge","Roof",Calculation.State,DemoFixtures.Rectangle(0,0,5,12),-1500),
            new("Interior","Roof",Calculation.State,DemoFixtures.Rectangle(5,0,10,12),-1000)];
        string axis=state.Direction is WindDirection.PositiveX or WindDirection.NegativeX?"WX":"WY";
        string direction=state.Direction is WindDirection.PositiveX or WindDirection.PositiveY?"POS":"NEG";
        Roof=new(Calculation,Model,Zones,true,Manifest);Roof.Scan();Roof.Map();Roof.SetPattern(Calculation.State.Id,$"{axis}_{direction}_{state.InternalPressure}");
        Save(Project.Id,SnapshotKind.Project,Project);
        Save(Manifest.Hash,SnapshotKind.StandardsManifestSnapshot,Manifest);
        Save(Calculation.Id,SnapshotKind.CalculationRunSnapshot,Calculation);
        Save(Model.Id,SnapshotKind.CsiModelSnapshot,Model);
        RecalculateFacade(FacadeMethod.EqualSplit);PreviewWall(WallLoads.DemoLibrary[0],3);
        SaveDraft();
    }
    public void RecalculateFacade(FacadeMethod method)
    {
        FacadePreview=null;FacadeApproval=null;FacadeAssignment=null;FacadeReadback=null;
        Method=method;
        double PressureAt(double z)
        {
            for(int i=0;i<Calculation.Profile.Length-1;i++)
            {
                var a=Calculation.Profile[i];var b=Calculation.Profile[i+1];
                if(z>=a.ElevationM && z<=b.ElevationM) return EngineeringMath.Linear(z,a.ElevationM,a.PressurePa,b.ElevationM,b.PressurePa);
            }
            throw new ArgumentException("Pressure fixture has no value at requested height");
        }
        var panels=new List<FacadeReportRow>();var contributions=new List<FrameSegmentLoad>();
        foreach(var pair in FacadePairs)
        {
            var lower=Model.Frames.Single(f=>f.Id==pair.LowerFrameId);var upper=Model.Frames.Single(f=>f.Id==pair.UpperFrameId);
            var loads=FacadeMapper.MapPair(lower,upper,PressureAt,0,Calculation.State,FacadePattern,method);
            contributions.AddRange(loads);
            foreach(var load in loads)
            {
                var length=(Model.Frames.Single(f=>f.Id==load.FrameId).End-Model.Frames.Single(f=>f.Id==load.FrameId).Start).Length;
                panels.Add(new($"{lower.Id}-{upper.Id}",load.FrameId,method.ToString(),load.StartFraction*length,load.EndFraction*length,load.LoadNPerM));
            }
        }
        PanelRows=panels.ToImmutableArray();
        _facadeContributions=FacadeMapper.Accumulate(contributions);
        FacadeRows=FacadeMapper.Accumulate(contributions).Select(l=>
        {
            var frame=Model.Frames.Single(f=>f.Id==l.FrameId);var length=(frame.End-frame.Start).Length;
            return new FacadeReportRow("Accumulated",l.FrameId,method.ToString(),l.StartFraction*length,l.EndFraction*length,l.LoadNPerM);
        }).ToImmutableArray();
        Save("facade-"+Model.Id+"-"+Calculation.Id+"-"+SnapshotJson.Hash(FacadeRows),SnapshotKind.FacadeMappingDraft,FacadeRows);
    }
    public void PreviewWall(WallType type,double heightM,double? directNPerM = null)
    {
        var result=WallLoads.Calculate(new(type,0,heightM,0,"WALL","B1",directNPerM));
        Walls=[new("B1","WALL",type.Id+(directNPerM.HasValue?" / direct override":" / illustrative"),heightM,result.GlobalLoadNPerM.Z)];
    }
    public void SelectFacadePair(string lowerId,string upperId)
    {
        (_selection??throw new InvalidOperationException("Load selected geometry first")).SelectPair(lowerId,upperId);
        RecalculateFacade(Method);
    }
    public void ClearFacadePairs()
    {
        (_selection??throw new InvalidOperationException("Load selected geometry first")).ClearPairs();
        RecalculateFacade(Method);
    }
    private void Edited()
    {
        Assignment=null;Readback=null;SaveDraft();
    }
    private void SaveDraft()
    {
        var draft=new {CalculationRunId=Calculation.Id,ModelSnapshotId=Model.Id,Model.GeometryFingerprint,WindState=Calculation.State,Mappings=Roof.Mappings,State=Roof.State};
        Save("roof-draft-"+SnapshotJson.Hash(draft),SnapshotKind.RoofMappingDraft,draft);
    }
    public void SetZone(string shellId,string zoneId,string user,string reason) {Roof.SetZone(shellId,Calculation.State.Id,zoneId,user,reason);Edited();}
    public void SetSign(string shellId,ManualLocal3Direction direction,bool acknowledgeMismatch,string user,string reason) {Roof.SetSign(shellId,Calculation.State.Id,direction,acknowledgeMismatch,user,reason);Edited();}
    public void SetPattern(string pattern) {Roof.SetPattern(Calculation.State.Id,pattern);Edited();}
    public void ExcludeShell(string shellId,string user,string reason) {Roof.Exclude(shellId,reason,user);Edited();}
    public ImmutableArray<RoofValidationIssue> ValidateRoof() => Roof.Validate();
    public RoofMappingApproval ApproveRoof(string user)
    {
        var a=Roof.Approve(user,DateTimeOffset.UtcNow);
        Save(a.Id,SnapshotKind.RoofMappingApprovalSnapshot,a,a.Id);
        Save("patterns-"+a.ContentHash,SnapshotKind.PatternMapping,a.Patterns,a.Id);
        foreach(var o in a.Overrides) Save("override-"+a.Id+"-"+SnapshotJson.Hash(o),SnapshotKind.EngineeringOverride,o,a.Id);
        return a;
    }
    public AssignmentPreview PreviewAssignment()
    {
        var a=Roof.Approval??throw new InvalidOperationException("Approve a validated simulation mapping first");
        var preview=_adapter.Preview(a);Save("preview-"+a.ContentHash,SnapshotKind.AssignmentPreviewSnapshot,preview,a.Id);return preview;
    }
    public async Task<AssignmentRun> ApplyAsync()
    {
        var a=Roof.Approval??throw new InvalidOperationException("Simulation approval required");
        Assignment=await _adapter.ApplyAreaLoadsAsync(a);
        if(Roof.State==RoofWorkflowState.Approved) Roof.MarkApplied();
        Save(Assignment.Id,SnapshotKind.SimulatedAssignmentRun,Assignment,a.Id);return Assignment;
    }
    public async Task<ReadbackVerification> VerifyAsync()
    {
        if(Assignment is null) throw new InvalidOperationException("Simulate apply first");
        Readback=await _adapter.VerifyReadbackAsync(Assignment.Id);
        if(Readback.Matches && Roof.State==RoofWorkflowState.Applied) Roof.MarkVerified();return Readback;
    }
    public ReportModel BuildReport(ReportDefinition? definition = null) => new(Project,Calculation,Manifest,Model,FacadeRows,Walls,Roof.Approval,Assignment,Readback,definition??new("default-report"),FacadeAssignment,FacadeReadback);
    public string SaveReport(ReportDefinition? definition = null)
    {
        var model=BuildReport(definition);var id="report-"+SnapshotJson.Hash(model);
        Save("definition-"+SnapshotJson.Hash(model.Definition),SnapshotKind.ReportDefinition,model.Definition);
        Save(id,SnapshotKind.ReportSnapshot,model,model.Approval?.Id);return id;
    }
    public void ExportReport(string path,ReportDefinition? definition = null)
    {
        IReportExporter exporter=Path.GetExtension(path).Equals(".html",StringComparison.OrdinalIgnoreCase)?new HtmlReportExporter():new MarkdownReportExporter();
        File.WriteAllText(path,exporter.Export(BuildReport(definition)),new System.Text.UTF8Encoding(false));
    }
    public IReadOnlyList<string> SavedReports() => _store.ListIds(SnapshotKind.ReportSnapshot,Project.Id);
    public ReportModel OpenReport(string id) => _store.Load<ReportModel>(id)??throw new InvalidOperationException("Report snapshot not found");
    public async Task<FacadeSimulationPreview> PreviewFacadeAsync()
    {
        FacadePreview=await _facadeAdapter!.PreviewAsync(Calculation,_facadeContributions,new(1,0,0),Method);
        FacadeApproval=null;FacadeAssignment=null;FacadeReadback=null;
        Save("facade-preview-"+SnapshotJson.Hash(FacadePreview),SnapshotKind.AssignmentPreviewSnapshot,FacadePreview);return FacadePreview;
    }
    public async Task<FacadeSimulationApproval> ApproveFacadeAsync(string user)
    {
        FacadeApproval=await _facadeAdapter!.ApproveAsync(FacadePreview??throw new InvalidOperationException("Preview facade first"),user);
        Save("facade-approval-"+FacadeApproval.ContentHash,SnapshotKind.AssignmentPreviewSnapshot,FacadeApproval);return FacadeApproval;
    }
    public async Task<FacadeSimulationRun> ApplyFacadeAsync()
    {
        FacadeAssignment=await _facadeAdapter!.ApplyAsync(FacadeApproval??throw new InvalidOperationException("Approve facade simulation first"),Calculation);
        Save(FacadeAssignment.Id,SnapshotKind.SimulatedAssignmentRun,FacadeAssignment);return FacadeAssignment;
    }
    public async Task<FacadeReadbackVerification> VerifyFacadeAsync()
    {
        FacadeReadback=await _facadeAdapter!.VerifyAsync(FacadeAssignment?.Id??throw new InvalidOperationException("Simulate facade apply first"));return FacadeReadback;
    }
}
