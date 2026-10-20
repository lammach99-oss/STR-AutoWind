# Executed qualification
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
