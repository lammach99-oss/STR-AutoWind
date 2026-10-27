# AS/NZS private source-to-lookup workflow — Sprint 4B.8

Technical reference: **AS/NZS 1170.2:2021 BASE TEXT — REFERENCE/REVIEW ONLY**. This is not a statement of governing code adoption, publisher permission, independent engineering acceptance or production qualification.

## Actual owner-source inventory and coverage

The existing publication, table references and workbook were inspected read-only. The publication has 124 PDF pages; its footer identifies temporary download and limited printing permissions. Those terms do not establish software extraction/storage/reuse permission. No such permission or project-specific jurisdiction/amendment decisions were found in the owner-referenced `Docs` directory. Implementation authorizations there explicitly distinguish software work from legal rights and governing adoption.

The ignored version `fixtures/standards/private/asnz1170-2-2021/base2021-stage-v1/` was created by the new command using the already supplied sources. It contains source inventory/hashes, read-only reference copies, a machine-readable schema, `source-staging.json` and a non-loadable `manifest.template.json`. It has **zero numerical cells transcribed, zero checked, zero engineer reviewed**. Its 13 `NOT_AVAILABLE` entries are table work-queue placeholders, not an estimate of how many numerical cells those tables contain. Numerical cell coverage is **not established**, and all 13 table groups are blocked. No country, region, terrain or roof method currently runs from an actual source-derived private dataset.

| Table group | PDF page / printed page | Actual numerical coverage |
|---|---|---|
| 3.1(A), 3.1(B) | 35/25, 36/26 | Preflight/schema only |
| 3.2(A), 3.2(B), 3.3 | 37/27 | Preflight/schema only |
| 4.1, 4.2 | 41/31, 42/32 | Preflight/schema only |
| 5.2(A), 5.2(B), 5.2(C) | 59/49, 60/50 | Preflight/schema only |
| 5.3(A), 5.3(B), 5.3(C) | 61/51 | Preflight/schema only |

Amendment 1:2023 and Amendment 2:2024 decisions remain UNCONFIRMED. Applied amendment identities require corresponding source documents and per-table decisions; no amendment numerical content was tested or ingested. No duplicate source upload or owner-authored dataset is requested.

## Commands and exit status

Run from repository root with the pinned .NET SDK:

```powershell
dotnet build src/StructuralLoadAssistant.Demo -c Release
$intake = 'src/StructuralLoadAssistant.Demo/bin/Release/net8.0/StructuralLoadAssistant.Demo.dll'
dotnet $intake --asnz-private-intake initialize `
  'fixtures/standards/private/asnz1170-2-2021/<new-version>' `
  'Reference Workbook/AS_NZS 1170.2.2021 - Wind load action.pdf' `
  'Reference Workbook/AS.NZS_Wind load.xlsm'
dotnet $intake --asnz-private-intake validate `
  'fixtures/standards/private/asnz1170-2-2021/<version>/source-staging.json'
dotnet $intake --asnz-private-intake package `
  'fixtures/standards/private/asnz1170-2-2021/<version>/source-staging.json' `
  'fixtures/standards/private/asnz1170-2-2021/<new-package-version>'
```

Exit 0 means software validation succeeded for private review; exit 2 means blocked/invalid; exit 1 means usage error. Initialize normally exits 2: it deliberately does not invent permissions, jurisdiction or coefficients. Intake commands run before any demo database or fake assignment is created. Initialize/package refuse existing output versions. Packaging captures a validated snapshot and evidence bytes, writes privately, validates again and renames into a new version atomically. Failed packaging never authorizes loading an incomplete dataset.

## Source-cell lifecycle and review contract

`NOT_AVAILABLE` contains no value. `TRANSCRIBED_UNREVIEWED` records a source transcription but cannot load for lookup. `SOURCE_CELL_CHECKED` requires a separately identified second reading, bound to the full cell hash, primary-source SHA-256, exact observed value and hashed reconciliation evidence. `ENGINEER_REVIEWED` additionally requires an evidence document; it remains documentary metadata, not authenticated independent certification or production authorization.

Each cell retains source document ID and byte hash; edition; table/page/printed page/row/column; alternative and face; procedure/country/region/limit state; numerical operands and SI units; sign/interpolation; restrictions; preparer; second checker; and evidence hashes. Unknown categories, missing axes, unsupported roof forms, contradictory A/B/C routes, unknown/duplicate amendment identities, face/case conflicts, unsafe/reparse paths, duplicate JSON properties and edited values fail closed.

Documented rights decisions must separately address numerical extraction, private storage and software-assisted use, be bound to source hashes and name the evidence reviewer/reference. A truthy `SoftwareUseAuthorized` flag is insufficient. The software verifies declarations, bytes and scope; it cannot determine whether a purported license is authentic or legally adequate. An owner/evidence reviewer must resolve that outside the software.

## Package and existing review integration

The V3 manifest/components remain the canonical numerical bundle. The package also contains reconciled staging, source/evidence copies and an authorization reference bound to the staging SHA-256. The existing `AsNzPrivateAutoSourceLoader` verifies those before issuing its process-local source receipt. It rebuilds the expected V3 bundle from the reconciled cells and rejects any changed record/manifest, even when the attacker recomputes matching raw/V3 hashes. Legacy private bundles without reconciled staging now fail closed; recreate them through the pipeline rather than adding a boolean bypass.

Use WPF **Load private dataset manifest** on the resulting `manifest.json` after legitimate rights/adoption and cell checks. Existing Flat Roof review inputs, background calculation, cancellation/stale invalidation, source-scoped selection, immutable worksheet/audit PDF identities and disabled production gates remain in place. Source hash, cell status, row/column/case and interpolation evidence flow through selected record → request coefficient evidence → calculation trace → audit PDF. Missing source categories or internal-pressure evidence continue to block affected results; there is no synthetic fallback for private packages.

No formulas, normative coefficients, engineering approvals, real CSI adapters or SQLite draft formats changed in this sprint. Existing blocked A0/coastal/mixed-terrain/separate-height methods, unsupported monoslope leeward and full-building gable/hip scopes remain blocked. Synthetic packaged rows exercise the integration; they are not real licensed data or independent benchmarks.

## Workbook and PDF evidence distinctions

`scripts/check-asnz-workbook-arithmetic.py` independently recomputes eight cached speed/velocity-pressure rows from the supplied workbook's Flat roof and Slope Roof sheets. Values are read only and results can be written only below `artifacts/standards-private`. The local run passed **8/8 at absolute tolerance 1e-10**, tagged `WORKBOOK_ARITHMETIC_CHECK`. It is not full A1:N124 row parity or normative source validation; remaining numerical rows are unverified. Source/workbook hashes matched their prior inventory. No private numerical operands are printed or committed.

The synthetic flat/sloped WPF worksheets and audits continue to be generated for software verification. There is **no real source-derived example PDF** while numerical extraction is blocked. Text extraction, byte-sidecar checks, all main-page rendering and representative audit-page inspection are software evidence only; owner visual acceptance and independent engineering review remain separate.

## Concrete owner decisions to unblock protected extraction

1. Identify the documentary source-use terms/permission covering numerical transcription, private structured storage and software-assisted review. Implementation approval or possession alone does not establish that scope.
2. Select AU/NZ project jurisdiction and provide table-specific base-2021/Amendment 1:2023/Amendment 2:2024 decisions and supporting text. No global country/adoption default is assumed.
3. Arrange the separate source-cell reconciliation and independent numerical benchmarks, including enclosure/internal pressure, reviewed geometry/true north and permitted roof scope. Hashes, workbook parity and CI do not substitute for these.

The coding agent will populate the dataset from the existing sources when these conditions permit. Production normative qualification and live CSI writes remain disabled throughout.

## Verification at this revision

Locked restore and final Debug/Release solution builds passed with zero warnings/errors. Each final nonphysical suite passed 586/586 tests (33 new cases); all eleven Release WPF smoke scenarios passed. An initial overlapping Debug test run timed out in PDF rendering; a serial rerun passed without weakening renderer limits. Private RED/GREEN TRX and smoke evidence is retained under `artifacts/sprint4b8`.

Synthetic flat/sloped PDF main reports contain three/four pages; all seven pages were rendered and visually inspected. The separate 290/206-page audits passed nonempty-text, SHA-256 sidecar, 320-case-ID and 1509/933-trace-ID checks, with representative audit pages visually inspected. No claim of visual review of every audit page or independent engineering approval is made. Actual source-derived examples remain unavailable.
