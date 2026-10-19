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
    public ImmutableArray<RoofShell3D> Shells3D { get; private set; } = [];
    public ImmutableArray<RoofShellMapping> RoofRows { get; private set; } = [];
    public ImmutableArray<RoofScopeExclusion> Exclusions { get; private set; } = [];
    private readonly Dictionary<string, RoofMappingDraft> draftsByState = new(StringComparer.Ordinal);
    public ImmutableArray<EngineeringOverride> Local3Reviews { get; private set; } = [];
    public GeometryReviewStatus ReviewStatus { get; private set; } = GeometryReviewStatus.GeometryDraft;
    public void SetGeometryZone(string shellId, string zoneId, string user, string reason)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Engineer and review reason are required.");
        var row = RoofRows.FirstOrDefault(r => r.ShellId == shellId)
            ?? throw new ArgumentException("Shell is not in the current selection.", nameof(shellId));
        var classification = string.Equals(zoneId, "Unmapped", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(zoneId)
            ? ShellZoneClassification.Unmapped
            : (zoneId.Contains("Cross", StringComparison.OrdinalIgnoreCase) ? ShellZoneClassification.CrossesZoneBoundary : ShellZoneClassification.NotGeometricallyValidated);
        RoofRows = RoofRows.SetItem(RoofRows.IndexOf(row), row with { ZoneId = zoneId, Classification = classification });
        Local3Reviews = Local3Reviews.Add(new(shellId, "GeometryZone", row.ZoneId ?? "", zoneId, user, DateTimeOffset.UtcNow, reason));
        ReviewStatus = GeometryReviewStatus.GeometryReviewed;
    }
    public void ExcludeShell(string shellId, string user, string reason)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Engineer and exclusion reason are required.");
        var row = RoofRows.FirstOrDefault(r => r.ShellId == shellId)
            ?? throw new ArgumentException("Shell is not in the current selection.", nameof(shellId));
        Exclusions = Exclusions.RemoveAll(e => e.ShellId == shellId).Add(new(shellId, user, reason, DateTimeOffset.UtcNow));
        RoofRows = RoofRows.SetItem(RoofRows.IndexOf(row), row with { ZoneId = "Excluded", Classification = ShellZoneClassification.Unmapped });
        ReviewStatus = GeometryReviewStatus.GeometryReviewed;
    }
    public RoofMappingDraft CreateDraft(string projectId, WindState state, int? explicitRevision = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var geomHash = Scan?.Model.GeometryFingerprint ?? "";
        var items = RoofRows.Select(r => new RoofMappingDraftItem(
            r.ShellId, r.ZoneId, r.Classification, r.ManualDirection, r.MismatchAcknowledged,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.User,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.Reason,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.CreatedAt
        )).ToImmutableArray();
        int rev = explicitRevision ?? (draftsByState.TryGetValue(state.Id, out var prev) ? prev.Revision + 1 : 1);
        var draftId = RoofMappingDraftStore.DraftId(projectId, Scan?.Model.ModelId ?? "unknown", state.Id, rev);
        var draft = new RoofMappingDraft(
            draftId, rev, projectId, Scan?.Model.ModelId ?? "unknown", geomHash, state,
            items, Exclusions, Local3Reviews, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ReviewStatus);
        draftsByState[state.Id] = draft;
        return draft;
    }
    public bool RestoreDraft(RoofMappingDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var currentGeomHash = Scan?.Model.GeometryFingerprint ?? "";
        var reconciledStatus = RoofMappingDraftStore.ReconcileGeometry(draft, currentGeomHash);
        if (reconciledStatus == GeometryReviewStatus.StaleNeedsRescan)
        {
            ReviewStatus = GeometryReviewStatus.StaleNeedsRescan;
            return false;
        }
        ReviewStatus = draft.ReviewStatus;
        draftsByState[draft.WindState.Id] = draft;
        Exclusions = draft.Exclusions;
        Local3Reviews = draft.Overrides;
        var rows = new List<RoofShellMapping>();
        foreach (var shell in Scan?.Model.Shells ?? ImmutableArray<ShellGeometry>.Empty)
        {
            var item = draft.Items.FirstOrDefault(i => i.ShellId == shell.Id);
            if (item != null)
            {
                rows.Add(new(shell.Id, draft.WindState.Id, item.ZoneId, item.Classification, item.ManualDirection, item.MismatchAcknowledged));
            }
            else
            {
                rows.Add(new(shell.Id, draft.WindState.Id, null, ShellZoneClassification.Unmapped));
            }
        }
        RoofRows = rows.ToImmutableArray();
        return true;
    }
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
        var previousDrafts = draftsByState.ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal);
        var previousGeomHash = Scan?.Model.GeometryFingerprint;
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
            var shells3D = shells.Select(s =>
            {
                var meta = scan.AreaMetadata.FirstOrDefault(a => a.AreaId == s.Id);
                return Roof3DGeometryProcessor.Process(s, scan.Model.ModelId, meta?.VerifiedOutwardNormal);
            }).ToImmutableArray();
            Scan=scan;Facade=facade;Roof=roof;Issues=scan.Issues;Outcome=scan.Outcome;Shells3D=shells3D;
            RoofRows=shells.Select(s=>new RoofShellMapping(s.Id,"",null,ShellZoneClassification.Unmapped)).ToImmutableArray();

            // Reconcile previous drafts if rescanning identical geometry
            if (!string.IsNullOrWhiteSpace(previousGeomHash) && previousGeomHash == scan.Model.GeometryFingerprint)
            {
                foreach (var kvp in previousDrafts)
                {
                    draftsByState[kvp.Key] = kvp.Value;
                }
            }
            else if (previousDrafts.Count > 0)
            {
                ReviewStatus = GeometryReviewStatus.StaleNeedsRescan;
            }
        }
        catch(OperationCanceledException){throw;}
        catch(Exception){if(current==Volatile.Read(ref revision)&&!disposed){ClearSelection();Outcome=CsiScanOutcome.Failed;Issues=[new("RefreshFailed","RefreshSelection","Selection could not be read. Stale geometry and reviews have been cleared; refresh again.")];}}
    }
    private static bool IsUnitNormal(Vec3 vector) => double.IsFinite(vector.X)&&double.IsFinite(vector.Y)&&double.IsFinite(vector.Z)&&Math.Abs(vector.Length-1)<=1e-6;
    private void ClearSelection(){Scan=null;Facade=null;Roof=null;Shells3D=[];RoofRows=[];Local3Reviews=[];Exclusions=[];draftsByState.Clear();ReviewStatus=GeometryReviewStatus.GeometryDraft;Issues=[];Outcome=Connection?.Connected==true?CsiScanOutcome.Disconnected:CsiScanOutcome.Disconnected;}
    public void Clear(){Interlocked.Increment(ref revision);Connection=null;ClearSelection();}
    public void ResetToCleanUnmappedDraft(string windStateId)
    {
        var shells = Scan?.Model.Shells ?? ImmutableArray<ShellGeometry>.Empty;
        RoofRows = shells.Select(s => new RoofShellMapping(s.Id, windStateId, null, ShellZoneClassification.Unmapped)).ToImmutableArray();
        Exclusions = [];
        Local3Reviews = [];
        ReviewStatus = GeometryReviewStatus.GeometryDraft;
    }
    public int GetDraftRevision(string windStateId)
    {
        return draftsByState.TryGetValue(windStateId, out var draft) ? draft.Revision : 0;
    }
    public async ValueTask DisposeAsync()
    {
        if(disposed)return;disposed=true;Clear();await adapter.DisposeAsync();
    }
}
