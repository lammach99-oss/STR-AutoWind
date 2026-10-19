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
 
     public void SaveDraft(string projectId, RoofMappingDraft draft)
     {
         ArgumentNullException.ThrowIfNull(draft);
         ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
 
         var metadata = new SnapshotMetadata(
             draft.Id,
             SnapshotKind.RoofMappingDraft,
             projectId,
             CsiModelSnapshotId: draft.ModelId,
             CreatedAt: draft.UpdatedAt,
             Provenance: DataProvenance.Unverified,
             IsSimulation: false
         );
 
         store.Save(metadata, draft);
     }
 
     public RoofMappingDraft? LoadLatestDraft(string projectId, string modelId, string windStateId)
     {
         var prefix = DraftIdPrefix(projectId, modelId, windStateId);
         var matching = store.ListIds(SnapshotKind.RoofMappingDraft, projectId)
             .Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
             .OrderByDescending(id => id, StringComparer.Ordinal)
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
             var latestId = group.OrderByDescending(id => id, StringComparer.Ordinal).First();
             var draft = store.Load<RoofMappingDraft>(latestId);
             if (draft != null) drafts.Add(draft);
         }
 
         return drafts;
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
 }
 
