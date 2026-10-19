# Executed qualification

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
- [ ] Physical SAP2000 connection/scan qualification.
- [ ] PID-aware instance selection and advanced 3D display.

Final WPF dual-mode smoke/results are in IMPLEMENTATION_STATUS.md. Mock screenshots are not actual customer scan proof.

Latest build/test counts and output paths are in IMPLEMENTATION_STATUS.md. A passing fake test does not qualify a live model or wind standard.
