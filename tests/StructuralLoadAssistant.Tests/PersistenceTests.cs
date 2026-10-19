using Microsoft.Data.Sqlite;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Persistence;

namespace StructuralLoadAssistant.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "sla-persistence-" + Guid.NewGuid());
    private string Database => Path.Combine(directory, "snapshots.db");
    private static SnapshotMetadata Metadata(string id = "project-1", SnapshotKind kind = SnapshotKind.Project) =>
        new(id, kind, "project-1", CreatedAt: new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void PersistsAcrossReopenInRealSqliteDatabase()
    {
        var project = new Project("project-1", "O'Brien; DROP TABLE snapshots;", "SI");
        new SqliteSnapshotStore(Database).Save(Metadata(), project);
        Assert.Equal(project, new SqliteSnapshotStore(Database).Load<Project>(project.Id));
        Assert.StartsWith("SQLite format 3", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Database), 0, 15));
    }

    [Fact]
    public void SameIdIsIdempotentButChangedPayloadOrMetadataConflicts()
    {
        var store = new SqliteSnapshotStore(Database);
        var project = new Project("project-1", "First", "SI");
        store.Save(Metadata(), project);
        store.Save(Metadata(), project);
        Assert.Single(store.ListIds());
        Assert.Throws<InvalidOperationException>(() => store.Save(Metadata(), project with { Name = "Changed" }));
        Assert.Throws<InvalidOperationException>(() => store.Save(Metadata() with { ProjectId = "different" }, project));
        Assert.Equal(project, store.Load<Project>(project.Id));
    }

    [Fact]
    public void PreservesRelationshipAndDemoMetadataForEveryRequiredKind()
    {
        var store = new SqliteSnapshotStore(Database);
        foreach (var kind in Enum.GetValues<SnapshotKind>())
        {
            var metadata = Metadata(kind.ToString(), kind) with
            { CalculationRunId = "calculation", CsiModelSnapshotId = "model", ApprovalId = "approval", ParentId = "parent" };
            store.Save(metadata, new Project(metadata.Id, "Synthetic", "SI"));
            Assert.Equal(metadata, store.GetMetadata(metadata.Id));
            Assert.Equal(new[] { metadata.Id }, store.ListIds(kind, "project-1"));
        }
        Assert.Empty(store.ListIds(projectId: "absent"));
        Assert.Null(store.Load<Project>("absent"));
    }

    [Fact]
    public void SqliteRejectsUpdateAndDeleteAndSetsMigrationVersion()
    {
        var store = new SqliteSnapshotStore(Database);
        store.Save(Metadata(), new Project("project-1", "Original", "SI"));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        Assert.Equal(1L, command.ExecuteScalar());
        foreach (var sql in new[] { "UPDATE snapshots SET payload='{}'", "DELETE FROM snapshots" })
        {
            command.CommandText = sql;
            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        }
    }

    [Fact]
    public void TypedReadDetectsTamperedHashAndUnknownPayloadVersion()
    {
        var store = new SqliteSnapshotStore(Database);
        store.Save(Metadata(), new Project("project-1", "Original", "SI"));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DROP TRIGGER snapshots_no_update; UPDATE snapshots SET payload='{}'";
        command.ExecuteNonQuery();
        Assert.Throws<InvalidDataException>(() => store.Load<Project>("project-1"));
        command.CommandText = "UPDATE snapshots SET schema_version=999";
        command.ExecuteNonQuery();
        Assert.Throws<NotSupportedException>(() => store.Load<Project>("project-1"));
    }

    [Fact]
    public void RejectsUnknownDatabaseVersionAndUnsupportedWrites()
    {
        var store = new SqliteSnapshotStore(Database);
        Assert.Throws<NotSupportedException>(() => store.Save(Metadata() with { SchemaVersion = 2 }, new Project("x", "x", "SI")));
        using (var connection = Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version=999";
            command.ExecuteNonQuery();
        }
        Assert.Throws<NotSupportedException>(() => new SqliteSnapshotStore(Database));
    }

    [Fact]
    public void ApprovedPayloadIsFrozenWhenLaterDraftChanges()
    {
        var store = new SqliteSnapshotStore(Database);
        var draft = new Dictionary<string, string> { ["shell-1"] = "zone-A" };
        store.Save(Metadata("approval-1", SnapshotKind.RoofMappingApprovalSnapshot), draft);
        draft["shell-1"] = "zone-B";
        store.Save(Metadata("draft-2", SnapshotKind.RoofMappingDraft) with { ParentId = "approval-1" }, draft);
        Assert.Equal("zone-A", store.Load<Dictionary<string, string>>("approval-1")!["shell-1"]);
        Assert.Equal("zone-B", store.Load<Dictionary<string, string>>("draft-2")!["shell-1"]);
    }

    [Fact]
    public void RejectsWrongPayloadTypeAndFalseSimulationClaim()
    {
        var store = new SqliteSnapshotStore(Database);
        store.Save(Metadata(), new Project("project-1", "Original", "SI"));
        Assert.Throws<InvalidDataException>(() => store.Load<Dictionary<string, string>>("project-1"));
        Assert.Throws<ArgumentException>(() => store.Save(
            Metadata("fake-run", SnapshotKind.SimulatedAssignmentRun) with { IsSimulation = false }, new { Result = "ok" }));
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
