# Sprint 4B.6C — AS/NZS Worksheet Width Parity, Geometry Fail-Closed Checks & Transactional PDF Export

**Repository:** `lammach99-oss/STR-AutoWind`  
**User-local repository:** `D:\Project\Etab-Sap Tool\AutoWind`  
**Starting point:** fetch the latest `origin/main` and verify the baseline is at least `7afb33670fd9c44943de70af659e41392816b110`. Do not assume the branch stayed unchanged.  
**Mode:** bounded corrective sprint; **EXPLICIT-INPUT ENGINEER REVIEW ONLY**.  
**Acceptance at start:** Sprint 4B.6B has successful GitHub CI run `40368030115` (438/438 automated tests; 9/9 WPF smoke modes; Release build 0 warnings/errors). **Engineering standards qualification, live CSI geometry qualification and owner's full visual acceptance remain OPEN.**

## 0. Why this increment is required — independently observed evidence

1. **Workbook widths are not preserved.** `src/StructuralLoadAssistant.Reporting/AsNzWorkbookCellLayout.cs` currently declares nine consecutive `6.140625` widths for columns A:I, repeats `6.5703125` for J/K and `5.5703125` for L/M. The actual ignored source workbook `AS.NZS_Wind load.xlsm`, worksheet `Flat roof`, has the following A:N `ColumnDimension.width` values (confirmed independently via read-only OpenXML inspection):

   | Column(s) | Original Excel width |
   | --- | ---: |
   | A | 6.140625 |
   | B, C, D, E, F, G, H, I | 13.0 each |
   | J | 6.5703125 |
   | K | 13.0 |
   | L | 5.5703125 |
   | M | 13.0 |
   | N | 4.85546875 |

   The workbook's original SHA-256 is `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`. **Do not claim `original widths` until the ratios and actual cell projections match.** The A4 print layout may uniformly scale all widths, but may not silently substitute a different vector. Inspect actual merges too, and prove that the rendered spans correctly reflect source merges or explicitly explain engineered deviations. Do not commit the workbook.

2. **PDF + metadata export is not a transaction.** At the verified SHA, `MainViewModel.ExportWorksheetPdfAsync()` performs `File.Move(staged,destination,true)` first, then awaits hashing the published PDF and writing `destination + ".integrity.json"`. A cancellation, disk failure or request/model change in this interval can leave an exported PDF with an absent, stale or mismatched sidecar. The sidecar also reads mutable `CurrentEngineeringRun` and `CurrentPdfDocument` after the await. Existing stale-request/tamper smoke tests do not cover this commit boundary. Treat this as a concrete source-order defect, not as a proven observed corruption event.

3. **Geometry validity and method support can disagree.** `AsNz2021ReviewCalculation.Execute()`'s `geometryOk` checks roof name, roof pitch range, length/width/reference-height positivity and standard edition, but does not check eave/ridge/ground elevation order. It accepts `RoofForm="Flat"` with any pitch under 90 degrees; `AsNz2021MethodDecision.Evaluate()` marks a nonzero-pitch `Flat` case unsupported (`flat` requires `pitch == 0`). `NumericalComputationPossible` ignores this mismatch and can be true. A demo JSON request can bypass the WPF override validation. Fail closed on **physically impossible** elevations and **unsupported** method decisions without inventing normative cutoffs.

4. **Audit PDF has a stranded identity page.** The uploaded `ASNZ_2021_FlatWorkbook_Review_Detailed_Audit.pdf` has 94 A4 pages; page 94 contains only the `Geometry and identity` block and has a very large unused tail. This is a print-pagination/reading issue; the existing 320 signed cases and trace nodes must not be dropped to make the PDF smaller.

5. The uploaded CSV contains **80 workbook/engine comparisons**: 40/40 `LEGACY_WORKBOOK_COMPARISON` comparisons match within 1e-8, while only 4/40 `EXPLICIT_SYNTHETIC_REVIEW` rows match and 36 differ. These differences are `SOURCE_UNVERIFIED`, **not** newly qualified 2021 coefficients. The primary 3-page worksheet is a legacy comparison review PDF, not a normative calculation certification.

## 1. Non-negotiable safety and scope

- Keep `CanCalculateNormativePressures == false` and `CanMutateCsi == false`; no live ETABS or SAP2000 writes, no production pressure assignment, no engineer approval automation.
- Preserve `StandardsDataIntegrityVerified == false`, `PrimarySourceApplicabilityVerified == false`, `IndependentEngineerReviewed == false`, and `ProductionAuthorized == false` unless a separately licensed and independently reviewed qualification route is formally authorized. **This sprint shall not open that route.**
- No copying/reconstructing protected AS/NZS coefficient tables into source/tests/CI, no fabricated normative lookup, no default regional values, no treating legacy workbook values as verified AS/NZS 1170.2:2021 applicability.
- Keep numerical evaluation restricted to explicit-input, demonstrably unqualified review mode with prominent `NOT FOR DESIGN` notices.
- No real CSI physical qualification claims. Selected-shell bounds/pitch and owner overrides remain candidates / engineer review, never automatic building-envelope qualification.
- Do not expand into Gable/Hip `Slope Roof` implementation. Do not add unrelated architecture/framework changes.

## 2. Workstream A — exact relative worksheet widths and genuine cell metadata

**TDD first:** add a failing test that independently defines the **correct 14-column width vector**, plus checks against `AsNzWorkbookCellLayout.Widths`. Validate normalization only by **one shared scale factor**; do not test ratios from the currently incorrect implementation. Treat the supplied vector as permitted *layout geometry*, not source normative coefficients.

- Correct column widths to the actual `Flat roof!A:N` source values shown above. Ensure `BuildHtml()` uses those ratios, including for the bordered `From / To / Cp,e / W` cells.
- Cross-check the **effective spans** from `ProjectCells()` against representative original merges: `A5:I5`, `J5:K5`, `L5:N5`, `A6:B6`, `C6:I6`, `F55:H55`, `I55:K55`, `F57:H57`, `I57:K57`, `A64:K64`, `A65:E65`, `F65:H65`, `I65:K65`, `A108:K108`, `F111:H111`, `I111:K111`, `A117:K117`, `F120:H120`, `I120:K120`. These are examples, not a substitute for checking the whole source registry. Where a display span intentionally differs from a source merge, document it as a **print-layout adaptation**, never `exact Excel merge parity`.
- Preserve semantic worksheet addresses, engineer correction labels, HTML escaping, mathematical subscript glyphs and non-overlapping cell projections. Keep numbers selectable.
- Render fresh 3-page **main** and **blocked diagnostic** A4 PDFs. Inspect each page full-size. Prevent text clipping, spillover, detached zone headings, excessive shrinking and inadvertent extra pages. Compare against the same hash-verified source workbook and existing Sprint 4B.6B output; produce a small image montage / before-after evidence. If preserving the three-page format is not possible without harming readability, report the exact trade-off rather than silently lying about parity.
- No normative coefficient changes are permitted while fixing layout.

## 3. Workstream B — fail closed on impossible geometry and unsupported method classification

**TDD first:** baseline valid explicit monoslope fixture remains numerically evaluable for arithmetic demonstration; invalid fixtures must be `BLOCKED / INPUTS OR GEOMETRY REQUIRED`, not `Ready` or design-qualified.

Required regression cases:

- `EaveHeightM <= GroundElevationM` and `RidgeHeightM < EaveHeightM` — explicit structural height contradictions.
- Nonpositive or nonfinite dimensions/roof geometry/invalid datum — structured input rejection, no false calculated pressures. Keep the existing nonfinite-policy consistent across entrypoints (no undocumented exceptions escaping a WPF button).
- `RoofForm="Flat"` plus a **nonzero slope currently classified `UNSUPPORTED`** by `AsNz2021MethodDecision.Evaluate()` — calculator must not return `NumericalComputationPossible=true` while the method is unsupported. Do not invent a flat-roof pitch threshold; choose a consistent conservative classification and document what needs independent normative review.
- For a supported explicit monoslope, height/reference values still require engineering adjudication. Do **not** blindly reject the owner's 14-degree example for unverified full-span pitch/rise inconsistency; flag that relationship explicitly as `GEOMETRY_REVIEW_REQUIRED` rather than pretending a roof plane has been physically verified.
- Tests must preserve 320 stable signed case IDs where diagnostic rows are generated, return null pressure fields when blocked, and ensure the main PDF/WPF uses `Blocked`, never `Ready`.
- Harden the **read-only candidate** helper against nonfinite/degenerate shell vertices and rotated/partial selection if necessary to prevent NaN candidate propagation or misleading `LengthM/WidthM` labels. Do not infer that selected shells form the complete footprint.

## 4. Workstream C — PDF and `.integrity.json` export publication consistency

**TDD first:** introduce deterministic fault/cancellation injection around the **publish boundary** instead of relying on timing sleeps or unrelated parallel Chromium workspaces.

- Capture an immutable export snapshot (run hash, document hash, request hash, model fingerprint, Blocked/Ready status, source PDF paths/hashes) **before** the first await. Validate the snapshot and current state again immediately before publication; no reading mutable VM identity halfway through sidecar creation.
- Stage and validate both the PDF and sidecar in the destination filesystem before final publication. Implement an explicit replace/rollback protocol such that a failure cannot leave an apparently current PDF paired with missing/stale metadata. Cross-file atomic rename is not guaranteed by Windows; **describe actual guarantees honestly** and fail closed when consistency cannot be proven. Preserve prior valid output on a failed replacement whenever practical.
- Cancellation and stale-run/model/request changes must either publish a fully consistent pair or publish neither. Temporary files must be removed; never silently return success if integrity write failed.
- Verify exported bytes against their recorded SHA-256, and check `RunHash`, `DocumentHash`, `ModelFingerprint`, `RequestHash`, `Status`, `NormativeQualified=false`, `CanMutateCsi=false` in sidecar. Audit PDF export must have the same protection.
- Tests: normal main export, normal audit export, blocked-diagnostic export, canceled export, sidecar write failure, replaced existing output, request/model mutation during export, source-byte tamper, and repeated export. Keep the existing nine WPF smoke scenarios green; add a narrowly focused tenth smoke only if needed.

## 5. Workstream D — complete but legible detailed audit PDF

- Keep the **full** 320 signed direction / ULS/SLS / internal-pressure cases and all available trace/factor evidence, with stable IDs and source class labels; do not suppress evidence to reduce page count.
- Fix `Geometry and identity` so it is not a nearly-empty last page. Reflow or move the small section with an appropriate preceding section, while preserving `break-inside` and long-token wrapping. Keep content safely inside A4 margins.
- Ensure huge IDs, factor notes, references and substituted formulas stay readable, with table headers repeated properly. Test text extraction plus visually inspect page 1, first factor table, representative dense trace pages and the final two pages.
- Do not promise exact screenshot pixel parity without owner-approved screenshots and explicit visual review. A separate acceptance checklist is acceptable.

## 6. Verification and delivery evidence

1. Fetch/check latest `origin/main`; document the actual baseline, branch state and diff.
2. Record **red** regressions for the incorrect column vector, invalid geometry/method mismatch and export publish fault before implementing fixes. Do not alter tests only to make them pass.
3. Run locked restore, Debug and Release solution builds (0 warnings/errors expected), automated test suite with 0 failed / 0 skipped, and WPF smoke including `asnz-worksheet` + `asnz-blocked`.
4. Generate and visually inspect the revised **main worksheet**, **blocked diagnostic** and **detailed audit** PDFs. Test PDF page counts, cell-coordinate extraction, source SHA-256, 320 signed case IDs, available trace coverage, `NOT FOR DESIGN` banners and all zones.
5. Re-run the workbook comparison: maintain 40/40 legacy arithmetic matches within 1e-8. Keep the 36/40 synthetic divergences **explicit**, not "fixed" by silently replacing synthetic source coefficients with legacy values.
6. Keep source `.xlsm`, standards `.pdf`, private data, downloaded reference images and generated report artifacts outside the committed repository. CI must not depend on licensed source files. Commit only code, tests and permissible metadata/docs.
7. Commit/push verified changes to `main`, **then** verify the CI run belongs to the exact pushed SHA and has actually finished successfully. Report precise SHA, CI URL, test counts, WPF count, PDF artifacts/page counts, fixed defects, and outstanding acceptance blockers.
8. If owner screenshot comparison, source rights or real CSI qualification is unavailable, state **PARTIAL / OPEN**. Stop this bounded sprint afterwards. Do not launch unlicensed normative lookup work without a separate evidence/rights authorization.

**Completion means:** true relative workbook widths with defensible print adaptations; no numerically successful *unsupported/physically impossible* explicit-review geometry; consistent, fail-closed PDF+sidecar publication; a complete, readable audit with no nearly blank identity tail; all existing safety gates unchanged. It does **not** mean automatic AS/NZS 2021 engineering qualification, automatic real CSI geometry extraction, or owner pixel-perfect visual acceptance.
