# AS/NZS 2021 workbook row coverage and acceptance status

This increment provides explicit-input engineer-review arithmetic and the Flat roof worksheet projection. It is NOT the complete normative engine requested in Sprint 4B.6A, and exact visual parity is NOT accepted.

Verified reference hashes:
- Publication: f0ab624bf1923547833ebb79e3a2947822b569828bc9472beffa491ae5521de4 (124 pages).
- Workbook: 64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c, inspected with shared read access; no macros executed.

Owner replied “yes, proceed” regarding source use. Written permission details, licensed-user scope, project jurisdiction and amendment adoption remain unspecified. No protected coefficient table is distributed. Normative data integrity, primary-source qualification, owner review and independent review flags remain false. CSI mutation gates are unchanged.

## Actual coverage

- ASNZ2021_EXPLICIT_MATRIX: four global directions, eight supplied directional multipliers, separate ULS/SLS regional speeds/return periods, interpolated sector design speeds and ULS 30 m/s minimum.
- Separate Cshp,e = Cpe Ka Kce Kl Kp and Cshp,i = Cpi Kci Kv; signed external/internal/net pressures; physical band clipping; roof normal from explicit slope and slope bearing.
- 320 identifiable cases: windward/leeward, both sidewall faces with four bands, roof cases 1/2 with five bands, two internal signs, four directions, two limit states.
- Missing inputs yield null results and blockers, never fake zero. Snapshots remain JSON serializable.
- Primary 120-row Flat roof layout; blue values, engineering subscripts, complete matrix and full-precision formula/input trace appendix. PDF export uses selected immutable run.
- WPF imports an explicit request JSON in review/demo mode; background calculation/PDF, invalidation and cancelled-result protection; new eighth smoke.

## Outstanding acceptance work (not hidden by passing tests)

- Qualified regional/recurrence/coast/Md/Mc/terrain lookup datasets, A0-specific applicability and authenticated amendment effects are not implemented. Bounded interpolation utilities accept supplied operands only; they do not establish normative applicability.
- Monoslope coefficient selection at 14 degrees and h/d boundaries is NOT independently validated. All surface factors are explicit inputs. No current standard tables copied from legacy workbook.
- Slope Roof gable/hip U/D/R method and its 5–103 row registry remain unimplemented in this path. Existing legacy renderer remains available, not certified by this increment.
- Automatic validated CSI envelope, roof-plane filtering, datum and override diagnostics are not connected to this matrix; geometry is explicit review input. No claim of live CSI geometry validation.
- Primary worksheet retains row order, not exact merged-cell/column geometry. Owner screenshots are absent locally. Exact screenshot parity and complete normative calculations cannot be claimed.
- Loaded areas, resultants/line loads and element mapping remain outside this explicit pressure matrix.
- Report trace uses full precision and source labels; source method qualification, amendment adoption and independent engineering acceptance remain required.

## Independent workbook comparison

Owner workbook cached values: VRu 80 m/s, VRs 53 m/s, Md .9, Mc 1.05, Mz .91, Ms=Mt=1. Explicit arithmetic reproduces Vsit,U=68.796 m/s, Vsit,S=45.57735 m/s and ULS velocity basis 2.8397337696 kPa. Workbook windward Cpe .8 gives 2.27178701568 kPa; explicit .7 gives 1.98781363872 kPa; delta -0.28397337696 kPa. This discrepancy is recorded, not resolved by changing coefficients to match. Selected windward height method and roof method require owner review.

Official amendment catalog checked: https://store.standards.org.au/search?query=AS%2FNZS1170.2%28Windactions%29&status=Current_PendingRevision lists Amd 2:2024. Amd 1:2023 and Amd 2:2024 adoption is pending; supplied base PDF is not assumed consolidated.

## Stable Flat roof row registry

Exact labels live in AsNzWorkbookRows.Flat. Every row below is tested by AsNzWorksheetTests.AllWorksheetRowsAndMatrixValuesAreRendered. Blank workbook rows are retained as separators; headings remain visible. Source families: geometry Cl 2.3/Fig 2.1; speed Cl 2.2–3.4; pressure Eq 2.4(1); shape Eqs 5.2(1)/(2); coefficient applicability 5.4 requires external review.

| Field ID | Origin / status |
|---|---|
| Flat-5 | Request / engine case / explicit blocker; see renderer Value(5). |
| Flat-6 | Request / engine case / explicit blocker; see renderer Value(6). |
| Flat-7 | Request / engine case / explicit blocker; see renderer Value(7). |
| Flat-8 | Request / engine case / explicit blocker; see renderer Value(8). |
| Flat-9 | Request / engine case / explicit blocker; see renderer Value(9). |
| Flat-10 | Request / engine case / explicit blocker; see renderer Value(10). |
| Flat-11 | Request / engine case / explicit blocker; see renderer Value(11). |
| Flat-12 | Request / engine case / explicit blocker; see renderer Value(12). |
| Flat-13 | Request / engine case / explicit blocker; see renderer Value(13). |
| Flat-14 | Request / engine case / explicit blocker; see renderer Value(14). |
| Flat-15 | Request / engine case / explicit blocker; see renderer Value(15). |
| Flat-16 | Request / engine case / explicit blocker; see renderer Value(16). |
| Flat-17 | Request / engine case / explicit blocker; see renderer Value(17). |
| Flat-18 | Request / engine case / explicit blocker; see renderer Value(18). |
| Flat-19 | Request / engine case / explicit blocker; see renderer Value(19). |
| Flat-20 | Request / engine case / explicit blocker; see renderer Value(20). |
| Flat-21 | Request / engine case / explicit blocker; see renderer Value(21). |
| Flat-22 | Request / engine case / explicit blocker; see renderer Value(22). |
| Flat-23 | Request / engine case / explicit blocker; see renderer Value(23). |
| Flat-24 | Request / engine case / explicit blocker; see renderer Value(24). |
| Flat-25 | Request / engine case / explicit blocker; see renderer Value(25). |
| Flat-26 | Request / engine case / explicit blocker; see renderer Value(26). |
| Flat-27 | Request / engine case / explicit blocker; see renderer Value(27). |
| Flat-28 | Request / engine case / explicit blocker; see renderer Value(28). |
| Flat-29 | Request / engine case / explicit blocker; see renderer Value(29). |
| Flat-30 | Request / engine case / explicit blocker; see renderer Value(30). |
| Flat-31 | Request / engine case / explicit blocker; see renderer Value(31). |
| Flat-32 | Request / engine case / explicit blocker; see renderer Value(32). |
| Flat-33 | Request / engine case / explicit blocker; see renderer Value(33). |
| Flat-34 | Request / engine case / explicit blocker; see renderer Value(34). |
| Flat-35 | Request / engine case / explicit blocker; see renderer Value(35). |
| Flat-36 | Request / engine case / explicit blocker; see renderer Value(36). |
| Flat-37 | Request / engine case / explicit blocker; see renderer Value(37). |
| Flat-38 | Request / engine case / explicit blocker; see renderer Value(38). |
| Flat-39 | Request / engine case / explicit blocker; see renderer Value(39). |
| Flat-40 | Request / engine case / explicit blocker; see renderer Value(40). |
| Flat-41 | Request / engine case / explicit blocker; see renderer Value(41). |
| Flat-42 | Request / engine case / explicit blocker; see renderer Value(42). |
| Flat-43 | Request / engine case / explicit blocker; see renderer Value(43). |
| Flat-44 | Request / engine case / explicit blocker; see renderer Value(44). |
| Flat-45 | Request / engine case / explicit blocker; see renderer Value(45). |
| Flat-46 | Request / engine case / explicit blocker; see renderer Value(46). |
| Flat-47 | Request / engine case / explicit blocker; see renderer Value(47). |
| Flat-48 | Request / engine case / explicit blocker; see renderer Value(48). |
| Flat-49 | Request / engine case / explicit blocker; see renderer Value(49). |
| Flat-50 | Request / engine case / explicit blocker; see renderer Value(50). |
| Flat-51 | Request / engine case / explicit blocker; see renderer Value(51). |
| Flat-52 | Request / engine case / explicit blocker; see renderer Value(52). |
| Flat-53 | Request / engine case / explicit blocker; see renderer Value(53). |
| Flat-54 | Request / engine case / explicit blocker; see renderer Value(54). |
| Flat-55 | Request / engine case / explicit blocker; see renderer Value(55). |
| Flat-56 | Request / engine case / explicit blocker; see renderer Value(56). |
| Flat-57 | Request / engine case / explicit blocker; see renderer Value(57). |
| Flat-58 | Request / engine case / explicit blocker; see renderer Value(58). |
| Flat-59 | Request / engine case / explicit blocker; see renderer Value(59). |
| Flat-60 | Request / engine case / explicit blocker; see renderer Value(60). |
| Flat-61 | Request / engine case / explicit blocker; see renderer Value(61). |
| Flat-62 | Request / engine case / explicit blocker; see renderer Value(62). |
| Flat-63 | Request / engine case / explicit blocker; see renderer Value(63). |
| Flat-64 | Request / engine case / explicit blocker; see renderer Value(64). |
| Flat-65 | Request / engine case / explicit blocker; see renderer Value(65). |
| Flat-66 | Request / engine case / explicit blocker; see renderer Value(66). |
| Flat-67 | Request / engine case / explicit blocker; see renderer Value(67). |
| Flat-68 | Request / engine case / explicit blocker; see renderer Value(68). |
| Flat-69 | Request / engine case / explicit blocker; see renderer Value(69). |
| Flat-70 | Request / engine case / explicit blocker; see renderer Value(70). |
| Flat-71 | Request / engine case / explicit blocker; see renderer Value(71). |
| Flat-72 | Request / engine case / explicit blocker; see renderer Value(72). |
| Flat-73 | Request / engine case / explicit blocker; see renderer Value(73). |
| Flat-74 | Request / engine case / explicit blocker; see renderer Value(74). |
| Flat-75 | Request / engine case / explicit blocker; see renderer Value(75). |
| Flat-76 | Request / engine case / explicit blocker; see renderer Value(76). |
| Flat-77 | Request / engine case / explicit blocker; see renderer Value(77). |
| Flat-78 | Request / engine case / explicit blocker; see renderer Value(78). |
| Flat-79 | Request / engine case / explicit blocker; see renderer Value(79). |
| Flat-80 | Request / engine case / explicit blocker; see renderer Value(80). |
| Flat-81 | Request / engine case / explicit blocker; see renderer Value(81). |
| Flat-82 | Request / engine case / explicit blocker; see renderer Value(82). |
| Flat-83 | Request / engine case / explicit blocker; see renderer Value(83). |
| Flat-84 | Request / engine case / explicit blocker; see renderer Value(84). |
| Flat-85 | Request / engine case / explicit blocker; see renderer Value(85). |
| Flat-86 | Request / engine case / explicit blocker; see renderer Value(86). |
| Flat-87 | Request / engine case / explicit blocker; see renderer Value(87). |
| Flat-88 | Request / engine case / explicit blocker; see renderer Value(88). |
| Flat-89 | Request / engine case / explicit blocker; see renderer Value(89). |
| Flat-90 | Request / engine case / explicit blocker; see renderer Value(90). |
| Flat-91 | Request / engine case / explicit blocker; see renderer Value(91). |
| Flat-92 | Request / engine case / explicit blocker; see renderer Value(92). |
| Flat-93 | Request / engine case / explicit blocker; see renderer Value(93). |
| Flat-94 | Request / engine case / explicit blocker; see renderer Value(94). |
| Flat-95 | Request / engine case / explicit blocker; see renderer Value(95). |
| Flat-96 | Request / engine case / explicit blocker; see renderer Value(96). |
| Flat-97 | Request / engine case / explicit blocker; see renderer Value(97). |
| Flat-98 | Request / engine case / explicit blocker; see renderer Value(98). |
| Flat-99 | Request / engine case / explicit blocker; see renderer Value(99). |
| Flat-100 | Request / engine case / explicit blocker; see renderer Value(100). |
| Flat-101 | Request / engine case / explicit blocker; see renderer Value(101). |
| Flat-102 | Request / engine case / explicit blocker; see renderer Value(102). |
| Flat-103 | Request / engine case / explicit blocker; see renderer Value(103). |
| Flat-104 | Request / engine case / explicit blocker; see renderer Value(104). |
| Flat-105 | Request / engine case / explicit blocker; see renderer Value(105). |
| Flat-106 | Request / engine case / explicit blocker; see renderer Value(106). |
| Flat-107 | Request / engine case / explicit blocker; see renderer Value(107). |
| Flat-108 | Request / engine case / explicit blocker; see renderer Value(108). |
| Flat-109 | Request / engine case / explicit blocker; see renderer Value(109). |
| Flat-110 | Request / engine case / explicit blocker; see renderer Value(110). |
| Flat-111 | Request / engine case / explicit blocker; see renderer Value(111). |
| Flat-112 | Request / engine case / explicit blocker; see renderer Value(112). |
| Flat-113 | Request / engine case / explicit blocker; see renderer Value(113). |
| Flat-114 | Request / engine case / explicit blocker; see renderer Value(114). |
| Flat-115 | Request / engine case / explicit blocker; see renderer Value(115). |
| Flat-116 | Request / engine case / explicit blocker; see renderer Value(116). |
| Flat-117 | Request / engine case / explicit blocker; see renderer Value(117). |
| Flat-118 | Request / engine case / explicit blocker; see renderer Value(118). |
| Flat-119 | Request / engine case / explicit blocker; see renderer Value(119). |
| Flat-120 | Request / engine case / explicit blocker; see renderer Value(120). |
| Flat-121 | Request / engine case / explicit blocker; see renderer Value(121). |
| Flat-122 | Request / engine case / explicit blocker; see renderer Value(122). |
| Flat-123 | Request / engine case / explicit blocker; see renderer Value(123). |
| Flat-124 | Request / engine case / explicit blocker; see renderer Value(124). |

## Verified delivery evidence

Locked restore and Debug/Release builds passed with zero warnings/errors. 421 automated nonphysical tests passed. All original seven WPF smokes and the new AS/NZS worksheet smoke passed after correcting a synchronization-context deadlock exposed by the renderer change. PDF text verification matched all 320 signed cases and all 120 primary rows; all 901 trace IDs were found after normalizing PDF line-wrap whitespace. Every page was rendered to private PNG evidence/contact sheets. The sample PDFs are dense (54 calculated pages / 28 diagnostic pages including all traces), and exact worksheet dimensional parity is not accepted.

Private artifacts: artifacts/4b6a/ASNZ_2021_FlatWorkbook_Comparison_Review.pdf, ASNZ_2021_Blocked_Diagnostic.pdf, workbook-comparison.csv, pdf-extraction-parity.json, release-final.trx and wpf-final-fixed/. Inputs and outputs are synthetic engineer-review evidence, not independent verification. Neither source PDF nor source workbook is tracked.

To reproduce: run the nonphysical suite on Windows with Edge installed; run scripts/test-wpf.ps1; run scripts/verify-asnz-pdf.py with Python pypdf installed. Generated review request schema is EngineeringCalculationRequest with ProcedureName ASNZ2021_EXPLICIT_MATRIX. Per-surface keys are Direction_Surface_RoofCase_Zone_Factor, e.g. PositiveX_Roof_1_0_Cpe. Factors: Cpe, Ka, Kce, Kl, Kp, Kci, Kv. Global keys: VR_Uls, VR_Sls, R_Uls, R_Sls, Mz_cat, Ms, Mt, Mc, rho, Cdyn, SlopeBearing and Md_0 through Md_315 in 45-degree steps; Cpi_Positive and Cpi_Negative remain independently supplied signed inputs. Owner overrides must carry source notes, document, clause, timestamp and provenance. No synthetic fixture is auto-loaded for real projects.
