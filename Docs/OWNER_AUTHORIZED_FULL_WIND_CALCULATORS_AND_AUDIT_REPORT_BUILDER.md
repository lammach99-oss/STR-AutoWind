# Owner Authorization: Full Wind Calculation Implementation and Auditable Report Builder

## Read first - owner decision supersedes the previous STOP instruction

**Repository root (Windows):** `D:\Project\Etab-Sap Tool\AutoWind`

**Place this file at:** `D:\Project\Etab-Sap Tool\AutoWind\Docs\OWNER_AUTHORIZED_FULL_WIND_CALCULATORS_AND_AUDIT_REPORT_BUILDER.md`

**Remote:** `https://github.com/lammach99-oss/STR-AutoWind`

**Last independently checked baseline:** `origin/main` commit `076d9e6cdf9ed4ad3dd0820d6fca26d37309ff02`; GitHub Actions run `39957627758` passed 325 non-physical automated tests and six WPF smoke scenarios. Always fetch and inspect the actual current HEAD before editing.

**Explicit new instruction from the owner (also the engineer performing the calculations' technical review):** Implement the wind calculations and a fully usable, detailed calculation-report builder now, so the owner can inspect and independently recompute each numerical step. The earlier "stop all implementation until external licensed standards and independent review" instruction is REPLACED for **software development and review-only arithmetic**. It is NOT authorization to invent normative coefficients, assert legal licensing, impersonate an independent reviewer, approve structural designs automatically, or mutate live CSI models.

**Do not respond by merely asking for a license, recreating staging folders, or repeating the previous stop instruction. Build working engineering review calculations and a working report builder with the data that is legally available, using explicitly entered coefficients where appropriate.** Complete source-independent numeric pathways even when private normative tables are absent. Never falsely claim full code compliance for an unverified standard-specific branch.

---

## A. Non-negotiable operating modes and trust boundaries

Implement these separate, impossible-to-confuse states:

1. **Synthetic Demonstration:** The existing demo and new synthetic test cases. Numeric outputs work and reports are detailed, but every page and exported result states `SYNTHETIC EXAMPLE - NOT FOR DESIGN`.
2. **Engineer Review / Explicit Factor Input:** The owner may directly provide all required numeric coefficients, wind speeds, reference heights, geometric parameters, and the chosen equation/procedure with an explicit source label. The app calculates numerical review results and creates full traceable reports. Status must say `ENGINEER REVIEW - INPUTS / STANDARD APPLICABILITY NOT INDEPENDENTLY VERIFIED` until source verification actually occurs. If a required value or applicability proof is missing, block that affected result rather than assuming a value.
3. **Private Licensed Dataset Review:** Read-only use of a standards bundle installed beneath `fixtures/standards/private/` (or other owner-authorized non-Git private location) with V3 hashes, component-byte coherence, rights classification, and recorded source locators. Display separate booleans for (a) numerical computation possible, (b) dataset integrity verified, (c) official primary-source applicability verified, (d) owner-engineer review completed, and (e) external independent review verified. NONE implies the others.
4. **Production Assignment:** OUT OF SCOPE. Preserve the current immutable `CanCalculateNormativePressures = false` for production authorization unless a future separately authorized milestone explicitly changes it. Do not call any live ETABS or SAP2000 setters, change CSI units, unlock models, save model files, or allow real load assignments. Engineer Review calculations are **read-only computational artifacts**, not production assignments.

Do not equate `OwnerEngineerReviewed` with `IndependentEngineerReviewed`. The owner may acknowledge and annotate calculations as a reviewer of their own work; do not auto-issue an independent sign-off, a compliance seal, or a real-model approval. Never label a calculation "ASCE 7-22 compliant" or "AS/NZS compliant" based solely on manually entered coefficients or a green CI result.

---

## B. Inspect the actual repository before making changes

Read and map these existing files and all their tests. Reuse contracts and capabilities, do not duplicate or silently bypass them:

- `src/StructuralLoadAssistant.Engineering/Standards.cs` - currently metadata-only; `WindCalculationResult.Run` is absent for normative calculations.
- `src/StructuralLoadAssistant.Engineering/Engineering.cs` - numeric integration, pressure/line-load units, facade tributary reactions, frame mapping, global normal conversion, wall gravity calculations.
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/{StandardsDataContracts,StandardsDataBundle,StandardsDataBundleValidator,ReferenceCalculationContracts}.cs` - V3 content hashing, private-file checks, family/procedure/limit-state lookup, fail-closed gates and benchmark comparison.
- `src/StructuralLoadAssistant.Domain/Contracts.cs` - existing immutable wind states, SI units, traces, projects, calculation runs, model snapshots and SHA-256.
- `src/StructuralLoadAssistant.Reporting/ReportModel.cs` - existing Markdown and HTML exporters, report snapshot validator and demo report definitions.
- `src/StructuralLoadAssistant.App.Wpf/{MainWindow,MainViewModel}.cs` - Wind tab currently has disabled calculation controls; Report tab currently exposes demo-only Markdown/HTML; RealReadOnly mode hides synthetic controls.
- `src/StructuralLoadAssistant.Application/{DemoSession,CsiInspectionSession,RoofZoneWorkspace}.cs`, `src/StructuralLoadAssistant.Persistence/SqliteSnapshotStore.cs`, `src/StructuralLoadAssistant.RoofMapping/*` and `src/StructuralLoadAssistant.Csi.Abstractions/*`.
- `tests/StructuralLoadAssistant.Tests/*`, `scripts/test-wpf.ps1`, existing CI workflow, `Docs/ARCHITECTURE.md`, `Docs/OPEN_ENGINEERING_ITEMS.md`.

Write a brief actual-code inventory, dependency map and implementation coverage matrix in repository documentation before building. Respect the .NET 8 WPF/C# application design, immutable snapshots and existing SQLite schema/versioning. Avoid any proprietary compile-time CSI assembly dependencies.

---

## C. Deliver a REAL calculation engine with complete step-by-step trace

### C1. Strongly typed, immutable calculation contract

Introduce/refine versioned types analogous to:

- `EngineeringCalculationRequest`: project ID, source-model snapshot ID or manually defined geometry, selected standard family/edition/revisions, governing jurisdiction, `ProcedureSelection`, MWFRS/C&C system, ULS/SLS, wind direction/angle, roof form, surface/zone state, enclosure classification, terrain/exposure, geometry, speed basis, source data selection, explicit coefficient overrides, unit system and report options.
- `CoefficientInputEvidence`: coefficient name, numerical value, unit, provenance (`Synthetic`, `EngineerEntered`, `PrivateLicensed`, `PrimarySourceChecked`), source document/edition/supplement/clause/table/figure, retrieval timestamp, original-unit representation, original-input value, manifest/component IDs and hashes, optional reviewer note.
- `CalculationTraceNode`: stable formula/stage ID, symbolic equation, explanatory assumptions, original input units, normalized SI inputs, fully substituted expression, full-precision value, display rounding, output unit, dependency nodes, source clause, coefficient record key, validation rule, warning/issue status and affected wind/zone state IDs.
- `EngineeringCalculationRun`: immutable request/source snapshots, family-specific intermediate results, pressure results, facade/roof/wall previews, step tree, checks, limitations, test/benchmark comparison, software version, provenance and deterministic content hash.
- `CalculationIssue` with typed severity and reason (`MissingCoefficient`, `UnverifiedSource`, `UnsupportedApplicability`, `OutOfRange`, `AmbiguousRecord`, `GeometryUnresolved`, `NumericalInvalid`, `ReferenceMismatch`, etc.).

Keep typed units (m, m/s, Pa, N/m, N, rad/degrees and dimensionless) and culture-invariant numerical serialization. Reject NaN/infinity, impossible negative dimensions, unsupported lookup, invalid boundary conditions and duplicate record keys. Never implicitly change ULS into SLS, MWFRS into C&C, one edition into another, or +internal into -internal pressure.

### C2. Formula execution and deterministic audit trace

**Every computed quantity must be traceable:** source inputs -> factors -> relevant dynamic/site velocity pressure -> external and internal pressure components -> net wall/roof pressure -> equivalent force/line load -> mapped preview. Report the symbolic formula AND the actual substituted numbers, with units and references. A trace node must be built by the numerical engine from the very operation executed, not reconstructed using independent duplicated arithmetic inside the renderer.

Support the following end-to-end steps when the required coefficient definitions are available:

- Input validation; governing edition/jurisdiction and wind speed basis; ULS/SLS; enclosure/positive-negative internal pressure states; building/roof geometry and angle of wind attack.
- Wind speed and height-dependent/site-factor chain; record values and legal interpolation/bounds; velocity/dynamic pressures; gust/dynamic effects where applicable; separate external and internal actions.
- Windward/leeward/side walls, flat and gable roofs, applicable roof zone edges/corners, independent wind states (+X, -X, +Y, -Y and arbitrary direction when supported), and all code-relevant pressure-case alternatives.
- Per-zone pressure assembly and correct area/surface reference; unambiguous inward/outward sign, physical outward normal, CSI Local3 orientation, and global signed force vectors. Never assume Local3 = outward.
- Facade wind pressure distributions with equal-split versus statically equivalent reactions, integration and equilibrium/moment residuals; multiple tributary segments and cumulative line loads. Use existing `FacadeDistribution`, `FacadeMapper` and `EngineeringMath` where sound.
- Roof plan/3D surface intersections, zone mapping and signed shell previews (3D area vs projected plan area, with the correct quantity explicitly chosen); the existing documented fixture-directed roof-grouping limitation remains visible.
- Wall self-weight (thickness, unit weight, finishes, height, openings/overrides), global -Z, with explicit material input provenance and independent tributary-height treatment.
- Limit states and wind-pressure alternatives must remain separate; if load combinations require additional licensed code information, represent those as explicit independent branches, never invented or silently combined.

**Minimum immediately usable pathway even without licensed tables:** Engineer-entered factor mode, using user-provided numeric factors and explicitly selected equation definitions, must produce quantitative intermediate steps, net wind pressures, facade/roof preview loads, and printable reports for fully supplied test cases. It must be clear which parts are general arithmetic and which are only valid when reviewed against a specific normative source. Numerical review output is NOT equivalent to a verified standard calculation.

Require dimensional analysis and independent balance checks wherever meaningful. Use raw values for calculations, round only for display. Document sign algebra explicitly and compare source snapshot input/output hashes before reporting. Never silently invent defaults like pressure coefficients, terrain multipliers, internal pressure factors, wind map speeds, roof zone widths or density constants.

### C3. Separate ASCE and AS/NZS calculation strategies

Create separate typed implementations such as `Asce7_22CalculationStrategy` and `AsNz1170_2_2021CalculationStrategy`, sharing ONLY validated physics/math primitives, not universal wind tables or cross-standard roof equations.

**ASCE/SEI 7-22 coverage:** Build procedure-specific paths and auditable intermediate stages covering Chapter 26 general wind requirements and, when within the selected/applicable domain, Chapter 27 MWFRS Directional, Chapter 28 MWFRS Envelope, Chapter 30 Components and Cladding, and Chapter 29 only for relevant nonbuilding/appurtenance cases. Inputs need wind-speed source and risk/importance basis, exposure/height, topography/elevation, directionality, gust/dynamics, enclosure, internal pressures, roof/wall external coefficients, zones, tributary/effective area and applicable wind directions. Never mix Directional, Envelope and C&C pressure algebra. Require typed applicability tests before each selected branch. For any pathway lacking a legally sourced table, the output must state `BLOCKED: REQUIRED NORMATIVE DATA` by named stage rather than simulate code compliance.

**AS/NZS 1170.2:2021 coverage:** Build a distinct typed chain for governing region/return period/design limit state, directional/site wind speed, terrain-height including TC2.5, shielding, topography, applicable climate/dynamic effects, directional aerodynamic factors, internal/external pressures and wall/roof load cases, with explicit applicability across Section 2 onward and relevant appendices. Derive equation and coefficient selection rules from licensed primary text; do not reuse 2011 tables or an unreviewed workbook shortcut. Keep ULS/SLS and both internal pressure alternatives separate.

**Important edition update (verified against publisher sources on 2026-10-24):** The ASCE official standards page now lists **ASCE/SEI 7-22 Supplement 1, Supplement 2, Supplement 3 and Errata**. Earlier project instructions only mentioned Supplements 1 and 2. Re-check applicability and affected wind provisions for Supplement 3, errata and jurisdiction before claiming final procedure completeness. The Standards Australia store currently lists AS/NZS 1170.2:2021 Amendments 1:2023 and 2:2024. Do not treat listing an amendment name as proof of its applicability to the user's jurisdiction.

Primary publisher discovery URLs (do not copy licensed tables into public Git):
- ASCE: https://www.asce.org/publications-and-news/asce-7
- ASCE 7-22 chapter contents: https://amplify.asce.org/content/standard/9780784415788/part/provisions/standard-chapter/s26
- AS/NZS publisher store: https://store.standards.org.au/search?query=AS%2FNZS1170.2%28Windactions%29&status=Current_PendingRevision

### C4. Datasource interface and human verification

- Preserve immutable V3 canonical hash and raw-component SHA-256 verification. Prefer a strongly typed `ICoefficientProvider` returning value + exact conditions + units + source; no lookup by magic numeric constant.
- Two providers: (a) explicit engineer-entered parameters, (b) a local-only licensed data bundle. Demo/synthetic providers must not be promoted into production.
- Manual entries must not be marked `Verified` automatically. Save editor identity, date, note, intended procedure, source reference and a tamper-detectable source/input hash. Allow owner-engineer review annotations distinct from external independent attestation.
- For missing normative source or unimplemented applicability, return a *structured partial run* with exact blocked stages; retain any legitimate preceding intermediate computation without fabricating the remainder. For a fully specified engineer-entered review case, show the actual arithmetic.
- The current `CanCalculateNormativePressures == false` is a PRODUCTION AUTHORIZATION gate, not a blanket ban on creating an explicitly unverified Review Only numeric run. Do not set it true merely to make the UI work.
- Private copyrighted data must remain outside Git, CI artifacts, public test fixtures and exported public examples. Prevent accidental leak of full normative tables; include only user-authorized project-specific applied values and citations in private reports.

---

## D. Implement a usable WPF Calculation Workspace and Report Builder

### D1. WPF calculation review workspace

Replace the disabled-only UX with a functioning isolated `Engineer Review` or `Calculation Sandbox` workspace that can use manually entered geometry or an immutable snapshot obtained from the read-only CSI adapter. Preserve the existing RealReadOnly no-write and synthetic-data isolation rules. Real CSI inspection must NOT cause synthetic demo calculation data to appear as real results.

Expose a cohesive user flow:

1. Select standard, edition, amendments/jurisdiction, procedure (system, method, limit state), wind direction/case and data source/mode.
2. Enter/edit inputs and explicit factors; each displays its unit, data provenance, source reference, whether confirmed by owner, validity and applicability.
3. Validate inputs and coverage, then `Calculate Review Only`. Show deterministic per-state/zone results with real numbers when requirements are met; otherwise list the exact missing factor/stage.
4. Present a collapsible **formula audit tree** with symbolic expression, actual substitutions, result, unit, lookup record, clause and warnings. Provide source value versus computed value side by side.
5. Show roof/zone diagrams and pressure schedules, wind state alternatives, facade equivalent line loads, shell Local3 signs and wall load schedules; make it possible to inspect one selected shell/zone/state from both diagram and table.
6. Save an immutable reviewed-run snapshot to the existing local SQLite persistence framework, reopen it, and compare against another run or user-entered hand-check values without silently recalculating historical versions.
7. Open the Report Builder directly from this selected calculation snapshot. Do not require a simulated assignment, an engineering approval, or any CSI write to report review-only work.

Enforce edit invalidation: changing geometry, edition, coefficient, wind state or selected procedure marks the current results stale and requires recomputation. Async cancellation/refresh cannot cross-contaminate sessions.

### D2. Detailed report content (required)

Extend the existing `StructuralLoadAssistant.Reporting` library and immutable `ReportModel` without breaking old saved demo report IDs. Report generation must only consume frozen, validated calculation snapshots. Never recalculate a pressure or substitute a missing value in an exporter.

A professional multi-section report must have:

- **Cover:** project, author, date, report/run IDs, revision, source model/geometry ID, standard and procedure/limit state, owner name field (editable by user), input/data qualification status, prominent review-only watermark/status on every page.
- **Design basis and assumptions:** governing standard edition, supplement/amendment status, wind map source, jurisdiction, terrain/exposure, risk category, return period, wind headings, building geometry, enclosure, all exclusions and unsupported scope.
- **Input register:** every user input and coefficient, unit, provenance source, license/manifest hash if relevant, table/clause locator, manual override, owner note and timestamp.
- **Formula ledger:** every intermediate quantity with symbolic equation, substituted numerical equation, step-by-step result, units, references and dependencies. No "black box" one-line final pressure.
- **Per-standard worksheets:** clearly separated ASCE 7-22 and AS/NZS 1170.2:2021 procedure paths; per-factor and per-wind-state tables, external/internal pressure components, positive and negative alternatives, zones and final net pressures.
- **Roof and facade sheets:** pressure-zone plan/3D identifiers, shell area convention, physical outward and Local3 sign check, component-to-frame/area distribution, line-load integration/reaction equilibrium and global vector components.
- **Wall loads:** input materials (non-synthetic values require explicit owner provenance), wall height, openings, formulas, resulting global -Z line loads and override notes.
- **Manual verification worksheet:** editable independent check field per row (expected value, tolerance, difference, Pass/Fail/NotChecked, reviewer identity, date, comment) that cannot alter the original calculation run. Never automatically claim independent verification from passing self-entered checks.
- **Validation appendix:** missing coefficient/invalid-domain/error list, code coverage matrix by applicable procedure, reference-comparison outputs, blocked calculations and "not implemented" areas explicitly identified.
- **Audit appendix:** manifest hash, component checksums, full input snapshot hash, source geometry fingerprint, software/engine version, computation timestamp, report builder version and immutable run ID.

The report MUST show both full-precision calculation values for reproducibility and sensible rounded display values with a clear rounding policy. Include SI base units and any selected display-unit conversions. Show whether pressures are positive inward, and signed global/local axes clearly. Reports with missing required inputs should print "INCOMPLETE - NOT AN ENGINEERING RESULT" and show what is missing; never print fictional zeros as outcomes.

### D3. Actual Report Builder features and exports

- A functioning WPF report-definition editor with include/exclude/reorder sections, custom title/project/header/footer, logo (optional), precision, page size/margins, orientation where relevant, selected wind cases/zone tables, and preview before export.
- Render a readable, paginated, deterministic **PDF** with page numbers, long table page breaks, embedded source-independent diagrams/charts, legible equation text and status watermark. Select a maintained, redistributable .NET library compatible with project licensing; evaluate NuGet license first. Do not rely on a printer being installed.
- **HTML** offline export with safely escaped user text and viewable formula/roof diagrams. **Markdown** and **machine-readable JSON** exports must preserve all source/trace/status content for audit and reproducibility.
- **DOCX** export is desirable for editable engineering reports; implement it if possible with a compatible open-source format library, without postponing the required working PDF/HTML report. If DOCX is deferred, disclose it explicitly as a remaining feature rather than claiming it is done.
- Export from a frozen run, not a live mutable `ViewModel`; support project-local report save/reopen and snapshot-hash validation.
- Restrict licensed/private exports to local user-selected destinations; disclose intended use and export rights. Do not auto-upload PDFs or private coefficient tables to a third-party service.
- One canonical report data model for all renderers; verify headings, facts, equations and numeric values are consistent across PDF, HTML, Markdown and JSON.

---

## E. Testing, QA, and engineering correctness proof

1. Preserve the existing **325** non-physical tests and all six WPF smoke scenarios as a baseline; do not reduce test count, loosen assertions or exclude failures merely to go green.
2. Add independent **synthetic arithmetic reference cases** (self-contained values, not copied from licensed standards) for the full chain: factors -> dynamic pressure -> external/internal -> net -> roof/facade vectors and wall gravity loads. Hand-calculate expected outputs independently from production code and assert intermediate traces and units.
3. Test both internal pressure alternatives, all four cardinal wind directions, wind normal/parallel to ridge, flat and gable roof, ULS/SLS, MWFRS vs C&C isolation, positive and negative suction/pressure, reversed Local3, unit conversions, zonal overlap and conservation/integration.
4. Test numerical edge cases: zero/negative/wrong-unit inputs, NaN/infinity, duplicate coefficients, mismatched edition/supplement, missing required conditions, illegal interpolation, out-of-domain heights/roof slopes, dynamic or special procedures unsupported, geometry stale, multiple model sessions and exact-key lookup.
5. Test user-provided factors: a fully specified review case computes real numbers with an honest unverified label; a missing source/factor blocks only the affected stage and displays an actionable reason. Never fabricate a code-certified value.
6. Test immutable report reproducibility: JSON/HTML/PDF content from the same frozen run, exact input hash, correct live-vs-demo provenance, HTML escaping, no report calculation duplication, cross-report snapshot corruption rejection, self-review != independent review, no private data leakage.
7. Provide visible report acceptance fixtures: at least one complete flat-roof review report and one gable-roof review report, both explicitly SYNTHETIC/REVIEW ONLY, with equation substitutions, both IP alternatives, zone pressure schedule and facade mapping. Generate their artifacts locally without committing private files.
8. Extend Windows WPF smoke to cover editing review inputs, running a calculation, inspecting one trace and exporting/reopening a report. Confirm all previous six smoke scenarios still pass. Add PDF structure/text rendering checks and verify every page shows provenance status. Avoid brittle screenshot-only numerical assertions.
9. Locked restore, Debug and Release builds with zero warnings/errors, all non-physical xUnit tests (`Category!=PhysicalCsi`), all six WPF smoke processes plus any new review/report smoke, and a public GitHub Actions green run at the exact pushed SHA. Do NOT rerun the private ETABS benchmark model creator or attach to a customer's CSI instance unless separately authorized.

---

## F. Phased implementation - complete the project instead of reopening the previous stop loop

Use sequential, verifiable increments within this single OWNER-AUTHORIZED implementation initiative:

**Phase 1 - Calculation core + audit schema:** typed request/results/coefficients, deterministic formula trace, independent math validation, review-only status separation, unit tests.

**Phase 2 - Immediately useful engineer-entered workflow:** actual quantitative wind and net pressure arithmetic for fully supplied input/factor cases; wall and facade/roof preview distribution; WPF input and trace UX, immutable persistence, reportable issues.

**Phase 3 - Separate standard-specific engines:** implement as much of the true ASCE 7-22 and AS/NZS 1170.2:2021 decision tree and formula algebra as primary-source-supported, with procedure-by-procedure coverage matrix and clear blockers for unavailable normative tables, maps, or code applicability. Do not confuse source-independent completion with full normative coverage. Verify the ASCE Supplement 3 discovery against governing source.

**Phase 4 - Report Builder:** working detailed report model, WPF customization/preview, PDF + HTML + Markdown + JSON export, reference comparison and self-review annotations; DOCX where feasible, otherwise explicitly deferred.

**Phase 5 - End-to-end validation:** synthetic independent hand calculations, diagram/signed-load invariants, report smoke and multi-format parity, locked builds/tests, docs and CI.

At the end of EACH phase, document completed/blocked pathways, current source evidence, test counts, screenshot/export examples and the next specific tasks. Continue implementation through the whole feasible review-only feature set without repeatedly stopping solely because licensed tables or an independent engineer sign-off are missing. However, never fabricate values to fill an unavailable normative branch.

**Git policy:** Work on latest verified `main`, do not force-push. The owner now explicitly authorizes code commits/pushes for these reviewed development increments. Push only after checks pass; provide exact SHA and CI URL. Keep all proprietary source data and reports with licensed table values outside Git. Any incomplete milestone must be reported honestly without asserting completion.

---

## G. Mandatory final delivery / acceptance report

Return the following sections:

1. Latest `origin/main` exact SHA and CI run URL/conclusion; Debug/Release build warnings/errors, test count, WPF smoke outcomes.
2. Implemented calculation scope matrix: **Standard -> Procedure -> Source assumptions -> Inputs -> Numeric stages -> Validated output -> Unsupported/missing licensed stage**. Clearly differentiate software arithmetic from licensed-standard qualification.
3. End-to-end visible example of one **synthetic flat roof** and one **synthetic gable roof**: input numeric factors, each substituted equation, intermediate dynamic pressure, external+internal, net signed pressure, frame/area load preview, and consistency checks. Exact hand-calculated expected values and comparisons.
4. Working WPF review UX and Report Builder demonstration, including **real exported PDF and HTML files** with step-by-step formulas. Include exact paths and screenshots, and report any DOCX status accurately.
5. Reused and modified files, snapshot schema/migration strategy, copyright/licensing/privacy checks, test evidence and known limitations.
6. Review/qualification labels for all artifacts; no automatic independent approval, no production CSI writes.
7. Exact external inputs the owner must still provide to verify ASCE/SEI 7-22 and AS/NZS 1170.2:2021 branches against authoritative licensed text and engineer hand calculations. Do NOT let that external dependency prevent completing source-independent calculation software and report exports.

**Definition of done:** The owner can open WPF, select a fully specified review-only test case, change a coefficient, compute actual changed numerical results, inspect every formula and substitution with units/lineage, inspect separate roof/wall/facade states, save and reopen the frozen run, and export a detailed paginated PDF whose pressures match the calculation snapshot. No user should mistake this for an independently verified normative design or a live CSI assignment.

**Start implementation from current main now. Do not simply return the intake checklist.**
