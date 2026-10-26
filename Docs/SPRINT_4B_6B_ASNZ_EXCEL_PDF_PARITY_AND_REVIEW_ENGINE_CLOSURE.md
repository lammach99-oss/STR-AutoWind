# Sprint 4B.6B — AS/NZS 1170.2:2021 Excel-Exact PDF, Engineering Truthfulness & Explicit-Input Closure

**Priority:** P0 owner-directed correction of incomplete Sprint 4B.6A. **Deliver running code and verified PDFs, not another design-only report.**

**GitHub repository:** `lammach99-oss/STR-AutoWind`

**Windows repository root:** `D:\Project\Etab-Sap Tool\AutoWind`

**This instruction file must be saved in the repository at:**

`D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_6B_ASNZ_EXCEL_PDF_PARITY_AND_REVIEW_ENGINE_CLOSURE.md`

**Independently checked baseline:** `origin/main` = `57a698268fff5f21f10c067bbd3a8e850fc3a224` (26 October 2026). Re-fetch HEAD before editing; do not overwrite newer work.

**Verified CI for that SHA:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/40359608418 — success, 421/421 unit/integration tests (0 failed/skipped), eight WPF smoke scenarios including `asnz-worksheet`, build 0 warnings/errors.

**Original directive:**
`D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_6A_ASNZ_2021_MONOPITCH_STANDARD_CALC_AND_EXCEL_PDF_PARITY.md`

**Primary owner workbook, READ-ONLY:**
`D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\AS.NZS_Wind load.xlsm`

SHA-256 `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`.

**Primary standard reference, subject to its actual usage rights:**
`D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\AS_NZS 1170.2.2021 - Wind load action.pdf`

SHA-256 `f0ab624bf1923547833ebb79e3a2947822b569828bc9472beffa491ae5521de4` (124-page AS/NZS 1170.2:2021 base PDF). Its page 2 contains restrictive subscriber/copyright terms; the owner's `yes, proceed` is permission to continue engineering work, **not evidence of a license permitting machine extraction, hardcoding or distribution of protected normative tables in software**. Jurisdiction, amendment adoption and documented source-data/software rights remain unresolved. **Never commit the standard or protected coefficient tables.** Use only authorized, git-ignored local data or explicit engineer-supplied inputs. Never invent license details or independent engineering acceptance.

**Review artifacts provided to owner:**
- `ASNZ_2021_FlatWorkbook_Comparison_Review.pdf`: 54 A4 pages; the nominal main worksheet occupies first three pages, with a very long 320-case table and trace ledger thereafter.
- `ASNZ_2021_Blocked_Diagnostic.pdf`: 28 A4 pages; with `Mc` removed, it correctly says `NOT CALCULATED` for dependent pressures, but the WPF state handling is wrong (see P0 item E).
- The owner's five screenshot references were shared in the conversation (`image(20261026-154532).png` to `image(20261026-154610).png`). The agent MUST establish local access to images if making image-by-image claims; if absent, use the actual workbook formatting and label screenshot pixel parity `NOT VERIFIED`.

---

## 0. Acceptance decision from independent review: **4B.6A IS NOT DONE**

The previous commit adds useful explicit-input arithmetic, 320 stable signed case identities, basic site and design speeds, an HTML-to-PDF flow, and cancellation support. It does **not** deliver the owner's main requirement of an Excel-look report and a standards-qualified automatic coefficient calculation. Passing CI proves regression success, not that AS/NZS coefficients have the right applicability.

Specific source locations audited in the exact pushed SHA:

- `src/StructuralLoadAssistant.Reporting/AsNz2021WorkbookPdfRenderer.cs`: the worksheet is a **two-column `<table>`** (`Workbook row / parameter` versus `Calculated value / equation`), each visible row is prefixed with the Excel index (`5.`, `6.`, etc.), and zone headers/From/To/Cp/W cells are concatenated with `|`. This is not `Flat roof!A1:N124` visual parity.
- `src/StructuralLoadAssistant.Engineering/AsNz2021ReviewCalculation.cs`: all relevant `Cp,e`, `Ka`, `Kc,e`, `Kℓ`, `Kp`, `Kc,i`, `Kv` come from named explicit inputs. There is no normative Table 5.2 / 5.3 applicability selector. Some synthetic fixtures intentionally use **the same coefficient for all bands, cases and directions**, which is useful for arithmetic but NOT a real 2021 coefficient validation.
- `src/StructuralLoadAssistant.Csi.Fake/AsNzMatrixFixture.cs`: the synthetic roof Case 1 uses `-0.6` on every interval, Case 2 `+0.15` on every interval, sides `-0.4` on every interval. These are test numbers, not code-qualified AS/NZS results.
- `src/StructuralLoadAssistant.App.Wpf/MainViewModel.cs` near `CalculateEngineeringRunAsync()`: a blocked AS/NZS matrix run is intentionally passed to PDF rendering, but after successful render **`CurrentReportState = Ready` regardless of `run.NumericalComputationPossible`**. The UI must not indicate successful calculation for a blocked diagnostic PDF.
- `MainViewModel.ExportDetailedReport()` can fall back to `DetailedReportBuilder().BuildSummaryPdf(...)`, which violates the owner-specified worksheet PDF-only result when Chromium is absent. The export also needs a strict current-run/current-model/current-request identity check and asynchronous cancellation handling.
- `AsNz2021WorkbookPdfRenderer.cs` worksheet row 34/35 **prints a formula containing `C_shp`** while its number is computed as `0.5*rho*Vdes²*Cdyn`, i.e. velocity pressure basis **without** a surface shape factor; its reference row text also refers to `Vsit,β`. Label the calculation actually performed, and distinguish speed and pressure bases. This is an engineering-report truthfulness defect, not just styling.
- `MainWindow.WindPage()` still loads an **external request JSON**, `RequireDemo()` controls calculation, and the source even identifies a fake CSI model. Real ETABS/SAP geometry is not bound to this new matrix. Do not claim automatic model extraction.
- `AsNz2021ReviewCalculation.cs` accepts slope/height inputs without a quantified check that the claimed roof plane is consistent with the selected measured roof run, natural ground datum and X/Y orientation. A 14-degree value must not be treated as proof of an actually detected roof plane.

### 0.1 Confirmed workbook-vs-synthetic-PDF differences (not normative adjudications)

Read-only cached values in `Flat roof!A1:N124` and visual PDF audit give these comparisons:

| Workbook cell | Workbook cached input/output | Current review PDF | Diagnosis required |
|---|---|---|---|
| `J44`, `J46` | windward `Cp,e=+0.80`; ULS external `+2.271787 kN/m²` | `+0.70`; `+1.987814 kN/m²` | `-0.283973 kN/m²` difference; verify applicable 2021 coefficient and height-speed method, do not force a match |
| `J50`, `J52` | leeward X `Cp,e=-0.30`; ULS `-0.851920 kN/m²` | `-0.40`; `-1.135894 kN/m²` | explicitly select using `d/b`, clause/table applicability and interpolation |
| `F57`, `I57` | sidewall band 0–h: `Cp,e=-0.65`; `-1.845827 kN/m²` | `-0.40`; `-1.135894 kN/m²` | synthetic uniform sidewall factor cannot pass region-based parity |
| `F67`, `I67` | roof Case 1, 0–0.5h: `Cp,e=-0.90`; `-2.555760 kN/m²` | `-0.60`; `-1.703840 kN/m²` | synthetic uniform roof factor cannot pass zone-based parity |
| `F76`, `I76` | roof Case 2, 0–0.5h: `Cp,e=-0.40`; `-1.135894 kN/m²` | `+0.15`; `+0.425960 kN/m²` | opposite sign; decisive evidence against claiming workbook numeric parity |

These workbook numbers are **comparison data**, not authorization to use legacy entries as normative 2021 coefficients. Report discrepancies truthfully with separate columns: workbook cached value, explicit-test / authorized-2021 result, signed delta, methodological reason, reviewer disposition.

---

## 1. P0A — Replace two-column generic report with actual Excel layout

Use the **actual cell locations, styles, merges and print area** in `Flat roof!A1:N124` as the authoring authority for visual layout. Do not simply print Excel row indexes. Do not use an ASCII/CSV pipe-separated compressed table as a visual substitute. Use structured HTML/CSS grid or exact worksheet-cell projection, retaining offline font assets and A4 printable pagination; reuse safe HTML/CSS architecture only, not anchor-design calculations.

The required visual contract:

1. **Header/title and rows** — recreate `Design code & reference`, `1. Basic parameters`, `2. Wind load calculation`, Wind X, Wind Y, Windward, Leeward, Sideward, Roof Case 1 and Case 2 at the same relative positions, dark-blue bold section titles and blue numerical inputs/results. Keep the worksheet's original row order, blank spacers and separate ULS/SLS flags. Internal IDs `Flat-5` ... `Flat-124` may remain as DOM metadata, but **do not display `5.`, `6.`, ...** in front of labels.
2. **Basic parameters** — labels on left (worksheet column A), equations/symbols right-of-center (column J), numeric result alignment around J/K, units and source hints separately in K/L, ULS/SLS in M. Do not append `R=500 years; ...` as a single arbitrary sentence where the workbook has E/F versus J/K/L. Input, measured-model and calculated quantities must have distinct provenance that can be read without an appendix.
3. **Four-column zone tables** — reproduce the structure of the workbook rows `55–60`, `65–71`, `74–80`, `99–104`, `109–115`, `118–124`: row label `A:C`, `From` in `D`, `To` in `E`, `Cp,e` in `F` (with relevant merged F:H header), **`W (kN/m²)` in `I`** (merged I:K header). Keep multi-level headers and individual table borders. No `From–To | Cp,e | pressure` string in one value cell.
4. **Mathematical typography** — proper `α`, `θ=0°, 180° / 90°, 270°`, `ρ_air`, `V_sit,β`, `V_des,θ`, `M_d`, `M_c`, `M_s`, `M_t`, `M_z,cat`, `C_p,e`, `C_p,i`, `C_shp`, `C_dyn`, subscripts/superscripts and superscript `²`/`³`, with correct sign and units. The current `θ = 00, 1800` is not acceptable. If retaining workbook's legacy `C_fig` visual term, annotate that 2021 uses `C_shp` without misrepresenting the normative text.
5. **Repeated blocks** — X and Y rows must have independent calculation identities; where X+ and X−, or Y+ and Y−, legitimately differ, report the direction used in the primary Excel-like sheet and provide individually reviewable alternatives in additional **worksheet-formatted** PDF pages rather than masking them with a pair heading. Show sector-defining `β` and selected `M_d`/`V_des` near the section.
6. **Readability** — no clipped symbols, broken superscripts, content spilling into adjacent cells, empty extra pages, huge item-ID strings in narrow cells, or tiny 5 pt ledger substituting for the primary workbook. Match the owner's workbook column widths, alignment, fill, border, and intended print area where feasible. Use page-break controls around engineering sections and repeat zone-table headers when necessary; no enormous empty page tails.
7. **Separate audit depth** — the **main report PDF** must be engineer-readable in workbook order (for the 124-row example, target a concise few A4 pages, not 54 pages). Preserve **all** matrix cases and full-precision trace nodes in a SECOND opt-in `..._Detailed_Audit.pdf` (also PDF only), linked in viewer via `Open detailed calculation PDF`. Never discard trace data to shorten the main report, and do not make a raw JSON/CSV screen the results UX. Include source/run/PDF hash and status in both reports.
8. **Blocked diagnostic** — show the same workbook layout with `NOT CALCULATED` or `INPUT REQUIRED`, attributable missing factor IDs and no invented pressures. Distinguish `Blocked report generated` from `Successful calculation`. Avoid printing `NOT CALCULATED kN/m²` as though the missing value were numeric; separate missing status from unit column.
9. **Copyright** — reproduce only the **owner's own workbook presentation**, not scans/full protected tables from the standard. Standard sources are cited by clause/table, not pasted wholesale.

The workbook screenshots show a **`Flat roof` print range with caption `Monopitch & flat roof`, pitch 14°**. This is a **layout identity**, NOT proof that the *normative* flat/near-flat roof method is applicable at that pitch. The `Slope Roof!A1:N103` is a separate physical/method profile (e.g., its U/D/R rows) and shall not be mislabeled as complete by this sprint. Do not claim full gable/hip support unless separately implemented and tested.

### Report regression acceptance

- Build a stable cell-to-field/geometry registry for each meaningful cell of `Flat roof!A1:N124` with cell position, row/group, original label, formatting class, trace source, value state and `ULS/SLS` label. Test all populated/merged fields used by the screenshot; do **not** accept merely `id='Flat-5' ... Flat-124` as visual or numerical completion.
- Snapshot HTML grid coordinates/semantic cells, inspect rendered A4 PDFs at legible resolution, compare against workbook styling and all five owner screenshots **if agent has local screenshots**; otherwise explicitly report screenshot comparison unavailable and perform direct workbook layout comparison.
- Verify text extraction, every applicable line, table heading, correct mathematical glyph, independent ULS/SLS, no overflow/missing page, no false `REPORT VERIFIED` engineering badge.

---

## 2. P0B — Numerical truthfulness, coefficient applicability and directional branches

Preserve the useful signed case matrix but strengthen its engineering semantics.

**2.1** In `AsNz2021WorkbookPdfRenderer.Value()` rows 34/35, do not call a velocity-pressure basis `p_uls` multiplied by `C_shp` while calculating only `q = 0.5 rho V² Cdyn`. Keep a clearly labeled `q`/pressure basis line (including correct `V_sit` vs `V_des` provenance) and separately calculate **external**, **internal** and **net** pressures with their actual coefficients at each surface. If necessary, show an explicit engineering note directly adjacent to the workbook-copied formula rather than quietly changing the math to force Excel equality.

**2.2** The real 2021 source controls method applicability: review Section 2 (site/design wind speed and pressures), Section 3 (`V_R`, `M_d`, `M_c`), Section 4 (`M_z,cat`, `M_s`, `M_t`), Section 5 (internal/external shape and Tables 5.2(A–C), 5.3(A–C), factors), and relevant Section 6 (`C_dyn`) per the prior directive. **Do not copy standards tables into public source, fixtures, report or CI.** Provide private/authorized lookup contracts and explicit conditional method decisions. If no rights-qualified private table data exists, use `LOOKUP_UNAVAILABLE / SOURCE RIGHTS REQUIRED`, continue only an explicit-input owner-review path and leave normative qualification false.

**2.3** Implement and test rule selection independent of a source table's numerical content: region/return period and amendment identity; jurisdiction-aware AU/NZ wind direction `M_d`; eight cardinal site speeds to each of four orthogonal design speeds and ULS floor; height/terrain interpolation with validated provided operands and explicit A0/terrain exceptions where governed; wall ratio `d/b`; sidewall zone boundaries; roof method selection from **actual roof type, slope, wind-relative bearing, h/d and case applicability**; and internal-pressure signs and combination factors. Never select the roof method merely by worksheet tab name.

**2.4** For an authorized engineer-supplied coefficient selection, the case record must state: `SourceClass`, owner/lookup source document edition and amendment, exact clause/table and row/condition selector, input operands, interpolation bounds, selected coefficient, `C_shp,e`/`C_shp,i` chain, `q` basis, `p_e`, `p_i`, `p_net`, and use of `C_dyn`. Assert that a factor used in calculations has a corresponding trace dependency. Avoid returning `CALCULATED` for a zone when its declared applicability is unsupported/unknown; distinguish `EXPLICIT REVIEW ARITHMETIC` from `QUALIFIED NORMATIVE RESULT`.

**2.5** Do not collapse all four directions to identical coefficients or speeds. Tests must include nonuniform eight-direction `M_d`, unequal +X/−X and +Y/−Y results, rotation relative to true North, and explicit leeward `d/b` and roof `h/d` at boundary thresholds. Address physical segment clipping and zero-length zones with status, never display a real zero-length zone as valid loaded area.

**2.6** Verify geometry semantics and overrides: X/Y footprint dimensions; ground datum (natural local ground rather than arbitrary highest model Z); roof maximum height; average roof height; actual roof plane/roof run; pitch and slope bearing; true North transformation; and explicit override reason/source. For the workbook demo, `h_peak=3.8m`, `h_avg=3.5m`, `alpha=14°`, footprint `11.44 x 17.8m` must be printed as the supplied example, **not asserted to be a geometrically proven full-span 14° roof**. Verify roof run separately when connecting a physical model; report ambiguity as `MODEL_GEOMETRY_REVIEW_REQUIRED`.

**2.7** The new matrix is currently behind `RequireDemo()` and loaded from a request JSON. That path does **not** extract live CSI geometry. For this bounded sprint, deliver a truthful, usable read-only adapter to existing CSI scan evidence *where available and verifiable* (unit-normalization, selected envelope X/Y, roof pitch and datum provenance), plus an engineer override form. If CSI cannot infer a valid roof plane, block **automatic geometry qualification** and permit manual input for the review-only calculation. Never fabricate roof planes from story elevations. Clearly list any real ETABS/SAP2000 validation still outstanding.

### Numeric regression and 3-way reconciliation

Read the workbook through a static, macro-disabled reader; compare **cached values separately from formulas**. Add a genuine comparison matrix at `artifacts/4b6b/workbook-vs-engine.csv` (git-ignored): workbook sheet/cell, source value, test/authorized engine result, delta, percent difference if meaningful, classification (`EXPECTED_LEGACY_DIFFERENCE`, `SOURCE_UNVERIFIED`, `ENGINE_BUG`, `WORKBOOK_FORMULA_DISCREPANCY`, `MATCH`) and reviewer action.

At minimum cover the 4B.6A discrepancies in section 0.1; basic velocity calculation (`68.796 m/s` and `45.57735 m/s` when supplied the corresponding benchmark input factors), ULS/SLS velocity pressure, windward/leeward walls, all four sidewall bands, all five roof zones in each Case 1/2 for both axes, and both pressure signs when used. Test 0°, 9.9°, 10°, 14°, 25°, `h/d` near 0.25/0.5/1.0, `d/b` near relevant limits, terrain interpolation endpoints, unprovided `Mc`, nonfinite and unsupported inputs. **Do not assert that workbook-derived coefficients are current-standard truth**; where the workbook is inconsistent with an authorized interpretation, explain the difference and retain engineering review status.

At least two distinct fixtures:

1. **Workbook arithmetic parity fixture:** reads or explicitly sets workbook coefficients, proves worksheet equations/cached numbers reproduce for comparison. Must show `LEGACY WORKBOOK COMPARISON / NOT A QUALIFIED STANDARDS LOOKUP`; not a production default.
2. **2021 explicit engineer-review fixture:** uses independently selected, evidenced factor inputs. Must show 2021 method designation, provenance and any absent qualification; never mark approved without actual evidence. Use intentionally varied direction/zone coefficients to ensure zone identity matters.

---

## 3. P0C — WPF/PDF state and rendering safety

1. Fix `MainViewModel.CalculateEngineeringRunAsync()` so a blocked calculation may produce and show an **available diagnostic PDF** but UI state remains `Blocked` (or explicit `DiagnosticReady` with blocking badge). `Ready` requires a numerically computed result with complete required evidence for the declared **review-only** path; it never means normative-licensed or engineer-approved by itself.
2. On new request, run ID change, model geometry revision, input edit, cancellation, mode switch or concurrent calculation, invalidate old report/run/export eligibility. Link the request-version cancellation source to the renderer process, not only a post-render stale-publication check. Include tests for cancellation DURING Edge rendering and restart; no orphan Edge processes or temporary files.
3. Export must use the latest immutable calculated/diagnostic report identity and verify current project, request hash, geometry fingerprint, run hash and report hash. Export in the background with user cancellation, ensure atomic PDF replacement. The legacy generic summary-PDF fallback is prohibited for AS/NZS worksheet mode; when Chromium is unavailable, return a clear export blocker or a fully worksheet-compatible offline renderer, never a generic report.
4. The main `Wind` results area must be an embedded PDF viewer only (page navigation, zoom, print, save). A separate **PDF** link/button must open `Detailed_Audit.pdf`. Input/override controls may appear in a separate input area. Do not leak raw unformatted calculation tables as the primary result UI.
5. No live CSI writes or new approval/assignment pathways. Preserve `CanCalculateNormativePressures == false` and `CanMutateCsi == false` unless a separate, explicit licensed standards + independent engineering qualification/release directive is issued. Source hashes, run hashes and PDF byte hashes are **integrity markers**, not a professional engineering certification.

---

## 4. Engineering and UI testing before commit

- Add **failing tests first** proving each P0 defect above, especially `Blocked -> Ready`, mismatch of visible row geometry, inaccurate `C_shp` formula label, fake equality of X/Y when factors differ, missing coefficient provenance, stale PDF export and misleading renderer fallback.
- Verify the core AS/NZS review engine with independently computed SI arithmetic (not tests that simply call the same engine to obtain expected values). Cross-check the workbook cached arithmetic and true 2021 method decisions separately.
- Render at least these local examples, fully inspect **all pages** and record image comparisons: `ASNZ_2021_FlatWorkbook_Review_MAIN.pdf`; `ASNZ_2021_FlatWorkbook_Review_Detailed_Audit.pdf`; `ASNZ_2021_Blocked_Diagnostic_MAIN.pdf`; plus the 4B.6A originals for visual delta. Run HTML/PDF extraction assertions for every meaningful source cell and all engineering case IDs.
- Ensure main workbook PDF preserves compact recognizable Excel styling, row/cell identity and ULS/SLS results. Detailed audit PDF must retain 100% of trace nodes with correct source classes and no blank/clipped pages. Distinct direction branches, signed net pressures, and `NO CSI WRITES` must be obvious.
- Run locked restore, Debug and Release builds, all automated nonphysical tests, **all eight existing WPF smoke scenarios**, and add at least an AS/NZS blocked-diagnostic state smoke plus PDF main-vs-audit visual/content checks. Include `asnz-worksheet` and stale-result/cancellation tests. Do not call an Edge skip a passed end-to-end PDF test.
- When hardware is available, use a real ETABS/SAP2000 model only in **read-only** mode. If unavailable, report `PHYSICAL CSI VERIFICATION PENDING` and never claim real-model pass from fake CSI tests.
- Verify no source PDF, workbook, protected normative table data, private rights evidence, personally identifying engineer registration, or owner images have been accidentally staged by Git. Keep rights manifests and benchmark references under existing git-ignored private paths.
- Commit/push **only after** verification to `origin/main` (unless owner branch-protection policy disallows), and report exact SHA/CI/test counts. If any P0 acceptance is not met, use `PARTIAL / OPEN` rather than `COMPLETE`.

## 5. Required completion report

1. Branch, commit SHA, CI URL; locked restore, Debug/Release, test counts, each WPF smoke result.
2. Exact implemented/blocked capabilities, with named source files and method scopes. Distinguish: explicit-input review arithmetic; rights-qualified normative coefficient selection; actual CSI geometry extraction; screenshot parity. Do NOT group these under an ambiguous `completed` heading.
3. Full local filesystem paths to all generated main/audit/blocked PDF examples plus representative rendered page images; if CI artifacts are available, provide artifact URLs.
4. Numerically explicit comparison of every sample pressure group, with workbook cell, test source, delta, explanation, and open reviewer action.
5. Signed `pe`, `pi`, `pnet`, directional speed and region/terrain method provenance; audit trail count and completeness checks.
6. Source rights status, jurisdiction, adopted amendments, unresolved engineer method decisions and independent sign-off status. If source rights remain unclear: **`EXPLICIT-INPUT REVIEW MODE ONLY / NORMATIVE LOOKUP BLOCKED`**.
7. Proof that blocked reports cannot be presented/exported as design-ready, and all `CanMutateCsi` / `CanCalculateNormativePressures` safeguards remain false.
8. A candid unresolved-limitation list (automatic CSI geometry, 2021 table qualification, physical building-specific coefficient selection, gable/hip `Slope Roof`, etc.).

**Owner acceptance:** The project owner must be able to open the compact PDF and see the same X/Y sections, rows, symbols, value alignment and separately bordered zone tables as the Excel screenshots, then inspect a dedicated detailed PDF for every case and formula. A two-column 54-page dump with uniform synthetic coefficients and a green CI is **not accepted**. Never force the output to equal questionable workbook values at the cost of AS/NZS 1170.2:2021 correctness.

**Do the code implementation now. Stop after presenting the verified outputs and blockers; do not broaden into unrelated ASCE, live model writes or automatic engineering approval.**
