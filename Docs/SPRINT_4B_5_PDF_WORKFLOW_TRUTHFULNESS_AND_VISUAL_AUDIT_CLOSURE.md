# Sprint 4B.5 - PDF Workflow Truthfulness, Background UX, and Visual Audit Closure

**Project:** Structural Load Assistant / STR-AutoWind  
**Repo:** `lammach99-oss/STR-AutoWind`  
**Local Windows repository root:** `D:\Project\Etab-Sap Tool\AutoWind`  
**Put this instruction file at:** `D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_5_PDF_WORKFLOW_TRUTHFULNESS_AND_VISUAL_AUDIT_CLOSURE.md`  
**Verified base commit:** `e0dfa5235f98715bfe1cd64b8ff7f90145a2ad6a` (re-fetch latest `origin/main` before work)  
**Verified CI:** `https://github.com/lammach99-oss/STR-AutoWind/actions/runs/40163392264` (396 passed; 7 WPF smoke scenarios)  
**Independently inspected sample:** `smoke-report.pdf`, 242,984 bytes, four A4 pages, Chromium-generated, synthetic ASCE demo. This attachment may not exist on the coding agent's machine: retrieve the actual generated `artifacts/wpf/.../report-export/smoke-report.pdf` or regenerate locally. Do not assume the ChatGPT attachment is on disk.

## 0. Binding owner requirements and current audit decision

Owner wants: complete wind calculations in background, a **PDF-only wind results UX**, and the PDF's calculation presentation to follow their **new simplified owner Excel workbooks**. The PDF must show the actual formula path, substitutions, sources, load cases and values so the owner/engineer can independently hand-check them.

**Current independent QA:** The new VB-inspired HTML/CSS/SVG architecture is real and CI passes, but it is **NOT yet acceptable as the finished owner workflow**. Close the precise defects below. Keep the work narrowly bounded to reporting, presentation truthfulness, background UX, and obvious identity/integrity issues. Do not falsely claim current-standard normative qualification.

Owner workbook references (layout only; read without VBA execution):

- `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\ASCE_Wind load.xlsm`, SHA-256 `04ce9b96eee4f917e66fa7d7be496b02d4d7139698bfba82875b37e4a2c90c29`, relevant sheet `Windload (Imperial)`.
- `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\AS.NZS_Wind load.xlsm`, SHA-256 `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`, relevant sheets `Slope Roof` and `Flat roof`.
- `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\AnchorDesignReportBuilder.vb` is a design-pattern reference, NOT an engineering-data source. Stage only under ignored/private paths if needed, and never commit the original to public GitHub.

**Evidence gap:** New C# comments still say `Hawaii Housing / ASCE` instead of identifying the new simplified workbook. Do not assert visual/layout parity unless the exact new input file hashes and a side-by-side comparison are recorded. If references are missing or hashes differ, report `TEMPLATE PARITY NOT VERIFIED` rather than silently using old copies.

## 1. P0 - Truly asynchronous calculation and PDF rendering

Current source evidence:
- `src/StructuralLoadAssistant.App.Wpf/MainWindow.cs` invokes `CalculateEngineeringRun()` via a synchronous button callback.
- `src/StructuralLoadAssistant.App.Wpf/MainViewModel.cs` in `CalculateEngineeringRun()` synchronously executes the strategy, builds Markdown, creates PDF document and calls `RenderDocument(...)`.
- `WindPdfRenderService.RenderHtmlToPdf(...)` uses `Process.WaitForExit(timeoutSeconds * 1000)` (30-second default) synchronously.

Required repair:
1. Create an explicit calculation/report state model (`Idle`, `Validating`, `Calculating`, `Rendering`, `Ready`, `Blocked`, `Failed`, `Cancelled`) and bind progress/errors in WPF. Do not show raw calculation traces as the owner-facing result view.
2. Use `async` + `await` with CPU work off the dispatcher and cancellable process execution for Edge; do not invoke WebView2 controls outside their dispatcher. Ensure WPF remains responsive when the renderer is slow, missing or fails.
3. Capture immutable request/model snapshot and a monotonically increasing request-generation token. Any new input, project, standard, direction, mode, geometry or run selection invalidates older work. Old async results MUST NOT overwrite a newer PDF.
4. Add cancellation, bounded timeout, proper child process-tree cleanup and useful error messages. No blocking `.Wait()`, `.Result`, `.GetAwaiter().GetResult()` on UI path.
5. Design a usable engineer-review wind input configuration workflow to create/validate `EngineeringCalculationRequest` without inserting a C# hardcoded smoke request; keep unsupported normative coefficient lookups blocked. Preserve the existing read-only real-model safety boundary.

Acceptance: a WPF UI responsiveness and rapid-request-switch integration test with a deliberately delayed renderer; cancelling run A and starting run B must display only B, without stale buttons or files.

## 2. P0 - No silent render failures, stale PDFs or false verification

Current source evidence:
- `MainViewModel.SetEngineeringRun` and `CalculateEngineeringRun` use `catch { }` around PDF construction/rendering.
- `CurrentPdfPath` and `CurrentPdfDocument` are not consistently cleared by mode/standard/project/wind-state changes.
- `MainWindow.UpdatePdfViewer()` only loads on a matching `CurrentEngineeringRun` but does not clear a previously displayed PDF when run becomes null; `WindPdfViewer` has no explicit `Clear()` operation.
- `WindPdfViewer.LoadPdf()` labels an arbitrary supplied run hash `Verified PDF Loaded` WITHOUT verifying either PDF contents or file SHA-256. Hash comparison alone is not engineer qualification.
- `ExportDetailedReport()` silently falls back to a summary PDF if Edge is absent, while still saying `detailed report`.
- The renderer checks `%PDF` header but does not require zero process exit code, a parseable page count or the expected run/document identity in PDF content.

Required repair:
1. Build report bytes/file atomically in a temporary directory, validate file length, parser readability, expected trace content, run identity and process status, then publish to the viewer only after success. Keep detailed-report fallback behavior explicit: if the detailed renderer is unavailable, return an error; do NOT emit a summary pretending to be complete.
2. Clear and dispose the viewer's source on invalidation/cancellation/failure/standard+project switch/return to Demo, and clear `CurrentPdfPath`/`CurrentPdfDocument` together. Save, print and zoom are unavailable while the PDF is invalid/absent.
3. Replace `Verified PDF Loaded` with `PDF ready - calculation run: <hash prefix> - engineer review status ...`. If a PDF file hash is shown, actually calculate SHA-256 of the PDF bytes. Treat run hash, report document hash and file hash as separate identities.
4. Ensure `MainViewModel.ExportDetailedReport()` always exports the **same approved detailed PDF document** currently previewed, or clearly regenerates the same run with checked identity. Refuse export of a stale/blocked run. Do not reconstruct a summary-only artifact.
5. Test missing Edge/WebView2, render crash/timeout/nonzero exit, PDF corruption and navigation failure; errors must remain visible and must not leave prior run report falsely active.
6. In `WindPdfViewer`, provide true page navigation (or explicitly rely on and verify built-in WebView2 PDF page controls), plus existing zoom/print/save. Check WebView2 initialization/navigation errors; do not declare viewer-ready merely because file exists.

Acceptance: explicit mode-switch, wind-state switch, old-render-completion-after-new-run, bad-PDF and renderer-unavailable tests. No silent success, fake hash verification or stale report.

## 3. P0 - No silent truncation of structural load rows

Current source evidence: `WindWorkbookHtmlReportBuilder.AppendModelIntegrationSection()` loops through `doc.FacadeRows.Take(12)` and `doc.WallRows.Take(6)` with no disclosure. This can silently omit real member results.

Required repair: render all eligible member/wall rows, page across tables as needed, repeat column headings and show total reported count. If data is unavailable, label `NOT PROVIDED`. Never turn a 13th facade or 7th wall into invisible omitted data. Add 40+ facade and 20+ wall regression fixtures; count exact rows in generated HTML AND extracted PDF, including final rows. Avoid duplicate rows after continuation.

Also ensure facade/wall gravity **demo fixture rows are not presented as calculated by the selected wind run**: distinguish their sources, source snapshot/hash, directions and calculation independence. If the selected model/run cannot be reconciled, either omit them with an explicit boundary or block this section.

## 4. P0 - Truthful numerical/standards presentation

Defects visible in supplied `smoke-report.pdf` and C#:

A. All five surface rows show `Alt: 0.00 lb/ft2` although the alternate limit state has not been computed. `SurfacePressureResult.AlternateLimitStateNetPressurePa` defaults to zero, which currently conflates **missing** with a true zero.

B. ASCE page 3 shows e.g. `qh = 17.41 lb/ft2` while the adjacent substituted equation still uses SI `0.613 ... (40.00)^2` and results such as `833.68`, `150.06`. Only the result value was converted. This is not a consistently Imperial hand-check chain.

C. `WindWorkbookHtmlReportBuilder.AppendAsceImperialSections()` writes `Standard Supplements | Applied` even when the manifest says applicability review pending. It also prints hardcoded assumed notes such as enclosed building, open terrain, relevant tables, and missing-coefficient fallback values (`?? 0.85`, `?? 1.0`, etc.).

D. Wind SVG elevation uses a fixed gable polygon regardless of roof type or measured eave/ridge/slope. Wind direction arrow in the plan is only an illustrative icon and must be labeled so it cannot imply validated pressure/normal orientation. The owner's AS/NZS flat-roof report must show a flat roof, and wind +X/-X/+Y/-Y diagrams must faithfully match the selected case.

E. `ReportUnitProfile.ConvertTraceResult` only converts `Pa`, `m/s`, `N`, leaving other dimensional trace outputs in SI (e.g. N/m, m, velocity-pressure factors), while the report claims standard-specific units.

Required repair:
1. Make `NotComputed`, `NotProvided`, `NotApplicable`, `CalculatedZero` and `CalculatedNonzero` distinct typed states in the report projection. Do not imply 0 when uncomputed. If the engine cannot distinguish alternate ULS/SLS status, enrich its contract compatibly rather than guessing.
2. For every ASCE reported calculation equation, show either a **fully Imperial numerical substitution using consistent coefficients/units** or a visibly labeled `SI internal calculation (for audit)` with *both* SI and converted output, including documented conversion. Never silently combine imperial result with SI substitution as if it were an imperial equation. Preserve canonical SI engine values.
3. AS/NZS uses m, m/s, kN, kN/m, kN/m2 consistently; ASCE uses ft, mph, lb-force (label `lb` per owner), lb/ft, lb/ft2. Test every report section including SVG annotations and every trace output unit. Avoid labelling pound-mass as force.
4. Report standard edition, amendments/supplements, coefficients, source-rights and review statuses FROM ACTUAL RUN/MANIFEST EVIDENCE. Do not state `Applied` from hardcoded text when applicability is pending. Missing sources must never be filled with silent default numerical assumptions. Display source provenance for each coefficient and mark unreviewed sources.
5. Base SVG roof form/dimensions/direction on actual typed request geometry or explicitly mark `SCHEMATIC - NOT TO SCALE`; roof form must not contradict labels. Do not claim automatically qualified zone boundaries without qualified geometry. Add tests for flat/gable and +X/-X/+Y/-Y.
6. Surface net results must expose correct internal-pressure sign, governing case, relevant limit state and calculated/not-calculated distinction. Do not reproduce a generic algebraic formula as if it reflected special actual pressure treatment when the run trace differs.
7. Only display source clause/table references supported by documented procedure and qualified provenance; schematic headings can remain, but invented normative applicability cannot.

Acceptance: machine-readable report-projection parity tests between typed run and visible PDF for every numerical pressure, coefficient, line load, case/limit state and status. At least one ASCE imperial hand-check chain and two AS/NZS metric chains shall be independently recomputed from explicit test input without relying solely on the engine under test.

## 5. P1 - Report identity and immutable audit correctness

Current source evidence:
- `WindPdfReportDocument.ContentHash` simply returns `CalculationRun.ContentHash` when a run exists, even if the report project name, model geometry, facade/wall rows, revision or presentation options change. This is a RUN hash, not a DOCUMENT hash.
- `ReportIdentityGuard.ThrowIfMismatch` checks project ID and standard **family** but does not prove exact amendment/procedure/run-to-model provenance for joined geometry or auxiliary load data.

Required repair:
1. Keep separate `RunContentHash` and a canonical versioned `ReportDocumentHash` (include engineering-affecting report content/snapshot identity and presentation basis, exclude rendering timestamps); optionally a separate byte-level `PdfFileSha256` outside the PDF to avoid circular self-hash issues.
2. A changed facade row, geometry fingerprint, revision, standard amendment, run, source snapshot or substantive report data must change `ReportDocumentHash` or cause explicit provenance blocking. Deterministic re-generation of the same stable data should retain the document hash.
3. Do not attest that a report is independently verified based on valid hashes alone. Print actual owner/independent review statuses separately.
4. Document what `ReportIdentityGuard` does NOT establish; require stronger checks for joining independent facade/wall/demo rows to a wind run.

Acceptance: deliberate mutation tests for wall rows, geometry, project display name, standard amendments, revision, opposite wind direction and mismatched run; prove report identity is not reused incorrectly.

## 6. P1 - PDF page geometry and exact owner layout parity

Observed 4-page synthetic PDF:
- Page 1 carries cover, diagrams, references and initial velocity pressure table.
- Page 2 contains the surface and integration tables, then ends roughly midway down the page; a forced appendix page break wastes most of this page.
- Page 3 contains part of the 20-node trace, page 4 contains the last few nodes and audit block, leaving large white space.
- Header and identifying footer are not repeated consistently on the middle pages.

Required repair:
1. Follow the THREE exact new workbook layouts, not the legacy Hawaii workbook by name alone. Compare local workbook section hierarchy, merged headers, column proportions and table ordering read-only; record verified hashes and side-by-side screenshots. Distinguish ASCE and AS/NZS flat/slope.
2. Rework manual `.page-break` around appendix so the layout uses available space without orphaning table titles/headers; maintain readable rows and repeated column headers across page breaks. Repeat project/run identity and `Page N of M` footer for every printed A4 page.
3. Preserve selectable text and embedded fonts/symbols. Avoid huge empty regions, clipped rows, overflow, tiny fonts and bad column breaks. Use a renderer-backed visual regression loop, not only static HTML assertions.
4. Do not pad missing calculations with visually plausible fake coefficients just to match workbook appearance.

Acceptance: source-generated PDF samples for ASCE Imperial, AS/NZS Slope Roof and AS/NZS Flat Roof, including long-table and 40+ load row cases. Provide first/middle/last rendered page screenshots and check actual PDF pages for missing/duplicate trace IDs and data rows. The owner must be able to examine the actual PDFs.

## 7. P1 - End-to-end PDF-only UX across tabs

`MainWindow.ReportPage()` still exposes a raw Markdown `ReportText` preview and a button labelled `Export summary report (PDF)`, while the Wind tab shows the new PDF. That is a contradictory user-facing reporting workflow.

Required repair: make PDF the singular owner-facing wind result/report surface; rename legacy summary-only action or hide it behind an explicitly separate diagnostic/legacy view if necessary for compatibility. Ensure the Report tab and Wind tab refer to the same selected run/report and cannot display contradictory/stale artifacts. Input editing and separate CSI geometry editing are allowed to remain non-PDF, but computed wind results must not be shown in a parallel Markdown ledger.

## 8. Tests, evidence and release gate

Write failing regression tests before code changes. Re-run and record:

- `dotnet restore --locked-mode` for the solution.
- Debug AND Release deterministic builds with 0 warnings and 0 errors.
- All automated tests, including all existing tests and new regressions, 0 skipped except intentionally opt-in physical CSI tests excluded by documented filter.
- Seven WPF smoke scenarios: `demo`, `report-export`, `readonly`, `roof-initial`, `roof-reopened`, `zone-initial`, `zone-reopened`.
- A real desktop interaction that sets inputs, invokes calculation asynchronously and views a PDF. Check navigation, save and print behavior separately where automation is feasible. A file `Exists` check alone is not a viewer test.
- PDF structure/text extraction tests for **100% trace nodes** and **100% reported facade/wall rows**, including long pagination; ensure there are no invented alternate zeros and no false approval phrases.
- PDF render failure/missing Edge tests (no silent summary fallback), and multiple rapid calculation/invalidation tests.
- 3 fully rendered PDF samples with actual page screenshots (ASCE Imperial, AS/NZS Slope, AS/NZS Flat), image-to-reference layout review when references are available.
- Explicit source-hash verification for both owner workbooks and VB source; if not accessible to the agent, record a blocker and do NOT claim exact visual parity.

**Safety invariants:** `CanCalculateNormativePressures == false` until separately qualified; `CanMutateCsi == false`; no live ETABS or SAP2000 writes; no invented licensed standards numerical coefficients, source-rights, approvals, independent reviewer signatures or external review records. Engineer-review calculations using explicit, labelled unverified/synthetic inputs remain allowed only with accurate disclaimers. ASCE 7-16/Hawaii and historic AS/NZS workbook values may be used as legacy comparison **only**, never silently elevated to current-edition normative authority.

**Deliver at end:** exact pushed SHA, CI URL and pass counts, a concise fixed/remaining issue table, screenshots and PDFs for each standard/roof profile, proof of actual background responsiveness, PDF extracted source-node/row parity, the three PDF document/run/file hash distinctions, verified workbook SHA-256 or precise missing-reference limitations. Commit and push verified code to `main` only after tests. If a safety-critical acceptance fails, do not declare success; report the blocker and stop.
