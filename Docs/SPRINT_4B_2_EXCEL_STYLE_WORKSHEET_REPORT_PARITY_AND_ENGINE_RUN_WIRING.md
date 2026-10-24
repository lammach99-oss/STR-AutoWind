# Sprint 4B.2 — Excel-Style Calculation Worksheet Report Parity and Engine-Run Wiring

**Repository:** `lammach99-oss/STR-AutoWind`

**Windows repository root:** `D:\Project\Etab-Sap Tool\AutoWind`

**Store this instruction file at:**
`D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_2_EXCEL_STYLE_WORKSHEET_REPORT_PARITY_AND_ENGINE_RUN_WIRING.md`

**Verified base at brief preparation:** `b0cfdedd8464c3ae4dbf2012b4eb1dac4e1997f5` on `origin/main`.

**Verified CI:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/40068286423 — completed successfully, 333 non-physical tests; WPF CI smoke evidence. Refresh `origin/main` before implementing; report the actual starting SHA if different.

## 0. Owner intent and non-negotiable outcome

The project owner is a practicing engineer who wants to **see and manually check every calculated number in the same practical worksheet layout as the Excel workbooks previously supplied**, not a loosely formatted Markdown transcript. This sprint delivers a **real, spreadsheet-like calculation report** in the WPF application and usable exports (especially **native `.xlsx` and polished PDF**). Match the reference workbook's **visual information architecture** while computing solely from the application's typed engineering calculation run and explicitly sourced coefficients. A visually attractive PDF without underlying traceable numbers is NOT acceptable.

**Scope:** Rework report data binding, structured layout, WPF review/preview, exports, independent numeric comparison, visual QA, and required safety fixes. Continue actual implementation. Do NOT substitute another checklist or claim Excel parity from a template screenshot alone.

**Safety:** Engineer-review arithmetic may be shown when explicit valid inputs are provided. No unsupported normative coefficient invention, no auto-certified engineering validity, no automatic independent-review claims, no live CSI writes, no real-model assignment/approval. Preserve `CanCalculateNormativePressures = false` for unqualified bundles and all existing `RealReadOnly` isolation gates.

## 1. Actual supplied workbook references (layout authorities only)

The following Excel files were inspected for layout. They were supplied in the project conversation; **their availability in the coding agent's Windows filesystem is not guaranteed**. Locate the owner's original files first. Likely project storage is `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\` (the older local evidence mentions `ASCE_Wind load.xlsm` and `AS.NZS_Wind load.xlsm`). The actual uploaded filenames were:

1. `Hawaii Housing_Wind load_B.xlsm` — contains `Windload`, `Windload for CC`, `Windload (Imperial)`, `DATA`, `Risk Category`. **Its main worksheet labels ASCE/SEI 7-16, NOT ASCE 7-22**. Use this workbook for presentation/layout, NEVER assume its 7-16 values, table labels, formulae or jurisdictional assumptions apply to 7-22.
2. `Wind load AS.NZS_250729.xlsm` — contains `Data Sheet`, `Slope Roof`, `Flat roof`, `test`, `roof-NZS3404`, `garage roof-NZS3404`, `Sheet2`. The `Slope Roof` / `Flat roof` layouts are the key references for the AS/NZS-specific report. Legacy workbook formulas may contain technical defects; presentation parity does not equal normative calculation parity.

If these exact filenames are missing from the Windows workspace, inspect the actual local `.xlsm` copies and document which files/sheet revisions were used. **If no workbook copy is accessible to the agent, implement the sheet geometry described below, but report visual parity as *unverified* and request the owner to place the two exact workbooks in `Reference Workbook/`; do not claim pixel parity.** Never execute workbook macros/VBA or alter the source files. Inspect workbooks read-only (OpenXML metadata, displayed values, styles, merged cells, print areas, pictures and formulas as inert text). Do not copy source workbook binary, macros, licensed coefficient tables or potentially confidential project details into the public repo, CI artifacts or distributable binaries.

### 1A. ASCE layout — actual `Hawaii Housing_Wind load_B.xlsm` / `Windload`

- **Page setup:** A4 portrait (`paperSize=9`), print region approximately **`A1:P330`**. Workbook contains many merged header/table cells; use deliberate column widths rather than a generic data grid.
- **Letterhead rows 6–11:** `APPENDIX - LOAD CALCULATION`, project, item/title, project code, calculated by, checked by, dates and revision. Use owner-supplied report metadata. Never display invented reviewer signatures or example client names.
- **Row 13:** `A. Reference`.
- **Row 16 onward:** `B. Basic wind pressure calculation`: location/risk category, design wind speeds, directionality, exposure, topography, ground-elevation factor, exposure/height factor, ULS/SLS velocity pressures, gust and enclosure/internal pressure. Typical layout: **description left, symbol/formula in center, numeric result adjacent, unit next, clause/table reference at right**.
- **Row 45:** `C. Design wind pressure of building calculation`.
- **Wind X at approximately row 97; Wind Y at approximately row 124:** each repeats B/L/h/roof pitch and separately shows **windward/leeward/sidewall coefficient and resulting pressures**. The wall matrix around rows **104 and 131** explicitly contrasts **positive vs negative internal-pressure alternatives** in neighboring columns. Roof zone/distance-band matrices follow at approximately **110/117** and **137/144**. Both sign alternatives must be independently calculable and visually distinct.
- **Canopy section at approximately row 151:** present only when the application supports the chosen canopy procedure; otherwise label **NOT IMPLEMENTED / NOT CALCULATED**. Never fill it from unrelated building MWFRS values.
- `Windload for CC` is a separate components-and-cladding presentation: preserve MWFRS/C&C separation. `Windload (Imperial)` is a unit-presentation reference; SI must remain canonical and display conversion explicit.
- Styles: conventional compact engineering calculation sheet, **blue engineer-entered values** (actual cells use `#0070C0`), bold section headings, centered numeric cells, restrained borders, merged group headers. Do not blindly copy all original styles; preserve legibility and consistency.

### 1B. AS/NZS layout — actual `Wind load AS.NZS_250729.xlsm`

- **Page setup:** A4 portrait (`paperSize=9`). `Slope Roof` print region approx **`A1:N103`**; `Flat roof` approx **`A1:N124`**. Key worksheets have many merged subheading and case-table cells.
- **Rows 5–8:** `LOAD CALCULATION` letterhead with project / address / item and calculated by / checked by / date / revision. Keep the two-sided metadata header.
- **Rows 10–13:** `Design code & reference`, `1. Basic parameters`.
- **Rows 14–35:** roof type and dimensions, pitch, mean/peak roof heights, region, terrain, ULS/SLS recurrence/design speeds; `Md`, `Mc`, `Ms`, `Mt`, `Mz,cat`; calculated site speeds and basic pressure. Example layout places parameter names to the left, the symbol/formula near **J**, numeric result near **K**, and unit/locator near **L**; never copy old values.
- **Row 37:** `2. Wind load calculation`.
- **`Slope Roof`:** Wind X around row **38**, windward/leeward/side wall subsections at **43/48/54**, roof cases at **62**, Wind Y at **71** and its wall/roof sections to the end. `Flat roof`: Wind X ~**38**, cases **1 / 2** in roof table ~**62–80**, Wind Y ~**82**, sidewall band/roof case matrices thereafter. Display zone distance bands (e.g., 0–h, h–2h, etc.) and only the appropriate supported cases; an unsupported region must be labelled, not filled.
- For each direction/case present external factor and **ULS/SLS results**, separately show positive/negative internal pressure alternatives (where applicable), explicit signed net pressures and unit conversions.
- Styles: dark-blue section and input typography similar to workbook `#0A328C`, occasional light-blue derived/input emphasis `#B9EDFF`, readable black derived values, compact grouped tables, bold case headers, centered unit-bearing numeric cells. Do not rely on colour alone; label all states.
- `Data Sheet` is a legacy source/reference table, **NOT an approved live normative data source**. The additional `roof-NZS3404`, garage and `test` sheets are not automatically the target scope.

### 1C. Canonical visual style tokens

Use a restrained worksheet theme: compact engineering table typography with 9–11 pt body; blue header bands and clear subsection rules; entered numbers blue; derived calculations black; imported/verified-source values green with source label; caution/orphan numbers orange; blockers red; neutral grey static labels. Consistent 2–3 decimal places for display according to physical quantity (but store full precision), fixed sign conventions, units on all numeric outputs, clear `NOT CHECKED`/`MISSING` labels. Styles must convey input/formula/import/review state using **text labels as well as colour**. Repeat table header rows across page breaks, use real cell borders and align numbers to the right. Avoid excessive ruled-grid clutter.

## 2. Source-reviewed defects in the current Sprint 4B.1 report implementation (must fix)

### P0 — WPF export is disconnected from engineering calculation run

`src/StructuralLoadAssistant.App.Wpf/MainViewModel.cs` currently has `ExportDetailedReport(string path)` that calls `builder.BuildPdf(model, null)` (and HTML/Markdown/JSON similarly). A `null` engineering run means formula/coefficient worksheets are absent in the actual desktop export even when the test-generated PDF contains such rows. The app must expose a **selected immutable `EngineeringCalculationRun`** created from the explicit engineer-review input scenario and pass that SAME run to preview, save, and export. If absent, show `NO ENGINEERING RUN SELECTED` and refuse numerical calculation tables, or create a clearly labelled *no calculations* report; **never silently substitute synthetic demo pressures**.

### P0 — Incoherent old/new calculation linkage

`src/StructuralLoadAssistant.Reporting/DetailedReportBuilder.cs` currently displays `model.Calculation.State`, `model.Calculation.Id` and `model.Calculation.Provenance` from the older `WindCalculationRun` while surface results, coefficients and formulas may be taken from `calcRun` (`EngineeringCalculationRun`). The uploaded PDF example shows an appendix `Calculation Run ID: demo-pressure-ASCE7_22-missing` while listing the newer synthetic calculation values. **Reject this mixed-snapshot report**. Introduce a single typed report snapshot/source contract, strict project/edition/procedure/limit-state/wind-state and provenance coherence, and record canonical hashes of the authoritative engineering run. If legacy model metadata is included, label it separately and verify its linkage. Prevent cross-project, cross-standard, cross-state, and synthetic/live data blending.

### P0 — Current PDF is Markdown printed as Courier text and loses content

`SimplePdfWriter` writes raw source lines in Courier and stops rendering page text when `currentY < 45`, dropping overflow lines. `BuildPdf` splits Markdown at `\n## ` and creates one page per section. The two provided sample PDFs show actual `| :--- |` table syntax instead of table grids, enormous blank space, formula ledger text clipped/truncated across pages, and no Excel-like layout. `Encoding.ASCII` and `EscapePdf` replace non-ASCII characters (Greek symbols, ±, degree sign, subscripts) with spaces. Replace this design with a structured layout engine: true tables, dynamic row heights, wrapping, automatic section/table continuation, Unicode-capable font/text rendering, repeat headers, column alignment, page numbers, watermarks, and intelligent page breaks. Do not simply add CSS to the current `<pre>` text representation.

### P0 — Manual-check worksheet has placeholders instead of numbers

`DetailedReportBuilder.BuildMarkdown` currently writes `Software Value | As calculated` instead of binding real `q_h`/`q_des`, wall/roof net pressure, roof-case alternatives and facade line-load values. Build an actual engineer-check worksheet row for **every supported calculated quantity** with source engine value, unit, allowable tolerance, a user-entered hand-check field, computed delta and status. No supplied hand-check ⇒ **NOT CHECKED**, never a PASS. Treat manual check input and reviewer attestation separately from automatic calculation, and do not auto-toggle independent review.

### P1 — Dimensional and arithmetic trace readability

`SurfacePressureResult.GlobalPressureVectorPa` is a **pressure vector (Pa)**, not a force in Newtons: do not label it `Global Force Vector (Pa)`. Real applied forces require verified area/tributary geometry (N or N/m). Present `p_ext`, `p_int`, `p_net`, local/global sign convention and units side by side. Every meaningful result must have both symbolic and substituted expression in an adjacent readable row/cell; no hidden unexplained constants, profiles or geometry assumptions. Values that were never computed must be `MISSING / BLOCKED`, not zero.

### P1 — Existing output tests are insufficient

Current `DetailedReportBuilderTests` check PDF header `'%PDF-1.4'`, `%%EOF`, and a few text tokens, but do not verify layout fidelity, non-clipping, PDF Unicode rendering, worksheet calculation coherence, displayed numeric parity, actual WPF export wiring, page breaks, or `.xlsx` formula accuracy. Replace/expand tests to cover those obligations.

## 3. Required typed report architecture (one source, multiple renders)

Implement an immutable structured `EngineeringWorksheetReport` (or equivalent) assembled once from a validated selected `EngineeringCalculationRun`, explicit owner-entered display metadata, optional geometry/mapping snapshot and optional manual-check records. Suggested components:

- `ReportHeader`: project ID/title, address (only if supplied), code edition/revisions, procedure, limit state, directions, roof case, report number, author, reviewer **if entered**, date, revision, dataset and run hashes, qualification badge text.
- `WorksheetSection`, `WorksheetTable`, `WorksheetRow`, `WorksheetCell`, `FormulaTraceReference`: stable semantic keys; typed input/derived/imported/review cells, units, source locators, complete numeric precision and display formats. Avoid using plain Markdown as the authoritative model.
- `WindStateMatrix`: explicit `+X/-X/+Y/-Y`, ULS/SLS, internal pressure **positive and negative**, roof cases and geometric zones; support only states with calculated data; show all missing/unsupported states as blocked.
- `CalculationTraceRow`: factor and source; formula ID; symbolic formula; substituted numeric expression; intermediate and final value; unit; sign convention; reference clause; warning; dependencies.
- `ManualVerificationRow`: selected run result, independently entered hand-check value, absolute and relative differences, tolerance, PASS/FAIL/NOT CHECKED, reviewer identity/date and evidence note/hash when actually supplied. User modifications should not overwrite frozen source runs.

Create a **single report-assembly validation gate** ensuring all report sections reference one coherent immutable run. Export writers may never recalculate or quietly modify engineering pressures. Preserve full original numbers in data/model and round only for display. Snapshots and hashes must reflect meaningful source content and not include unstable `DateTimeOffset.UtcNow` unless capture is explicitly part of the frozen report ID/revision.

## 4. Required report worksheets and WPF user experience

### 4A — Workbook-style report outline

Use per-standard worksheet groups, not a generic one-page-per-section appendix. At minimum:

**Common cover/identification strip:** project, location, report code, calculation author, optional manual reviewer, revision/date; standard edition and engineering review status visible on every page.

**ASCE/SEI 7-22:**

1. `A. Reference / Design Basis` — governing edition, supplements/errata applicability, design state, procedure, input sources.
2. `B. Basic Wind Pressure` — risk/exposure/speed and K factors; symbolic equations; alongside ULS/SLS quantities only when both were calculated.
3. `C. Building Wind Pressures — Wind X` — B, L, h, pitch; wall Cp + p_ext / p_int / p_net for **both** internal states; sidewall/band and roof case table.
4. `C. Building Wind Pressures — Wind Y` — separate direction/scenario table; no reuse of X coefficients unless justified by actual same sourced record.
5. `D. Canopy / C&C` — only if selected/procedure-supported; show BLOCKED otherwise, not fabricated rows.

**AS/NZS 1170.2:2021:**

1. `1. Basic Parameters` — roof type and geometry; Region, Terrain, VR ULS/SLS; Md/Mz,cat/Ms/Mt/Mc; calculated Vsit and q for each state.
2. `2.1 Wind X (0°/180°)` — windward, leeward, sidewall band matrix, flat/sloped roof zone Case 1/2 as applicable, ULS/SLS and internal alternatives.
3. `2.2 Wind Y (90°/270°)` — same organized tables with direction-specific calculations.
4. `Roof Case / Zone Summary` — concise matrix of sign, Cp/Cpe, positive and negative internal states, p_net, geometry/normal and status.

**Shared appendix worksheets:** `Coefficient Register & Source`, `Formula Audit Trail`, `Roof/Facade Load Distribution`, `Wall Dead Load`, `Manual Check`, `Issues/Missing Data`, `Immutable Sources`. Only include facade/roof loads if the data are linked to the same calculation and are actually computed; independent wall calculations must carry their own source IDs and flags.

### 4B — Interactable WPF Report Builder

- A **worksheet-style preview** (tabs/sections and a virtualized data grid) with frozen identifier columns, visible units, editable *review/check* cells only, and navigation/filtering by family, wind direction, ULS/SLS, internal state, roof case, wall/roof zone. The calculation source itself must remain immutable.
- Row selection shows full symbolic formula, numeric substitution, intermediate values, clause/figure locator, coefficient provenance, and a clearly marked 'owner hand-check' section.
- User selects a specific engineering run before preview/export. Include a selector/banner with actual run ID, standard, method, source, review status. Treat missing or mixed runs as failures. Work in offline **Engineer Review** mode without connecting a real CSI model; retain RealReadOnly model-write lock. Do NOT expose demo calculations in RealReadOnly.
- Include controls for `Excel (.xlsx)`, `PDF`, `HTML`, `Markdown`, `JSON` export and preview the same selected report. Do not call existing fake-only `ReportModel` without the selected engineering run.

## 5. Mandatory native Excel `.xlsx` export

Make `.xlsx` a first-class export, because the owner needs to click numeric cells and inspect derivations. Macro-free only: never export/import `.xlsm` VBA.

- Workbook tabs corresponding to the actual ASCE/ASNZ reference sheet structures above, plus sources, formula trace, manual check and limitations; **do not export all tabs for an unselected standard**.
- True Excel **formulas** for computed worksheet cells and reviewer comparison `Difference` / `Check Result`; formulas must reference input/factor cells with clear relative/absolute addressing. Also show the substituted arithmetic string in an adjacent, non-formula cell for fast visual checking.
- Preserve original exact engine result in a clearly labelled `Engine Value` column. On opening/recalculation, Excel formula outputs must match the engine within the report-defined precision/tolerance. If a calculation cannot be expressed in a maintainable Excel formula, provide the exact source trace and a numeric output with an explicit `ENGINE-CALCULATED / NO EXCEL FORMULA` status rather than a fabricated formula.
- Formula display and result conversion must not hide factor sources. Distinguish engineer-entered (blue), derived (black), source-provenance (green), review input (light-blue/orange), error (red), and static grey; legend included.
- Excel `Print_Area`, A4 page layout, repeating header rows, reasonable column widths, merged section headers, frozen panes, number formats, print-preview pagination and clean PDF export parity.
- Hand-check column initially blank; `Status = NOT CHECKED` until reviewer enters a value. Difference and PASS/FAIL use explicit, validated tolerance rules and formula cells. No prefilled fabricated hand-check.
- Apply CSV/XLSX/HTML/PDF injection protections for untrusted project names, identifiers and source strings; only program-created formulas may be executed as formulas.
- Do not expose private source workbooks, macros, unredacted proprietary data or copyrighted standard tables in public samples or CI artifacts.

## 6. PDF and HTML must visually match the worksheet, not print Markdown

- Render structured report tables directly, not `.md` lines. PDF should show the **same grouped rows, heading positions, adjacent formula/value columns, wind direction matrices and case boundaries** as the `.xlsx` export.
- Use A4 portrait for standard calculation pages (landscape only for genuinely wide pressure-state matrices), running project/standard/status header, page X/Y and calculation-run ID footer, page-safe borders/merged group headings, repeat table header on continuation pages.
- Flow sections continuously to avoid a full blank page after every tiny table. No dropped overflow: a 100-row ledger must continue across as many pages as needed; preserve row wrapping and explicit 'continued' headers.
- Display Unicode technical math and units correctly: `θ`, `α`, `β`, `±`, `°`, `²`, `³`, subscripts where supported. No ASCII replacement of these symbols. Validate fonts and distribution rights; do not bundle unauthorized font files.
- Use actual vector/table/paragraph layout. Style HTML tables with semantic `<table>`, `<thead>`, accessible labels and print CSS; avoid an entire escaped Markdown report in `<pre>`.
- For Markdown retain a text alternative with the same numerically accurate values and source references; JSON remains the exact structured audit export.

## 7. Test-first acceptance criteria and expected evidence

**RED tests before code changes; GREEN after**. Add cases covering at least:

1. WPF export of a selected engineering run contains that run's unique ID and its `q_h`/`q_des` and roof pressure results; it must NOT list `demo-pressure-ASCE7_22-missing` or old `Session.Calculation` as the authoritative calculation ID.
2. Cross-project, cross-edition, cross-limit-state, cross-wind-state and unverified mixed-snapshot exports are rejected with typed errors. Missing engineering run produces explicit `NO ENGINEERING RUN`/`NOT CALCULATED`, never fake values.
3. Workbook-style **ASCE** layout smoke: has A/B/C headings, B basic factors, X/Y directions, positive/negative internal pressure table columns, line-by-line p_ext/p_int/p_net and source locator fields. Unsupported cases are labelled.
4. Workbook-style **AS/NZS** layout smoke: has basic parameter rows, ULS/SLS, multiplier/site-speed factors, Wind X/Y wall/sidewall/roof subsections, and roof Case 1/Case 2 only when supported.
5. Every factor/surface cell has canonical unit, exact engine-source numeric value, trace ID and provenance; changing engineer inputs changes the expected outputs and ALL formats consistently. No silent default/fallback factors.
6. `.xlsx` contains proper sheet names, stored formulas, correct references, effective number formats, freeze panes, print areas, titles and a review-status legend. Formula results independently checked against the engine; no accidental formula injection.
7. Manual Check rows show actual software value and blank human value; blank => NOT CHECKED, wrong => FAIL, within tolerance => PASS. Manual value must not mutate frozen calculation run or falsely mark independent approval.
8. PDF render tests on both supplied synthetic fixtures: real table structures; no literal Markdown table delimiter rows; all rows visible; correct printed Unicode; repeated headers; no massive unintended whitespace; logical multi-page ledger continuing without truncation; readable numeric alignment.
9. HTML real tables, escaping of `<script>` and long malicious inputs, print CSS; JSON/MD/PDF/XLSX all show identical quantities and sign conventions at consistent displayed precision.
10. Report geometry, local axis/normal and pressure vs force units validated: pressure vector (Pa) never called 'force (N)'; N/m or N only when actual mapped dimensions exist. Positive/negative internal pressure alternatives, ULS/SLS and X/Y must never overwrite or reuse wrong values.
11. Save/reopen immutable report snapshots and tamper/fingerprint checks. Independent review status requires explicit, persisted actor/time/review evidence; owner hand check is not external sign-off.
12. All existing 333 nonphysical tests and six WPF smoke processes continue to pass, plus new acceptance tests. Locked restore, Debug + Release, report PDF/XLSX/HTML/Markdown/JSON smoke tests; on Windows inspect print/rendered pages visually. Show source-to-report numeric comparisons and screenshots stored only in approved synthetic CI artifacts.

**Baseline reports provided to compare visually:**
- `asce7_22_gable_roof_audit.pdf` — existing 10-page synthetic report has raw `|` Markdown table pipes and empty whitespace; NOT a satisfactory acceptance reference.
- `asnz1170_2_flat_roof_audit.pdf` — same issues. Both are design defects to correct, not final templates.

**Deliver actual output evidence:**
- Two exported representative synthetic `.xlsx` files (ASCE gable and AS/NZS flat roof) and corresponding polished `.pdf` + `.html`, all sourced from the selected calculated `EngineeringCalculationRun`.
- Screenshot(s) of workbook-layout parity against the owner's reference `Windload` and `Slope Roof`/`Flat roof` sheet sections, **not proprietary source values**. Keep any source workbook screenshots private; only synthetic screen evidence may appear in public CI.
- A compact table listing each checked row/trace ID, engine value, `.xlsx` recalculated value and PDF extracted/displayed value, with units and numerical delta.
- Explicit report of any unavailable reference workbook and therefore *unverified layout parity*; do not fake visual review.

## 8. Work sequence, push policy and handoff

1. Fetch `origin/main`, verify baseline and existing WPF export paths. Read the two reference workbooks read-only, inventory sections, print regions, style semantics, merged headings and row hierarchies. Preserve them untouched.
2. Write failing tests for missing selected-run binding, legacy/new snapshot mix, PDF overflow, hand-check placeholders, and worksheet export formulas.
3. Implement structured report model and source/run coherence; wire WPF to explicit run selection and saved report sources (no auto review approval).
4. Implement `.xlsx` first, using workbook-like sections and formula/values. Verify calculated values and layout.
5. Implement genuine PDF/HTML table renderers from the same structure, plus MD/JSON parity; inspect actual exported pages for both fixtures.
6. Implement reviewer worksheet UX, explicit input audit and provenance, robustness/security checks, documentation and tests.
7. Run locked restore, Debug + Release builds, all automated tests, 6 WPF smoke modes and report export/render checks. Avoid new proprietary runtime dependencies; verify any added PDF/XLSX library license before use.
8. Commit and push only verified, source-only changes directly to `main` per owner workflow; never commit the original `.xlsm`, private coefficients, reviewer files, or sample client data. Report SHA, CI URL, test count, screenshots/export paths, resolved issues and remaining blocked normative steps.

**Definition of Done:** In the actual WPF app the owner can select a numerical engineering run, inspect a worksheet visually similar to the supplied Excel calculation sheets, compare each factor and stage to manual calculations, and export a **native formula-bearing `.xlsx`** plus a **properly laid out, printable PDF** that show the SAME results and accurate qualification state. The calculator does not claim that the legacy Excel's old technical values are governing ASCE 7-22 or approved AS/NZS tables; unauthorized real CSI writes remain impossible.

**Implement this sprint now. Do not stop after generating a design proposal.**
