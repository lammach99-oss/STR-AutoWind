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
    private RoofMappingDraft? retainedDraft;
    public RoofMappingDraft? PendingScopeDraft { get; private set; }
    public void RecordZoneGeometryEdit(string beforeHash,string afterHash,string user,string reason)
    {
        if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Reviewer and reason required.");
        Local3Reviews=Local3Reviews.Add(new("Roof","ZonePolygonGeometry",beforeHash,afterHash,user,DateTimeOffset.UtcNow,reason));
    }
    public void InvalidateGeometryReview()
    {
        ReviewStatus=GeometryReviewStatus.GeometryDraft;
        RoofRows=RoofRows.Select(r=>Exclusions.Any(e=>e.ShellId==r.ShellId)?r:r with{Classification=ShellZoneClassification.Unmapped,ZoneId=null,ZoneCoverages=[],ShellAreaM2=null,MappingContractVersion=2}).ToImmutableArray();
        if(retainedDraft is { } old)retainedDraft=old with{ReviewStatus=GeometryReviewStatus.GeometryDraft,
            Items=old.Items.Select(i=>old.Exclusions.Any(e=>e.ShellId==i.ShellId)?i:i with{Classification=ShellZoneClassification.Unmapped,ZoneId=null,ZoneCoverages=[],ShellAreaM2=null,MappingContractVersion=2}).ToImmutableArray()};
    }
    public void SetIntersectionMappings(IEnumerable<ShellZoneIntersection> classifications,string stateId)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        var updated=RoofRows;
        foreach(var result in classifications)
        {
            var row=RoofRows.First(r=>r.ShellId==result.ShellId);
            var classification=result.Classification switch {
                GeometryClassification.FullyInsideZone=>ShellZoneClassification.FullyInsideZone,
                GeometryClassification.CrossesZoneBoundary=>ShellZoneClassification.CrossesZoneBoundary,
                GeometryClassification.OverlappingZones=>ShellZoneClassification.OverlappingZones,
                GeometryClassification.InvalidGeometry=>ShellZoneClassification.InvalidGeometry,
                GeometryClassification.ExcludedWithReason when Exclusions.Any(e=>e.ShellId==result.ShellId)=>ShellZoneClassification.ExcludedWithReason,
                GeometryClassification.Unmapped=>ShellZoneClassification.Unmapped,
                _=>throw new InvalidOperationException("Unknown classification or unaudited exclusion.") };
            updated=updated.SetItem(updated.IndexOf(row),row with{StateId=stateId,
                ZoneId=result.Coverages.Length==1?result.Coverages[0].ZoneId:null,Classification=classification,
                MappingContractVersion=2,ZoneCoverages=result.Coverages,ShellAreaM2=result.ShellAreaM2});
        }
        RoofRows=updated;
    }
    public void SetGeometryZone(string shellId, string zoneId, string user, string reason)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Engineer and review reason are required.");
        var row = RoofRows.FirstOrDefault(r => r.ShellId == shellId)
            ?? throw new ArgumentException("Shell is not in the current selection.", nameof(shellId));
        var classification = string.Equals(zoneId, "Unmapped", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(zoneId)
            ? ShellZoneClassification.Unmapped
            : ShellZoneClassification.NotGeometricallyValidated;
        RoofRows = RoofRows.SetItem(RoofRows.IndexOf(row), row with { ZoneId = zoneId, Classification = classification,MappingContractVersion=2,ZoneCoverages=[],ShellAreaM2=null });
        Local3Reviews = Local3Reviews.Add(new(shellId, "GeometryZone", row.ZoneId ?? "", zoneId, user, DateTimeOffset.UtcNow, reason));
        ReviewStatus = GeometryReviewStatus.GeometryDraft;
    }
    public void ExcludeShell(string shellId, string user, string reason)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Engineer and exclusion reason are required.");
        var row = RoofRows.FirstOrDefault(r => r.ShellId == shellId)
            ?? throw new ArgumentException("Shell is not in the current selection.", nameof(shellId));
        Exclusions = Exclusions.RemoveAll(e => e.ShellId == shellId).Add(new(shellId, user, reason, DateTimeOffset.UtcNow));
        RoofRows = RoofRows.SetItem(RoofRows.IndexOf(row), row with { ZoneId = "Excluded", Classification = ShellZoneClassification.ExcludedWithReason,MappingContractVersion=2,ZoneCoverages=[],ShellAreaM2=null });
        ReviewStatus = GeometryReviewStatus.GeometryDraft;
    }
    public RoofMappingDraft CreateDraft(string projectId, WindState state, int? explicitRevision = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var geomHash = Scan?.Model.GeometryFingerprint ?? "";
        var items = RoofRows.Select(r => new RoofMappingDraftItem(
            r.ShellId, r.ZoneId, r.Classification, r.ManualDirection, r.MismatchAcknowledged,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.User,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.Reason,
            Local3Reviews.LastOrDefault(rv => rv.ObjectId == r.ShellId)?.CreatedAt,
            r.MappingContractVersion,r.ZoneCoverages,r.ShellAreaM2
        )).ToImmutableArray();
        int rev = explicitRevision ?? (draftsByState.TryGetValue(state.Id, out var prev) ? prev.Revision + 1 : 1);
        var draftId = RoofMappingDraftStore.DraftId(projectId, Scan?.Model.ModelId ?? "unknown", state.Id, rev);
        var retainedItems = retainedDraft?.Items.Where(i => !items.Any(current => current.ShellId == i.ShellId)) ?? [];
        var stamps = Shells3D.Select(s => new SourceShellFingerprint(s.ShellId, s.GeometryFingerprint)).ToImmutableArray();
        var retainedStamps = retainedDraft is { SourceShells.IsDefault: false } old
            ? old.SourceShells.Where(s => !stamps.Any(current => current.ShellId == s.ShellId)) : [];
        var draft = new RoofMappingDraft(
            draftId, rev, projectId, Scan?.Model.ModelId ?? "unknown", geomHash, state,
            items.AddRange(retainedItems), Exclusions, Local3Reviews, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ReviewStatus,
            stamps.AddRange(retainedStamps), Shells3D.Select(s => s.ShellId).ToImmutableArray());
        return draft;
    }
    public void RememberSavedDraft(RoofMappingDraft draft) => draftsByState[draft.WindState.Id] = draft;

    public bool RestoreDraft(RoofMappingDraft draft, string expectedProjectId, string expectedWindStateId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var currentGeomHash = Scan?.Model.GeometryFingerprint ?? "";
        var reconciledStatus = RoofMappingDraftStore.ValidateAndReconcile(draft, expectedProjectId,
            Scan?.Model.ModelId ?? "", expectedWindStateId, currentGeomHash, Shells3D);
        var interpretedItems=draft.Items.Select(i=>InterpretDraftItem(i,draft.Exclusions)).ToImmutableArray();
        ResetToCleanUnmappedDraft(expectedWindStateId);
        if (reconciledStatus == GeometryReviewStatus.ScopeReviewRequired)
        {
            PendingScopeDraft = draft; ReviewStatus = reconciledStatus; return false;
        }
        if (reconciledStatus == GeometryReviewStatus.StaleNeedsRescan)
        {
            ReviewStatus = GeometryReviewStatus.StaleNeedsRescan;
            return false;
        }
        ReviewStatus = draft.ReviewStatus;
        retainedDraft = draft with{Items=interpretedItems};
        if(draft.Items.Any(i=>i.MappingContractVersion<2))ReviewStatus=GeometryReviewStatus.GeometryDraft;
        draftsByState[draft.WindState.Id] = draft;
        Exclusions = draft.Exclusions;
        Local3Reviews = draft.Overrides;
        var rows = new List<RoofShellMapping>();
        foreach (var shell in Scan?.Model.Shells ?? ImmutableArray<ShellGeometry>.Empty)
        {
            var item = draft.Items.FirstOrDefault(i => i.ShellId == shell.Id);
            if (item != null)
            {
                var interpreted=InterpretDraftItem(item,draft.Exclusions);
                rows.Add(new(shell.Id, draft.WindState.Id, interpreted.ZoneId, interpreted.Classification, item.ManualDirection, item.MismatchAcknowledged,
                    interpreted.MappingContractVersion,interpreted.ZoneCoverages,interpreted.ShellAreaM2));
            }
            else
            {
                rows.Add(new(shell.Id, draft.WindState.Id, null, ShellZoneClassification.Unmapped));
            }
        }
        RoofRows = rows.ToImmutableArray();
        return true;
    }
    // v1 merged zone/shell overlaps and inferred crossing from labels. Never reconstruct evidence from comma-delimited labels.
    private static RoofMappingDraftItem InterpretDraftItem(RoofMappingDraftItem item,ImmutableArray<RoofScopeExclusion> exclusions)
    {
        if(item.MappingContractVersion is not (1 or 2))throw new NotSupportedException("Unsupported mapping contract version.");
        if(item.MappingContractVersion==2)return item;
        var classification=exclusions.Any(e=>e.ShellId==item.ShellId&&!string.IsNullOrWhiteSpace(e.User)&&!string.IsNullOrWhiteSpace(e.Reason))
            ?ShellZoneClassification.ExcludedWithReason:item.Classification==ShellZoneClassification.Unmapped?ShellZoneClassification.Unmapped:ShellZoneClassification.NotGeometricallyValidated;
        return item with{Classification=classification,MappingContractVersion=2,ZoneCoverages=[],ShellAreaM2=null};
    }
    public void AcceptScopeMerge(string user, string reason)
    {
        if (PendingScopeDraft is not { } pending || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A pending selection-scope change, reviewer and reason are required.");
        // Retain omitted objects and their mappings; the decision changes the selected scope only.
        var reconciled = pending with { SelectedScope = Shells3D.Select(s => s.ShellId).ToImmutableArray() };
        if (!RestoreDraft(reconciled, pending.ProjectId, pending.WindState.Id))
            throw new InvalidOperationException("Scope merge cannot accept modified source geometry.");
        ReviewStatus = GeometryReviewStatus.GeometryDraft;
        Local3Reviews = Local3Reviews.Add(new("Roof", "ScopeMerge", string.Join(",", pending.SelectedScope),
            string.Join(",", reconciled.SelectedScope), user, DateTimeOffset.UtcNow, reason));
    }
    public void SetLocal3Review(string shellId,ManualLocal3Direction direction,string user,string reason)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(string.IsNullOrWhiteSpace(user)||string.IsNullOrWhiteSpace(reason)||!Enum.IsDefined(direction)||direction==ManualLocal3Direction.Unacknowledged)
            throw new ArgumentException("An explicit Local 3 direction, engineer and review reason are required.");
        var row=RoofRows.FirstOrDefault(r=>r.ShellId==shellId)??throw new ArgumentException("Shell is not in the current selection.",nameof(shellId));
        RoofRows=RoofRows.SetItem(RoofRows.IndexOf(row),row with{ManualDirection=direction});
        Local3Reviews=Local3Reviews.Add(new(shellId,"Local3Review",row.ManualDirection.ToString(),direction.ToString(),user,DateTimeOffset.UtcNow,reason));
        ReviewStatus = GeometryReviewStatus.GeometryDraft;
    }

    public void ValidateGeometry()
    {
        if (Scan is null || Shells3D.IsEmpty || Shells3D.Any(s => !s.IsValid) || ReviewStatus is GeometryReviewStatus.StaleNeedsRescan or GeometryReviewStatus.ScopeReviewRequired)
            throw new InvalidOperationException("Refresh and resolve invalid or stale shell geometry before geometry validation.");
        ReviewStatus = GeometryReviewStatus.GeometryValidated;
    }

    public void CompleteGeometryReview(string user, string reason)
    {
        if (ReviewStatus != GeometryReviewStatus.GeometryValidated || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Explicit geometry validation, reviewer and reason are required. Pressure approval remains unavailable.");
        Local3Reviews = Local3Reviews.Add(new("Roof", "GeometryReview", "GeometryValidated", "GeometryReviewed", user, DateTimeOffset.UtcNow, reason));
        ReviewStatus = GeometryReviewStatus.GeometryReviewed;
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
    private void ClearSelection(){retainedDraft=null;PendingScopeDraft=null;Scan=null;Facade=null;Roof=null;Shells3D=[];RoofRows=[];Local3Reviews=[];Exclusions=[];draftsByState.Clear();ReviewStatus=GeometryReviewStatus.GeometryDraft;Issues=[];Outcome=CsiScanOutcome.Disconnected;}
    public void Clear(){Interlocked.Increment(ref revision);Connection=null;ClearSelection();}
    public void ResetToCleanUnmappedDraft(string windStateId)
    {
        retainedDraft = null; PendingScopeDraft = null;
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
