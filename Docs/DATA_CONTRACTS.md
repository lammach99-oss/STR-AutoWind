# Data contracts and snapshot rules

Sprint 3.3 geometry mappings/draft items carry `MappingContractVersion` (absent = 1), `ZoneCoverages` (structured opaque ZoneId, intersection m², percent), and `ShellAreaM2`. Version 2 separates OverlappingZones, InvalidGeometry and ExcludedWithReason; original enum numbers remain stable. Scalar ZoneId is populated only for a single coverage (manual labels remain unverified). Never parse commas as identity boundaries. `BlocksPressureMapping` is conservative geometry metadata, not engineering approval. The editor demotes ambiguous version 1 evidence and requires explicit revalidation; the stored snapshot bytes/hash remain immutable and a later save appends a revision. Unknown versions are rejected before modifying the editor. This is versioned payload interpretation; SQLite schema is unchanged.

Domain/Contracts.cs owns stable project/calculation/model/frame/shell IDs, normalized WindState(direction,internal pressure,roof case,ULS/SLS), provenance and SI snapshots. Synthetic calculation IDs are deterministic per standard and selected wind state; date/profile/trace are fixture values, not generated engineering results.

Engineering public numeric names carry units. FrameSegmentLoad stores relative start/end fractions (0..1) and inward scalar N/m with explicit state/pattern. Global signed vectors are captured in FacadeFrameProposal. WallResult uses positive gravity magnitude and negative global Z.

RoofPressureZone defines synthetic projected polygon/state/net pressure. AreaLoadProposal captures shell/state/zone/pattern/net Pa/manual direction/axes/signed CSI Pa separately. RoofMappingApproval captures every source plus audit/exclusions/time/hash. SHA256 covers serialized immutable content, with sorted pattern keys and hash excluded from its own payload.

AssignmentRun.IsSimulation is always true; area/facade assignments have separate run types and explicit approval links. A hash detects content changes; it is not an engineer digital signature or proof of normative data review.

ReportModel references exact source snapshots and optional roof/facade assignments/readbacks. Export validates references and frozen proposal values. ReportDefinition toggles predefined sections; rendering cannot recalculate pressure or load.

SqliteSnapshotStore schema 1: explicit id/kind/project/calculation/model/approval/parent/version/time/provenance/simulation/type columns plus deterministic payload/hash. Relationships are explicit logical IDs, not row-order keys. No mutable snapshot overwrite; append a new ID/revision. See PERSISTENCE.md for SQL triggers and unsupported schema handling.
