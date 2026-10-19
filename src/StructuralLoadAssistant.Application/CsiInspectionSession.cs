using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.RoofMapping;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Application;

/// <summary>Owns the current read selection; it has no CSI mutation or simulation dependency.</summary>
public sealed class CsiInspectionSession : IAsyncDisposable
{
    private readonly ICsiSessionReadAdapter adapter;
    private int revision;
    private bool disposed;
    public CsiInspectionSession(ICsiSessionReadAdapter adapter) => this.adapter=adapter??throw new ArgumentNullException(nameof(adapter));
    public CsiConnectionResult? Connection { get; private set; }
    public CsiReadScan? Scan { get; private set; }
    public CsiScanOutcome Outcome { get; private set; } = CsiScanOutcome.Disconnected;
    public ImmutableArray<CsiReadIssue> Issues { get; private set; } = [];
    public FacadeSelection? Facade { get; private set; }
    public RoofMappingWorkflow? Roof { get; private set; }
    public ImmutableArray<RoofShellMapping> RoofRows { get; private set; } = [];
    public ImmutableArray<EngineeringOverride> Local3Reviews { get; private set; } = [];
    public void SetLocal3Review(string shellId,ManualLocal3Direction direction,string user,string reason)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(reason)||!Enum.IsDefined(direction)||direction==ManualLocal3Direction.Unacknowledged)
            throw new ArgumentException("An explicit Local 3 direction, engineer and review reason are required.");
        var row=RoofRows.FirstOrDefault(r=>r.ShellId==shellId)??throw new ArgumentException("Shell is not in the current selection.",nameof(shellId));
        RoofRows=RoofRows.SetItem(RoofRows.IndexOf(row),row with{ManualDirection=direction});
        Local3Reviews=Local3Reviews.Add(new(shellId,"Local3Review",row.ManualDirection.ToString(),direction.ToString(),user,DateTimeOffset.UtcNow,reason));
    }
    public async Task ConnectAsync(CsiConnectionOptions options,CancellationToken cancellationToken=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(options);Clear();var current=Volatile.Read(ref revision);
        try
        {
            var result=await adapter.ConnectAsync(options,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if(current!=Volatile.Read(ref revision)||disposed)return;
            if(result.Connected&&!result.Capabilities.IsReadOnly)
                result=result with{Connected=false,Issues=result.Issues.Add(new("ReadOnlyRequired","Connect","Inspection requires a read-only adapter."))};
            Connection=result;Issues=result.Issues;
        }
        catch(OperationCanceledException){throw;}
        catch(Exception){if(current==Volatile.Read(ref revision)&&!disposed)Issues=[new("ConnectionFailed","Connect","Unable to connect to the selected CSI session. Check installation and API compatibility.")];}
    }
    public async Task RefreshAsync(CancellationToken cancellationToken=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(Connection?.Connected!=true)throw new InvalidOperationException("Connect a read-only CSI session before refreshing.");
        ClearSelection();var current=Interlocked.Increment(ref revision);
        Outcome=CsiScanOutcome.Scanning;
        try
        {
            var scan=await adapter.RefreshSelectionAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if(current!=Volatile.Read(ref revision)||disposed)return;
            if(!scan.Capabilities.IsReadOnly)throw new InvalidOperationException("Read-only adapter capability changed.");
            // An object axis never establishes the physical outward normal.
            var issues=scan.Issues.ToBuilder();
            var metadata=scan.AreaMetadata.Select(area=>
            {
                if(area.VerifiedOutwardNormal is not { } normal||IsUnitNormal(normal))return area;
                issues.Add(new("NormalMetadataInvalid","RefreshSelection","A claimed outward normal is not a finite unit vector; physical outward direction remains unknown.",CsiIssueSeverity.Warning));
                return area with{VerifiedOutwardNormal=null,NormalStatus="Invalid outward normal; engineer verification required."};
            }).ToImmutableArray();
            scan=scan with{AreaMetadata=metadata,Issues=issues.ToImmutable()};
            var shells=scan.Model.Shells.Select(shell=>shell with{OutwardNormal=scan.AreaMetadata.FirstOrDefault(a=>a.AreaId==shell.Id)?.VerifiedOutwardNormal??default}).ToImmutableArray();
            scan=scan with{Model=scan.Model with{Shells=shells}};
            var facade=new FacadeSelection(scan.Model.Frames);
            var calculation=new WindCalculationRun("inspection-"+scan.Model.Id,DateTimeOffset.UtcNow,"unverified",DataProvenance.Unverified,
                new(WindDirection.PositiveX,"Unverified","Unverified",LimitState.Uls),[],[]);
            var roof=new RoofMappingWorkflow(calculation,scan.Model,[],simulation:false);roof.Scan();
            Scan=scan;Facade=facade;Roof=roof;Issues=scan.Issues;Outcome=scan.Outcome;
            RoofRows=shells.Select(s=>new RoofShellMapping(s.Id,"",null,ShellZoneClassification.Unmapped)).ToImmutableArray();
        }
        catch(OperationCanceledException){throw;}
        catch(Exception){if(current==Volatile.Read(ref revision)&&!disposed){ClearSelection();Outcome=CsiScanOutcome.Failed;Issues=[new("RefreshFailed","RefreshSelection","Selection could not be read. Stale geometry and reviews have been cleared; refresh again.")];}}
    }
    private static bool IsUnitNormal(Vec3 vector) => double.IsFinite(vector.X)&&double.IsFinite(vector.Y)&&double.IsFinite(vector.Z)&&Math.Abs(vector.Length-1)<=1e-6;
    private void ClearSelection(){Scan=null;Facade=null;Roof=null;RoofRows=[];Local3Reviews=[];Issues=[];Outcome=Connection?.Connected==true?CsiScanOutcome.Disconnected:CsiScanOutcome.Disconnected;}
    public void Clear(){Interlocked.Increment(ref revision);Connection=null;ClearSelection();}
    public async ValueTask DisposeAsync()
    {
        if(disposed)return;disposed=true;Clear();await adapter.DisposeAsync();
    }
}
