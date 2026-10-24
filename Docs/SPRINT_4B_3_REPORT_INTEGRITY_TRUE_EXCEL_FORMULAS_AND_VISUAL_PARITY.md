# Sprint 4B.3 - Report Integrity, Real Excel Formulas, Workbook-Style PDF, and WPF End-to-End Verification

## 0. Authoritative handoff and scope

**Repository:** `lammach99-oss/STR-AutoWind`  
**Windows root:** `D:\Project\Etab-Sap Tool\AutoWind`  
**Read this file from:** `D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_3_REPORT_INTEGRITY_TRUE_EXCEL_FORMULAS_AND_VISUAL_PARITY.md`  
**Audited `origin/main`:** `efb80237899e9e44dee1c2e200aa450bdc4d6ce4`  
**Audited CI:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/40070656043 - completed success, 337/337 non-physical tests, zero build warnings/errors, six WPF smoke scenarios.  
**Previous sprint:** 4B.2. Its delivery claim of report parity is **NOT ACCEPTED** by independent QA because the new report path has demonstrable data-loss, identity, and formula defects.

**Owner's principal requirement:** The reports must look and work like the owner's original Excel wind-load worksheets: organized engineering parameters, formulas with substitutions, Wind X/Y and internal-pressure alternatives, ULS/SLS, roof/wall pressure matrices, and visible manual-check fields. Engineers must be able to inspect, vary explicit inputs, recalculate, compare, and reproduce the reported numbers. This is a **bounded corrective implementation sprint**; do not replace it with another requirements-only memo.

### 0.1 Reference files (read locally, never modify)

- ASCE layout-only reference: `Reference Workbook/Hawaii Housing_Wind load_B.xlsm`, especially `Windload`, `Windload for CC`, and `Windload (Imperial)`. The source references **ASCE 7-16**, not 7-22. **No legacy 7-16 coefficients/formulas shall be imported as normative 7-22 evidence.** Visual cues: A6 cover/letterhead, A8 project metadata, A10 title, A13 references, A16 basic wind calculation, left-hand descriptions, right-hand symbols/inputs/units/source clauses; printed A:P range.
- AS/NZS layout-only reference: `Reference Workbook/Wind load AS.NZS_250729.xlsm`, especially `Slope Roof`, `Flat roof`, and the non-publishable `Data Sheet` for *structural layout guidance only*. Visual cues: A5 letterhead, A6-A8 project/reviewer/date, A10 standards, A13 parameters, A14-A23 roof geometry, region, terrain, separate ULS/SLS wind speeds, and subsequent X/Y wind and coefficient/pressure blocks; printed A:N range. **Do not silently import unreviewed formulas, source tables, or unusual legacy values.**
- Previous 4B.1 / 4B.2 synthetic samples in local ignored `artifacts/sample_reports/`; new samples must use freshly regenerated and mutually coherent snapshots.
- Both reference workbooks and all licensed or proprietary source extracts remain **untracked/private**. Do not copy entire workbook content, licensed tables, VBA modules, customer details, or private PDFs into public Git/CI artifacts.

## 1. Independent QA findings: reproduce these as failing regression tests FIRST

### P0-A: Spreadsheet is not actually formula-bearing

`src/StructuralLoadAssistant.Reporting/ExcelReportBuilder.cs` builds cells via `t="inlineStr"` with formatted strings such as `qVal.ToString("F2")`, including source inputs and outputs; the current writer has **no `<f>` formula cells**. `Manual Verification` uses strings like `"833.68 Pa"`, a blank manual-check cell, and hardcoded `"NOT CHECKED"`. Editing an input will not recompute the numbers. This directly violates the requested Excel audit behavior and falsifies the claim of formula-bearing workbook parity.

**RED tests:** inspect produced XLSX with an OpenXML parser, e.g., OpenXml SDK or independent `openpyxl`: numerical cells must be typed numbers, derived values must have valid `<f>` expressions and cell references, editable check cells must be numeric-or-blank, formula-driven difference and PASS/FAIL must exist, and recalculation mode must be configured. Do not accept a ZIP signature, sheet names, fixed text, or cached copies as proof. Independently recalculate a changed-input scenario in Excel or a trustworthy formula evaluator and compare with engine outputs.

### P0-B: PDF silently truncates half the calculation ledger

`DetailedReportBuilder.BuildPdf()` passes Markdown chunks to `SimplePdfWriter`; `SimplePdfWriter.Build()` loops lines and stops at `currentY < 45` with `break`, without creating overflow pages. Actual uploaded 4B.2 sample PDFs each contain **only 10 of 20 `TraceNodes[].NodeId` identifiers** present in the paired JSON. Missing ASCE identifiers include `TR-ASCE-04` and all five `TR-ASCE-SURF-*` formula nodes; AS/NZS likewise loses `TR-ASNZ-04` and all five net-pressure nodes. Page 5 renders raw Markdown pipes, not a worksheet table.

**RED tests:** every required trace node ID, symbolic equation, substituted expression, numeric output/unit, surface/zone row, issue, and provenance field in the report document must be extractable from the completed PDF (or reliably represented when PDF fonts transform glyphs). Verify long/overflow case and Unicode engineering symbols. A PDF must never return successfully when content cannot fit or is discarded.

### P0-C: Contradictory standard/project/run identities

The supplied synthetic **AS/NZS flat-roof JSON/PDF** includes `CalculationRun.Request.StandardEdition == "AS/NZS 1170.2:2021"` but `ReportModel.Manifest.Edition == "ASCE/SEI 7-22"`. Its calculation project ID is `PRJ-SYNTH-FLAT`, while the report cover uses `demo-project`, and the underlying legacy `ReportModel.Calculation.Id` is `demo-pressure-ASCE7_22-missing`. `DetailedReportBuilder` still renders design basis, active wind state, `Calculation Provenance`, and legacy hash from `model`, while selectively taking the run ID from `calcRun`. The previous fix is therefore incomplete.

**RED tests:** deliberately combine an AS/NZS run with an ASCE manifest, a mismatched project, a different wind state, or unrelated geometry. Reporting must **fail closed with structured identity diagnostics**, not produce a misleading merged report. Rebuild sample fixtures with matching project/standard/geometry or explicitly segregate a labeled unrelated demonstration geometry appendix which may not imply the calculation was performed for that model. No auto-fixing identifiers just to pass tests.

### P0-D: The WPF UI is not a working calculation-to-report workflow

`MainViewModel` defines `CurrentEngineeringRun` and a public `SetEngineeringRun()` method, but no verified WPF calculate/selection flow supplies it. `MainWindow.WindPage()` still has disabled `Validate`, `Calculate`, and `Save engineering run` buttons. `PreviewReport()` and stored report operations still use the legacy demo exporter; `ExportDetailedReport()` conditionally uses `CurrentEngineeringRun`, which is commonly `null` in normal UI usage. Unit tests calling `DetailedReportBuilder` directly do **not** prove the end-to-end WPF use case.

**RED tests:** launch an actual WPF review workflow, enter valid explicit factors, execute a calculation, select the resulting immutable run, preview, export XLSX/PDF/HTML/JSON, save/reopen, and compare run ID/hash and displayed pressure values. Invalid/review-blocked input must not yield a completed numerical report. RealReadOnly CSI mode must still block synthetic exposure and writes.

### P1-E: Missing-stage is misrepresented as zero

Both XLSX calculation and manual verification worksheets fall back to `0.0` when neither `qh` nor `q_des` is present. Zero is a *valid engineering number*, not a substitute for missing data. Expected behavior is `BLOCKED / NOT COMPUTED`, blank numeric value, and explicit issue details, never a fabricated zero or false PASS.

### P1-F: Other exporters still behave as raw text dumps

`BuildHtml()` uses escaped Markdown within `<pre>`, not semantic worksheet tables. PDF is ASCII/Courier raw Markdown; ordinary non-ASCII math glyphs become spaces, no real table/grid, and long lines may overflow/crop. Prior uploaded 4B.2 PDFs are byte-for-byte identical to the 4B.1 PDFs (independent SHA-256 check). Fix actual layouts and regenerate acceptance artifacts after code changes. Ensure HTML/Markdown/JSON and XLSX/PDF agree on the SAME coherent source.

## 2. Architecture: one coherent, typed source for all report formats

1. Add a typed immutable `EngineeringAuditReportDocument` (or equivalent) containing **exactly one selected EngineeringCalculationRun**, applicable standard/edition/amendments/procedure/method/limit-state/state, project, reviewed input evidence, formulas, numerical stages, zone/wall/roof outputs, review issues, approval status, geometry/model context, and run/manifest/model hashes. `ReportModel` legacy synthetic data is optional and may be referenced only after compatible IDs/geometry/standard are verified.
2. Introduce `AuditReportDocumentBuilder` that performs strict identity checks and resolves all report values **once**. All exporters (PDF/XLSX/HTML/MD/JSON) render this model. Do not compute new wind coefficients in renderers, silently recalculate using different formulas, or merge unrelated demo projects.
3. Ensure `Request.ProjectId`, project displayed, standard family/edition, procedure, wind state, roof form, source manifest, geometry fingerprint, and selected-run ID/hash are consistent and immutable. For an engineer-entered arithmetic run without licensed manifest, mark provenance as engineer-provided/unverified and identify which evidence gates are missing; do not present a contradictory ASCE manifest for AS/NZS.
4. Validate the complete report before export, reusing existing snapshot validators where applicable; reject contradictory cases with typed errors rather than producing polished misinformation. Preserve real read-only isolation.
5. Separate **numerical arithmetic completed**, **owner reviewed**, **independently verified**, **licensed source qualified**, and **production authorized** into distinct explicit indicators. Do not infer approvals from a `OwnerReviewed=true` string, author name, or a passing synthetic arithmetic test.
6. Report generation time can vary, but freeze a stable report source ID/content hash; do not claim the output is deterministic if timestamps or dynamic input collections affect it. Document hash scope explicitly.

## 3. Excel: actual editable and auditable workbook, not text masquerading as numbers

1. Recreate the reference *visual hierarchy* with recognizable project letterhead and lettered/numbered groups. ASCE sheets should visually echo `Windload`; AS/NZS sheets should echo `Slope Roof` / `Flat roof`. Design dynamically for the **selected standard and case**, not a generic 5-column table.
2. Distinguish input/hardcoded engineer values, documented source-linked values, derived formulas, and review states by font/fill/borders and legend. Show parameter, symbol, value, unit, source/section, and applicable wind state. Use genuine numeric cell storage for quantities and stable cell addresses; strings only for labels/notes.
3. Make derived cells evaluate using **real spreadsheet formulas** (`<f>` via proper OpenXML), e.g. site wind speed, dynamic pressure, external/internal/net pressure and verification differences, with visible references to audited inputs and signed coefficient cells. Do not use opaque formula strings that have no active dependency on numeric input cells. Keep engine arithmetic as the authoritative reference for parity tests.
4. Add a clear `Inputs` and/or `Coefficient Evidence` section and preserve all explicit source citations, sign conventions, units and state context; no auto-filling missing coefficients with fixture placeholders. If Excel formula coverage cannot implement an algorithm, mark that cell `NOT SUPPORTED` rather than faking a formula or numeric constant.
5. `Manual Verification`: each row has numeric software value, editable numeric hand-check input, appropriate absolute/relative tolerance cells (user-selectable; no unconditional default as evidence of compliance), computed difference and **formula-driven** `NOT CHECKED / PASS / FAIL`. Blank/invalid manual input cannot pass. Include verification user/date fields without manufacturing signatures. Add separate owner-reviewed and independent-reviewed states.
6. Include Wind +X/-X/+Y/-Y, internal pressure +/-, ULS/SLS, and roof-case zone matrices **where the engineering run truly contains these scenarios**. If not implemented, show an explicit `NOT CALCULATED / UNSUPPORTED` matrix with named blockers. Never duplicate one direction's numeric results into all states.
7. Add actual freeze panes, merged section headings when useful, column widths, number formats, print titles, margins/page breaks/scaling, repeat table headers. Excel must open without repair prompts. Respect Windows Excel usability and avoid macros, external links, volatile formulas, and unnecessary dependencies.
8. Force Excel recalculation on open; validate caches/recalculated outputs where supported. Prove that changing a meaningful input changes downstream formula results and check statuses. Maintain unrounded numeric values internally, round only for display. Handle decimal separators and SI unit display deliberately.
9. Implement tests for invalid/blocked run (no `qh`/`q_des`), negative suction, positive internal pressure, out-of-range inputs, duplicate/ambiguous states, untrusted formula strings, escaping and Excel injection; do not write any formula from untrusted user-provided source text verbatim.
10. **User deliverables:** real `.xlsx` files for ASCE gable and AS/NZS flat, plus at least one different wind direction/internal pressure and a ULS/SLS comparison case. Upload/attach actual XLSX files for independent audit, not just a screenshot or a list of claimed file sizes.

## 4. PDF and HTML: readable worksheet presentation and zero lost content

1. Stop converting raw Markdown lines into one fixed-height page per section. Use a genuine grid/table renderer with headers, cell widths, text wrapping, engineering symbols/font coverage, section hierarchy, and continuous pagination. Open-source or native .NET library is acceptable only after checking the project's licensing and locked-dependency policy. Maintain zero vendor CSI dependency.
2. Every trace node and each zone/wall/facade output must render completely. Long formula ledger continues across pages, repeated headers; no clipping, truncation, or overwide strings. Bold/review-highlight net pressures, velocity pressure, signs, and missing values. Use landscape pages for wide matrices if needed.
3. Structure PDF as the original workbook: letterhead/design basis, selected factors/geometry, site/velocity pressure arithmetic, Wind X/Y & IP states, roof/wall coefficients, net-pressure matrix, manual-check table, numbered formula trace, source evidence and audit appendix. Different standard procedures have appropriate sheet headings; no ASCE fields on an AS/NZS report.
4. HTML uses real semantic `<table>` elements and properly escaped data; CSS print styles should closely match the PDF tables. No single Markdown `<pre>` dump. Markdown remains a readable fallback, JSON retains exact unrounded numbers and IDs.
5. Test PDF text and visual output: assert **20/20 trace nodes** appear for both current sample runs (and complete content in longer stress fixtures); compare all result cells with JSON/engine to appropriate precision; render every PDF page to PNG and inspect for overflow, black squares, broken symbols, collisions and page-edge clipping. A section may not be silently shortened to fit a fixed page.
6. Do not publish customer details or protected licensed data in screenshots/public CI. Use synthetic fixtures only for public regression samples.

## 5. WPF and saved report lifecycle: prove the real user workflow

1. Create or finish an explicit **Engineer Review (Explicit Input)** calculation workflow, logically separate from both fictional Demo and actual CSI RealReadOnly. The owner can choose standard, supported procedure, roof/geometry, limit state, wind direction, IP alternative and input evidence; review missing inputs; calculate; inspect formulas; and select/save an immutable numerical run. Real CSI data must never silently enter a simulated calculation.
2. Wire `Validate input`, `Calculate`, and `Save engineering run` controls to the true engine where allowed; show named blocked stages and source/evidence status. Do not enable a button that calls a blocked normative calculation with fabricated coefficients. A valid explicit-input run may be audited without claiming normative qualification.
3. `CurrentEngineeringRun` must be set by an actual user-initiated calculation or deliberately selected saved run, not merely by an unreferenced setter. Editing inputs, switching case/project/workspace, or refreshing geometry invalidates stale selections. Preview, Save, Open and Export must use the same frozen selected-run identity and hash; otherwise refuse export.
4. Include an in-app worksheet-style preview (tabular input and result panels) and explicit `No engineering run selected` banner when empty. Show all active pressure states/units, source notes and review status. Keep all existing six smoke scenarios passing and add at least one WPF *actual calculate -> preview -> save/reopen -> XLSX/PDF export* smoke with matching run hash.
5. `RealReadOnly` must preserve `AssertReadOnlyVisualIsolation`, no demo leakage, no load assignment, no model save/unlock, no CSI setters. No live CSI writes in this sprint.

## 6. Test and evidence gate (required to call this sprint complete)

A. **RED-to-GREEN regression tests** for (1) typed numbers and nonempty `<f>` formula cells, (2) downstream Excel recalc after input edits, (3) hand-check PASS/FAIL/NOT CHECKED, (4) PDF 100% trace-node coverage, (5) project/standard/run mismatch blocked, (6) blocked quantity never represented as 0, (7) ASCE/ASNZ separate design bases, (8) WPF real selected-run lifecycle, (9) no RealReadOnly weakening, (10) cross-format exact source/hash and numeric parity.

B. **Two complete synthetic examples minimum**, ASCE 7-22 gable and AS/NZS 1170.2:2021 flat, plus negative/alternative-state tests. Include a state matrix for directions/IP/ULS-SLS where supported. Distinguish a numerical match to synthetic hand-check from independent engineering qualification. No presumed normative constants.

C. **Independent verification**: inspect the XLSX with a second reader, e.g. openpyxl/Open XML SDK, enumerate numeric versus formula cells and evaluate a changed-input example; extract all PDF page text and compare every trace ID and result with JSON. Build a machine-readable `report_parity_audit.json` or Markdown table listing `case/state/quantity/engine/xlsx-cached-or-recalculated/pdf/html/delta/sourceHash/checkState`. An identity mismatch should be a recorded **BLOCKED** test, not papered over.

D. **Visual QA**: render actual PDF pages and workbook main/verification/formula sheets, compare layouts side by side with original workbook reference screenshots *without reusing source coefficient values*, and retain **private local** screenshots/artifacts. No claims of layout parity without views of the actual new workbooks and PDFs.

E. **Build/test**: locked restore, Debug and Release solution builds (zero warnings/errors), full non-physical automated suite, all six existing WPF smoke processes and an additional report-export smoke. Preserve the project safety boundary: `CanCalculateNormativePressures == false`, no independent engineering approval claims, no live CSI writes.

F. After all checks pass, commit and push verified changes directly to `main` according to repository workflow. Report exact SHA, CI run URL/status, test counts, affected files, regression outcomes, genuine attached XLSX/PDF/HTML/JSON outputs, relevant visual evidence, and any unresolved standards-data limitations. If a mandatory acceptance item fails, do **not** state Report Builder parity complete; disclose it clearly and keep the production gate closed.

## 7. Explicit non-goals

- Do not copy ASCE 7-16 coefficients from the layout reference or assume its formulas are compliant with ASCE 7-22.
- Do not import copyrighted standard tables into Git, artifacts, binary builds, or CI.
- Do not fabricate licensed source evidence, numerical benchmark sign-off, or independent reviewer status.
- Do not enable production normative calculation authorization, automatic engineering approval, live ETABS/SAP2000 load writes or vendor CSI setters.
- Do not substitute a static textual Excel for a formula-bearing engineering worksheet, nor a PDF screenshot for an actual `.xlsx` deliverable.

**Execution instruction:** Implement Sprint 4B.3 now. The result is accepted only when the owner can open the generated `.xlsx`, change an explicit review input and see the derived cells/verification statuses update, while the matching PDF/HTML/JSON fully and coherently reproduce the chosen engineering calculation without missing formulas or mismatched identities.
