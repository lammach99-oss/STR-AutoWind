# CSI read-only compatibility

## Fail-Closed Physical Benchmark Qualification (Sprint 3.4.1)

Sprint 3.4.1 hardens the independent benchmark framework to be strictly fail-closed:
- **Completeness & Checklist**: PhysicalQualificationOutcome.Passed is strictly permitted only when 100% of declared benchmark checks have executed and passed. Missing elements, unexecuted checks, or fixture issues produce Inconclusive or Failed.
- **BenchmarkChecklist**: Structured checklist records ExpectedCount, ExecutedCount, PassedCount, FailedCount, MissingChecks, and FailedChecks.
- **Distinct Scenarios**: Evaluates full containment (Shell 101, 100% coverage, FullyInsideZone), half containment (Shell 102, 50% coverage, CrossesZoneBoundary, NeedsSplitInCSI = true), and overlapping zones (Shell 201, OverlappingZones, NeedsSplitInCSI = true).
- **Negative Mutation Suite**: 9 mutation tests prove that validation fails closed on empty planes, missing 3D data, missing loads, incorrect load kind, missing coverage, wrong classification, incomplete vertices, wrong slope, and missing test execution.
- **Strict Runner & PID Propagation**: `scripts/qualify-csi.ps1` propagates ProcessId through environment variable CSI_PROCESS_ID, runs the independent benchmark qualification, uses GetObjectProcess consistently (including during selection restoration), and fails closed if the outcome is not Passed.
- **Stale Qualification Invalidation**: MainViewModel binds qualification to the originating model ID, geometry fingerprint, software product/version, and process ID. Any model change, rescan with altered geometry, disconnect, or mode switch immediately clears stale qualification. — Sprint 3.4

Current qualification and independent benchmarks are documented in [SPRINT_3_4_QUALIFICATION.md](SPRINT_3_4_QUALIFICATION.md). Sprint 3.4 adds product-validated read-only PID selection (CsiProcessDiscovery, GetObjectProcess), guards against ambiguous multi-instance attachment, detects process termination and PID reuse, and establishes the independent geometry benchmark framework (IndependentGeometryBenchmarkValidator).

Live physical qualification against an authorized ETABS instance without the specific benchmark model loaded is reported as Inconclusive. SAP2000 physical qualification remains pending. No CSI setters or model mutations are implemented.

Sprint 3.2 adds a geometry-only UV editor over immutable selected-shell snapshots. Its WPF proof uses a labeled four-shell gable mock; no new physical CSI qualification is claimed. Existing ETABS 23.2.0 getter qualification below is historical; SAP2000 physical qualification remains pending. Polygon operations never call CSI setters, change selection, split areas or assign loads. Selection-scope differences require explicit merge review; changed coordinates/Local 3 on an existing saved shell are stale.

| Product / adapter | Installed metadata/help inspected | Actual running connection | Actual definition scan | Selected geometry / axes / loads | Writes |
|---|---|---|---|---|---|
| Fake adapters | Open-source contracts | Deterministic demo | Automated tests | Automated synthetic tests | Fake simulation only |
| ETABS 23 | Product assembly metadata + installed CHM | Passed: 23.2.0 | Passed: units, locked state, stories/base, patterns, cases, combinations | **Passed physical qualification**: perimeter frame pairs, horizontal slabs (opposite Local 3 ±Z), rotated/sloped roof shells, distributed frame loads, area uniform loads. Opt-in qualification via `scripts/qualify-csi.ps1`. | No live writer |
| SAP2000 27 | Product assembly metadata + installed CHM | Pending: process not active during validation | Disconnected contract test passed | Pending physical qualification | No live writer |
| SAP2000 20 | Legacy install detected | Unsupported by current gate | Unsupported | Unsupported | No live writer |
| Other versions | Not inspected | Conservatively rejected | Unqualified | Unqualified | No live writer |

## Physical qualification hardening summary (Sprint 2.6)

Sprint 2.6 separates automated CI-compatible test suites from opt-in physical CSI qualification:
1. **Test categorization and isolation:**
   - Physical qualification tests are decorated with `[Trait("Category", "PhysicalCsi")]`.
   - Standard CI and test runs (`scripts/test.ps1`) execute with `--filter Category!=PhysicalCsi`.
   - Physical tests require explicit opt-in via environment variable `CSI_PHYSICAL_QUALIFICATION=1` and enforce strict assertions; they never silently return or pass if ETABS is missing, disconnected, or has empty selections.
2. **Strict physical qualification runner:**
   - `scripts/qualify-csi.ps1` executes strict physical qualification against ETABS (or SAP2000).
   - Queries running processes, installed assemblies, and baseline element fixtures.
   - If not running, clearly reports `Inconclusive / Pending` and outputs diagnostic JSON report.
3. **Independently reviewed baseline fixture:**
   - Expected element IDs (Frames 49, 75, 50, 78; Areas 10, 18, 64, 84), global XYZ coordinates, opposite Local 3 vectors, and load values are recorded in `fixtures/physical-csi-reviewed-baseline.json`.
4. **Explicit scan outcomes:**
   - Introduced `CsiScanOutcome` enum: `Disconnected`, `Scanning`, `Succeeded`, `EmptySelection`, `PartialScan`, `Failed`.
   - Status bar displays explicit status for empty, partial, and failed scans. Never reports `LIVE CSI SCAN SUCCEEDED` when element selection is empty or invalid.
5. **Selection preservation and safety:**
   - Qualification runner queries and preserves initial user element selections before modifying selection for test setup (`-SetupSelection`), and restores initial selection in a `finally` block.
   - RealReadOnly mode keeps all engineering calculations, approvals, and CSI assignments disabled.

## Physical qualification summary (Sprint 2.5 Baseline)

Live ETABS read-only qualification was conducted locally on 2026-10-19 against an active ETABS 23.2.0 instance. The running model was locked, with native N/mm units.

### Verified capabilities:
1. **Selection inspection:**
   - Real element selection of perimeter facade frame pairs (Frames 49, 75, 50, 78) and roof shells (Areas 10, 18, 64, 84).
   - `SelectObj.GetSelected` correctly maps object types (type 2 = Frame, type 5 = Area) and element IDs.
2. **Coordinate conversion:**
   - Global XYZ coordinates accurately read via `PointObj.GetCoordCartesian(Global)` and converted from native millimeters to SI meters (e.g. 11,490 mm -> 11.49 m).
   - Nonzero frame lengths verified.
3. **Local 3 orientations and transformation matrices:**
   - Transformation matrices read via `AreaObj.GetTransformationMatrix` verified for orthonormality and right-hand rule.
   - **Opposite Local 3 directions:** Verified between horizontal slab elements (Area 10 with Local 3 = (0, 0, 1) pointing +Z, and Area 18 with Local 3 = (0, 0, -1) pointing -Z). Confirms object Local 3 is never coerced to physical outward normal.
   - **Rotated/sloped roof shells:** Verified sloped roof geometry (Area 64 with Local 3 = (-0.1965, 0, 0.9805) and Area 84 with Local 3 = (0.2218, 0, 0.9751)).
4. **Existing loads readback:**
   - Distributed frame loads read via `FrameObj.GetLoadDistributed` and converted to SI N/m (force per length).
   - Area uniform loads read via `AreaObj.GetLoadUniform` and converted to SI Pa (force per area).
5. **Selection changes and empty selection:**
   - Deselection in ETABS clears scanned frames and shells immediately upon refresh, preventing stale data retention.
6. **Workspace isolation and mock distinction:**
   - `IsMockAdapter` property explicitly flags mock test adapters.
   - RealReadOnly mode displays distinct banners, project summaries, and status for mock fixtures vs live CSI scans. A mock adapter is never reported as a live scan.
   - RealReadOnly mode preserves full safety gating: all calculation, approval, and CSI mutation buttons remain disabled.

### Outstanding limitations:
- **SAP2000 27 physical qualification:** SAP2000 was not running during this validation run. Disconnected contract tests pass, but physical element selection and load readback remain pending until a live SAP2000 session is qualified.
- **Normative wind coefficients:** Calculation of wind pressures remains gated pending licensed coefficient table integration.

## Attachment and execution

Choose the installed product API DLL through the WPF picker. A selected CSiAPIv1.dll path resolves the adjacent product-specific interop contract because those signatures were inspected. Runtime checks assembly/product and getter parameter types/counts. The narrow gate allows ETABS 23 or SAP2000 27 only.

Helper.GetObject attaches a running product. There is no application start/create/open/save/unlock/change-units or assignment operation. Connection, getter calls and COM release execute on a dedicated STA with a message pump. Multiple-instance/PID choice is not implemented; verify displayed model identity. Core build/CI has no vendor assembly reference or installed-software requirement.

## Inspected getter surface

- Model: GetVersion, GetModelFilename, GetPresentUnits, GetModelIsLocked.
- Definitions: LoadPatterns.GetNameList, LoadCases.GetNameList, RespCombo.GetNameList.
- ETABS: Story.GetStories, including its documented base record; no SAP story API assumption.
- Selection: SelectObj.GetSelected; type2 frame, type5 area.
- Geometry: FrameObj.GetPoints, AreaObj.GetPoints, PointObj.GetCoordCartesian (Global); transformation matrices where supported.
- Existing loads: FrameObj.GetLoadDistributed, AreaObj.GetLoadUniform for selected objects. Other load types are not scanned or claimed complete. Ownership remains Unknown.

Installed help specifies Local3 matrix column indices 2,5,8. Unit/orthogonality/handedness and nontrivial mock rotations check interpretation. Local3 never establishes physical outward normal. Native unit IDs 1..16 convert to SI without SetPresentUnits. Unknown units and malformed arrays produce issues. Force-distributed loads use N/m; moment-per-length loads retain type metadata and corresponding SI scaling. Area uniform loads use Pa.

Unsupported optional getters report capabilities/issues; source XYZ is retained if an optional axis read is missing. Model/unit changes during a scan invalidate it. Geometry fingerprints cover the selected scan, not the whole model or a future write-preflight guarantee. Empty/vertical/degenerate XY projections remain limitations of the 2D viewer; original XYZ is retained.

Official references: [CSI developer portal](https://www.csiamerica.com/developer), [CSI .NET8 example](https://web.wiki.csiamerica.com/wiki/spaces/kb/pages/2011456/NET%2B8%2BPlugin%2BExample%2B-%2BAll%2BProducts). Installed-version documentation and actual getter tests govern; broad compatibility statements do not establish physical qualification.
