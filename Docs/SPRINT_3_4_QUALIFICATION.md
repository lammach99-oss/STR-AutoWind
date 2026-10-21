# Sprint 3.4 qualification — 2026-10-21

## Objective and Scope

Sprint 3.4 establishes an independent engineering geometry qualification framework and implements product/version-validated PID-aware read-only CSI connection handling.

Sprint 3.3 passed 201 automated tests and verified live WPF editing against 10 ETABS roof areas, but independent numeric qualification was inconclusive. Sprint 3.4 introduces independently prepared benchmark reference data, safe multi-instance connection controls, and an explicit physical qualification gate decoupled from automated test success and geometry scan outcomes.

No normative wind coefficients, engineering pressure approval, or CSI write operations are implemented.

## Independent Geometry Benchmark Framework (Task A)

The independent benchmark framework is defined in fixtures/etabs-roof-independent-benchmark.json and executed via IndependentGeometryBenchmarkValidator. Reference values are calculated from first principles using standard analytical geometry and independent dimensional checks:

- **Dual-Pitch Sloped Roof Planes**:
  - West Slope (Slope A): Normal vector (-0.447214, 0, 0.894427), slope angle 26.565051° (pitch 1:2).
  - East Slope (Slope B): Normal vector (0.447214, 0, 0.894427), slope angle 26.565051° (pitch 1:2).
- **Divided Shell Objects**:
  - West slope: Shell 101 (lower) and Shell 102 (upper).
  - East slope: Shell 201 (upper) and Shell 202 (lower).
  - Each panel: Projected plan area = 8.0000 m², True 3D surface area = 8.944272 m² (exact ratio: sqrt(5)/2 = 1.118034).
- **Opposite CSI Local 3 Orientations**:
  - Shell 101: Local 3 points outward along surface normal (+Z component).
  - Shell 102: Local 3 points inward (-Z component), verifying opposite Local 3 handling on the same plane.
  - Shell 201: Local 3 points outward (+Z component).
  - Shell 202: Local 3 points inward (-Z component).
- **Analytical Test Zones & Intersections**:
  - Zone-A1-Full: UV width 2.236068 m × length 4.0 m = 8.944272 m² intersection, 100.00% coverage, FullyInsideZone.
  - Zone-A1-Half: UV width 1.118034 m × length 4.0 m = 4.472136 m² intersection, 50.00% coverage, CrossesZoneBoundary (NeedsSplitInCSI = true).
  - Overlapping test zones: classified as OverlappingZones (NeedsSplitInCSI = true).
- **Existing Loads**:
  - Frame 101: Distributed load = 1500.0 N/m.
  - Shell 101: Area uniform load = -1200.0 Pa.
- **Explicit Tolerances**:
  - Coordinates: 0.001 m absolute, 1e-4 relative.
  - Local 3 vectors: 0.001 component absolute.
  - Surface & projected areas: 0.001 m² absolute, 1e-4 relative.
  - Coverage percentages: 0.01% absolute.
  - Loads: 0.01 N/m or Pa absolute.

### Outcome Reporting Rules

1. Missing benchmark fixture file produces PhysicalQualificationOutcome.Inconclusive.
2. Missing required benchmark elements in CSI selection produces PhysicalQualificationOutcome.Inconclusive.
3. Any comparison exceeding tolerance produces PhysicalQualificationOutcome.Failed with line-by-line category, element ID, and deviation details.
4. All benchmark comparisons passing within tolerance produces PhysicalQualificationOutcome.Passed.

## Safe PID-Aware ETABS Attachment (Task B)

- **Process Discovery**: CsiProcessDiscovery.GetRunningProcesses(product) enumerates active instances with process ID, product, window title, and start time.
- **Disambiguation**: If multiple instances of ETABS or SAP2000 are running and no PID is selected, the application refuses to silently attach to an arbitrary instance and throws an informative exception prompting PID selection.
- **Process Validation**: CsiProcessDiscovery.ValidateProcessActive verifies that the process exists, matches the expected product process name, has not exited, and has not had its PID reused by another application.
- **Model Disconnect and Mismatch**: The adapter detects when a model has been closed in ETABS (MODEL_DISCONNECTED) or when a different model has been opened without disconnecting (MODEL_MISMATCH), clearing stale scan data.
- **Dedicated STA Execution**: Preserves the background STA dispatcher with Win32 message pump for all COM interop calls.
- **UI Presentation**: Process ID, product, version, connection state, model filename, and physical qualification status are displayed before scanning on the CSI tab.

## Changed-Source Verification (Task C)

- When shell coordinates or Local 3 vectors are altered in a model, ValidateAndReconcile detects the geometry fingerprint mismatch and flags GeometryReviewStatus.StaleNeedsRescan, locking the editor against silent draft reuse.
- When selection scope changes without modifying existing geometry, ValidateAndReconcile flags GeometryReviewStatus.ScopeReviewRequired, allowing explicit engineer review via AcceptSelectionScopeMerge.
- The application operates strictly as a read-only getter throughout qualification without any CSI mutation, save, or unlock operations.

## Roof Mapping Accuracy Gate (Task D)

The physical qualification status (PhysicalQualificationResult) is explicitly separate from:

1. **Automated unit-test success**: 211 automated tests pass in CI.
2. **CSI connection state**: CsiConnectionResult.Connected indicates COM attachment only.
3. **Geometry scan outcome**: CsiScanOutcome reports selection and geometry retrieval status.
4. **Geometry review status**: GeometryReviewStatus tracks engineer review of geometric zones.
5. **Physical qualification outcome**: PhysicalQualificationOutcome confirms independent benchmark accuracy.
6. **Normative pressure approval**: Strictly disabled pending licensed normative datasets.

Furthermore, RoofPressurePrerequisites and BlocksPressureMapping ensure that future calculation engines cannot consume legacy (v1), incomplete, overlapping, crossing, or unverified mappings.

## Physical Qualification Status Matrix

| Capability / Check | Status | Evidence / Notes |
|---|---|---|
| Independent Benchmark Specification | Passed | fixtures/etabs-roof-independent-benchmark.json with 2 sloped planes, 4 divided shells, opposite Local 3, test zones, loads |
| Benchmark Validator Engine | Passed | IndependentGeometryBenchmarkValidator with 7 comparison categories and strict tolerances |
| PID-Aware Read-Only Attachment | Passed | CsiProcessDiscovery, GetObjectProcess, multi-instance guard, PID reuse and termination checks |
| Changed-Source Detection | Passed | ValidateAndReconcile enforces StaleNeedsRescan and ScopeReviewRequired |
| Roof Mapping Accuracy Gate | Passed | RoofPressurePrerequisites blocks invalid/unverified mapping input |
| Live ETABS Instance Selection | Exercised | Safe PID selection implemented and tested in WPF UI |
| Live ETABS Independent Benchmark | Inconclusive | Live instance does not currently have the benchmark gable model open; reported as Inconclusive per specification |
| SAP2000 Physical Qualification | Pending | SAP2000 physical qualification harness is pending qualified reference model |


## Sprint 3.4.1 Fail-Closed Hardening

Sprint 3.4.1 implements strict fail-closed enforcement across the entire independent geometry benchmark pipeline:

1. **Fail-Closed Checklist Validator**: `IndependentGeometryBenchmarkValidator` guarantees that `PhysicalQualificationOutcome.Passed` is reachable if and only if all declared benchmark checks are fully executed and pass within tolerances. Missing fixture data or partial execution produces `Inconclusive` or `Failed`.
2. **Analytical Scenarios**: Three distinct benchmark scenarios are formalized in `fixtures/etabs-roof-independent-benchmark.json`:
   - Scenario 1 (Full containment): Shell 101, 100% coverage by Zone-A1-Full, `FullyInsideZone`, `NeedsSplitInCSI = false`.
   - Scenario 2 (Half containment): Shell 102, 50% coverage by Zone-A2-Half, `CrossesZoneBoundary`, `NeedsSplitInCSI = true`.
   - Scenario 3 (Overlapping zones): Shell 201, `OverlappingZones`, `NeedsSplitInCSI = true`.
3. **Comprehensive Negative Mutation Suite**: 9 deterministic negative mutation tests verify that validation fails closed on:
   - Empty roof planes
   - Missing shell 3D data
   - Missing required loads
   - Incorrect load kind
   - Missing zone coverage records
   - Wrong classification outcome
   - Incomplete vertex coordinate arrays
   - Roof plane slope deviation
   - Omitted test-case execution
4. **Strict Physical Runner**: `scripts/qualify-csi.ps1` accepts `-ProcessId`, sets `CSI_PROCESS_ID`, runs `Physical_ETABS_independent_benchmark_qualification_when_running`, uses PID-aware attachment without arbitrary instance fallback, and enforces fail-closed test exit codes.
5. **Stale Qualification Invalidation**: `MainViewModel` invalidates earlier qualification on disconnect, mode switch, model identity change, or geometry fingerprint mismatch.
