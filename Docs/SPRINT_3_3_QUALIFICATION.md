# Sprint 3.3 qualification — 2026-10-20

## Result and boundaries

Geometry contract fixes and automated WPF regressions pass. A real ETABS 23.2.0 roof editor exercise also passed the application workflow checks below. **Full independent physical qualification remains inconclusive:** no independently reviewed GUI coordinate/area/intersection fixture was supplied for this selection. Successful getters and application-generated comparisons alone do not establish physical accuracy.

The owner authorized PID 34456 and manually selected objects after an initial EmptySelection result. The application did not change that selection, geometry, units or files. Multiple ETABS instances were running; an ignored local qualification host attached exactly that PID using the installed `cHelper.GetObjectProcess` contract through the existing adapter session factory. Production attachment still uses `GetObject` and has no PID selector. No vendor binary or private model data is published.

## Physical ETABS matrix

| Check | Actual result | Qualification limit |
|---|---|---|
| Authorized running attachment | Passed, ETABS 23.2.0, PID 34456 | Model path/name and full identity hash retained privately |
| Selected scan | Succeeded; 12 frames, 10 areas; no read issues | IDs/XYZ stored privately; no selection setter invoked |
| Selected area IDs | 101, 20, 38, 63, 80, 83, 88, 94, 97, 98 | These identifiers alone contain no customer/model identity |
| Global XYZ / Local 3 / SI reads | All 10 shells processed as valid; present units N_mm_C | Independent GUI numeric comparison pending; units were never changed |
| Opposite Local 3 | Not exercised by this selection | All selected axes share one direction within floating-point tolerance; tiny numeric differences are not distinct orientations |
| Plane reconstruction | One sloped coplanar surface reconstructed | Multiple distinct live planes pending; two-plane gable covered by synthetic regression |
| Existing selected loads | 122 records returned | Not independently requalified against GUI in this sprint |
| Actual WPF UV editor / 3D viewer | Opened and rendered using the real selected geometry | Local captures remain private |
| Containment | Shell-footprint polygon classified FullyInsideZone | Self-derived polygon, not independent metrology |
| Crossing | Translated local draft polygon classified CrossesZoneBoundary / NeedsSplitInCSI | No ETABS shell was modified or subdivided |
| Overlap | Duplicate local draft zone classified OverlappingZones with structured coverage | Distinct from OverlapsAnotherShell; future pressure mapping blocked |
| Two wind-state drafts | Independent WX+ and WY− local geometry saved/switched | No engineering pressures or pressure approval |
| Process restart / unchanged rescan | Passed in new WPF OS processes, same database; exact polygon identity/coordinate hashes preserved | Does not prove changed-source physical detection |
| Changed-source stale detection | Pending physical check | Prohibited CSI modifications were not performed; deterministic mock regression passes |
| Safety gates | Approval, load preview and assignment controls disabled; no assignment or pressure approval | Getter-only injected session rejects non-allowlisted methods |
| SAP2000 | Pending | No SAP physical claim from this ETABS exercise |

Initial live editor process: 3932. New-process restoration: 24344; additional 3D/restoration rendering: 35284. All exited successfully. Geometry and databases remain under ignored `artifacts/sprint33/live-editor-private/`; raw scan under ignored `artifacts/sprint33/live-scan-private.json`. These are local evidence references, not files committed or uploaded by CI. No independently reviewed physical baseline was created from these application outputs.

## Automated evidence

- Locked restore; Debug and Release solution builds, zero warnings/errors.
- 201 automated tests, 0 failed, 0 skipped, `Category!=PhysicalCsi`; 17 more cases than the 184-test Sprint 3.2 baseline. Physical tests remain explicitly opt-in and outside this total.
- Regression cases cover all corrected classification states, label-only semantics, audited exclusion, per-zone coverage, opaque IDs containing commas, SQLite restart, conservative legacy interpretation, unsupported versions and atomic failure handling.
- TDD reproduced the original classification/label defects, partial-batch publication, and unsupported-version editor clearing before their fixes. Full suite passes after the fixes.
- Six actual WPF processes: demo, mock read-only, roof initial/reopened, polygon editor initial/reopened. Includes actual controls/drag events, coverage and overlap blocker assertions, adjustable viewing area, state switching, disk restart, stale-source mock and safety controls.
- Synthetic WPF captures and process results: `artifacts/sprint33/wpf-final/0e932d406371438eb3ab8da405323125/`. Synthetic evidence is explicitly marked MOCK; it is not live qualification.

## Remaining gates before calculation integration

Obtain independently reviewed GUI values for the authorized disposable selection and complete strict comparisons, multiple planes/opposite axes, and externally prepared changed-source tests without application mutations. Separately qualify SAP2000. Obtain rights-cleared exact ASCE 7-22 and AS/NZS 1170.2:2021 data revisions plus independent engineer-reviewed reference calculations and coefficient traces. Geometry review must remain separate from engineering pressure approval. Production calculations, proposals, approvals and real CSI writes remain disabled in RealReadOnly.
