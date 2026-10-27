# Ms provenance maintenance and private intake — 2026-10-27

- Owner separately authorized the Ms provenance fix and private intake subject to software-use permissions. Exact/interpolated shielding source evidence is retained without reclassifying it as an assumption; no formulas or numerical operands changed. No-shielding-benefit remains an explicit assumption.
- TDD reproduced two provenance failures before the fix; targeted regressions are green. Locked restore and Debug/Release builds passed (zero warnings/errors); 553/553 nonphysical automated tests passed in each configuration.
- Private intake verified unchanged source PDF/workbook hashes and recorded a 13-table local extraction plan. Numerical extraction remains blocked pending documented software-use permission, governing jurisdiction/adoption and independent benchmarks. No protected numerical dataset was populated. See `ASNZ_MS_PROVENANCE_AND_PRIVATE_INTAKE.md`.
- Normative qualification, engineering approval and live CSI writes stay disabled. Earlier milestone notes below remain historical.

# Automatic AS/NZS review lookup closure — 2026-10-27

Baseline: `b6674bca99032fb23f8089177a358b9974f854eb`. The bounded owner directive is implemented as source-gated review software; real-standard numerical lookup remains BLOCKED pending software-use rights and evidence. See `ASNZ_AUTOMATIC_LOOKUP_COVERAGE.md` for exact complete/partial/blocked table coverage.

- Added roof-form/face/pitch routing with exact ten-degree boundary, explicit unsupported incidence, A distance bands versus B/C whole-face extents, same-sign h/d interpolation and steep C breadth/depth requirement. Single-face gable/hip lookup is available; full multi-face building results remain blocked.
- Added terrain/height source grids, source-scoped wall support/height basis, state-specific shielding, explicit true-north/eave/peak/slope inputs, endpoint/weight audit, and external-only review results when internal pressure is unavailable. No normative table numbers are embedded.
- Closed QA-4B7-01/02: direct selectors require coherent V3 bytes, exact scope, valid SI units and source receipts; imported requests cannot self-assert automatic provenance. Private receipts prove local execution/evidence hashes, not legal or engineering acceptance.
- Locked restore passed. Final Debug and Release builds: zero warnings/errors. Automated tests: **550 passed, zero failed/skipped** in each configuration; physical CSI tests excluded. All **eleven WPF smoke scenarios passed**, including real bound-control sloped review and process-restart workflows. Valid RED evidence retained privately under `artifacts/lookup-closure/`.
- Owner-workbook cached arithmetic: **8/8** local comparisons, separate from automated/independent engineering qualification. Source PDF/workbook remained unchanged. Private register stores table locators, source identity and rights/amendment limitations; no protected coefficient package was populated.
- Production normative qualification, engineering approval and live CSI mutation stay disabled. Read-only CSI physical verification was not performed in this task. A0/coast/terrain transitions, separate internal/external height methods, unsupported monoslope incidence/leeward branches, complete gable/hip geometry, independent benchmarks, authorized datasets and amendment decisions remain explicit blockers.

The earlier milestone notes below are historical; their test totals are not current.

# Implementation status — 2026-10-23

## Sprint 4B.0.1: Intake Readiness Truthfulness & Owner Evidence Handoff

- **Truthful Readiness Gate (No Error Bypasses)**: Hardened StandardsDataBundleValidator.GenerateIntakePreflight so that overall status derivation inspects the complete StandardsValidationReport. Any issue at StandardsValidationSeverity.Error (e.g. missing unit, incomplete internal pressure states, invalid amendment, count mismatch) or FatalBlocker prevents ReadyForIndependentReview and drives IntakeQualificationStatus.Invalid. All Error and FatalBlocker issues are aggregated directly into the preflight blockers list with explicit issue codes and messages.
- **Robust Integrity & Coverage Flag Deduction**: Refactored boolean indicators (SemanticHashValid, ComponentBytesCoherent, ProcedureCoverageValid) from brittle negative-lists to comprehensive proof-of-check rules. SemanticHashValid strictly requires non-empty ContentHash and absence of semantic hash failures, non-finite values, and missing keys. ComponentBytesCoherent verifies IsBytesVerified and absence of JSON malformation, byte mismatches, missing/empty files, extra undeclared files, duplicate keys, or unsafe paths. ProcedureCoverageValid requires non-empty coverage matrix and absence of empty mandatory categories, incomplete internal pressure states, and procedure/edition mismatches.
- **Deep Reference Benchmark Eligibility**: Overhauled reference fixture qualification in GenerateIntakePreflight. Verifies non-synthetic status, matching family/edition, matching procedure ID when specified, non-empty assertions, finite expected values, non-negative finite absolute and relative tolerances, and independent engineering review attestation (rejecting self-review and unverified reviewers). Synthetic fixtures remain strictly non-production regardless of numerical match.
- **Mandatory Procedure-Scoped LimitState Applicability**: Hardened StandardsDataBundle.MatchesProcedureMetadata to strictly require an explicit LimitState categorical condition on records. Records lacking LimitState are rejected during procedure-scoped retrieval.
- **Owner Handoff & Licensed Intake Guidance**: Clarified protected private storage boundaries (fixtures/standards/private/, artifacts/standards-private/, *.pdf) and documented practical legal intake handoff requirements for ASCE 7-22 and AS/NZS 1170.2:2021.
- **Safety Invariant Preserved**: CanCalculateNormativePressures == false across all bundles, statuses, and rights classifications. CSI writes, pressure calculations, and engineering approvals remain strictly disabled.
- **Verification**: 325 automated tests pass (Debug/Release, non-physical, 10 new regression tests). All 6 WPF smoke processes pass.

## Sprint 4B.0: Standards Qualification Preflight & Fail-Closed API Closure

- **Fail-Closed Public Validation Boundary (Zero Unhandled Exceptions)**: Eliminated unhandled exceptions from StandardsDataBundleValidator.Validate() for all anticipated invalid inputs. An unsupported hash version (e.g. 4.0 or arbitrary) returns a structured STD_HASH_VERSION_UNSUPPORTED FatalBlocker issue without throwing NotSupportedException. Malformed component JSON bytes produce STD_COMPONENT_JSON_MALFORMED without throwing JsonException. Missing/empty component files emit STD_COMPONENT_FILES_EMPTY. Non-finite record values (NaN, +Inf, -Inf) and numerical conditions emit STD_NONFINITE_VALUE and STD_NONFINITE_NUMERICAL_CONDITION without calling hashing or throwing InvalidOperationException. Direct throwing contracts on ComputeCanonicalHash() remain intact when called directly.
- **Genuine Procedure Identity, Method, and Limit-State Scoping**: Hardened StandardsDataBundle.FindRecordForProcedure(ProcedureSelection, ...) to enforce non-empty edition, valid procedure ID, recognized method, valid limit state, and system type. Records must declare explicit procedure applicability metadata (ProcedureId/Procedure, Method, LimitState) to be retrieved in procedure scope; bare records lacking procedure metadata return null.
- **Strict Direct Unscoped Lookup Invariant**: Enhanced MatchesCategorical() so that direct unscoped calls to FindExactRecord() without a validated systemType parameter strictly require explicit "System" condition matching; "System" condition omission relaxation is strictly restricted to validated procedure-scoped queries.
- **Standards Intake Preflight Reporting (GenerateIntakePreflight)**: Delivered StandardsIntakePreflightReport providing separate, truthful auditing of source rights, V3 structural semantic integrity, component byte coherence, procedure coverage, local evidence file hash verification (Verified, MissingFile, HashMismatch, NotPerformed), human review role separation (SelfDeclaredMetadata, EvidenceHashVerified, IndependentEngineerReviewed), non-synthetic reference benchmark availability, and overall qualification readiness (Blocked, Invalid, Unverified, ReadyForIndependentReview).
- **Owner Handoff & Checklist Readiness**: Updated Docs/STANDARDS_DATA_INTAKE_AND_ENGINEER_REVIEW.md with explicit owner checklists for ASCE 7-22 and AS/NZS 1170.2:2021. Both standards are accurately reported as Blocked / MissingExternalEvidence.
- **Safety Invariant Preserved**: CanCalculateNormativePressures == false across all bundles and rights categories. All real CSI writes, production pressure calculations, and engineering approvals remain strictly disabled.
- **Verification**: 315 automated tests pass (Debug/Release, non-physical). All 6 WPF smoke processes pass.

## Sprint 4A.3: Final Standards-Data Trust Closure & Licensed Reference Intake Preflight

- **Structural Canonical Semantic Hash (V3)**: Implemented CANONICAL_STANDARDS_BUNDLE_V3 with individual element length prefixing ([len]:[utf8_bytes];) for nested collections, amendments, coverage matrix entries, and condition maps. Eliminates delimiter-injection collisions across joined strings (e.g. {"A":"B,C=D"} vs {"A":"B","C":"D"} and ["A,B"] vs ["A","B"]). Preserved V1/V2 read compatibility for synthetic fixtures while enforcing fail-closed rejection for unsupported algorithm versions. Enforced finite-only double checks and normalized -0.0 to +0.0.
- **Source-Byte / In-Memory Record Coherence Closure**: Hardened StandardsDataBundleValidator.RecordsMatch() to compare all engineering fields using exact StringComparison.Ordinal, explicitly including ApplicabilityNotes. Mutating in-memory applicability notes or condition values while retaining verified component bytes is caught and blocked as STD_RECORD_BYTES_MISMATCH.
- **Graceful Duplicate Raw Component Keys**: Replaced direct dictionary construction with duplicate key grouping, returning structured STD_DUPLICATE_SOURCE_KEY FatalBlocker issues rather than throwing unhandled exceptions.
- **Fail-Closed System Scoping & Substring False-Positive Elimination**: Enhanced MatchesSystem to reject unrecognized system types and eliminate category substring heuristics (Contains("CC")). Added FindRecordForProcedure(ProcedureSelection, ...) enforcing family, edition, and procedure-scoped system isolation.
- **Licensed Data Intake Kit & Private Directory Protection**: Added fixtures/standards/private/, artifacts/standards-private/, and *.pdf to .gitignore. Delivered public JSON templates in fixtures/standards/templates/ (template-manifest.json, template-coefficients.json, template-reference-calculation.json) and comprehensive documentation in Docs/STANDARDS_DATA_INTAKE_AND_ENGINEER_REVIEW.md. Added StandardsDataBundleValidator.VerifyEvidenceDocument() preflight verification helper.
- **Safety Invariant Maintained**: CanCalculateNormativePressures == false across all bundles and rights categories. RealReadOnly pressure calculation, approvals, and live CSI writes remain disabled.
- **Verification**: 297 automated tests pass (Debug/Release, non-physical). All 6 WPF smoke processes pass.

## Sprint 4A.2: Standards Bundle Coherence & Procedure-Safe Coefficient Retrieval

- **Source-Byte and In-Memory Record Coherence Gate**: Implemented strict verification in `StandardsDataBundleValidator.Validate()` comparing in-memory records against records deserialized from verified raw component bytes. Any discrepancy in record counts, record keys, values, conditions, units, or sign conventions triggers `STD_RECORD_BYTES_MISMATCH` (FatalBlocker). A bundle with valid component SHA256 and matching semantic hash but mutated in-memory records is strictly rejected.
- **Delimiter-Injection Collision-Proof Canonical Hashing (V2)**: Implemented length-prefixed UTF-8 canonical encoding (`CANONICAL_STANDARDS_BUNDLE_V2`) in `StandardsDataBundle.ComputeCanonicalHash()`. Length-prefixed segments (`[len]:[utf8_bytes];`) mathematically prevent collisions from special characters (`|`, `=`, `,`, `;`, `:`) in keys, categories, locators, or conditions. Preserved V1 backward compatibility for synthetic v1 fixtures via `CanonicalHashAlgorithmVersion`.
- **Strict Exact Numerical Record Matching**: Enhanced `FindExactRecord()` with bidirectional numerical condition matching. Rejects lookups when records have numerical dimensions (e.g. pitch, height) but query conditions are missing or partial. Enforces exact IEEE 64-bit numerical equality, eliminating loose floating tolerances for exact lookup.
- **Procedure-Scoped System Isolation**: Decoupled bundle integrity validation from procedure system scope. Bundles legitimately containing both MWFRS and Components & Cladding (C&C) records pass schema and integrity checks without false `STD_SYSTEM_MISMATCH` errors. Querying with `systemType: "MWFRS"` strictly isolates MWFRS records from C&C records.
- **Evidence Hash Format Validation**: Enforced valid 64-character hexadecimal SHA-256 format for `EvidenceDocumentHash` on non-synthetic bundles (`STD_EVIDENCE_HASH_INVALID`).
- **Verification**: **279 automated unit and integration tests pass** with zero failures and zero skips in both Release and Debug configurations (8 new coherence and procedure-scope tests). All 6 WPF smoke processes pass in Release mode with clean shutdown markers.
- **Safety Gates Maintained**: Production normative wind pressure calculations, pressure approvals, load proposals, and live CSI write operations remain strictly disabled in RealReadOnly mode.

## Sprint 4A.1: Standards Data Integrity Hardening, Provenance, and Qualification-Gate Closure

- **Comprehensive Canonical Semantic Hash**: Expanded `StandardsDataBundle.ComputeCanonicalHash()` to hash all engineering-significant fields into a versioned canonical preimage: bundle ID, family, edition, rights, sorted amendments, sorted coverage items, and for each record sorted by key: record key, category, table/figure, clause locator, sign convention, physical unit, interpolation policy, invariant value string, sorted categorical conditions, sorted numerical conditions, and applicability notes. Excludes `Manifest.ContentHash` from its own preimage. Semantic hash is invariant to JSON whitespace and formatting on disk, while raw component file SHA256 detects byte-level changes.
- **Mandatory Component File & Path Verification**: `StandardsDataBundleValidator.Validate()` enforces mandatory component byte presence (`STD_INTEGRITY_UNVERIFIED` on missing bytes), safe relative path traversal validation (`STD_UNSAFE_PATH` rejecting rooted, `..`, or colon paths), component file existence (`STD_COMPONENT_MISSING`), component file SHA256 checksum matching (`STD_HASH_MISMATCH`), and extra undeclared component detection (`STD_EXTRA_COMPONENT`). Verified bytes are immutably captured in `StandardsDataBundle.RawComponentBytes` to prevent TOCTOU races.
- **Strict Promotion Gate**: `CanCalculateNormativePressures` is strictly `false` across the board in Sprint 4A.1, even for non-synthetic manifests with seemingly valid review metadata. Prevents premature calculation eligibility before Sprint 4B.
- **Reviewer Independence Verification**: Enforced distinct preparer and reviewer identities (`STD_SELF_APPROVAL_REJECTED` when preparer equals reviewer), verified reviewer organization, and mandatory evidence document hash for non-synthetic datasets. Asserted metadata without verified artifacts is treated as unverified.
- **Exact Procedure Coverage & Isolation**: Mandatory coverage matrix enforces exact record count matching (`STD_COVERAGE_COUNT_MISMATCH`). MWFRS vs. Components & Cladding (C&C) procedure queries strictly isolate systems (`STD_SYSTEM_MISMATCH`). Internal pressure sign alternatives check elevated to `Error` severity (`STD_INTERNAL_PRESSURE_INCOMPLETE`).
- **Hardened Lookup & Interpolation**: `FindExactRecord` requires bidirectional exact categorical matching, numerical condition tolerance matching, and system type filtering. `Interpolate1D` requires identical categorical regimes in both directions, identical physical units, sign conventions, source tables, and numerical conditions, rejecting extrapolation and Bilinear2D policies. `ValidateDomain` validates finite, ordered bounds ($MinValue \le MaxValue$) and strictly rejects out-of-range extrapolation.
- **Reference Calculation Benchmark Hardening**: `ReferenceCalculationValidator.Evaluate()` fails closed on empty fixtures (`REF_FIXTURE_EMPTY`), duplicate expected factor names, duplicate expected pressure keys, or negative/non-finite tolerances. Strictly enforces exact `(ZoneId:StateId)` pressure key matching, eliminating zone-only fallback. Distinguishes numerical match (`IsNumericalMatch`) from production authorization (`CanAuthorizeProductionCalculation`), which remains strictly `false`.
- **Verification**: **271 automated unit and integration tests pass** with zero failures and zero skips in both Release and Debug configurations (21 new integrity hardening tests). All 6 WPF smoke processes pass in Release mode with clean shutdown markers.
- **Safety Gates Maintained**: Production normative wind pressure calculations, pressure approvals, load proposals, and live CSI write operations remain strictly disabled in RealReadOnly mode.

## Sprint 4A: Wind Standards Data Foundation, Provenance, and Qualification Gates

- **Standards Data Foundation Contracts**: Created versioned, strongly typed domain contracts in `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataContracts.cs` separating ASCE 7-22 and AS/NZS 1170.2:2021 domain models without false shared abstractions. Contracts define `StandardFamily`, `StandardDataLifecycleState`, `DataRightsClassification`, `InterpolationPolicy`, `ProcedureSelection`, `ParameterDomain` (with `AllowsExtrapolation = false` default), `CoefficientRecord` (with stable keys, units, locators, condition maps), and `EngineeringReviewTrace` (requiring independent reviewer attestation, evidence document URI, and cryptographic hash; self-approval rejected).
- **Standards Data Bundle Parser & Canonical Content Hashing**: Implemented `StandardsDataBundle.cs` supporting directory-based and in-memory bundle loading, manifest parsing, component file resolution, and deterministic canonical content hashing (`ComputeCanonicalHash()`) over sorted records to detect tampering or accidental modification.
- **Fail-Closed Validation Engine**: Implemented `StandardsDataBundleValidator.cs` enforcing all 9 fail-closed validation rules: manifest integrity, component file SHA256 checksums, component presence, mandatory procedure coverage completeness, duplicate/conflicting key rejection, non-finite value rejection, parameter domain boundary enforcement (no extrapolation), categorical boundary interpolation rejection, explicit AS/NZS amendment resolution (`Amendment 1:2023`, `Amendment 2:2024`), independent reviewer attestation, and strict promotion prevention: synthetic test data (`SyntheticTestData` / `SyntheticDemoFixture`) can never authorize normative pressure calculations (`CanCalculateNormativePressures` strictly `false`).
- **Reference Calculation Benchmark Contracts**: Created `ReferenceCalculationContracts.cs` defining `StandardReferenceCalculationFixture` with expected intermediate factors, expected net pressures, analytical tolerances, and reviewer trace. Evaluator compares actual vs expected factors and net pressures within strict tolerances.
- **Rights-Cleared Synthetic Fixtures**: Added rights-cleared, non-normative test fixtures under `fixtures/standards/` for ASCE 7-22 (`asce7-22-synthetic`), AS/NZS 1170.2:2021 (`asnz1170-2-2021-synthetic`), and reference calculations (`reference-calculations/`), clearly labeled `TEST ONLY / SYNTHETIC / NOT STANDARDS-COMPLIANT`. No licensed tables or proprietary coefficients were introduced.
- **Protective Benchmark Housekeeping**: Added `[switch]$Force` and fail-closed overwrite guard to `scripts/create-benchmark-model.ps1` preventing accidental overwrite of existing benchmark models.
- **Backward Compatibility**: Preserved legacy `StandardCalculators.Asce7_22` and `StandardCalculators.AsNz1170_2_2021` signatures and fail-closed gating.
- **Verification**: **250 automated unit and integration tests pass** with zero failures and zero skips in both Debug and Release configurations (17 new deterministic foundation tests). All 6 WPF smoke processes pass in Release mode with clean shutdown markers.
- **Safety Gates Maintained**: Production normative wind pressure calculations, pressure approvals, load proposals, and live CSI write operations remain strictly disabled in RealReadOnly mode.

## Sprint 3.4: Independent ETABS geometry qualification & safe instance selection

- **Safe PID-Aware Attachment**: Added CsiProcessDiscovery to enumerate running ETABS and SAP2000 instances. The WPF CSI tab now provides PID targeting, process scanning, and validation. If multiple instances are active without a selected PID, the application refuses to silently attach to an arbitrary process. Process termination, PID reuse, model disconnects, and model identity mismatches are detected and handled safely.
- **Independent Benchmark Framework**: Created fixtures/etabs-roof-independent-benchmark.json containing independently calculated reference values for a dual-pitch gable roof with 4 divided panels on 2 sloped planes, opposite Local 3 orientations, test-zone intersections, and existing loads.
- **Accuracy Validator**: Implemented IndependentGeometryBenchmarkValidator comparing coordinates, Local 3 vectors, roof plane transforms, 3D surface areas, projected areas, test zone intersections, and existing loads against strict absolute and relative tolerances. Missing fixtures or non-matching selections produce Inconclusive, not Passed.
- **Roof Mapping Accuracy Gate**: Added RoofPressurePrerequisites to ensure future pressure calculation engines cannot consume legacy, incomplete, crossing, overlapping, or unverified zone mappings.
- **Verification**: 211 automated unit and integration tests pass with zero failures and zero skips. All 6 WPF smoke processes pass in Release mode with clean shutdown markers.

## Sprint 3.3: Mapping integrity and partial live verification

The active geometry mapping contract now distinguishes zone overlap from shell overlap, invalid geometry and audited exclusion. Labels never infer crossing or containment. Version 2 rows/draft items preserve structured zone IDs, per-zone intersection areas/coverage and shell surface area. Crossing, overlap, invalid, unmapped, excluded and unverified rows conservatively block future pressure mapping; a geometrically complete row does not authorize engineering pressure approval.

Legacy immutable SQLite payloads are interpreted as version 1 without rewriting stored JSON or hashes. Ambiguous old classifications lose geometric validation in the editor; opaque comma-containing IDs remain opaque. Explicit revalidation creates version 2 evidence in a new revision. Existing enum numeric values and database schema remain unchanged. Unsupported versions fail before clearing current edits, and failed intersection batches publish no partial rows.

The editor shows readable coverage and NeedsSplitInCSI blockers, independent geometry-review/pressure-approval status and adjustable viewing space. State changes/reopening clear stale selected-zone metadata. RealReadOnly calculation, proposal, engineering approval and assignment gates remain disabled.

Verification: locked restore; Debug/Release zero-warning builds; **201 automated tests**, no failures/skips (17 new cases); six successful synthetic WPF processes including disk restart. Real ETABS 23.2.0 PID 34456: selected 12 frames/10 valid shells, one roof plane; actual WPF containment/crossing/overlap and WX+/WY− draft restoration in new processes passed. **Independent physical accuracy qualification remains inconclusive** without GUI-reviewed expectations; multi-plane/opposite-axis/changed-source checks remain pending. See [the detailed matrix and local evidence references](SPRINT_3_3_QUALIFICATION.md). Older sections below are historical milestone records, not current capability totals.

## Sprint 3.2: Interactive roof zone geometry

Geometry-only roof editing now uses plane-local surface meters with a deterministic orthonormal frame and global XYZ inverse transform. Coplanar shells share a plane; separate slopes/elevations remain separate even with overlapping XY projections. Plane normal, CSI Local 3 and physical outward normal remain distinct.

- Persistent editable polygons include identity, label, plane/state, UV coordinates, source fingerprint, revision, notes, timestamps and edit audit. Legacy label-only snapshots remain readable and immutable. Existing SQLite expected-base concurrency is unchanged.
- Surface editor supports polygon drawing/finish/cancel, shell multi-selection, footprint union, zone selection, vertex and whole-polygon drag, insert/delete vertices, edge/vertex snapping within 0.10 m, zoom/pan/reset, undo/redo and metadata editing. Coordinates and areas are displayed in meters/m²; 3D overlays follow the selected zone and shell scope.
- NetTopologySuite 2.6.0 performs validated simple-polygon intersections and union. Each shell reports true surface area, per-zone intersection and coverage, full containment, crossing/NeedsSplitInCSI, overlapping zones, invalid geometry, unmapped or audited exclusion. Labels alone never establish polygon containment. Absolute area tolerance is 1e-6 m² and relative tolerance 1e-7 of shell area.
- Geometry edits invalidate review. Intersection validation and engineer geometry review remain separate; no pressure dataset, engineering approval or CSI assignment is enabled.
- Per-shell source fingerprints distinguish changed selection scope from changed existing geometry. Scope differences block editing/saving until an explicit reviewer/reason merge. Omitted mappings, exclusions and polygon data are retained; no CSI selection or model is modified by the editor.
- WPF acceptance uses a clearly labeled divided gable mock with four shells. Actual vertex-drag events produce a crossing, Undo restores full containment, independent WX+/WY− geometries survive separate OS processes, exact coordinate/identity hashes match after restart, and changed source geometry blocks reuse. Windows CI runs the same six-process smoke script.

Geometry limits: zones represent one simple polygon without holes. Disconnected or holed shell unions are rejected explicitly. Curved/nonplanar shells are invalid; no auto-repair, mesh splitting or normative zone generation occurs. Planarity tolerance is 1e-6 m; near-coincident roof planes require engineer inspection. The legacy XY view remains a global projection; the UV editor supplies surface geometry for sloped roofs. Session undo history restarts on state/source reload, while polygon audit history persists.

Dependency review: pinned NTS package is BSD-style with bundled third-party notices, retained in `NETTOPOLOGYSUITE_LICENSE.md`; it adds no vendor binary. Historical ETABS physical getter qualification is unchanged and was not rerun in this sprint; SAP2000 physical qualification remains pending. New editor proof is mock-only.

Verification: locked restore, Debug/Release builds with zero warnings/errors; **184 automated tests, zero failed/skipped**, excluding opt-in PhysicalCsi. All six WPF smoke processes pass, including exact polygon restart hashes. Independent geometry and integration review findings were reproduced with regressions and corrected (stable UV axes, invalid polygon rendering, omitted-shell invalidation and undo audit lineage). Screenshots and process evidence remain under ignored `artifacts/sprint32`; CI uploads synthetic PNG/JSON diagnostics without databases.

Next normative integration: obtain licensed, independently reviewed ASCE 7-22 and AS/NZS 1170.2:2021 zone definitions and applicability inputs; add versioned provenance and source clause references; map verified definitions into this plane-local polygon contract with reviewed reference geometry tests. Geometry validation must remain separate from normative pressure validation and engineering approval. No coefficients are supplied by this sprint.

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


## Sprint 3.4.1 — Fail-Closed Physical Benchmark Qualification

- **Status**: Complete.
- **Automated Tests**: 226 passed, 0 failed, 0 skipped. All 6 WPF smoke processes verified.
- **Key Accomplishments**:
  - Hardened `IndependentGeometryBenchmarkValidator` to fail-closed with structured `BenchmarkChecklist`.
  - Formalized 3 distinct benchmark scenarios in `etabs-roof-independent-benchmark.json` (Full, Half, Overlapping).
  - Added 9 negative mutation tests proving no false Passed results.
  - Strengthened `qualify-csi.ps1` and `CsiPhysicalQualificationTests.cs` with PID propagation and fail-closed non-zero exit on non-Passed outcome.
  - Implemented stale qualification invalidation in `MainViewModel`.
- **Safety Guarantees**: Engineering calculations, approvals, and CSI writes remain disabled in RealReadOnly mode.


## Sprint 3.4.2 — Close the Physical Benchmark Execution Pipeline

- **Status**: Complete.
- **Automated Tests**: 226 passed, 0 failed, 0 skipped. All 6 WPF smoke processes verified.
- **Key Accomplishments**:
  - Implemented `BenchmarkExecutionPipeline` to execute real production geometry components (Roof3DGeometryProcessor, RoofPlaneCoordinateSystem, ZonePolygonEngine.Classify).
  - Defined 2 explicit overlapping polygons for Shell 201 with exact mathematical areas and coverage percentages.
  - Hardened `scripts/qualify-csi.ps1` with fixture path propagation, schema modernization, zero-test TRX verification, and machine-readable reports.
  - Added frame start/end coordinates and plane shell coplanarity checks to `IndependentGeometryBenchmarkValidator`.
  - Created deterministic integration tests covering full pipeline and failure conditions.
- **Safety Guarantees**: All engineering wind calculations, pressure approvals, and CSI writes remain disabled in RealReadOnly mode.


## Sprint 3.4.2 Preflight Hotfix — Physical Runner Hardening

- **Status**: Complete.
- **Automated Tests**: 232 passed, 0 failed, 0 skipped. All 6 WPF smoke processes verified.
- **Key Accomplishments**:
  - Repaired default ETABS interop DLL path in `scripts/qualify-csi.ps1` with safe multi-candidate path discovery and validation.
  - Enforced strict PID identity throughout the runner and selection restoration, using `GetObjectProcess($ProcessId)` without falling back to `GetObject()`. Re-verifies PID and process identity before restoration.
  - Guarded against ambiguous multi-instance operations and refused selection mutation unless explicit PID is provided.
  - Do not swallow selection-setup safety exceptions; if initial selection cannot be safely recorded or restored, refuses selection mutation without modifying selection.
  - Propagates exact resolved and validated ETABS API assembly path to physical test runner.
  - Persists structured `PhysicalQualificationResult` to JSON report and derives runner status directly from structured outcome.
  - Added regression test suite (`PhysicalRunnerRegressionTests.cs`) covering wrong PID, missing API path, disallowed selection mutation, failed restoration, model mismatch, and qualification outcome propagation.
  - Aligned distinct exit codes: 0 (Passed), 1 (Failed), 2 (Inconclusive), 3 (Pending), 4 (NotExecuted). Selection restoration failures are strictly reported as Failed with non-zero exit.
  - Documented fixture-directed plane grouping scope in `BenchmarkExecutionPipeline`.


## ETABS Physical Qualification — Independent Benchmark Verification

- **Status**: Complete — Physical Benchmark PASSED.
- **Physical Model Verification**: Created dedicated disposable model `artifacts/physical-benchmark-private/ETABS-BENCHMARK.EDB` in new ETABS 23.2.0 instance (PID 30520). Protected client model (PID 24084) was left completely untouched.
- **Benchmark Checks**: 125 of 125 independent comparisons passed within tolerances (0 missing, 0 failed).
- **Automated Tests**: 233 passed, 0 failed, 0 skipped. All 6 WPF smoke processes verified.
- **Winding & Local 3 Investigation**: Verified that in ETABS, shell Local 3 is mathematically coupled to vertex winding order. Distinguished geometric polygon equivalence from vertex-list ordering in `IndependentGeometryBenchmarkValidator` while preserving strict independent Local 3 vector comparisons.
- **Safety Guarantees**: Engineering calculations, approvals, and live CSI write operations remain strictly disabled in RealReadOnly mode.
