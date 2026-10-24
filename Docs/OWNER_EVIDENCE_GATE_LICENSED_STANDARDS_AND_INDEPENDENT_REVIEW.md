# Owner Evidence Gate - Licensed Wind Standards and Independent Engineering Review

**Project:** Structural Load Assistant / STR-AutoWind  
**Repository:** https://github.com/lammach99-oss/STR-AutoWind  
**Windows repository root:** `D:\Project\Etab-Sap Tool\AutoWind`  
**Full intended repository path for this prompt:**  
`D:\Project\Etab-Sap Tool\AutoWind\Docs\OWNER_EVIDENCE_GATE_LICENSED_STANDARDS_AND_INDEPENDENT_REVIEW.md`

**Latest independently verified `main` commit:** `076d9e6cdf9ed4ad3dd0820d6fca26d37309ff02`  
**Verified GitHub Actions:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39957627758  
**Verified CI:** 325/325 non-physical automated tests passed, 0 failed/skipped, build with zero warnings/errors. WPF smoke workflow completed successfully.  
**User-facing milestone:** Sprint 4B.0.1 has ended. No production normative wind calculator is qualified.

## 1. Role and explicit stop boundary

Act as a standards-data intake coordinator and evidence auditor, not as a production calculator implementer. Produce a practical owner-and-independent-engineer evidence handoff using the already implemented `StandardsDataBundleManifest`, `StandardsIntakePreflightReport`, `GenerateIntakePreflight`, reference-calculation contracts, and existing documentation.

**Do not begin another source-code sprint.** The previous task expressly stopped implementation until valid licensed primary sources and independent engineering review evidence are available.

Do not:

- Invent or infer normative wind speeds, coefficients, design pressure formulas, amendment applicability, or engineering reference answers.
- Claim an independent engineer has reviewed or signed anything based on an `IsIndependentReview` flag, a reviewer name, or a matching SHA-256 alone.
- Enable `CanCalculateNormativePressures`, production pressure previews, engineering approvals, or any live CSI writes.
- Create or alter a customer ETABS/SAP2000 model, perform automated model selection mutation, or reuse an old PID.
- Copy licensed standards contents, coefficient tables, full clauses, protected figures, customer data, signed memos, or workbook extracts into public Git, issues, CI artifacts, chat transcripts, or logs.
- Create fake review memos, fake license claims, or synthetic calculation values labeled normative.
- Commit, push, force-push, or create a PR unless the owner separately authorizes a specifically scoped, public-safe maintenance change.

If required external evidence is not available, give a precise **BLOCKED / OWNER ACTION REQUIRED** report and stop. Do not compensate by adding more software features or more synthetic tests.

## 2. Verified baseline and scope distinctions

The last agent reports 125/125 private physical ETABS 23.2.0 benchmark checks passed on a disposable roof model. This is **agent-reported private evidence**, not externally inspected structural-engineer certification. It tests fixture-directed geometry, CSI readback and intersections, not automatic discovery on arbitrary roofs, normative wind pressures, or SAP2000 27.

Sprint 4B.0.1 public CI verifies 325 non-physical automated tests. It does not demonstrate the availability, licensing, correctness, or independent approval of ASCE/SEI 7-22 or AS/NZS 1170.2:2021 coefficient data.

Existing `GenerateIntakePreflight` readiness and positive hash flags concern defined software checks only. `EvidenceHashVerified` proves local file-byte/hash consistency, not the authenticity or engineering adequacy of the underlying review. `ReferenceCalculationAvailable` is not evidence that an independent reference calculation was numerically executed and passed. `ReadyForIndependentReview` is not `EngineerReviewed` and never means production calculation is authorized.

**Source-review note for future maintenance only:** The Sprint 4B.0.1 `Reg1` test changes a record's `Unit` without updating original component bytes and canonical manifest hash. That simultaneously triggers additional integrity issues, so it is not an isolated test of a single `Error`-severity condition. Record this as a test-evidence caveat, but do not open a new sprint merely to increase test counts.

## 3. First operation: local metadata-only inventory

Use the current `origin/main` read-only. Check only whether these local paths exist, without opening protected source documents until the owner has confirmed lawful access and authorized inspection:

- `D:\Project\Etab-Sap Tool\AutoWind\fixtures\standards\private\`
- `D:\Project\Etab-Sap Tool\AutoWind\artifacts\standards-private\`
- `D:\Project\Etab-Sap Tool\AutoWind\Reference Workbook\` (historical reference material, **not** automatically a licensed primary standard or an independently approved source)

Record inventory metadata privately: logical package ID, source family and claimed edition, basename (avoid personal names in a public report), extension, size, availability, and local SHA-256 if permitted. Do not print file content or excerpts. A file's presence is **not** a license, approval, or coefficient validation.

If those directories do not exist or contain no authorized primary materials, mark all normative intake rows `MISSING` and stop after the owner checklist.

## 4. Owner evidence checklist: ASCE/SEI 7-22

Ask the owner to provide or confirm, through a private lawful channel:

1. **Legal access / authorized use:** Identity of the legitimate publisher/source, edition, lawful license held by the relevant organization, and affirmative permission for local extraction, private storage, and software-assisted use. Record only a license reference and constraints in the shareable report.
2. **Governing basis:** Applicable jurisdiction / building code adoption basis, risk category, design level, wind speed map/data basis, and applicable supplements, errata or jurisdiction-specific amendments. Do not assume an edition or supplement applies just because a file name mentions it.
3. **Scoped procedure:** Choose one precisely documented engineering method for initial qualification, such as an applicable MWFRS directional roof procedure, versus C&C or envelope methods. Do not conflate systems, methods, or editions.
4. **Coefficient provenance:** Independently verify the applicable primary-source locators and assumptions for velocity pressure, directionality, exposure, topography, elevation, gust effects, enclosure/internal pressure, external roof/wall pressure, zoning, heights, pitch and wind directions. Reference licensed source locators only in public-safe documents; retain actual coefficients privately.
5. **Independent reference cases:** At least one flat roof and one gable roof (with appropriate wind directions), covering separate internal-pressure signs and intermediate steps and units. Cases must be calculated outside the production software and checked by a truly independent engineer.
6. **Independent review:** Reviewer qualifications and organization, full review scope, signed or otherwise independently verifiable artifact, review date, exact reviewed source/data revision, and evidence SHA-256. Verify authenticity separately; a hash match alone is insufficient.

Status of every item must be one of `MISSING`, `SUPPLIED_UNVERIFIED`, `VERIFIED_FOR_LOCAL_INTAKE`, or `ENGINEER_REVIEWED_WITH_EVIDENCE`. Never assign the last status without independently demonstrated review.

## 5. Owner evidence checklist: AS/NZS 1170.2:2021

Require the same rights, data lineage, and independent-review separation, plus:

1. Written primary-source confirmation of governing national/jurisdictional application and actual applicability of any amendments, including items labeled Amendment 1:2023 and Amendment 2:2024. This is a **check to perform**, not an assertion that both automatically govern.
2. Provenance and selected design context for regional wind speed, return period / limit state, wind direction, terrain/height category, shielding, topography, climate, dynamic assumptions, and external/internal aerodynamic cases.
3. Individually reviewed values for the selected procedure and flat/gable roof zones, including wind normal/parallel to the ridge and plus/minus internal pressure alternatives as applicable.
4. Independently hand-checked numerical cases with intermediate factors, explicit units, pressure sign conventions, assumptions and tolerances. Keep all protected values private.
5. Independent qualified engineer review artifact, matching the exact dataset snapshot and all claimed amendments.

Do not reuse historical spreadsheets as authoritative standards without confirming source edition, formulas, VBA, supersession and licensing. Private workbook parity is not independent qualification.

## 6. Private package structure after authorization

For each authorized dataset, use a unique ID below the ignored private root, for example:

```text
D:\Project\Etab-Sap Tool\AutoWind\fixtures\standards\private\
  asce7-22\<dataset-id>\
    manifest.json
    <private-coefficient-components>.json
    <private-reference-calculation>.json
    <private-review-evidence>.pdf
  asnz1170-2-2021\<dataset-id>\
    manifest.json
    <private-coefficient-components>.json
    <private-reference-calculation>.json
    <private-review-evidence>.pdf
```

Use `CanonicalHashAlgorithmVersion: "3.0"` for new candidates; independently verify component raw-file SHA-256 and V3 canonical semantic hash. Keep original source-document fingerprint and the reviewed revision. The actual file layout may be adapted to existing typed contracts, but avoid inventing new schema fields without an explicit, reviewed reason.

**Do not create populated private coefficient files or a fake signed memo just to fill this folder.** Blank public templates may guide human preparers; production candidate values must come from authorized primary sources.

## 7. Read-only validation only when actual authorized data exists

Only after valid licensed source access and the owner authorizes working with the private files:

1. Verify source provenance and allowed use, then inspect the supplied manifest and declared component files locally without sharing the contents.
2. Load the bundle through existing typed loader functions. Do not bypass exact file SHA-256, canonical V3 hash, byte-to-record coherence, or procedure applicability checks.
3. Run `StandardsDataBundleValidator.Validate()` and `GenerateIntakePreflight()` with the explicitly selected procedure, raw component directory and actual evidence-file path.
4. Validate reference-fixture structure and trace; separately arrange independent numerical evaluation and review. No app-derived expected values; no self-approval.
5. Distinguish `EvidenceFileStatus.Verified`, `HumanReviewStatus.EvidenceHashVerified`, and actual independent engineering authorization. The present code must not report normative calculations as eligible.
6. Keep any generated private preflight JSON, hashes, or detailed issue output in `artifacts/standards-private/`, not public CI or chat.
7. Report blockers with issue code, category and remediation **without** copying protected coefficient values or sensitive paths.

Do not treat a `ReadyForIndependentReview` status as permission to implement or release production calculations. A future scope-specific engineering sign-off and separate software authorization gate are required.

## 8. Deliverables for the owner

Produce a short human-readable owner package using **no unlicensed content**:

- `OWNER_ACTION_REQUIRED` or, only if real authorized evidence is supplied, `READY_FOR_PRIVATE_PREFLIGHT`.
- An independent intake table with one row per ASCE and AS/NZS required evidence category and its factual status.
- Clear distinction between software checks already passing, private ETABS benchmark evidence, pending SAP2000 qualification, licensed data availability, human review, and normative calculator authorization.
- Exact local private folder for each *existing* package; omit sensitive filenames from shareable reports.
- A list of the documents and human decisions the **owner** must obtain before a standards qualification sprint can be considered.
- A list of independently checked flat/gable scenarios needed from the external engineer, with procedure IDs, units, zones, wind states and tolerance fields; **no guessed pressure numbers**.
- The specific legitimate next decision: (a) source/license missing -> stop; (b) data present but unreviewed -> commission reviewer; (c) data and independent evidence ready -> propose a separately authorized, strictly scoped numerical qualification project.

The output should be a concise owner handoff, not a misleading new "Sprint 4B complete" claim.

## 9. Completion rule

**No coding, no synthetic coefficient population, no new production functionality, no automatic Git push.** Do not create further sprints without new source evidence or explicit owner direction. If external inputs remain absent, hand off the exact checklist and stop.
