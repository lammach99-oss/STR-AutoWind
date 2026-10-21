# Executed qualification
- [x] Sprint 3.4.2: Closed physical benchmark execution pipeline via BenchmarkExecutionPipeline; runs 3D geometry processing, roof plane reconstruction, UV projection, and production NetTopologySuite overlay engine (ZonePolygonEngine.Classify).
- [x] Sprint 3.4.2: Defined 2 explicit overlapping polygons for Shell 201 (Zone-B1-OverlapA and Zone-B1-OverlapB at 75% coverage each, 6.7082 m² intersection area, OverlappingZones classification, NeedsSplitInCSI = true).
- [x] Sprint 3.4.2: Strict qualification runner scripts/qualify-csi.ps1 parses benchmarkFrames and benchmarkShells schemas, propagates CSI_BENCHMARK_FIXTURE_PATH and CSI_PROCESS_ID, avoids silent GetObject fallback, checks TRX counters to reject 0-test runs, and writes machine-readable reports.
- [x] Sprint 3.4.2: Comprehensive benchmark completeness checks added: frame start/end XYZ coordinates, roof plane shell membership, native unit and SI multiplier consistency, and load direction/type validation.
- [x] Sprint 3.4.2: 226 automated tests passing; end-to-end integration test verifies complete pipeline from scan through plane reconstruction and polygon classification to validator checklist.
- [x] Sprint 3.4.1: Fail-closed benchmark qualification; 100% of declared checks must execute and pass for Passed status.
- [x] Sprint 3.4.1: Structured BenchmarkChecklist tracks Expected, Executed, Passed, Failed, MissingChecks, and FailedChecks.
- [x] Sprint 3.4.1: 3 distinct benchmark scenarios: Full containment (Shell 101), Half containment (Shell 102 CrossesZoneBoundary / NeedsSplitInCSI), Overlapping zones (Shell 201 OverlappingZones).
- [x] Sprint 3.4.1: 9 negative mutation tests prove no false Passed results (empty planes, missing 3D data, missing loads, wrong load kind, missing coverage, wrong classification, incomplete vertices, wrong slope, missing test execution).
- [x] Sprint 3.4.1: Strict physical runner qualify-csi.ps1 propagates ProcessId, runs independent benchmark, and fails on non-Passed outcome.
- [x] Sprint 3.4.1: Stale qualification invalidation in MainViewModel across disconnect, mode switch, model change, or geometry fingerprint change.
- [x] Sprint 3.2: XYZ/UV round trips; sloped/rotated/flat frames; stable coordinates across coplanar selection changes.
- [x] Concave/convex intersections; full/crossing/overlapping/unmapped classifications; tolerances, invalid/degenerate polygons.
- [x] Footprint union and explicit disconnected/holed rejection; edge snapping; undo/redo with audit and monotonic revisions.
- [x] SQLite polygon/audit restart, legacy snapshot compatibility and expected-base revision rejection.
- [x] Selection scope review and explicit merge retain omitted mapping/exclusion history; changed source shell is stale.
- [x] Actual WPF gable editor controls/vertex drag/crossing correction and exact polygon hashes across two processes.
- [x] Mock/live labels and disabled pressure approval/production assignment gates remain in place.
- [ ] Physical ETABS/SAP editor workflow qualification (mock regression is separate).
- [x] Sprint 3.1.1: 156 deterministic tests pass; physical CSI tests remain opt-in and excluded.
- [x] Pending dropdown values do not change active draft Save identity.
- [x] Actual WPF zone control enabled for valid mock read-only geometry; engineering controls disabled.
- [x] 2D polygon and 3D ray hits synchronize independent shell zone, sign, metadata and exclusion.
- [x] Geometry edits invalidate validation/review; explicit validation and separate review required.
- [x] Restore rejects project/model/state mismatch and clears mappings on stale geometry.
- [x] Transactional expected-base revision rejects stale saves and concurrent duplicate append.
- [x] Dirty edits survive refresh/disconnect/mode switching/close; failed saves retain edits and cancel close.
- [x] Two separate WPF processes share one database and restore independent WX+/WY- mappings and close-saved exclusion.
- [x] Repeated switching, revisions beyond 9, unchanged rescan, changed-geometry staleness, demo/read-only smoke.
- [ ] Sprint 3.1.1 physical CSI requalification (not required for mock workflow regression; no live assignments).

- [x] Sprint 3.1: Wind-state switching preserves independent drafts and avoids cross-state mapping contamination.
- [x] Sprint 3.1: SQLite draft versioning parses revisions numerically; optimistic concurrency rejects older revision overwrites.
- [x] Sprint 3.1: Real read-only mode permits geometry draft editing, Local 3 review, and draft saving while blocking engineering approvals/writes.
- [x] Sprint 3.1: Shell zone label assignment marked NotGeometricallyValidated rather than FullyInsideZone.
- [x] Sprint 3.1: 3D geometry validation flags nonplanar, self-intersecting, and degenerate polygons with IsValid and GeometryError.
- [x] Sprint 3.1.1: Full control/restart sequence covered by `scripts/test-wpf.ps1`; earlier Sprint 3.1 smoke alone did not prove this sequence.

- [x] Windows 10 x64, SDK 8.0.422 and Windows Desktop runtime verified.
- [x] Full kickoff contract read; older document priorities overridden by newer approved roof/facade workflow.
- [x] Original workbook SHA256 and sheet/VBA inventory; untouched originals and read-only ignored copies.
- [x] Purposeful solution projects, nullable, warnings-as-errors, deterministic build, package locks.
- [x] Compiled failing tests before implementation for engineering, roof, persistence, fake CSI, reports and application use cases.
- [x] Core SI/interpolation/integration, invalid NaN/Infinity/dimensions.
- [x] Facade equal/static resultant and moment, nonlinear integration, true overlaps, segmentation, accumulation, datum, global sign.
- [x] Roof inward/suction/mixed Local3, concave zones, projected geometry, crossing/overlap/unmapped blockers, audited exclusions and mismatch acknowledgment.
- [x] Edits/source state/pattern/model changes invalidate approval; immutable previous snapshots; alternative states require distinct patterns.
- [x] Fake area/frame preview/approval/apply/readback, duplicate protection, manual-load preservation, missing/case-pattern rejection, stale-source handling.
- [x] Metadata-only standard labels cannot authorize engineering approval; no live CSI writer.
- [x] SQLite migration, typed/versioned payload, immutable SQL triggers, conflict/idempotency, provenance and hash checks.
- [x] Reapproval override persistence and repeated Apply/Verify regressions.
- [x] Report reproducibility, section controls, HTML escaping, snapshot linkage and approved load-value consistency.
- [x] Actual WPF window launched/rendered: seven tabs; roof+facade fake workflow; clean shutdown; screenshots/JSON.
- [ ] Licensed normative dataset regression qualification.
- [ ] ETABS and SAP physical read-only integration and full write/rollback qualification.
- [ ] Human usability/accessibility review on multiple DPI/screen sizes.

## Sprint 2 read-only gates

- [x] Clean main synchronized with origin/main; direct main commits/pushes, no branch/PR/force-push.
- [x] Domain-only Csi.Abstractions read contracts; fake behavior retained.
- [x] Installed ETABS23/SAP27 metadata/enums/CHM inspected locally; no proprietary files added.
- [x] Getter-only runtime adapter, STA pump, return-code/units/disconnection diagnostics.
- [x] Actual ETABS23.2.0 attachment/definitions/units/locked/stories; empty selection disclosed.
- [x] Mock-backed geometry/axis/load conversion and unsupported-version contracts.
- [x] Explicit facade pairs, audited groups, existing equal/static formulas retained.
- [x] XYZ/Local3 and unknown outward metadata; real pressure Unmapped; no real approval/apply.
- [x] Refresh/cancel/mode changes invalidate stale inspection context.
- [x] Windows CI locked restore/Release build/tests without CSI installation.
- [x] Physical selected geometry/transform/load qualification in ETABS (Sprint 2.5 baseline qualified; Sprint 2.6 strict opt-in qualification harness).
- [x] Separation of automated CI test suite from opt-in physical qualification (`Category!=PhysicalCsi`).
- [x] Strict physical qualification runner (`scripts/qualify-csi.ps1`) with selection capture and safe restoration.
- [x] Independently reviewed baseline fixtures (`fixtures/physical-csi-reviewed-baseline.json`).
- [x] Explicit scan outcomes (`CsiScanOutcome`: Disconnected, Scanning, Succeeded, EmptySelection, PartialScan, Failed) and accurate status reporting.
- [x] Real 3D roof geometry model: Newell normal, slope, azimuth, 3D surface area, projected plan area, ear-clipping triangulation.
- [x] Interactive coordinated 2D plan and 3D orbit viewport with Local 3 arrows and two-sided rendering.
- [x] Persistent mapping drafts per wind state (WX+, WX-, WY+, WY-) stored transactionally in SQLite (`RoofMappingDraftStore`).
- [x] Geometry review status tracking (GeometryDraft, GeometryValidated, GeometryReviewed, StaleNeedsRescan).
- [x] Stale geometry detection and reconciliation preventing stale draft application to modified models.
- [x] Real read-only safety gates preserved: engineering calculation, approvals, and CSI assignments strictly disabled.
- [ ] Physical SAP2000 connection/scan qualification.
- [ ] PID-aware instance selection and advanced 3D display.

Final WPF dual-mode smoke/results are in IMPLEMENTATION_STATUS.md. Mock screenshots are not actual customer scan proof.

Latest build/test counts and output paths are in IMPLEMENTATION_STATUS.md. A passing fake test does not qualify a live model or wind standard.
## Sprint 3.4 current qualification

- [x] 211 automated tests pass with physical tests excluded, zero failures and zero skips.
- [x] Safe PID-aware read-only attachment implemented and tested (CsiProcessDiscovery, GetObjectProcess).
- [x] Multi-instance disambiguation guard: refuses silent arbitrary attachment when multiple instances run.
- [x] Process termination, PID reuse, model disconnect, and model mismatch detection.
- [x] Independent geometry benchmark framework (etabs-roof-independent-benchmark.json, IndependentGeometryBenchmarkValidator).
- [x] Accuracy gate decoupled from connection, scan, and review statuses (PhysicalQualificationOutcome).
- [x] Roof mapping prerequisites enforce complete and valid geometry input for future pressure engines (RoofPressurePrerequisites).
- [x] All 6 WPF smoke processes pass cleanly with visual and process-result artifacts.
- [ ] Independent physical benchmark verification against live ETABS with the dedicated benchmark model open (reported as Inconclusive when model not loaded).
- [ ] SAP2000 physical qualification (reported as Pending).

## Sprint 3.3 current qualification

- [x] 201 automated tests pass with physical tests excluded, no silently skipped physical assertions counted.
- [x] Locked restore, Debug/Release builds; real WPF demo/read-only and disk-restart regressions.
- [x] Distinct mapping states; structured coverage roundtrip; comma-containing zone IDs stay opaque.
- [x] Legacy snapshots interpreted conservatively without rewriting; unsupported versions preserve editor state; failed batches are atomic.
- [x] WPF overlap/coverage blockers, viewing-space controls, state independence and pressure/write safety gates.
- [x] Real ETABS 23.2.0 PID 34456 read-only selected scan and live editor workflow/persistence exercised.
- [ ] Independent ETABS GUI coordinate/area/intersection comparisons for this selected model.
- [ ] Physical multiple-plane/opposite-Local-3 qualification for the roof editor.
- [ ] Physical stale-source check against an externally prepared disposable changed copy.
- [ ] SAP2000 physical editor qualification.

See [Sprint 3.3 qualification matrix](SPRINT_3_3_QUALIFICATION.md). Historical checkmarks in earlier sections do not close these current pending checks. No application-generated selection/model/unit/file mutation was used.
