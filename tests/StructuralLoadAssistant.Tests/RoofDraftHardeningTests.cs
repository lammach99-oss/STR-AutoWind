using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Persistence;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public sealed class RoofDraftHardeningTests
{
    [Fact]
    public async Task Concurrent_editors_have_exactly_one_successful_append()
    {
        var path = Path.Combine(Path.GetTempPath(), $"roof-race-{Guid.NewGuid():N}.db");
        var state = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
        try
        {
            var a = new RoofMappingDraftStore(new SqliteSnapshotStore(path));
            var b = new RoofMappingDraftStore(new SqliteSnapshotStore(path));
            var draft = new RoofMappingDraft(RoofMappingDraftStore.DraftId("P", "M", state.Id, 1),
                1, "P", "M", "geometry", state, [], [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            using var start = new ManualResetEventSlim();
            Task<bool> Save(RoofMappingDraftStore store) => Task.Run(() =>
            {
                start.Wait();
                try { store.SaveDraft("P", draft, 0); return true; }
                catch (InvalidOperationException) { return false; }
            });
            var first = Save(a); var second = Save(b); start.Set();
            Assert.Single((await Task.WhenAll(first, second)).Where(success => success));
            Assert.Equal(1, a.LoadLatestDraft("P", "M", state.Id)!.Revision);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Stale_editor_cannot_save_a_new_revision_over_a_concurrent_edit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"roof-cas-{Guid.NewGuid():N}.db");
        var state = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
        var first = new RoofMappingDraft(RoofMappingDraftStore.DraftId("P", "M", state.Id, 1),
            1, "P", "M", "geometry", state, [], [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        try
        {
            var editorA = new RoofMappingDraftStore(new SqliteSnapshotStore(path));
            var editorB = new RoofMappingDraftStore(new SqliteSnapshotStore(path));
            editorA.SaveDraft("P", first, 0);
            var second = first with { Id = RoofMappingDraftStore.DraftId("P", "M", state.Id, 2), Revision = 2 };
            editorA.SaveDraft("P", second, 1);
            // Editor B loaded revision 1. It must not skip straight to revision 3.
            var stale = first with { Id = RoofMappingDraftStore.DraftId("P", "M", state.Id, 3), Revision = 3 };
            Assert.Throws<InvalidOperationException>(() => editorB.SaveDraft("P", stale, expectedBaseRevision: 1));
            Assert.Equal(2, editorA.LoadLatestDraft("P", "M", state.Id)!.Revision);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_rejects_mismatched_project_identity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"roof-identity-{Guid.NewGuid():N}.db");
        var state = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
        try
        {
            var store = new RoofMappingDraftStore(new SqliteSnapshotStore(path));
            var draft = new RoofMappingDraft(RoofMappingDraftStore.DraftId("P", "M", state.Id, 1),
                1, "WRONG", "M", "geometry", state, [], [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            Assert.Throws<InvalidOperationException>(() => store.SaveDraft("P", draft, 0));
        }
        finally { File.Delete(path); }
    }
}
