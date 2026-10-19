# SQLite snapshot storage

`SqliteSnapshotStore` uses Microsoft.Data.Sqlite **8.0.28**, pinned in the persistence project and package lock. It writes a real SQLite database; no JSON-file or in-memory fallback is used. The caller supplies an absolute database path. The desktop runtime should use `Environment.SpecialFolder.LocalApplicationData` under `StructuralLoadAssistant`, outside installation directories.

```csharp
var store = new SqliteSnapshotStore(databasePath);
store.Save(new SnapshotMetadata(project.Id, SnapshotKind.Project, project.Id), project);
var reopened = store.Load<Project>(project.Id);
var ids = store.ListIds(SnapshotKind.Project, project.Id);
```

All thirteen kinds from kickoff section 12 are represented by `SnapshotKind`: project, calculation, standards manifest, CSI model, pattern mapping, facade draft, roof draft, roof approval, override, assignment preview, simulated run, report definition, and report snapshot. The generic API stores their strongly typed payloads without coupling storage to engineering, mapping, or reporting implementations. Each identifier is globally unique across kinds. Callers use concrete payload types consistently between writes and reads.

## Schema and immutability

First open creates schema migration 1 transactionally and sets `PRAGMA user_version=1`; reopening preserves existing rows. Future or unsupported database versions are rejected. Snapshot schema version 1 is explicit and checked on both writes and typed reads. Future payload schema versions require a deliberate reader/migration change.

The `snapshots` table has separate columns for ID, kind, project ID, calculation run ID, CSI model snapshot ID, approval ID, parent/revision ID, schema version, creation time, provenance, simulation flag, payload type, payload JSON, and SHA-256 hash. Project/kind are indexed. Relationship IDs are explicit logical references rather than foreign keys, permitting snapshot insertion in dependency-independent order; the workflow must validate relationship existence and business meaning before approval or assignment.

Serialization uses the domain `SnapshotJson` contract. SHA-256 covers the exact stored UTF-8 JSON; reads verify the bytes before deserializing and require the original concrete type. This detects accidental corruption, not malicious database replacement or edits by a user with filesystem access. Metadata remains queryable without deserialization through `GetMetadata`.

Parameterized inserts prevent IDs and payload values from becoming executable SQL. Transactions serialize duplicate checks and insertion. Repeating the same ID, metadata, type, and serialized payload is idempotent. Any difference conflicts. If omitted, creation time is filled with UTC time; retries with omitted time reuse the original value. Explicit creation times are preserved.

SQLite `BEFORE UPDATE` and `BEFORE DELETE` triggers reject mutation through ordinary SQL, including external application connections. Revision writes use new IDs and an optional parent ID. Existing approved JSON stays unchanged when a caller later edits its mutable draft object. No delete/update API exists. Triggers cannot prevent a database administrator from deliberately dropping them.

## Demo provenance

Metadata defaults to `SyntheticDemoFixture` and `IsSimulation=true`. Simulated assignment rows explicitly reject `IsSimulation=false`. Persistence records provenance; the engineering and approval layers remain responsible for preventing a synthetic payload from being classified as verified. Model product/version/fingerprints and approval details belong to their immutable typed payload; explicit IDs link their frozen sources.

## Verification

`dotnet test tests/StructuralLoadAssistant.Tests/StructuralLoadAssistant.Tests.csproj --no-restore -m:1 -nr:false --filter FullyQualifiedName~PersistenceTests`

The tests exercise actual temporary databases: reopen/read, SQLite file header, parameterized data containing SQL syntax, duplicate/conflict behavior, all required kinds and relationship columns, immutable approval across draft revisions, migration version, raw SQL update/delete rejection, tampered payload hash, unknown versions, wrong payload type, and false simulation claims. Evidence is in `artifacts/persistence-red-serial.txt` (six compiled-stub failures) and `artifacts/persistence-green.txt` (eight passing tests).
