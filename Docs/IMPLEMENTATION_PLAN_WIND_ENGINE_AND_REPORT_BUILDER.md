# Wind Calculation Engine and Auditable Report Builder - Implementation Plan

## 1. Architectural Strategy & Design
- **Domain & Engineering Contracts:**
  - Strongly typed EngineeringCalculationRequest
  - CoefficientInputEvidence (with DataProvenance: SyntheticDemoFixture, EngineerEntered, PrivateLicensed, Verified)
  - CalculationTraceNode (FormulaId, SymbolicEquation, SubstitutedEquation, InputsWithUnits, ResultWithUnits, OutputUnit, Reference, Warning, Issue)
  - EngineeringCalculationRun (Request, State, Profile, SurfaceResults, Summary, Trace, ContentHash)
  - CalculationIssue (Severity: Error, Warning, Info; Code, Message, AffectedStage)
- **Calculation Engines:**
  - Common physics/math primitives: dynamic pressure, integration, vector normal projection.
  - Asce7_22CalculationStrategy (MWFRS Ch 26/27, Ch 28, Ch 30; Supplement 1-3 awareness; blocked stage reporting when normative table missing; fully supported arithmetic when factors entered).
  - AsNz1170_2_2021CalculationStrategy (Section 2, 3, 4, 5; Regional V_R, Site wind speed, dynamic pressure q_des, aerodynamic factors C_fig, net surface pressures; Amendment 1:2023 & 2:2024 awareness).
- **Report Model & Exporters:**
  - Multi-section report model: Cover, DesignBasis, InputRegister, FormulaLedger, Worksheets (ASCE/ASNZS), RoofFacadeSheets, WallLoads, ManualVerification, ValidationAppendix, AuditAppendix.
  - Exporters:
    - PDF Exporter: pure, deterministic vector PDF using standard PDF syntax / format without external printer or complex dependencies, supporting pagination, headers, watermarks, tables, and formula blocks.
    - HTML Exporter: standalone, responsive HTML with SVG diagrams and escaped content.
    - Markdown Exporter: complete GitHub-flavored markdown.
    - JSON Exporter: deterministic JSON serialization with SnapshotJson.
- **WPF UI Integration:**
  -  Wind tab: Add Engineer Review / Calculation Sandbox controls in Demo / Review workspace.
  - Preserve AssertReadOnlyVisualIsolation in RealReadOnly mode.
  - Report tab: Update preview, section toggles, and multi-format export (PDF, HTML, MD, JSON).
- **Validation & Tests:**
  - Synthetic independent flat-roof and gable-roof hand calculation benchmarks.
  - All 325 existing tests pass + new test suites.
  - All 6 WPF smoke scenarios pass + new report export smoke.

