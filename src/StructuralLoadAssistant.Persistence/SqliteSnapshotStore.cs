using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Persistence;

public enum SnapshotKind
{
    Project, CalculationRunSnapshot, StandardsManifestSnapshot, CsiModelSnapshot,
    PatternMapping, FacadeMappingDraft, RoofMappingDraft, RoofMappingApprovalSnapshot,
    EngineeringOverride, AssignmentPreviewSnapshot, SimulatedAssignmentRun, ReportDefinition, ReportSnapshot
}

public sealed record SnapshotMetadata(string Id, SnapshotKind Kind, string ProjectId,
    string? CalculationRunId = null, string? CsiModelSnapshotId = null, string? ApprovalId = null,
    string? ParentId = null, int SchemaVersion = 1, DateTimeOffset CreatedAt = default,
    DataProvenance Provenance = DataProvenance.SyntheticDemoFixture, bool IsSimulation = true);

public sealed class SqliteSnapshotStore
{
    private readonly string connectionString;
    private const string Columns = "id,kind,project_id,calculation_run_id,csi_model_snapshot_id,approval_id,parent_id,schema_version,created_at,provenance,is_simulation";

    public SqliteSnapshotStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version is not (0 or 1)) throw new NotSupportedException($"Database schema version {version} is unsupported.");
        if (version == 0)
        {
            command.CommandText = """
                CREATE TABLE snapshots (
                    id TEXT PRIMARY KEY NOT NULL, kind TEXT NOT NULL, project_id TEXT NOT NULL,
                    calculation_run_id TEXT, csi_model_snapshot_id TEXT, approval_id TEXT, parent_id TEXT,
                    schema_version INTEGER NOT NULL CHECK(schema_version > 0), created_at TEXT NOT NULL,
                    provenance TEXT NOT NULL, is_simulation INTEGER NOT NULL CHECK(is_simulation IN (0,1)),
                    payload_type TEXT NOT NULL, payload TEXT NOT NULL, payload_hash TEXT NOT NULL
                );
                CREATE INDEX snapshots_project_kind ON snapshots(project_id,kind);
                CREATE TRIGGER snapshots_no_update BEFORE UPDATE ON snapshots
                BEGIN SELECT RAISE(ABORT,'Snapshots are append-only'); END;
                CREATE TRIGGER snapshots_no_delete BEFORE DELETE ON snapshots
                BEGIN SELECT RAISE(ABORT,'Snapshots are append-only'); END;
                PRAGMA user_version=1;
                """;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void Save<T>(SnapshotMetadata metadata, T value) => SaveCore(metadata, value, null, 0);

    // The revision check and insert share an immediate write transaction, including across processes.
    public void AppendRevision<T>(SnapshotMetadata metadata, T value, string prefix, int expectedBaseRevision)
        => SaveCore(metadata, value, prefix, expectedBaseRevision);

    private void SaveCore<T>(SnapshotMetadata metadata, T value, string? revisionPrefix, int expectedBaseRevision)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.ProjectId);
        if (metadata.SchemaVersion != 1) throw new NotSupportedException("Unsupported snapshot schema version.");
        if (!Enum.IsDefined(metadata.Kind) || !Enum.IsDefined(metadata.Provenance)) throw new ArgumentException("Unknown snapshot kind or provenance.");
        if (metadata.Kind == SnapshotKind.SimulatedAssignmentRun && !metadata.IsSimulation)
            throw new ArgumentException("Simulated assignments must be marked as simulations.");
        var payload = SnapshotJson.Serialize(value);
        var hash = Hash(payload);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        if (revisionPrefix is not null)
        {
            using var revisionCheck = connection.CreateCommand();
            revisionCheck.Transaction = transaction;
            revisionCheck.CommandText = "SELECT COALESCE(MAX(CAST(substr(id,$offset) AS INTEGER)),0) FROM snapshots WHERE kind=$kind AND project_id=$project AND substr(id,1,$length)=$prefix";
            Add(revisionCheck, "$offset", revisionPrefix.Length + 1);
            Add(revisionCheck, "$length", revisionPrefix.Length);
            Add(revisionCheck, "$prefix", revisionPrefix);
            Add(revisionCheck, "$kind", metadata.Kind.ToString());
            Add(revisionCheck, "$project", metadata.ProjectId);
            var latest = Convert.ToInt32(revisionCheck.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (latest != expectedBaseRevision)
                throw new InvalidOperationException($"Draft conflict: expected base revision {expectedBaseRevision}, current revision {latest}. Reopen the draft before saving; edits were retained.");
            if (metadata.Id != revisionPrefix + (latest + 1).ToString(CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Draft revision must immediately follow its expected base revision.");
        }
        using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = $"SELECT {Columns},payload_type,payload,payload_hash FROM snapshots WHERE id=$id";
        existing.Parameters.AddWithValue("$id", metadata.Id);
        using (var reader = existing.ExecuteReader())
        {
            if (reader.Read())
            {
                var prior = ReadMetadata(reader);
                if (metadata.CreatedAt == default) metadata = metadata with { CreatedAt = prior.CreatedAt };
                if (prior != metadata || reader.GetString(11) != TypeName<T>() || reader.GetString(12) != payload || reader.GetString(13) != hash)
                    throw new InvalidOperationException($"Snapshot '{metadata.Id}' already exists with different content or metadata.");
                return;
            }
        }
        if (metadata.CreatedAt == default) metadata = metadata with { CreatedAt = DateTimeOffset.UtcNow };
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = $"INSERT INTO snapshots ({Columns},payload_type,payload,payload_hash) VALUES ($id,$kind,$project,$calculation,$model,$approval,$parent,$schema,$created,$provenance,$simulation,$type,$payload,$hash)";
        Add(insert, "$id", metadata.Id); Add(insert, "$kind", metadata.Kind.ToString());
        Add(insert, "$project", metadata.ProjectId); Add(insert, "$calculation", metadata.CalculationRunId);
        Add(insert, "$model", metadata.CsiModelSnapshotId); Add(insert, "$approval", metadata.ApprovalId);
        Add(insert, "$parent", metadata.ParentId); Add(insert, "$schema", metadata.SchemaVersion);
        Add(insert, "$created", metadata.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        Add(insert, "$provenance", metadata.Provenance.ToString()); Add(insert, "$simulation", metadata.IsSimulation ? 1 : 0);
        Add(insert, "$type", TypeName<T>()); Add(insert, "$payload", payload); Add(insert, "$hash", hash);
        insert.ExecuteNonQuery();
        transaction.Commit();
    }

    public T? Load<T>(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_version,payload_type,payload,payload_hash FROM snapshots WHERE id=$id";
        Add(command, "$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return default;
        if (reader.GetInt32(0) != 1) throw new NotSupportedException("Unsupported snapshot schema version.");
        if (reader.GetString(1) != TypeName<T>()) throw new InvalidDataException("Snapshot payload type does not match requested type.");
        var payload = reader.GetString(2);
        if (Hash(payload) != reader.GetString(3)) throw new InvalidDataException("Snapshot content hash verification failed.");
        return JsonSerializer.Deserialize<T>(payload) ?? throw new InvalidDataException("Snapshot payload is null.");
    }

    public SnapshotMetadata? GetMetadata(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM snapshots WHERE id=$id";
        Add(command, "$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadMetadata(reader) : null;
    }

    public IReadOnlyList<string> ListIds(SnapshotKind? kind = null, string? projectId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM snapshots WHERE ($kind IS NULL OR kind=$kind) AND ($project IS NULL OR project_id=$project) ORDER BY id";
        Add(command, "$kind", kind?.ToString()); Add(command, "$project", projectId);
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read()) ids.Add(reader.GetString(0));
        return ids.AsReadOnly();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string TypeName<T>() => typeof(T).FullName ?? typeof(T).Name;
    private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static SnapshotMetadata ReadMetadata(SqliteDataReader reader) => new(
        reader.GetString(0), Enum.Parse<SnapshotKind>(reader.GetString(1)), reader.GetString(2),
        NullableText(reader, 3), NullableText(reader, 4), NullableText(reader, 5), NullableText(reader, 6),
        reader.GetInt32(7), DateTimeOffset.ParseExact(reader.GetString(8), "O", CultureInfo.InvariantCulture),
        Enum.Parse<DataProvenance>(reader.GetString(9)), reader.GetInt32(10) == 1);
    private static string? NullableText(SqliteDataReader reader, int column) => reader.IsDBNull(column) ? null : reader.GetString(column);
}
