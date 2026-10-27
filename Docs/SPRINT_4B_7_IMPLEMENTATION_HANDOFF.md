# Sprint 4B.7 bounded implementation handoff

Status: PRIVATE REVIEW ARCHITECTURE IMPLEMENTED; REAL SOURCE LOOKUP BLOCKED.

## Delivered

Guided WPF AU/NZ and dataset-filtered region selection, terrain 2 and return-period 500/25 defaults; explicit reviewed metric geometry/site/enclosure/dynamic inputs. Background local lookup feeds the existing matrix engine and PDF-only viewer, with cancellation/stale-result guards and integrity-bound main/audit exports. Physically flat 0-degree rectangular enclosed MWFRS only. Monoslope 14 degrees is not eligible for this selector.

Exact categorical scope and one/two dimensional source-declared interpolation; duplicate grids, missing endpoints, extrapolation, wrong country/edition/limit state, missing rights and per-table amendment decisions fail closed. V3 semantic/component integrity preflight applies. Immutable request includes dataset SHA, site decisions, rights/adoption references and lookup diagnostics. No normative tables added.

## Owner source evidence

Reference Workbook contains the base 2021 publication and revised workbook with the expected SHA-256 values. No versioned private dataset, written software-use rights evidence or jurisdiction/adoption decision was present. The folder path and prior yes/proceed response do not supply these documents. No private numerical extraction was performed.

Publisher listing checked: https://www.intertekinform.com/en-au/standards/as-nzs-1170-2-2021-117837_saig_as_as_2984818/ identifies reissues incorporating Amendment 1 (May 2023) and Amendment 2 (June 2024). Local base text does not establish their adoption or per-table effect.

Owner must provide a versioned private bundle under ignored fixtures/standards/private/asnz1170-2-2021/<dataset-id>/ with manifest.json and coefficient components; auto-authorization.json containing SoftwareUseAuthorized, RightsFile, RightsSha256, AdoptionFile, AdoptionSha256 and TableDecisions (table locator to documented applicability decision). Evidence files are relative to the bundle. Rights must cover extraction/storage and software-assisted use; adoption must identify jurisdiction and amendment decisions. Hash verification establishes file identity, not legal sufficiency or professional approval.

## Coverage limits

No real-standard review result generated. Public test numbers are deliberately invented SYNTHETIC fixtures, not qualified AS/NZS coefficients. Regional regimes are supported through scoped dataset records, not hardcoded formula tables. C/D require coast-distance records and low-rise ULS internal branch; A0, mixed terrain, advanced topography/shielding, C&C, sensitive dynamics and non-flat methods remain blocked. Wall evaluation height must equal mean roof height pending separate external/internal height basis implementation. Roof Case 1/2 uses the existing review matrix and requires complete applicable source records; optional single-alternative methods are not implemented. Qualified real CSI envelope recognition and engineering benchmarks remain pending. Negative-axis differences may add worksheet pages under existing report behavior.

## Acceptance

Synthetic arithmetic and rendering evidence is software verification only. Independent engineering and owner visual acceptance remain PARTIAL / OPEN. Production normative qualification, engineering approval and live CSI mutation gates remain disabled. Stop after this sprint pending source evidence and independent review.

## Verification results

Locked restore; Debug/Release builds: zero warnings/errors. 485/485 nonphysical tests (460 baseline plus 25 new regression cases). Ten WPF smoke scenarios including guided auto lookup, cancellation, changed Region/Terrain invalidation, PDF tamper rejection, diagnostic Blocked state, main/audit export. Synthetic main and blocked PDFs: three A4 pages each. Audit: 287 pages, 320 case IDs and 1508 trace nodes present, all calculated case numeric sequences matched extracted PDF values after normalizing line breaks/discretionary hyphens. Representative rendered pages inspected; independent full visual acceptance not claimed.

Initial sample-test publication failure reproduced when overwriting a pre-existing locally opened PDF. Test artifacts now use unique run directories, preserving existing owner artifacts. No renderer production behavior weakened.

Evidence root: artifacts/4b7/final-wpf/aa3afae88bbc458e85b43e944c018515/asnz-auto/; extraction evidence artifacts/4b7/visual/qa.json; RED/GREEN TRX under artifacts/4b7/. Source PDF and workbook hash matches verified read-only. Synthetic vs workbook pressures intentionally differ: synthetic factors are invented test data; zero-degree geometry is not the owner's 14-degree example. Existing legacy arithmetic comparison tests remain preserved.
