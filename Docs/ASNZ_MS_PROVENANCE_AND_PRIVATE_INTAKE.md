# Shielding provenance correction and private intake readiness

Owner authorization: targeted Ms provenance fix and private standards-data intake subject to documented software-use permissions; verified public-safe changes may be committed/pushed directly to main. Baseline: `3f80d2a6349bda67650051a34c44cbad05b7f3a0`.

## Confirmed defect and bounded correction

The source-backed ULS/SLS shielding lookup already created `CoefficientInputEvidence` containing dataset identity/hash, source locator, record IDs and exact/interpolated selection evidence. A subsequent `Supply()` overwrote that evidence as an engineer-selected assumption and added a duplicate diagnostic entry. The correction removes only that second write. No coefficient, formula, applicability rule or numerical operand is changed.

Three synthetic regression cases cover exact lookup, interpolated lookup and the separate no-shielding-benefit assumption. The first two failed before the correction because the dataset document was replaced by the assumption label. They now check retained request evidence, calculation input trace, unique diagnostics and unchanged state-specific design speeds. The explicit no-benefit assumption remains labeled as such. This proves a provenance defect; it does not establish a normative numerical calculation failure.

## Private intake preflight

The supplied publication, reference tables and workbook are already available; duplicate copies are unnecessary. The private source register and current shared-read SHA-256 checks identify the existing source PDF/workbook without changing either file. A local ignored readiness report includes the 13-table extraction plan, exact page locators, units, scope and outstanding evidence:

`fixtures/standards/private/asnz1170-2-2021/base2021-review/intake-readiness-2026-10-27.json`

Status remains **BLOCKED / SOFTWARE-USE RIGHTS AND ADOPTION EVIDENCE REQUIRED**. No written permission for numerical extraction, private storage and software-assisted reuse was found in the existing private intake/evidence directories. The observed publication permissions cannot be expanded by an implementation approval. Governing country/jurisdiction and table-specific base/amendment decisions also remain unconfirmed. An evidence-path clarification is pending with the owner. No numerical coefficients were transcribed and no loadable normative dataset was created.

Once documented permissions and adoption decisions are supplied, use these existing local sources for scoped extraction, V3 byte-coherent packaging, item-level transcription checks and independently reviewed numerical benchmarks. Never substitute public synthetic operands, owner-workbook arithmetic parity, hashes or self-declared metadata for independent engineering qualification.

Production normative qualification, engineering approval and live CSI writes remain disabled. This correction does not resolve unsupported monoslope leeward applicability, full-building gable/hip assembly, real CSI verification or owner PDF visual acceptance.

## Executed software verification

Locked restore passed. Debug and Release solution builds completed with zero warnings/errors; each configuration passed 553 automated tests, zero failed/skipped, excluding opt-in physical CSI tests. All eleven Release WPF smoke processes passed, including flat/sloped automatic review, PDF export/tamper checks and restart workflows. Private RED/GREEN TRX and process evidence are under `artifacts/ms-provenance-intake/`. No physical CSI qualification was performed.
