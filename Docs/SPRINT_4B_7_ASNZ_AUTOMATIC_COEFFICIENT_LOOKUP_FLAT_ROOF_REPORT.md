# Sprint 4B.7 - AS/NZS 1170.2:2021 Automatic Coefficient Lookup and Flat-Roof Worksheet PDF

**Owner authorization:** The project owner explicitly authorizes this **new, bounded implementation sprint** following the Sprint 4B.6C hold. The authorization covers implementation of automatic, source-traceable lookup for owner-selected wind region and terrain category and its application to the **Flat Roof** calculation/report workflow. It does **not** authorize engineering self-certification, redistribution of copyrighted normative datasets, production normative qualification, or live CSI writes.

**Repository root (Windows):** `D:\Project\Etab-Sap Tool\AutoWind`

**Prompt path:** `D:\Project\Etab-Sap Tool\AutoWind\Docs\SPRINT_4B_7_ASNZ_AUTOMATIC_COEFFICIENT_LOOKUP_FLAT_ROOF_REPORT.md`

**Baseline to fetch/re-verify:** `origin/main` at `e52d5528a586dae5705d7c7b4e8a9163daa56218`; CI run `40373922181` passed 460/460 nonphysical tests and nine WPF smokes. If newer, audit the new HEAD first; do not overwrite unrelated work.

## 0. Owner-visible target and hard boundaries

The user must be able to select **AU/NZ jurisdiction, Wind Region, Terrain Category (default 2), ULS/SLS recurrence intervals (defaults 500/25 years)**, then press Calculate. The application must automatically look up applicable wind-speed and multiplier coefficients from an **authorized, local, versioned private standards dataset**; combine them with actual reviewed geometry and wind orientation; calculate Flat Roof pressure zones in the background; and show only the existing **three-page Excel-layout PDF as the primary results UX**, with a separate complete audit PDF.

The user must **not** have to type `VR_Uls`, `VR_Sls`, eight `Md_*` values, `Mc`, `Mz_cat` or every wall/roof `Cpe` when supported, matching private lookup records exist. Show any remaining mandatory site/enclosure/topographic input explicitly with a reason; no fabricated fallback.

**Two distinct gates:** (1) `NumericalReviewPossible` from legitimate source-traceable private lookup + applicable inputs, and (2) **ProductionNormativeQualified**. Implement the first as a bounded review feature, but preserve `CanCalculateNormativePressures == false` and `CanMutateCsi == false` (including all CSI mutation routes). Never label a privately sourced but unreviewed result as professionally/independently qualified or ready for structural design. Automatic retrieval is **not** independent engineering verification.

## 1. Primary references, status and rights

Owner-provided local source (read only): `Reference Workbook\` contains the revised `AS.NZS_Wind load.xlsm` (SHA-256 `64c6c21d6a5aefacf8077244f6359b5ce2316d5cee8518dcc26d41a17445326c`), and the owner-provided base AS/NZS 1170.2:2021 PDF (SHA-256 `f0ab624bf1923547833ebb79e3a2947822b569828bc9472beffa491ae5521de4`). Locate and verify the actual local PDF filename; it may have been placed under a private reference directory, not automatically copied to the developer's machine. The source PDF supplied to this conversation is an original 2021 document, **not evidence that Amendment 1:2023 or Amendment 2:2024 is embedded**.

The publisher's current listing identifies a reissue incorporating Amendment 1 (May 2023) and Amendment 2 (June 2024). Check the governing jurisdiction/code adoption and source documents; do not assume every amendment is automatically adopted for every project.

- Public publication-status reference: https://www.intertekinform.com/en-au/standards/as-nzs-1170-2-2021-117837_saig_as_as_2984818/
- Publisher amendment index: https://store.standards.org.au/search?query=AS%2FNZS1170.2%28Windactions%29&status=Current_PendingRevision
- Private intake guide: `Docs/STANDARDS_DATA_INTAKE_AND_ENGINEER_REVIEW.md`.

**Rights:** Check with the owner that the source license authorizes local private extraction/storage and software-assisted use. Merely possessing the PDF does not prove those rights. Do **not** bulk dump the DRM-protected standard, ship/copy the full normative table data into `src/`, `tests/`, `Docs/`, public Git, CI artifacts, user-facing logs, or a distributed application binary. Source-bound numeric records belong ONLY in git-ignored `fixtures/standards/private/asnz1170-2-2021/<dataset-id>/` after rights confirmation. Existing public templates are illustrative and MUST NOT be promoted as authentic data. Tests/CI must use obviously synthetic, rights-cleared fixtures.

If rights or updated standard inputs are missing, **still implement and test the lookup architecture, UI and review workflow** against synthetic fixtures. A real-source result must clearly say `LOCAL SOURCE DATA UNAVAILABLE / OWNER RIGHTS OR AMENDMENT EVIDENCE REQUIRED` until an authorized, appropriate private bundle exists. Do not claim actual automatic 2021 production lookup has been completed using demo data.

## 2. Engineering source map (verify exact edition and amended provisions)

Use the owner's primary PDF as a **local, read-only engineering reference** (not a public table dump). Verify the actual table/figure/clause and dependent conditions for each lookup:

| Lookup or decision | Source in the supplied base 2021 document | Mandatory dimensions / special checks |
|---|---|---|
| Regional speed `VR` | Section 3.2, Tables 3.1(A) Australia / 3.1(B) New Zealand, regional maps | AU/NZ, exact region/subregion, R=ULS/SLS, C/D smoothed-coastline transition where applicable, table versus formula and rounding requirements |
| Wind direction multiplier `Md` | Section 3.3, Tables 3.2(A) / 3.2(B) | Eight cardinal bearings relative to **true north**, exact AU/NZ subregion; exceptions for certain structural systems/components are **not** general MWFRS rules |
| Climate multiplier `Mc` | Section 3.4, Table 3.3 | Region, applicable amendments and jurisdiction |
| Terrain/height `Mz,cat` | Section 4.2, Table 4.1, Clause 4.2.3 | Terrain category 1,2,2.5,3,4; governing `z`, height interpolation, intermediate terrains only where permitted; Region A0 exception; mixed-upwind-terrain averaging requires segment evidence |
| Shielding `Ms` | Section 4.3, Table 4.2 | Direction and upwind shielding parameter; permit 1.0 only for explicitly declared no shielding benefit / no applicable shielding |
| Topography `Mt` | Section 4.4 | Site topography, elevation, hills/escarpments, special regional provisions; do not silently set 1.0 where topographic assessment is missing |
| Directional design speed `Vdes,theta` | Sections 2.2-2.3 | Eight `Vsit,beta`, true-north/global-X relationship, prescribed angular/sector interpolation, applicable speed minimum, no arbitrary component combination |
| Pressure basis / external/internal/net | Section 2.4, Section 5 | `q = 0.5*rho*Vdes^2*Cdyn`; external and internal `Cshp` must remain distinguishable; sign convention and surface normal |
| Wall external coefficients | Section 5.4.1, Tables 5.2(A)/(B)/(C) | Windward height; leeward `d/b`, roof class/pitch and wind-relative bearing; sidewall strip distances |
| Flat / near-flat / monopitch roof coefficients | Section 5.4.1, Tables 5.3(A)/(B)/(C) | **Method decision first:** pitch, roof type, upwind/downwind/crosswind, `h/d`, edge distance, table columns, permitted interpolation, both alternatives where prescribed |
| Internal pressure `Cpi` | Section 5.3, Tables 5.1(A)/(B) and opening clauses | Enclosure/leakage/openings, ULS/SLS, height, C/D low-rise cases, potential debris openings; never universally assume +/-0.2 |
| Local, area, combination, permeability factors | Sections 5.4.2-5.4.5, Tables 5.4-5.8 where applicable | Tributary area, effective contributing surfaces, local zones, parapets, porosity, external/internal combinations and factor interactions |
| Dynamic response `Cdyn` | Section 6 | Use 1.0 only if the selected method's criteria permit; otherwise obtain independently supported dynamic inputs or block |

**Critical 2021 source checks:** Table 4.1 explicitly excludes A0 from its general grid. Region C/D regional speeds may depend on distance from the smoothed coastline. For ULS **below 25 m in Regions C/D**, internal-pressure selection follows a different rule from most other cases. Table 5.3(A) is NOT a universal selector for a 14-degree monopitch: determine Table 5.3(A)/(B)/(C) applicability using roof slope and wind-relative surface classification. The `Flat roof` worksheet name does not override the standard's physical-method conditions.

Do NOT copy legacy workbook `Cfig`, `Cpe`, `Cpi`, `Kl`, etc. values into automatic 2021 selectors. The workbook is a **format/arithmetic comparison**, not source qualification. Use `Cshp` as the 2021 factor in pressure formulas; the workbook's `Cfig` label may appear only as an explicitly explained legacy notation.

## 3. Implementation architecture

Add a typed, isolated `AsNz2021LocalAutoLookupService` (or equivalent) in Engineering/Application with:

1. **Input DTO:** standard edition/amendment revision, country, precise region, terrain category, R_ULS/R_SLS, eight cardinal wind directions relative true North, global model orientation, `h`, wind-relative `d/b`, roof class/pitch/normal, relevant site/topographic/shielding/enclosure/area inputs, chosen pressure system (MWFRS vs C&C). Do not infer physical site conditions from Region alone.
2. **Output DTO:** per-factor `Resolved | InputRequired | NotApplicable | Ambiguous | OutsideDomain | RightsOrEditionBlocked`; numeric nullable value, unit, lookup record IDs and SHA, source table/clause, exact selector conditions, interpolation endpoints/policy, reason, owner/manual override and provenance. Never report unknown as 0 or 1.
3. **Data provider:** reuse existing `StandardsDataBundle`, `CoefficientRecord`, strict `ProcedureSelection` and V3 canonical/byte hash preflight as applicable. Exact records/anchors may use existing APIs; implement a separate validated selection/interpolation layer for ordered grids and conditional tables because current `FindRecordForProcedure()` matches **exact numerical conditions only**. Fail on duplicate rows, ambiguous methods, missing endpoints, out-of-range values, incompatible limit state/edition/amendment/system. No cross-category or prohibited extrapolation.
4. **Version-aware private loader:** reject silent cross-edition lookup, never treat a base-2021 PDF as amended 2024 text, require a signed-off amendment applicability decision **for each affected table** or transparently mark affected lookup `AmendmentReviewRequired`. Local rights may allow review calculations before independent signoff but never open production normative gate.
5. **Readable dependency trace:** every chosen coefficient must explain which input(s) selected it, the actual private source locator and whether it was exact, interpolated or an explicit engineer assumption. `Override` remains an explicit engineering alternative with a reason, never silently substituted for auto lookup.

Do not create a parallel calculator that diverges from `AsNz2021ReviewCalculation` and `AsNz2021MatrixEngine`. Have the local selector produce an immutable, fully traceable coefficient input snapshot compatible with the existing 320-case review matrix. The calculation engine remains deterministic in SI; PDF projection remains metric (kN, kN/m, kN/m2, m, m/s).

## 4. Auto-resolution flow: region and terrain to speed

Implement the following behavior, in this order:

1. User selects AU or NZ; filter eligible region codes and subregions. Never default region D or mix AU/NZ tables. If a named region has a coastal gradient, require a supported distance/boundary input rather than silently selecting maximum.
2. ULS return period defaults to 500 years, SLS to 25 years; both editable. Resolve each `VR` from the applicable edition-verified Table 3.1/formula regime. Retain unrounded intermediate values where prescribed and exact display rounding in PDF.
3. Resolve eight `Md` values by region and true-north cardinal bearing. Build `Vsit,beta` for ULS/SLS using looked-up `VR`, `Md`, `Mc`, `Mz,cat`, `Ms`, `Mt`; select `Vdes,theta` for each global +/-X and +/-Y via the true-north model orientation and prescribed sector maximum. Apply ULS minimum only where actually required and record it in trace.
4. Terrain Category defaults to 2 **for the dropdown only**. Resolve `Mz,cat(z)` from the authorized Table 4.1 with permitted interpolation, A0 exception and mixed-terrain path; for uniform terrain allow explicit `uniform upwind terrain` confirmation. Multiple height sampling where windward-wall height dependence is required; do not silently use one roof-height factor for all elevations.
5. Shielding default is `no shielding benefit claimed`, yielding a documented `Ms=1` only in that case. Topography default should be `assessment required` unless owner affirmatively declares no topographic amplification applicable; then `Mt=1` with a clear assumption flag. Handle eligible advanced topo/shield cases using actual site inputs, or fail closed.
6. Preserve the source selected for every `VR`, `Md`, `Mc`, `Mz,cat`, `Ms`, `Mt`, with exact conditions and no source-data leakage into public logs.

## 5. Flat Roof pressure coefficient decision tree

**First delivery target:** physically **flat roof with verified pitch 0 degrees**, rectangular enclosed building and defined structural pressure system. This is the initial automatic-selection coverage. Keep the existing 14-degree `Monoslope` review example but do not run flat-roof coefficient shortcuts for it.

- Obtain full building length along X / width along Y footprint, natural ground, peak/eave heights, standard-defined mean roof height, roof pitch and true-north orientation from the existing read-only CSI candidate workflow; require engineer review when selection only supplies candidate shell extents. Accept explicit manual override with reason; **do not** claim fully qualified envelope recognition.
- Choose windward/leeward/sidewall `Cp,e` through tables 5.2(A)/(B)/(C) with true wind-relative orientation, roof class, `h`, `d/b`, height and zoning conditions; derive sidewall zone endpoints and both +/-X and +/-Y cases.
- Choose applicable roof `Cp,e` by standard table 5.3 method conditions and the `h/d`/edge-distance bins. Generate **both Case 1 and Case 2** only where two alternatives are required. Preserve linked condition evidence across all zones (0-0.5h, 0.5h-h, h-2h, 2h-3h, beyond 3h) without inventing non-applicable strips.
- Determine internal pressure through the distinct Clause 5.3 classification paths. Require enclosure/opening/debris information, especially Region C/D ULS low-rise; evaluate signed plus/minus `Cpi` combinations and worst-case net result. **Region + Terrain alone is insufficient** to infer `Cpi`.
- Resolve `Ka`, `Kc,e`, `Kc,i`, `Kl`, `Kp`, `Kv` only in their applicable scope. Do not default to 1 without documenting a legitimate applicability rule or user assumption; preserve interacting factor restrictions and tributary-area/local-zone differences. Resolve `Cdyn` only after checking dynamic sensitivity.
- Produce signed external, internal and net surface pressures with consistent outward-normal force vectors, ULS/SLS, four global wind directions and all applicable roof zones. Keep MWFRS and C&C method IDs distinct.
- A roof reported as Monoslope (e.g., 14 degrees), gable or hip must select the correct **separate** standard method or explicitly display `METHOD_NOT_SUPPORTED / COEFFICIENT_SELECTOR_INCOMPLETE`. Do not mark it as Flat solely to obtain a numerical output. Include negative regressions.

## 6. WPF inputs and review UX

Replace the compulsory `Load explicit AS/NZS review request.json` as the sole input route. Implement ordinary WPF controls in `WindPage()`/VM for a **guided, minimal** owner setup:

- Country `Australia / New Zealand` -> region/subregion dropdown; Wind Region required and not silently defaulted.
- Terrain Category dropdown `1 / 2 / 2.5 / 3 / 4`, default **2**.
- ULS R (default **500 years**) and SLS R (default **25 years**).
- Roof type `Flat / Monoslope / Gable / Hip` with Flat eligible first. Pitch from read-only reviewed model if available, otherwise user input; show the source (model candidate/engineer override).
- Model bearing relative to true north; length X, width Y, mean roof height, natural ground and source status. Confirm model local axes versus wind cardinal directions.
- Compact expandable advanced inputs for coast distance (C/D), mixed terrain, topography/shielding, enclosure/openings/debris, parapet, tributary areas, roof local factors and dynamic response.
- `Calculate wind / Generate PDF` button, cancellation, recalculation on changed selected inputs, source-status details and **clear blockers**. Do all lookup + calculations + PDF generation asynchronously, retaining existing stale-run/PDF integrity safeguards.

Primary results UX remains PDF-only, displaying the `Flat roof!A1:N124`-mapped main PDF using exact workbook widths/section hierarchy/units, with an optional separate audit PDF. No giant raw trace table in primary WPF view. The user can inspect which factors were **AUTO LOOKUP**, **ENGINEER OVERRIDE**, **ASSUMPTION** or **UNRESOLVED** in PDF and/or expandable input source details.

## 7. PDF contract and source disclosure

Reuse `AsNz2021WorksheetProjection`, `AsNzWorkbookCellLayout`, `AsNz2021WorkbookPdfRenderer`, `WindPdfRenderService` and `WorksheetPdfExport`. Maintain existing 3-page main PDF; do not regress 14-column widths, original effective merges, From/To/Cp/W tables, signed audit, audit trace coverage or fail-closed sidecar publication.

- Populate basic worksheet rows for Region, Terrain, R, `VR`, eight-direction `Md` selection (main axis summary), `Mc`, `Mz,cat`, `Ms`, `Mt`, site speed, design speed, rho, Cdyn, ULS/SLS q basis.
- Wall and flat roof tables must show the actual source-resolved `Cp,e`, shape factors, `Ka/Kc/Kl/Kp`, external and net pressures by appropriate zone/case. Make `q` distinct from surface pressure with `Cshp`.
- Output provenance: exact private dataset bundle ID/hash, edition+amendments, applicable source locators/record IDs, input review and qualification statuses, model/datum/orientation/geometry evidence, any assumptions and missing inputs.
- If a selector is unresolved, print `INPUT REQUIRED`, `SOURCE REVIEW REQUIRED`, `NOT APPLICABLE` or `BLOCKED`; never substitute workbook cached values or 0.00 merely to fill a blue PDF cell.
- Both PDF and `.integrity.json` remain bound to the immutable request, model, selected dataset revision and rendered PDF bytes. Changing Region, Terrain, R, roof pitch, model orientation, rights/edition or source-bundle hash invalidates previous PDF and requires recalculation.

## 8. Test-first numerical and applicability acceptance

Add RED regression evidence before implementation. All production code for lookup logic must be testable without CSI or PDF engines.

1. **Synthetic private-like rights-cleared data** (NOT actual normative coefficients) tests region->VR, eight bearing-specific Md, Mc, terrain/height interpolation, model X/Y orientation, Vdes sector maximum and ULS/SLS separation. Test height boundaries, exact grid points, 0/360 wrap, ±45 boundaries and forbidden extrapolation.
2. Region A0 exception, non-eligible region, incorrect AU/NZ country, unmapped subregion, missing C/D smoothed-coastline inputs, duplicate/conflicting source rows, wrong edition or amendments, wrong pressure system, outside range, and missing right to local extraction must fail closed.
3. Actual user-example **arithmetic** must still reproduce source workbook comparison cells as a **legacy arithmetic fixture** only (not automatic source qualification). Keep source workbook/private tables out of CI; do not demand that independently selected 2021 coefficients equal legacy cached values.
4. Flat-roof 0-degree synthetic input: four directions, both limit states, plus/minus internal states, relevant wall/roof zones, external/internal/net signs and trace IDs. Control roof-pitch transitions and disallow mismatched Monoslope 14-degree through flat path.
5. Explicit C/D low-rise ULS internal-pressure branch, windward height-dependent Cpe, roof-table alternatives, roof h/d branch, leeward d/b branch, A0 terrain special handling, mixed terrain averaging, unsupported dynamic response, local/tributary-area factors and Kc interplay.
6. Mixed/missing source entries produce **partial diagnostic PDF** but no fabricated calculated pressure for affected cases. A true calculated review PDF must be generated from a fully populated authorized **local** dataset only when rights and amendment inputs are present; otherwise prove the architecture with synthetic data and mark it prominently as synthetic.
7. WPF smoke: select Region, Terrain 2, R 500/25, verify auto lookup input assembly; change Region/TC invalidates PDF; cancellation and tamper block export; no UI freeze; main and audit PDF paths both work. Real physical CSI testing remains separate and pending.
8. Main PDF renders in workbook order `A1:N124`; source metadata and numeric values are legible; no duplicated/missing zones, clipping, incorrect subscripts, unit mixups or hardcoded `14-degree` footer for a 0-degree case.
9. Preservation: all existing 460 nonphysical tests plus nine WPF smoke modes, Debug/Release, locked restore, and PDF all-page visual QA; run red/green provenance checks, no numerical fixture data from copyrighted sources in public commits.

## 9. Deliverables and stop condition

Deliver (a) working owner-selectable WPF input workflow and source-aware lookup engine, (b) actual 0-degree Flat Roof auto-lookup main/audit PDF samples from rights-cleared test fixtures, (c) local-only owner-source end-to-end example **if legally permitted and supplied**, (d) traceable applicability and failure diagnostics, (e) test / visual results, and (f) a public-safe owner list of additional data or site details needed to enable local real-standard review.

Run locked restore, Debug/Release builds, full tests, 9+ WPF smoke cases, and report QA. Commit and push public-safe source changes to `main` only after verified builds; report exact SHA and corresponding **completed successful** GitHub CI run. Do not stage private source PDF, licensed data tables, private dataset files, screenshots containing protected tables, or unrestricted full report exports from such data.

**Acceptance is scoped:** `AUTO LOOKUP FEATURE IMPLEMENTED / PRIVATE REVIEW MODE`; NOT `PRODUCTION NORMATIVE VERIFIED`. Production normative pressure gate, real CSI model writes/assignments and professional engineering approval remain **disabled** until a separate independent source/engineering qualification and owner authorization. Do not claim full auto-lookup completion without a genuine authorized dataset, and do not stop at another checklist or UX mockup: implement the working selector and UI within this sprint.

Stop after this bounded sprint and submit a verified completion handoff. Do not start ASCE or gable/hip scope without separate owner instructions.
