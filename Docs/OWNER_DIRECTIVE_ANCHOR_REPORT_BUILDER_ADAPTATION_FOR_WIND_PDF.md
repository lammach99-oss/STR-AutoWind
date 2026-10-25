# Owner Directive: Adapt AnchorDesignReportBuilder VB Architecture to Wind PDF Report Builder

**Project:** Structural Load Assistant / STR-AutoWind
**Repository:** `lammach99-oss/STR-AutoWind`
**Windows repository root:** `D:\Project\Etab-Sap Tool\AutoWind`
**Instruction destination:** `D:\Project\Etab-Sap Tool\AutoWind\Docs\OWNER_DIRECTIVE_ANCHOR_REPORT_BUILDER_ADAPTATION_FOR_WIND_PDF.md`
**Audited main baseline:** `fa4435e7d2d7012a8959c04a2a8c0b3043f8fa3e` (GitHub Actions run `40114426620`, 387 nonphysical tests passed). Re-fetch `origin/main` first and incorporate any later changes.
**Priority:** The owner's latest directive takes precedence over old summary-only-PDF and trace-only-in-UX decisions; preserve existing engineering gates and applicable safety invariants.

## 1. The binding owner decision

The owner supplies a VB.NET source file, `AnchorDesignReportBuilder.vb`, from another application written by the owner. **Study it and apply its reusable report-design and rendering architecture to Structural Load Assistant.** Do not merely paraphrase it or present a mockup; implement and test a working C#/.NET 8 wind-report pipeline.

This directive **extends and complements**:
`Docs/OWNER_DIRECTIVE_PDF_ONLY_BACKGROUND_WIND_CALCULATION_AND_WORKBOOK_FORMAT.md`.
If that earlier document conflicts with this one on internal implementation, use the latest instructions here; if it describes presentation content/layout or engineering safety more specifically, preserve its requirements.

The calculation engine performs all wind calculations behind the UX. The owner-facing wind **results view contains only the final in-app PDF**, with page/zoom/print/save controls; input configuration and other unrelated project/CSI workflows remain available. The PDF itself contains every required calculation, coefficient provenance, intermediate numerical result and review disclaimer. Do not replace it with raw HTML, markdown, trace grids, or a summary-only document.

## 2. Mandatory exact source and layout references

The owner-provided **VB reporting reference** is an implementation/design example, NOT wind or anchor engineering authority:

- File: `AnchorDesignReportBuilder.vb`
- SHA-256: `e9dd4839a083f0de9ab1b9710328bdff08397dea888fdec2e1d01dd11da2f247`
- Preferred LOCAL PRIVATE staging location: `D:\Project\Etab-Sap Tool\AutoWind\artifacts\report-reference-private\AnchorDesignReportBuilder.vb` (under the already-ignored `artifacts/` directory).
- The owner must copy/download the attached `.vb` to that location if not already accessible to the Windows coding agent. Verify its hash before claiming inspection. Do **not** commit it or copied branding/customer data to the public repository.

The **new simplified owner workbooks** are the authoritative visible layout and information hierarchy, superseding previous same-named workbook versions:

1. `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\ASCE_Wind load.xlsm` -- SHA-256 `04ce9b96eee4f917e66fa7d7be496b02d4d7139698bfba82875b37e4a2c90c29`; use the `Windload (Imperial)` presentation sheet (`A1:P225`).
2. `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\AS.NZS_Wind load.xlsm` -- SHA-256 `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`; use `Slope Roof` (`A1:N103`) and `Flat roof` (`A1:N124`).

Read the workbooks **without executing VBA or external links**. Never commit them or confidential licensed extracts. The existing workbook ASCE 7-16/Hawaii formulas and any illustrative values do not qualify ASCE 7-22 normative data.

If an exact reference is unavailable, state precisely which missing file/hash prevents visual sign-off; proceed with source-independent engineering/UI changes but do not claim verified template parity.

## 3. VB design patterns to adapt (and patterns NOT to copy)

Read the COMPLETE VB source, including these anchored methods/features:

| VB source section | Reusable feature | Adaptation for wind PDF |
|---|---|---|
| `BuildReport` around lines 28-45 | Builds one complete HTML document from a project and result snapshot | Pure C# rendering of one immutable wind report document; no mutable singleton builder state |
| `@page`, `.sheet`, print CSS around lines 49-80 | A4 page, consistent margins, screen/print media | A4 portrait PDF (landscape only if a verified table absolutely requires it); do not hard-clip overflow |
| `.report-header`, `.page-footer` around lines 102-158, `RenderPageFooter` around 1547 | Repeated project identity/date/page footer | Wind-standard edition, project ID, run ID/hash, status, revision and actual `Page N of M` |
| tables and `.calc-box` around 118-174, `RenderCheckBlock` around 912-976 | Reusable 5-column detailed calculation steps; continued tables | Wind quantity, symbol, formula, substituted equation, result/unit, source; repeat headers and keep trace-node identity |
| `StartSheet`, `CloseSheet`, `EnsureSpace` around 447-482 | Multi-page sectioning | Page/section templates and tested print pagination; avoid manual floating-point height guesses as the sole layout authority |
| `RenderPlanDrawing` around 538-678 | Inline SVG diagrams for geometry, axes and force arrows | Wind X/Y plan, building dimensions, roof pitch, facade/roof orientation and pressure direction diagrams, using actual wind-run geometry |
| `RenderHeatmap` around 1330-1418 | SVG color legend and scalar field graphic | Optional wind roof-zone pressure diagram only when the real run provides zone pressure and validated geometry; do not synthesize a heatmap |
| Cover, approval, revision around 1420-1545 | Consistent report identity and signature/revision record | Compact workbook-like heading and optional engineering audit/issue appendix; no mandatory multi-page corporate cover |
| MathJax around 220-233 | Legible equations, symbols and superscripts | Use fully OFFLINE available equation rendering (local assets or safe MathML/HTML/SVG); no CDN or remote fonts |

**Do not port:** ACI 318-25 anchor/baseplate checks, anchor loads/FEA stress fields, TLC brand/logo/customer metadata, anchor-specific units and hardcoded pass/fail, or any old numerical formula not germane to wind calculations.

**Do not repeat VB implementation weaknesses:**
- The VB is a mutable instance holding `_sb`, `_project`, `_currentPage`, `_currentY`: do not share that mutable builder across asynchronous runs or jobs.
- `EnsureSpace` uses approximate millimetre increments; with `overflow:hidden`, oversized blocks could be silently clipped. Use actual browser print pagination for flow content, robust `break-inside`, repeated table headers, controlled explicit section breaks, and rendered-PDF visual tests.
- Its MathJax script imports `https://cdn.jsdelivr.net/...` and Google Fonts. Offline/privacy-first wind reporting must not require a network connection or retrieve external data.
- It interpolates user/project names and values directly into markup. **HTML-encode all text and attributes**, sanitize SVG labels and file paths, reject unsanitized raw HTML, prevent active-content injection and block external navigation/resource requests.
- It sometimes labels internal numerical quantities in US units by changing the string only. Every wind quantity must be converted numerically from canonical SI before presentation.
- A self-computed result is not equivalent to independently engineering-verified or production-approved.

## 4. Target C#/.NET 8 architecture

Implement a coherent production-quality pipeline (names can be adapted to the repository conventions):

```
Typed Wind Input Editor (WPF)
  -> validated EngineeringCalculationRequest
  -> IWindCalculationStrategy.Execute in cancellable background work
  -> immutable EngineeringCalculationRun (canonical SI, full traces)
  -> immutable WindPdfReportDocument / presentation projection
      (project/edition/run hash, all cases, provenances, SI values, output units)
  -> workbook-layout selector (ASCE Imperial / ASNZ Slope / ASNZ Flat)
  -> pure C# HTML+CSS+SVG authoring from escaped structured data
  -> local Windows Chromium/WebView2 print-to-PDF renderer
  -> validated PDF bytes/file bound to the original run ID and hash
  -> WPF embedded PDF viewer, save/print/zoom/navigate
```

Separation of responsibilities:

A. **Engineering:** numerical arithmetic, trace nodes, source applicability and validation. No calculated numeric quantity may be invented by the report builder.

B. **Presentation projection:** convert the full immutable engineering document to typed rows/sections and **standard-specific output units**; retain raw numeric values for exact test comparison, formatted values for display, formula/trace IDs for traceability. Distinguish `Calculated`, `Blocked`, `NotApplicable`, `NotProvided`, and `Unverified` from `0`.

C. **HTML builder:** creates static, self-contained, offline HTML/CSS/SVG. Adapt VB's design language, but follow the owner's Excel section order and readable compact cells as the visual authority. Accept no unescaped HTML inputs. Repeated headers, footers, watermarks and all section/page breaks must be deterministic for a fixed run/configuration.

D. **PDF renderer:** use a robust Windows-supported HTML-to-PDF print path. Prefer Microsoft WebView2's `CoreWebView2.PrintToPdfAsync(...)` or `PrintToPdfStreamAsync(...)` with proper print settings, if viable for the pinned .NET/WPF environment. The public documentation is https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/print . Keep WebView2 operations on their required UI/STA dispatcher; execute CPU calculations and HTML document building away from the UI dispatcher. Do not claim that all WebView2 rendering can execute in an arbitrary thread. Before printing, wait for document/navigation completion, `document.fonts.ready`, loaded local images/SVG and any offline equation typesetting promise; use a testable print-ready signal rather than a hardcoded sleep. Check Windows runtime availability, add vetted pinned NuGet references and update package locks. No silent PDF fallback to raw monospace/ASCII markdown or clipped content. On renderer failure, show a **clear error**, retain no stale successful-looking PDF and do not pretend export worked.

E. **Viewer:** the owner-facing results pane renders the same PDF bytes/file that Print and Save use. Provide zoom, pages, print, save and reliable cleanup of temporary files. Do not use app-external network URLs as viewer sources.

Suggested files or equivalents:
- `src/StructuralLoadAssistant.Reporting/WindPdfReportDocument.cs`
- `src/StructuralLoadAssistant.Reporting/WindWorkbookHtmlReportBuilder.cs`
- `src/StructuralLoadAssistant.Reporting/WindReportLayoutProfiles.cs`
- `src/StructuralLoadAssistant.Reporting/ReportUnitProfile.cs` (extend existing)
- `src/StructuralLoadAssistant.App.Wpf/Services/WindPdfRenderService.cs`
- `src/StructuralLoadAssistant.App.Wpf/Controls/WindPdfViewer.cs`
- `src/StructuralLoadAssistant.App.Wpf/MainViewModel.cs` and `MainWindow.cs`

Do not create a second disconnected wind calculation engine; use the existing immutable runs and correct existing engineering logic first where required.

## 5. Exact visible report content, layout and units

**ASCE layout:** owner workbook `Windload (Imperial)`. Preserve workbook's A. Reference, B. Basic Wind Pressure, Wind X/Wind Y, wall/roof coefficients and design pressure sections, with compact aligned columns, source references, relevant substitutions and final results. All wind pressure/force/line-load outputs use **lb/ft^2, lb, lb/ft**, with **ft** dimensions and **mph** wind speeds. Within this product, explicitly document that `lb` in engineering force tables means pound-force.

**AS/NZS layouts:** owner's `Slope Roof` and `Flat roof` sheets, selected by actual typed roof geometry. Preserve basic parameters, regional/site speeds and multipliers, wind X/Y, ULS/SLS, internal pressure cases, Case 1 / Case 2 roof geometry and pressure tables where supported by the calculation. Use **kN/m^2, kN, kN/m**, with **m** dimensions and **m/s** wind speeds. Do not collapse distinct roof or zone cases into a single number.

Each PDF must show:
- Compact workbook-style top information block: project, item, standard edition/supplement or amendment selection, procedure, calculation date, prepared/review status, revision, run ID and complete content hash accessible in audit appendix.
- Source assumptions and coefficient provenance; no source value silently implied.
- All numerical inputs and key intermediate values, symbolic and substituted formulas, results and units, ordered in the corresponding workbook's hierarchy.
- Wind directions X+, X-, Y+, Y- **only for actually computed cases**, or an explicit `NOT CALCULATED` mark where unavailable. Do not reuse one run's data as if it covered all directions.
- Internal pressure positive/negative where applicable and calculated; properly identified governing envelope and limit state, no duplicated `ULS` columns when active run is SLS.
- Surface/zone IDs, wall/roof resultants, direction/normal, and any line/area loads when computed from known geometry and valid source; no speculative vectors or fabricated zones.
- Optional appendix within the **same PDF** containing every source `CalculationTraceNode.NodeId`, substituted equation and reference, including long sequences that exceed one page. All trace nodes must survive pagination.
- Clear watermarks `SYNTHETIC EXAMPLE - NOT FOR DESIGN`, `ENGINEER REVIEW - NOT INDEPENDENTLY VERIFIED`, `BLOCKED / NOT COMPUTED` as appropriate. A blocked run has no invented numerical design pressures. Owner self-review must never become `IndependentEngineerReviewed` by default.
- Consistent page header/footer with project identity, run hash prefix and page sequence, A4 portrait readable typography, accessible copyable text rather than an image-only PDF.

### Unit conversion boundary

Keep canonical values in SI throughout the engine and immutable run. Convert **for PDF output only** with tested explicit quantity types; convert all displayed numeric values in summary, detail, appendix, diagrams and audit rows. Examples for units conversion, NOT normative wind data:

- `1 N = 0.22480894387096 lb (force)`
- `1 N/m = 0.06852176585679 lb/ft`
- `1 Pa = 0.02088543423315 lb/ft^2`
- `1 N = 0.001 kN`, `1 N/m = 0.001 kN/m`, `1 Pa = 0.001 kN/m^2`

Avoid rendering a computed SI value with only a relabelled unit. Compute at full precision, format only at the last step. Equation symbols must remain legible; support Greek letters, +/- signs and superscripts without lossy ASCII-only fallback.

## 6. Engineering safety and scope protection

The previous source audit found issues in procedure selection, wind direction/vector orientation, workbook legacy coefficient mixing, unqualified AS/NZS roof shortcuts, Excel formula dependencies and trace provenance. If newer `main` has already addressed any of these, **verify the tests and preserve the fixes**. This directive does not authorize skipping those engineering problems merely to make a pretty PDF.

- Do not promote old ASCE 7-16/Hawaii workbook coefficients to verified ASCE 7-22 inputs.
- Do not import coefficients or execute VBA from either workbook as a substitute for an authorized licensed dataset.
- No fabricated source clauses, benchmark hand checks, engineering sign-off or completeness claims.
- `CanCalculateNormativePressures` remains false unless a separately authorized, independently evidenced qualification gate changes; this task does not change it.
- The existing RealReadOnly no-CSI-mutation boundary stays intact; no ETABS/SAP2000 live writes.
- Do not leak VB source, private workbooks, personal/client data, proprietary tables or signed evidence into public commits/CI artifacts.

## 7. Implementation order and required tests

1. **Read/verify references** (VB and both new workbook hashes); read previous owner directive and current `main`; record precise intended layout sections and a short feature map from VB to C#.
2. **Write failing tests first** for current WPF summary-only export, raw trace UX, unit inconsistency, missing equations/page clipping, absent/mismatched project-run identity and new HTML safety requirements.
3. **Build typed immutable wind PDF projection**, with all trace nodes, coefficients, sources, run fingerprint and correct SI-to-output unit conversions. Ensure all `EngineeringCalculationRun` data remains authoritative.
4. **Implement escaped HTML/CSS/SVG workbook-style templates**, adapting only reusable VB ideas, not its ACI calculation content, branding or mutable state.
5. **Build working offline HTML -> PDF engine** with page settings, font/equation assets, result verification and predictable failure handling. Bundle all assets needed for no-internet runtime.
6. **Wire WPF background computation -> PDF-only results view**: real input editor, no blocked UI dispatcher, cancellation/change sequencing, stale-run protection, embedded viewer, page/zoom/print/save. Stop using `BuildSummaryPdf` as the only user export; keep legacy public APIs if old tests or consumers use them, but direct owner UX to the new detailed PDF.
7. **Tests/QA** (must run, not just describe):
   - Three template scenarios: ASCE Imperial, AS/NZS Slope Roof, AS/NZS Flat Roof, with A4 page size and reference-based visually inspected page renders.
   - Unit tests for all force, line-load, area-load, pressure, speed, length, vector, intermediate and ULS/SLS outputs; detect cross-system units and label-only conversions.
   - `PDF extracted trace node IDs` equals all reportable nodes in the corresponding run (no dropped IDs, no duplicate/invented steps); key displayed numbers match immutable computed quantities within presentation-rounding tolerances.
   - Multi-page test with an intentionally long calculation trace (e.g. >150 steps) and long coefficients/source names; no clipped tables, orphan section titles, missing tail content or blank overflow pages; print headers/footers properly aligned.
   - Escaping tests with project/zone/source fields containing `<script>`, `&`, quotes, Unicode, math operators and control characters; no HTML/JS/SVG injection, no external resource network requests.
   - Offline/first-run tests: no internet, MathJax/fonts still display or a deterministic local fallback is used; missing WebView2 runtime yields an accurate diagnostic, not a blank/false-success report.
   - WPF end-to-end smoke: input -> calculation -> full PDF generated -> displayed -> saved -> printed/tested where feasible; file is parseable and opens locally, run ID/hash match; changing input/project/standard invalidates previous PDF even if an earlier task completes later.
   - Negative cases: blocked calculation, missing coefficient, unknown procedure, mismatched report model, cancelled job, failed PDF render and source unavailability all produce honest status, never stale or numerical data presented as verified.
   - CI: locked NuGet restore, Debug + Release builds, all applicable nonphysical tests, WPF smokes including `report-export`. Attach only synthetic sample ASCE/Slope/Flat PDFs and safe rendered page screenshots; private workbook/VB content stays local.

### Definition of done

A successful result is **not** a design screenshot or a generated HTML string. The owner can configure a legitimate engineer-review input, initiate a background calculation, and inspect ONLY a fully rendered, accurate, pageable/zoomable PDF in the result area, with the input/trace/pressure sections arranged like the three owner workbook layouts. The same exact PDF is saved and printed. Every relevant calculation trace survives, displayed units are correct, the PDF respects engineering statuses, and no live CSI writes occur.

## 8. Agent handoff and reporting

Work in bounded, testable changes. If WebView2 renderer prerequisites are missing in CI/local runtime, report the exact limitation and implement the largest independently testable portions without claiming a working PDF viewer. Avoid replacing the requirement with a plain-text report.

When finished, commit and push verified code to `main` **only after successful tests**, and report:

- Exact commit SHA and GitHub Actions URL;
- Main files/classes changed, dependency/lock-file updates and verified runtime prerequisites;
- VB reference hash, both workbook template hashes and resulting three synthetic sample-PDF paths;
- PDF page images (safe synthetic data), extracted text/trace completeness counts, and numerical/unit cross-check results;
- Windows WPF real PDF viewer end-to-end smoke results;
- Remaining owner-licensed standards, procedure and independent review limitations.

**Start implementation. Do not stop after producing another design document.**
