# Sprint 4B.6B verification handoff

Status: **PARTIAL / OPEN — EXPLICIT-INPUT REVIEW MODE ONLY / NORMATIVE LOOKUP BLOCKED**.

Baseline: `57a698268fff5f21f10c067bbd3a8e850fc3a224`; fetched origin/main again before commit. Workbook SHA-256 independently reread with read sharing: `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`. Source PDF SHA-256: `f0ab624bf1923547833ebb79e3a2947822b569828bc9472beffa491ae5521de4`. Neither source is staged.

Implemented:

- Workbook-coordinate projection (14 columns, original widths, merged zone headers, independently bordered From/To/Cp/W cells, blue values, X/Y groups, ULS/SLS, physical bounds). Three-page main worksheet; negative-direction alternatives add pages when results differ. Separate detailed audit preserves signed external/internal/net cases and full-precision traces.
- Correct q basis equation excludes Cshp; surface pressure equations include their actual shape chain. Every supplied factor has a trace dependency. Case records carry method conditions, selected factor evidence and source classification; applicability remains explicitly unverified.
- Blocked diagnostics retain WPF Blocked state. Current request/model/project/run/document and PDF byte integrity are checked for asynchronous atomic export. Request changes and cancellation invalidate main/audit availability and cancel rendering. No generic PDF fallback in this worksheet export path. Regression smoke rejects changed request and changed PDF bytes.
- Read-only selected-shell candidate adapter (already SI normalized), natural-ground evidence, planar-pitch candidate and engineer override controls. Candidate geometry never qualifies the building envelope automatically.

Validation:

- Locked restore PASS; Debug and Release builds PASS, zero warnings/errors.
- Debug and Release nonphysical tests: **438 passed, 0 failed, 0 skipped**, baseline 421. Failing regression evidence is retained in artifacts/4b6b/regression-red.trx, border-red.trx, blocked-red and tamper-red/smoke-error.txt; final green evidence in final-debug.trx / final-release.trx and verified-wpf.
- Nine WPF scenarios PASS: demo, report-export, asnz-worksheet, asnz-blocked, readonly, roof-initial, roof-reopened, zone-initial, zone-reopened. Final run directory artifacts/4b6b/verified-wpf/52ae68148d9f489baa0cf9b2a0ef9572.
- Local artifacts/4b6b: ASNZ_2021_FlatWorkbook_Review_MAIN.pdf (3 pages); ASNZ_2021_FlatWorkbook_Review_Detailed_Audit.pdf (94 pages); ASNZ_2021_Blocked_Diagnostic_MAIN.pdf (3 pages); blocked detailed audit (53 pages); ASNZ_2021_Explicit_Review_MAIN.pdf (3 pages); ASNZ_2021_Varied_Directions_MAIN.pdf (5 pages).
- PDF extraction passes every 320 case IDs / 925 traces for calculated audit and 320 case IDs / 580 available traces for blocked audit. All pages rendered to PNG/contact sheets; representative full-size worksheet and factor/trace pages checked. All-page contact overview and extraction are not a claim that every dense audit line received human engineering review. The final identity page has a large unused tail; dense audit pagination still needs owner acceptance.
- Workbook-vs-engine.csv: 80 comparisons, all 40 legacy arithmetic comparisons match within 1e-8. K29=68.796m/s; K30=45.57735m/s; K34=2.8397337696kN/m²; K35=1.2463768998135kN/m². Workbook formulas and cached values are retained separately. The verifier uses an ignored read-only workbook copy; it requires local pypdf plus the bundled rendering runtime, and is not a normative CI dataset.

Selected ULS differences for the synthetic explicit fixture (engine minus workbook, kN/m²):

| Cell / quantity | Workbook | Synthetic explicit | Delta |
|---|---:|---:|---:|
| J46 windward | 2.271787016 | 1.987813639 | -0.283973377 |
| J52 leeward | -0.851920131 | -1.135893508 | -0.283973377 |
| I57 side 0-h | -1.845826950 | -1.135893508 | +0.709933442 |
| I67 roof Case 1 | -2.555760393 | -1.703840262 | +0.851920131 |
| I76 roof Case 2 | -1.135893508 | +0.425960065 | +1.561853573 |

These are SOURCE_UNVERIFIED comparisons, not standards adjudications. The separate legacy comparison fixture reproduces cached arithmetic and never qualifies a 2021 lookup. Synthetic explicit and varied-direction fixtures are not independently evidenced engineering benchmarks. Every pressure band for both primary axes is included in the CSV; signed net/internal and SLS cases remain in the full audit.

Outstanding acceptance / qualification:

- No rights-qualified private normative table data, documented software reuse rights, confirmed jurisdiction or adopted amendment identity. Owner's proceed permission is not an invented license. Automatic coefficient lookup remains unavailable. Method decision records describe physical operands and unmet table conditions; they do not implement a qualified automatic 2021 selector. AU/NZ selection, A0 exceptions, interpolation and physical coefficient applicability still need licensed inputs and engineer adjudication.
- No verified real ETABS/SAP2000 geometry session: **PHYSICAL CSI VERIFICATION PENDING**. The adapter uses selected shell evidence only. The review calculator still operates in Demo review mode; read-only candidates and manual override are not a live-model integration qualification.
- Gable/hip Slope Roof method/layout is outside completed coverage. Full-span 14-degree roof geometry is not established by the example footprint/heights.
- Owner's five screenshot files are unavailable locally: screenshot pixel parity NOT VERIFIED. The PDF uses workbook cells/widths/zone hierarchy with explicit engineering corrections and A4 pagination, not a claim of exact screenshot or every-style parity. Owner visual acceptance remains OPEN.
- No independent engineering sign-off. CanCalculateNormativePressures and CanMutateCsi remain false. All outputs are review/diagnostic PDFs, never qualified design results. No live CSI writes.

GitHub CI evidence is reported against the pushed SHA in the completion message; local test success alone is not CI success.

CI follow-up: initial run 40367099078 exposed cancellation workspace cleanup timing under parallel Chromium renders (437/438). Cleanup now retries bounded profile-handle release and reports persistent failure instead of swallowing it. The cancellation regression identifies its own HTML marker rather than another test's concurrent workspace. Release regression rerun: 438/438; final follow-up CI must pass before reporting CI success.
Final cleanup-corrected WPF rerun: all nine PASS; artifacts/4b6b/cleanup-wpf/f509920eed2646648d3a7bee0ae70b3f. Debug rerun 438/438. No worksheet calculations or normative gates changed in the CI follow-up.
