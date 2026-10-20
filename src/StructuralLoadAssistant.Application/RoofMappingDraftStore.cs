 using System.Collections.Immutable;
 using StructuralLoadAssistant.Domain;
 using StructuralLoadAssistant.Persistence;
 using StructuralLoadAssistant.RoofMapping;
 
 namespace StructuralLoadAssistant.Application;
 
 public sealed class RoofMappingDraftStore
 {
     private readonly SqliteSnapshotStore store;
 
     public RoofMappingDraftStore(SqliteSnapshotStore store)
     {
         this.store = store ?? throw new ArgumentNullException(nameof(store));
     }
 
     public static string DraftIdPrefix(string projectId, string modelId, string windStateId) =>
         $"draft:{projectId}:{modelId}:{windStateId}";
 
     public static string DraftId(string projectId, string modelId, string windStateId, int revision) =>
         $"{DraftIdPrefix(projectId, modelId, windStateId)}:r{revision}";

    public static int ExtractRevision(string draftId)
    {
        int idx = draftId.LastIndexOf(":r", StringComparison.Ordinal);
        if (idx >= 0 && int.TryParse(draftId.AsSpan(idx + 2), out int rev))
        {
            return rev;
        }
        return 0;
    }
 
     public void SaveDraft(string projectId, RoofMappingDraft draft, int expectedBaseRevision)
     {
         ArgumentNullException.ThrowIfNull(draft);
         ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.GeometryFingerprint);

        if (draft.ProjectId != projectId || draft.Revision < 1 ||
            draft.Id != DraftId(projectId, draft.ModelId, draft.WindState.Id, draft.Revision))
            throw new InvalidOperationException("Draft identity or revision mismatch.");
 
         var metadata = new SnapshotMetadata(
             draft.Id,
             SnapshotKind.RoofMappingDraft,
             projectId,
             CsiModelSnapshotId: draft.ModelId,
             CreatedAt: draft.UpdatedAt,
             Provenance: DataProvenance.Unverified,
             IsSimulation: false
         );
 
         store.AppendRevision(metadata, draft,
             DraftIdPrefix(projectId, draft.ModelId, draft.WindState.Id) + ":r", expectedBaseRevision);
     }
 
     public RoofMappingDraft? LoadLatestDraft(string projectId, string modelId, string windStateId)
     {
         var prefix = DraftIdPrefix(projectId, modelId, windStateId);
         var matching = store.ListIds(SnapshotKind.RoofMappingDraft, projectId)
            .Where(id => id.StartsWith(prefix + ":r", StringComparison.Ordinal))
            .OrderByDescending(ExtractRevision)
             .ToArray();
 
         if (matching.Length == 0) return null;
 
         return store.Load<RoofMappingDraft>(matching[0]);
     }
 
     public IReadOnlyList<RoofMappingDraft> LoadAllDraftsForModel(string projectId, string modelId)
     {
         var drafts = new List<RoofMappingDraft>();
         var matching = store.ListIds(SnapshotKind.RoofMappingDraft, projectId)
             .Where(id => id.Contains($":{modelId}:", StringComparison.Ordinal))
             .ToArray();
 
         var byState = matching
             .GroupBy(id => id.Substring(0, id.LastIndexOf(":r", StringComparison.Ordinal)))
             .ToArray();
 
         foreach (var group in byState)
         {
            var latestId = group.OrderByDescending(ExtractRevision).First();
             var draft = store.Load<RoofMappingDraft>(latestId);
             if (draft != null) drafts.Add(draft);
         }
 
         return drafts;
     }
 
    public static GeometryReviewStatus ValidateAndReconcile(
        RoofMappingDraft draft,
        string expectedProjectId,
        string expectedModelId,
        string expectedWindStateId,
        string currentGeometryFingerprint)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!string.Equals(draft.ProjectId, expectedProjectId, StringComparison.Ordinal) ||
            !string.Equals(draft.ModelId, expectedModelId, StringComparison.Ordinal) ||
            !string.Equals(draft.WindState.Id, expectedWindStateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Draft identity mismatch: draft belongs to project '{draft.ProjectId}', model '{draft.ModelId}', state '{draft.WindState.Id}', but attempted restoration for '{expectedProjectId}', '{expectedModelId}', '{expectedWindStateId}'.");
        }
        if (string.IsNullOrWhiteSpace(currentGeometryFingerprint) ||
            !string.Equals(draft.GeometryFingerprint, currentGeometryFingerprint, StringComparison.Ordinal))
        {
            return GeometryReviewStatus.StaleNeedsRescan;
        }
        return draft.ReviewStatus;
    }

    public static GeometryReviewStatus ReconcileGeometry(RoofMappingDraft draft, string currentGeometryFingerprint)
     {
         ArgumentNullException.ThrowIfNull(draft);
         if (string.IsNullOrWhiteSpace(currentGeometryFingerprint) ||
             !string.Equals(draft.GeometryFingerprint, currentGeometryFingerprint, StringComparison.Ordinal))
         {
             return GeometryReviewStatus.StaleNeedsRescan;
         }
         return draft.ReviewStatus;
     }
 
 
    public int GetNextRevision(string projectId, string modelId, string windStateId)
    {
        var latest = LoadLatestDraft(projectId, modelId, windStateId);
        return (latest?.Revision ?? 0) + 1;
    }
}
