# AS/NZS 1170.2:2021 automatic review lookup coverage

This register separates software regression evidence, owner-workbook arithmetic and independent engineering qualification. None enables production normative calculation, pressure approval or CSI mutation.

## Source and rights disposition

The owner-provided base-2021 publication was inspected read-only, including a rendered visual comparison of Tables 5.2(B), 5.2(C) and 5.3(A)-(C). The workbook is a layout/arithmetic reference, not an independently established 2021 applicability benchmark. No protected numerical table has been added to public source or fixtures.

A local private source register is held under the ignored directory `fixtures/standards/private/asnz1170-2-2021/base2021-review/`. Rendered source-page evidence stays under `artifacts/standards-private/`. The observed publication footer permits temporary download and limited printing; it does not establish extraction, storage or software-reuse permission. Consequently the real numerical dataset remains **BLOCKED / SOFTWARE-USE RIGHTS REQUIRED**. Base-2021 source inspection does not establish adoption of later amendments.

## Table coverage register

PDF page numbers below are one-based; the publication's printed page is ten lower. Applicability observations are source inspection, not independent engineering certification.

| Source | PDF page / printed page | Generic software scope | Real-source qualification |
|---|---|---|---|
| 3.1(A) AU regional speed | 35 / 25 | Exact country, region, limit state and return period; coast exceptions require explicit evidence | Blocked: rights, source package and independent benchmarks |
| 3.1(B) NZ regional speed | 36 / 26 | Separate country/region identity; no AU fallback | Blocked: same |
| 3.2(A)/(B) direction | 37 / 27 | Eight bearings and explicit model-to-cardinal orientation | Blocked: same |
| 3.3 climate | 37 / 27 | Exact region scope; no country-wide fallback | Blocked: same |
| 4.1 terrain | 41 / 31 | Declared interpolation; mixed terrain and special regional rules need separate evidence | Blocked: same |
| 4.2 shielding | 42 / 32 | Shielding eligibility and parameter evidence required; explicit no-benefit assumption remains distinct | Blocked: same |
| 5.2(A) windward | 59 / 49 | Support classification and height/speed basis must be explicit | Blocked: same |
| 5.2(B) leeward | 60 / 50 | Gable/hip form, incidence, pitch and ratio; unsupported monoslope cannot inherit gable rows | Blocked: same |
| 5.2(C) side wall | 60 / 50 | Independent clipped distance bands, no negative physical widths | Blocked: same |
| 5.3(A) roof | 61 / 51 | Source-defined low-slope U/D, crosswind gable and downwind monoslope routing; complete face geometry still required | Blocked: same |
| 5.3(B) roof | 61 / 51 | Eligible upwind routing at and above the exact ten-degree boundary; generic grids retain alternatives | Blocked: same |
| 5.3(C) roof | 61 / 51 | Eligible downwind and crosswind hip routing; breadth/depth distinct from inverse; complete face geometry still required | Blocked: same |

## Roof-method source observations

- Pitch alone is insufficient. The selected roof form and wind incidence must establish the source's U, D or R identity.
- Table 5.3(A)'s all-pitch column covers crosswind gable slopes and downwind monoslope R slopes. Its U/D column is restricted to pitch below ten degrees.
- Table 5.3(B) starts at exactly ten degrees for an eligible upwind U slope. Table 5.3(C) starts at exactly ten degrees for downwind D and crosswind hip R slopes.
- Table 5.2(B)'s crosswind gable branch differs from hip Note 3; monoslope applicability is not supplied by that table.
- The steep-slope branch in Table 5.3(C) uses breadth/depth. It must not be evaluated using depth/breadth without inversion.
- The source requires same-sign interpolation in height/depth for Tables 5.3(A)-(C), and coupled upwind alternatives with the downwind case. A generic bilinear interpolator alone is not proof of these restrictions.
- Roof-face geometry, ridge/slope direction and cardinal wind bearings remain separate evidence. No automatic full-building or roof-plane qualification follows from a selected-shell extent.

## Numerical verification matrix and evidence classes

| Evidence class | Required cases | Current meaning |
|---|---|---|
| A: synthetic software regression | Exact and interpolated terrain; AU/NZ identity; rotated four wind directions; ULS/SLS; separate internal signs; roof boundaries below/at/above ten degrees; invalid forms/incidences; no extrapolation; tampered source; forged provenance | Invented operands validate code paths only. Public tests must label them synthetic |
| B: owner workbook arithmetic | Eight cached-value comparisons on the owner's Flat roof and Slope Roof sheets: ULS/SLS speed products and velocity-pressure bases | **8/8 passed**, absolute tolerance 1e-10; private evidence only. Not standards applicability, rights or benchmark proof |
| C: independently established applicability | Authorized base/amended source package, exact roof/incidence table rows, independent AU/NZ numerical cases and signed review | **Pending**; no independent numerical benchmark or source-use rights evidence supplied |

Independent reference cases must cover regional 25/500-year speeds, direction sectors including rotated axes, climate, terrain limits/interpolation, shielding eligibility, windward support/height methods, leeward ratio/pitch interpolation, clipped side bands, A/B/C roof incidence branches and pressure alternatives. Each requires units, source locators, intermediate values, tolerances, revision/adoption decisions and reviewer evidence distinct from transcription.

The private workbook comparison is recorded in `artifacts/standards-private/base2021-review/workbook-arithmetic-parity.json`. It recomputes the actual saved worksheet operands, without executing macros or changing the workbook. Both sheets' K34/K35 formulas are velocity-pressure bases; shape factors have not yet been applied. Source PDF and workbook SHA-256 values were unchanged after inspection. These eight local comparisons are separate from automated CI test totals.

Routing and generic interpolation are partial software coverage. They do not supply missing roof-face geometry: a full gable/hip multi-face calculation matrix remains blocked until each face and wind incidence are established. A supported monoslope external review result does not establish a supported monoslope leeward-wall coefficient or a complete net-pressure design.

## Implemented review contracts

`RoofFormAutoReviewV2` records explicitly scope country, region, procedure, method, roof form and limit state. Roof coefficients also require the face, actual pitch, positive h/d and a matching source table. The steep Table 5.3(C) route requires b/d explicitly. Single-face gable/hip source lookup is implemented; assembling a whole-building multi-face result remains blocked. Unsupported oblique and monoslope crosswind incidence does not borrow a gable branch.

Regional speeds retain separate return periods and limit states; direction factors retain eight bearings and four rotated model directions. Climate uses exact regional scope. Terrain supports declared one/two-dimensional source grids, including intermediate terrain only within supplied domains. Shielding requires explicit eligibility and a parameter/height source grid; ULS/SLS factors remain distinct. The WPF guided path currently claims no shielding benefit instead of offering parameterized shielding. A0 special exposure, coast exceptions without supplied domain evidence, terrain transitions and separate windward/internal velocity-pressure height methods remain blocked.

Windward source scope includes support and speed-evaluation basis. Monoslope leeward results are blocked because the supplied Table 5.2(B) evidence does not establish that applicability. Missing internal-pressure evidence blocks net pressure while preserving independently available external review arithmetic. B/C selections use whole-face extents; A and sidewall bands are physically clipped. Both source alternatives remain separate.

Direct selectors check V3/component coherence and SI units. Private source instances require the loader's hashed rights/adoption evidence receipt. Automatic request provenance is process-local and bound to the exact immutable request; imported JSON and edited copies must rerun lookup. Receipts establish a software execution path, not legal rights or independent engineering acceptance. Legacy flat fixtures remain synthetic only. Interpolation audit records include source keys, endpoint operands, values and weights.

Guided WPF inputs distinguish true-north review, uphill model bearing, roof form, pitch, eave/peak levels and mean height. An eleventh smoke scenario exercises the sloped partial-results workflow through actual bound controls. The worksheet retains fourteen columns, blue numerical values, separate From/To/Cpe/W fields, SI arithmetic with kN/m² presentation, explicit N/A/NOT CALCULATED and a complete detailed audit. Rendered synthetic PDFs are software evidence only.

## Remaining release gates

Local software verification: locked restore succeeded; Debug/Release builds had zero warnings/errors; 550 nonphysical tests passed in each configuration; eleven WPF smoke processes passed. Actual synthetic worksheets contain three flat and four sloped pages. Their detailed audits contain 290 and 206 pages respectively. All seven main pages were rendered for visual inspection; audit visual sampling covered first/middle/last pages, separate from full text extraction and integrity checks. Owner visual acceptance and engineering review of these outputs remain outstanding.

Real numerical lookup needs documented extraction/storage/software-use rights, a private immutable and byte-coherent V3 bundle, table-specific amendment/adoption decisions, complete internal-pressure/site/geometry applicability and independent benchmark review. Missing evidence remains a structured blocker. Source hash verification proves byte identity only. Physical ETABS/SAP2000 roof validation and owner visual acceptance of worksheet/audit exports remain separate activities.
