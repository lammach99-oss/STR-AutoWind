# Sprint 4B.6C verified corrective handoff

Baseline: fetched `origin/main` and local `main` both `7afb33670fd9c44943de70af659e41392816b110` before editing. Scope: explicit-input review corrections only. Full engineering/owner visual acceptance: **PARTIAL / OPEN**.

## Corrections

- Exact supplied/read-only workbook width vector: `[6.140625,13,13,13,13,13,13,13,13,6.5703125,13,5.5703125,13,4.85546875]`. HTML normalizes by one common scale. Direct source SHA-256: `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`. Source workbook is never saved or committed.
- Source merge inventory: 130 merges in rows through 124; 128 effective spans retained. Header adaptations: N7:N8 is represented by a single-row revision value; L8:M8 date expands through N8. Source unmerged rows additionally use explicit presentation spans: wall J:K numerical values, L:M units and N limit state; factor symbols I:J; factor numeric J:K with explanatory text L:N; site speed beta M:N. Zone From/To remain D/E, Cp F:H, pressure I:K. Exact width ratios are preserved; this is **not exact Excel merge/style/pixel parity**. Numeric/pressure-unit cells do not wrap within a number/unit. Semantic addresses and independently bordered zones retained.
- Calculator requires eave above ground, ridge at/above eave and the same supported physical roof classification as method decisions. Nonzero-pitch Flat is blocked conservatively without inventing a normative pitch threshold. Invalid finite geometry retains 320 stable diagnostic identities with null pressures. Existing nonfinite entry policy remains ArgumentException; WPF catches calculation exceptions. Valid supplied 14-degree monoslope remains arithmetic-evaluable with GEOMETRY_REVIEW_REQUIRED warning; no inferred full-span roof validation.
- Read-only candidate helper rejects nonfinite vertex sets and degenerate plane normals; labels extents as axis-aligned selected-shell candidates, not building dimensions or a qualified envelope.
- Geometry/identity and warnings moved before signed cases in the audit, avoiding the old identity-only last page. All trace/factor content retained.

## Export guarantees and limits

`WorksheetPdfExport` receives immutable identity, status, source path and expected byte hash captured before await. It stages and verifies both PDF and serialized metadata on the destination filesystem, checks current identity immediately before publication, withdraws old metadata before replacing the PDF, publishes metadata last, and verifies the final pair. No await occurs within commit/rollback. A per-destination exclusive lock rejects concurrent application exports. Main, audit and blocked diagnostics use the same path. Source byte changes, identity changes, cancellation and injected write/publish faults fail closed; caught failures restore prior files and remove staging. If rollback itself cannot complete, recovery backups remain instead of being deleted.

**This is not a cross-file atomic transaction or a power-loss guarantee.** Readers must require a sidecar whose PDF hash matches the bytes. A process/system crash between renames may leave a PDF without metadata, or backups in `.wind-export-*`; that PDF is untrusted and must not be treated as current. A raw PDF file alone cannot prove currentness. External programs ignoring the lock or modifying files are outside the transaction protocol. Persistent I/O/rollback failures are surfaced, not reported as successful export.

The sidecar records PdfSha256, RunHash, DocumentHash, ModelFingerprint, RequestHash, Status, NormativeQualified=false, CanMutateCsi=false. Snapshot SourcePath is also recorded for local provenance. No mutable VM identities are read to construct metadata after await.

## Verification

- Red evidence: artifacts/4b6c/layout-geometry-red.trx (six failures); export-red/smoke-error.txt (prior PDF replaced when sidecar destination is a directory). Green deterministic publication tests cover failure before staging metadata/before publish/after PDF/after sidecar, cancellation, prior replacement, repeated/main/audit/blocked, source tamper and request/model mutation.
- Locked restore PASS. Debug and Release solution builds: 0 warnings/errors. Debug and Release automated suites: **460 passed, 0 failed, 0 skipped** (baseline 438).
- Nine WPF smoke scenarios PASS, including unsupported geometry remains Blocked with no pressure; sidecar failure preserves prior PDF. Evidence: artifacts/4b6c/final-wpf/68732dfe6fdd4bf7b739b0a3d86bf0f9. Existing demo/report-export/asnz-worksheet/asnz-blocked/readonly/roof-initial/roof-reopened/zone-initial/zone-reopened modes retained.
- Main and blocked worksheet PDFs: 3 A4 pages each; review detailed audit: **93 pages** (previously 94); blocked audit: 53 pages. Identity is on audit page 1. Final two audit pages contain calculation traces, not a stranded identity section.
- All PDF pages rendered locally. Main pages and blocked pages, first audit page/factor table/dense traces/final two pages inspected. Before/after montage: artifacts/4b6c/worksheet-before-after.png. Source widths/merges: source-layout-audit.json. Owner's five original screenshot files unavailable: exact screenshot pixel parity NOT VERIFIED. Dense audit still requires owner legibility/engineering review; text extraction alone is not independent visual acceptance.
- Extraction verifies all 320 signed cases and 925 traces in review audit; blocked audit 320 cases / 580 available traces. All applicable primary external values present. 80 workbook comparisons: 40/40 legacy matches within 1e-8; synthetic 4/40 matches and 36 SOURCE_UNVERIFIED differences unchanged. No coefficients changed. Workbook formulas and cached values retained separately in ignored comparison CSV.

## Local deliverables

Under `D:\Project\Etab-Sap Tool\AutoWind\artifacts\4b6c`:

- ASNZ_2021_FlatWorkbook_Review_MAIN.pdf
- ASNZ_2021_Blocked_Diagnostic_MAIN.pdf
- ASNZ_2021_FlatWorkbook_Review_Detailed_Audit.pdf
- ASNZ_2021_Blocked_Diagnostic_Detailed_Audit.pdf
- worksheet-before-after.png; rendered page PNGs/contact sheets; workbook-vs-engine.csv; page-audit.json; source-layout-audit.json

GitHub CI URL/SHA are reported after the exact pushed run finishes. No source workbook, standards PDF, private tables/rights evidence or generated PDFs/images are staged. Unrelated pre-existing 4B.6A directive remains untracked.

## Qualification remains disabled

CanCalculateNormativePressures=false; CanMutateCsi=false; standards integrity, primary-source applicability, independent engineering review and production authorization remain false. No live CSI writes. No normative coefficients or qualifications added. Source software reuse rights, jurisdiction/amendment adoption, independent method evidence, actual real CSI validation, gable/hip Slope Roof and full owner visual acceptance remain open. This corrective sprint stops here pending separate evidence/authorization.
Final unit-spacing refinement rerun: Debug/Release 460/460; all nine WPF modes PASS at artifacts/4b6c/units-wpf/13e5d8463101438a8389dfc636f14e7c. Units span L:M and ULS/SLS occupy N; full-size main and blocked pressure rows reviewed after this change.
