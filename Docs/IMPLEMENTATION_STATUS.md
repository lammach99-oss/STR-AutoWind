# Implementation status — 2026-10-20

## Sprint 3.1.1: Final roof workflow fixes

This section supersedes earlier Sprint 3.1 acceptance claims. Verification uses explicitly labeled mock read-only geometry; it does not physically qualify ETABS or SAP2000.

- Active state is `currentActiveState`; pending direction/internal-pressure/roof-case dropdown changes cannot redirect Save. Switching saves dirty edits under the old active state before restoring the target.
- RealReadOnly zone control binds to valid selected geometry. Both 2D polygon events and 3D ray hit selection synchronize zone, Local 3 sign, exclusion, review status and shell metadata.
- Zone, exclusion and Local 3 changes invalidate geometry validation. Explicit geometry validation and a separate reviewer action are required for GeometryReviewed. Neither action grants pressure approval.
- Restoration validates project, model, wind-state and geometry identity. Stale geometry clears mappings and blocks editing/saving.
- Expected-base revisions are checked inside the same SQLite immediate transaction as insertion. Stale editors cannot invent a newer revision; concurrent append regression permits exactly one winner.
- Dirty applied geometry edits save before refresh, disconnect, mode switch and close. Save failures retain edits and block the destructive operation; close is cancelled.
- `scripts/test-wpf.ps1` runs demo and mock read-only smoke plus the actual-control roof regression in two distinct OS processes sharing one SQLite database. It checks pending dropdown Save, independent states/shells, 3D hits, review invalidation, revisions beyond 9, reconnect, dirty-close persistence, unchanged refresh and changed-geometry staleness. A changed database destination exercises failed-save refresh/disconnect/close guards.
- Locked restore succeeds. Debug and Release builds: zero warnings/errors. Automated tests: **156 passed, 0 failed, 0 skipped**, excluding opt-in PhysicalCsi. All four WPF processes pass with clean shutdown. CI executes the same WPF script and retains synthetic screenshots/JSON without databases.

Limitations: physical CSI tests were not run for this UI/persistence correction; SAP physical qualification remains pending. Existing normative-data and production-write blockers remain. A stale draft requires matching geometry to resume; no automatic geometry migration is provided. Only edits applied through the geometry commands become draft changes; uncommitted dropdown selections are editor inputs.

## Sprint 3.1: Roof Mapping Workflow Stabilization

Fixed workflow and data integrity issues identified during Sprint 3 review:

- **Wind-State Switching (Task A):**
  - Dedicated active-state identity maintained independently of the dropdown selection.
  - Required switching sequence strictly implemented:
    1. Persist outstanding edits under current active state ID.
    2. Change active state to target state.
    3. Load latest saved draft for target state.
    4. If no draft exists, initialize clean unmapped draft (no copied mappings).
    5. Update inspector and views.
  - Added HasUnsavedChanges tracking to UI and view models.
- **SQLite Draft Versioning (Task B):**
  - Numerical revision extraction (ExtractRevision) prevents lexicographical sorting errors (e.g. revision 15 correctly ordered after revision 9).
  - Optimistic concurrency check in RoofMappingDraftStore.SaveDraft rejects saving an older revision over a newer revision.
  - Added ValidateAndReconcile validating project ID, model ID, wind state ID, and geometry fingerprint.
- **Real Read-Only Geometry Draft Editing (Task C):**
  - Enabled geometry draft operations in RealReadOnly mode: state switching, draft saving/restoring, geometry zone label assignment, Local 3 sign review, and shell exclusion.
  - Kept normative pressure calculations, engineering approvals, load previews, and CSI assignments disabled.
  - Renamed UI controls to distinguish "Geometry Zone Review" from verified "Wind Pressure Zone Approval".
- **Geometry Review Semantics (Task D):**
  - Introduced ShellZoneClassification.NotGeometricallyValidated to distinguish user-assigned geometry labels from mathematically verified zone boundary containment.
  - Geometric review cannot be confused with engineering pressure approval.
- **3D Geometry Validation Hardening (Task E):**
  - Reject/flag nonplanar, degenerate zero-area, and self-intersecting (bowtie) polygon shells.
  - Newell normal cancellation on self-intersecting polygons handled gracefully with non-collinear vertex plane discovery.
  - Validated triangulated area against surface area within tolerance; recorded IsValid and GeometryError.
- **Automated and End-to-End WPF Acceptance Tests (Task F):**
  - 151 unit and integration tests passing in CI.
  - Historical smoke coverage did not prove the full control/restart sequence; superseded by the Sprint 3.1.1 two-process control harness above.
## Sprint 3: Visual roof geometry, wind mapping drafts & persistence

Built interactive 2D plan and 3D orbit viewers, real 3D roof geometry models, and persistent per-wind-state SQLite drafts with geometry staleness reconciliation:

- **Sprint 2.6 Qualification Fixes:**
  - Strict physical qualification asserts coordinates, opposite Local 3 vectors, and loads against `fixtures/physical-csi-reviewed-baseline.json`.
  - `scripts/qualify-csi.ps1` explicitly routes SAP2000 requests to pending/inconclusive status without running ETABS tests.
  - Selection capture and restoration preserves all CSI object types (Points, Frames, Areas, Links).
  - Single element-type selections (e.g. only frames or only shells) correctly evaluate to `CsiScanOutcome.Succeeded`, reserving `PartialScan` for warning conditions.
- **Real 3D Roof Geometry Model:**
  - Added `RoofShell3D`, `Roof3DGeometryProcessor`, Newell normal calculation, slope degrees, azimuth compass angles, 3D true surface areas, plan projected areas, and triangulation.
- **Interactive 2D and 3D Viewers:**
  - Added hardware-accelerated WPF `Roof3DViewport` with orbit/pan/zoom/reset, two-sided shading, and centroid Local 3 arrows.
  - Coordinated interactive shell selection across 2D plan Canvas and 3D Viewport.
  - Dynamic coloring based on zone classifications (Edge, Interior/Field, Corner, Excluded, Crosses boundary, Unmapped).
- **Persistent Wind-State Mapping Drafts:**
  - Drafts keyed by project ID, model ID, and wind state (WX+, WX-, WY+, WY-, internal pressure states, roof cases).
  - Transactionally stored in SQLite via `RoofMappingDraftStore` (`SnapshotKind.RoofMappingDraft`).
  - State switching saves and restores independent draft mappings; drafts survive application restarts.
  - Geometry changes detect fingerprint mismatches and flag `GeometryReviewStatus.StaleNeedsRescan`.
- **Read-Only Safety Gates:**
  - RealReadOnly mode keeps pressure calculations, engineering approvals, and live CSI write operations strictly disabled.

### Verification summary
- Automated test suite: **145 passed, 0 failed, 0 skipped** across all solution projects.
- Dual-mode WPF smoke tests passed: `--smoke` (Demo mode) and `--smoke-readonly` (RealReadOnly mode) completed cleanly with visual verification artifacts generated.
- SAP qualification reporting: correctly outputs pending status.

## Sprint 2.6: CSI qualification hardening

Hardened physical qualification testing, test suite separation, and live CSI status reporting:

- **Separation of CI from physical tests:** Categorized physical qualification tests under `[Trait("Category", "PhysicalCsi")]`. Automated CI and standard testing runs (`scripts/test.ps1`) execute with `--filter Category!=PhysicalCsi`, passing all 134 automated unit/contract tests without failing on environments without live CSI installations.
- **Strict physical qualification runner:** Created `scripts/qualify-csi.ps1` with strict validation logic. Requires explicit opt-in (`CSI_PHYSICAL_QUALIFICATION=1`), validates process presence, assembly paths, and elements against independently reviewed fixtures. It fails or marks inconclusive clearly rather than silently passing on empty/missing connections.
- **Selection capture and safe restoration:** When `-SetupSelection` is passed, the runner queries and records existing element selections in ETABS, temporarily selects benchmark frames and shells, and restores initial user selections in a `finally` block.
- **Independently reviewed baseline fixtures:** Created `fixtures/physical-csi-reviewed-baseline.json` storing reviewed geometry coordinates, opposite Local 3 vectors, and load values for benchmark elements (Frames 49, 75, 50, 78; Areas 10, 18, 64, 84).
- **Explicit scan outcomes:** Introduced `CsiScanOutcome` enum (`Disconnected`, `Scanning`, `Succeeded`, `EmptySelection`, `PartialScan`, `Failed`). Updated `CsiReadContracts`, `InstalledCsiReadAdapter`, `CsiInspectionSession`, and `MainViewModel`. UI status explicitly differentiates empty, partial, and failed scans; never displays `LIVE CSI SCAN SUCCEEDED` on invalid scans.
- **Safety gates preserved:** Engineering calculations, approvals, and production CSI writes remain strictly disabled in RealReadOnly mode.

### Verification summary
- Automated test suite: **134 passed, 0 failed, 0 skipped** across all solution projects.
- WPF smoke test (`--smoke`) and read-only smoke test (`--smoke-readonly`): both passed with clean shutdown and all safety assertions verified.
- Physical qualification script (`scripts/qualify-csi.ps1`): verified for Sap2000 (correctly outputs `Inconclusive / Pending: process not active`).

## Sprint 2: real CSI read-only integration

This section supersedes the initial milestone's CSI and fixed-selection limitations below. No production CSI writes or normative wind calculation have been enabled.

Created Csi.Abstractions (Domain-only read contracts) and Csi.ApiV1 (optional runtime-loaded installed getter adapter). Extracted the read interface while retaining fake simulated-write records/type names. Added explicit FacadeSelection and CsiInspectionSession, audited grouping/Local3 review, selection/async invalidation, and separate WPF Demo versus RealReadOnly modes. Demo facade pairs are now editable rather than hardcoded; reaction mathematics is unchanged.

The adapter validates inspected getter signatures, checks return codes, converts present units to SI without changing model units, and uses an owned STA with a message pump for connect/read/release. It reads definitions, supported story/base records, selected frame/shell XYZ, Local3 and selected distributed/uniform loads. Unknown outward direction is never guessed. Unsupported axes retain XYZ with unknown-axis metadata. Fatal model/unit staleness clears the whole scan.

### Actual verification

- Full locked restore succeeded. Debug solution build: zero warnings/errors. Full automated suite: **128 passed, 0 failed, 0 skipped** in artifacts/sprint2/sprint2-tests.trx (91 baseline plus 37 extraction/adapter/inspection/pair regressions).
- Actual live ETABS23.2.0 GetObject attachment and definition scan succeeded: locked model, N_mm_C, story/base records, patterns/cases/combinations, zero scan issues. Selection was empty. Therefore selected real XYZ, axes and assignment interpretation remain physically unqualified. Local private evidence: api-live-result-final.txt.
- Actual SAP2000 absence returned Connected=false with DISCONNECTED; installed SAP27 metadata/help inspected, but no running SAP model was qualified. SAP20/other versions are outside the current gate.
- Native WPF demo smoke passed with both fake readbacks true. Native mock-readonly smoke passed with Mode=RealReadOnly, mock source labeling, no assignment/readback, no synthetic pressure mapping, guard assertions and clean shutdown. Both rendered all seven tabs, with additional CSI/roof detail captures. Local paths: artifacts/sprint2/ui-demo-final and ui-readonly-final. These screenshots are actual windows with synthetic/mock data, not actual customer geometry scans.
- Independent review checked getter signatures against installed help, matrix Local3 column indexing, base-story count, dimensional conversions, source/mode boundaries and refresh races. Corrected optional-axis data loss, stale-scan retention, stale capabilities display and atomic group review. Final test suite covers relevant regressions.
- First Windows CI commit 789e90c completed successfully on a clean GitHub-hosted Windows runner without CSI installed. Main-push workflow uses pinned SDK/locked restore/Release build/tests and diagnostic TRX artifacts. Later main commits trigger the same workflow; final run URL/status is included in the handoff.

### Environment and scope disclosures

Checked-in global.json requested SDK10.0.400 although only8.0.422 was installed and this sprint requires .NET8. Restored pin8.0.422. Local sandbox MSBuild attempts sometimes returned no diagnostics or orphaned workers; inspected parent command lines and removed only workers from exited build processes, then used single-node builds with server/node reuse disabled for reliable verification. No application process or user model was terminated/modified for validation.

No proprietary DLL/help, actual model path/name/geometry, workbook/specification, DB or generated scan was staged. Shared read-only contracts can build/test without a CSI installation. Existing dependency advisories remain documented separately in SECURITY_NOTES.md; no breaking upgrade was introduced.

### Exact next steps

1. Select known horizontal frame pairs and roof shells in an already open disposable ETABS23 model; Refresh Selection and independently compare XYZ, mixed Local3, load extents and SI conversion. Adapter never changes CSI selection.
2. Repeat with nontrivial rotations and alternate native units, then verify no dirty/model-unit state change.
3. Run SAP200027 and repeat connection, selected geometry and load tests; retain unsupported story/optional API diagnostics.
4. Add inspected version support only after installed signature/help and physical read qualification; add explicit PID selection for multiple running instances.
5. Add 3D display for vertical/nonplanar roofs and engineer-verified physical outward-normal provenance before pressure mapping.
6. Resolve native SQLite/test dependency advisories in a separate regression-tested update. Import only licensed/reviewed standards data before enabling engineering pressure calculation.
7. Production CSI writes remain a future separately authorized milestone requiring backup, ownership/conflict, rollback and physical readback qualification.

## Initial P0/P1 milestone (historical evidence)

Implemented preliminary P0/P1 with native WPF demo and SQLite/report proof (P2/P3). This is a simulated milestone, not production wind-code or CSI qualification.

## Created

Ten-project solution: nine purposeful source projects (Domain, Engineering, RoofMapping, Csi.Fake, Persistence, Reporting, Application, Demo, App.Wpf), plus one .NET8 test project. Responsibility consolidation is documented in ARCHITECTURE.md. Native executable is `src/StructuralLoadAssistant.App.Wpf/bin/Debug/net8.0-windows/StructuralLoadAssistant.App.Wpf.exe`; console executable is under Demo/bin/Debug/net8.0.

Repository initialized locally on milestone/bootstrap. Original Docs and Reference Workbook content preserved; workbook sources excluded from Git/binaries. Settings include Nullable, deterministic output, warnings-as-errors, package locks, pinned .NET8 SDK, editorconfig and build/test/run scripts. DI/debug logging are composed in WPF Program.cs.

## Executed verification

```powershell
dotnet restore StructuralLoadAssistant.sln --locked-mode -m:1 -nr:false
dotnet build StructuralLoadAssistant.sln --no-restore -m:1 -nr:false
dotnet test tests/StructuralLoadAssistant.Tests --no-build --no-restore -m:1 -nr:false --logger 'trx;LogFileName=final.trx' --results-directory artifacts
dotnet run --no-build --project src/StructuralLoadAssistant.Demo -- artifacts/final-demo
dotnet run --no-build --project src/StructuralLoadAssistant.App.Wpf -- --smoke --smoke-output artifacts/desktop-final
```

Build: 0 warnings, 0 errors. Automated tests: **91 passed, 0 failed, 0 skipped** (final.trx). Valid compiled RED/GREEN evidence exists for each pure feature group and review fixes under artifacts. Generated template test is not counted as engineering qualification.

Final one-command verification also executed `./scripts/build.ps1 -Locked`: restore/build/tests all succeeded, 91/91, artifacts/tests.trx. Both independent core reviews were remediated: engineering-label gate, alternative-pattern conflict, reapproval override IDs, repeat apply/verify, report source/proposal checks, and state-specific facade pattern selection. Corresponding failing regression evidence preceded fixes; no outstanding review finding is silently waived.

NuGet restore initially failed under restricted proxy (127.0.0.1:9); approved network escalation restored pinned packages. A later unprivileged locked restore also failed NU1900 because online vulnerability metadata was unreachable, then was rerun with network permission. No dependency audit was silently disabled.

WPF was actually launched, not merely compiled: seven tab images rendered from the real window, Success=true, Roof State=Verified, both Roof/FacadeReadback=true, one stored report, clean shutdown marker. Evidence: artifacts/desktop-final/{01-Project..07-Report}.png, smoke-summary.json, shutdown-complete.txt, desktop-report.html, demo.db. Root visually inspected the actual roof capture. This automation checks view-model commands/rendering, not exhaustive mouse/keyboard usability or real CSI.

Headless evidence: artifacts/final-demo/project.db, report.md, report.html, session-state.json and fixture-model.json. Roof readback is true, facade readback is true, manual loads preserved=2. Approval and report snapshots persisted; reports reopen by exact ID.

## Demonstrated behavior

- EqualSplit panel1: 2.1/2.1 kN/m; StaticallyEquivalent: 2.0/2.2. Middle beam accumulated loads: 4.65/4.70 kN/m. Global sign follows physical normal; true overlap and invalid geometry tested.
- Roof synthetic suction -1500 Pa: outward Local3 R1 -> +1500, inward R2 -> -1500. Projected zone map, click selection, editable zone/sign/pattern, audited mismatch/exclusion, crossing blockers, validation, immutable simulation approval and edit invalidation.
- Distinct selected wind direction/internal state/case resets draft and uses its existing target pattern. Alternative states cannot share a pattern inside one approval.
- Fake area/frame apply and readback, source staleness protection, repeat idempotency, owned-load conflict, existing manual loads retained. No production writer exists.
- Independent wall height/material/openings/direct gravity math and desktop preview; illustrative materials.
- SQLite schema1 typed immutable snapshots, deterministic Markdown/HTML reports with source-link/proposal checks and HTML XY diagram. Predefined section toggles, stored-report reopening.

## Limits / next concrete tasks

1. `Engineering/Standards.cs`, standards-data manifests: import licensed, rights-cleared exact revision data and independent reference fixtures; qualify both calculation stages before enabling engineering calculation.
2. Complete the private cell/VBA read-only audit outside the public repository; compare differing source baselines; retain historical parity separately from corrected standard fixtures.
3. `Application/DemoSession.cs`, `App.Wpf/MainWindow.cs`: expose arbitrary validated facade frame pairs and review tributary boundaries beyond fixed fixture pairs.
4. `Csi.Fake/CsiContracts.cs`: extract vendor-neutral Csi.Abstractions; add installed-version read-only API adapter and unit/axis/result-code compatibility tests; no writes yet.
5. `Persistence/SqliteSnapshotStore.cs`, `Application/DemoSession.cs`: transactional use-case batches, immutable active-draft revision persistence/resume, fake-ledger rehydration and persistent project chooser.
6. `Reporting/ReportModel.cs`: extend signed-source trace model, report templates, then PDF/DOCX exporter implementations without recalculation.
7. `App.Wpf/MainViewModel.cs`: broaden typed input editing and input diagnostics once coefficient datasets exist; human accessibility/DPI/keyboard review.
8. Extend tests with independent domain-engineer reviewed roof zones, complex facade topology and sloping-ground policies.
9. Before live writes: backup/SaveAs, conflict resolution, partial failure/rollback, ownership and physical ETABS/SAP readback gates.

Standards: **verified normative data: none**; unverified metadata: both requested editions; synthetic demo: all demonstrated pressures/materials. CSI: fake only; genuine versions supported/tested: none. Proprietary dependencies do not block this demo.
