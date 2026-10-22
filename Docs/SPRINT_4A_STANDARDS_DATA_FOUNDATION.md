# Sprint 4A - Wind Standards Data Foundation, Provenance, and Qualification Gates

**Agent role:** Lead structural-engineering software architect, independent QA engineer, and senior C#/.NET 8 developer.

**Repository:** https://github.com/lammach99-oss/STR-AutoWind  
**Branch policy:** Work directly on `main` after fetching `origin/main`. No pull requests, feature branches, force pushes, or history rewrites.  
**Last independently verified baseline:** `1f2650b4f876321903436eb8de628d4c3476d956`  
**Verified CI:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39724837818 - successful, 233 non-physical automated tests, zero failed/skipped, clean build and WPF smoke.

## 0. Engineering baseline and trust boundary

The previous agent **reports** a successful opt-in ETABS 23.2.0 qualification on a disposable gable benchmark: 125/125 physical comparisons, none missing or failed, exact analytical geometry, correct reversed shell winding and opposite Local 3, existing load readback, and a reopened local `.EDB` file. Code and public CI were independently inspected, but the private model, physical-result JSON, raw ETABS tables, and independent human review were **not** available to the reviewing assistant. Record these as distinct facts:

- **Software automated verification:** independently confirmed CI passed.
- **Physical benchmark execution:** agent-reported Passed, based on private local evidence; do not turn this into an external reviewer certification without inspecting evidence.
- **Independent engineer sign-off:** not established by the public repository; remains a separate gate.
- **SAP2000 27 physical qualification:** Pending.
- **ASCE/SEI 7-22 and AS/NZS 1170.2:2021 numerical wind standards qualification:** Not Started / Blocked because no licensed, independently checked coefficient datasets are present.

The benchmark geometry pipeline is *fixture-directed* for roof-plane grouping. It does not prove automatic roof-plane discovery on arbitrary real models. Preserve this scope distinction.

**Scope of this sprint:** build standards-data contracts, provenance, validation infrastructure, applicability metadata, independent-reference fixture format, immutable lineage, and fail-closed readiness gates. **Do not build or unlock a production normative wind-pressure calculator.**

## 1. Start with a repository audit, not a duplicate implementation

Inspect and reuse the existing boundaries before editing:

- `src/StructuralLoadAssistant.Engineering/Standards.cs` (`AsceWindInput`, `AsNzWindInput`, `StandardCalculators`, and metadata-only standard boundary).
- Existing `StandardManifest`, `StandardsGate`, `DataProvenance`, `WindState`, `LimitState`, `Pressure`, `RoofPressurePrerequisites`, and related domain contracts; locate their definitions and current tests.
- WPF wind/roof/CSI mode gating and state transitions.
- SQLite immutable-snapshot, revision/CAS, and provenance contracts.
- `Docs/OPEN_ENGINEERING_ITEMS.md`, `Docs/IMPLEMENTATION_STATUS.md`, and the repo's QA checklist.
- Existing fixtures and package lock policy.

Write a short internal architecture map. Prefer additive, well-scoped types with versioned adapters instead of breaking current public signatures, drafts, reports, or UI bindings. Document any deliberate migration.

Important existing behavior: `StandardCalculators.Asce7_22` and `.AsNz1170_2_2021` currently return a gated result with no normative calculation. Preserve that behavior until future approved numerical datasets and review fixtures exist. In particular, the current AS/NZS path contains a hardcoded requirement for named amendments; treat its engineering applicability as **unverified**, not as a legal or normative conclusion. Record amendment support/applicability in data, validate against licensed reviewed references, and fail closed when unresolved.

## 2. A family-independent envelope without a false shared formula

Create strongly typed, serializable, versioned contracts for the *data envelope*, not a universal coefficient model. Common infrastructure can own identity, hashes, provenance and validation; calculation procedures and records must remain family-specific.

### Minimum common record concepts

- `StandardFamily`: ASCE/SEI 7 versus AS/NZS 1170.2.
- Exact edition identifier, publication/revision date, errata/supplement/amendment IDs, applicability decision and supporting source.
- Dataset ID, schema version, semantic revision, content SHA256, source-document fingerprint, import/verification timestamps.
- Procedure ID and applicability: scope, building geometry, mean/reference height, direction, design situation/limit state, dynamic/static assumptions, and explicit method selection.
- Parameter/value record: units, dimensions, sign convention, validity domain, optional piecewise ranges, interpolation policy (if authorized), and source clause/table/figure locator.
- Responsible data preparer and **independent** reviewer identity; evidence URI/hash, review date, and review scope. No simulated or automatically self-approved engineer sign-off.
- Rights classification: redistribution allowed / local licensed only / restricted / unknown; publication eligibility independent from engineering correctness.
- Coverage matrix describing every mandatory dataset category for a specific chosen procedure and whether actual reviewed rows exist.
- Exact input and coefficient provenance within a future result trace (not merely a top-level edition label).

Use an explicit `Unknown`, `Missing`, `Unsupported`, `Unreviewed`, `Invalid`, `ReadyForQualification`, `EngineerReviewed` state machine or similarly restrictive typed model. A boolean `IsApproved=true`, file name, version label or positive unit test count must **never** independently authorize engineering calculation.

### Family-specific domain boundaries

**ASCE/SEI 7-22:** support metadata and applicability for risk category, chosen velocity / wind-speed source, strength/service design context, exposure, reference height and elevation, topography, directionality, gust/dynamic assumptions, enclosure/internal pressure, MWFRS versus Components & Cladding, walls versus roofs, wind direction, roof cases and individual pressure-zone geometry definitions. Distinguish which procedure supplies which coefficient; never intermingle MWFRS with C&C or old-edition coefficients.

**AS/NZS 1170.2:2021:** support metadata and applicability for wind region, selected return period/design limit state, regional and site wind-speed inputs, directionality, terrain (including TC2.5 if supported by reviewed source), shielding, topography, climate, dynamic effects, internal/external aerodynamic coefficients, roof forms/pitch/wind-angle cases, and zone geometry definitions. Amendments must be represented explicitly. Determine actual amendment applicability from licensed primary documents, not by guessing or hardcoding an unconditional combination.

Where procedure applicability differs between standards, model it separately. Do **not** invent a shared roof edge/corner width rule.

## 3. Rights-cleared dataset packaging and manifest

Propose and implement a public schema (JSON Schema, if useful) and typed .NET parser/validator for a minimal standalone dataset bundle. Include:

- `manifest.json`: family, edition/revision, amendments, schema version, bundle ID, expected hash and component-file hashes, usage/redistribution rights, exact source locators, preparation and review evidence, mandatory coverage inventory.
- Family-specific coefficient/geometry record files with stable identifiers and explicit SI units. Do not publish real tables unless redistribution rights are positively documented.
- A separately supplied private local data-directory convention excluded from Git via existing or carefully extended `.gitignore`.
- Deterministic canonical serialization or clearly documented byte-exact content hash policy. Guard against hash self-reference, whitespace ambiguity, changed records, and manifest-component mismatch.
- Unique stable keys, deterministic record order and duplicate/conflict detection; amendable records must have unambiguous supersession lineage.
- Authenticity/reviewer-evidence boundary: a plain text reviewer name or unverified hash is metadata, not proof of independent engineering approval.

Store only **clearly synthetic, rights-cleared fixtures** in the public repo. Mark them visibly `TEST ONLY / SYNTHETIC / NOT STANDARDS-COMPLIANT`. No private workbook data, full clause text, figures, tables, copied maps, customer documents, or licensed coefficient data may enter commits, artifacts or CI logs.

## 4. Fail-closed validation and applicability

Implement an explicit validation report with individual issue codes, severity, location, offending record key, evidence pointer, and a machine-readable gate outcome. The validator must reject or block:

1. Missing or invalid manifest, unsupported schema/family/edition/revision, invalid amendment-applicability status.
2. Hash mismatch, tampering, invalid source lineage, ambiguous signed/reviewed state, unknown redistribution rights for any public export.
3. Missing mandatory procedure tables, missing rows at queried conditions, incomplete coverage, null or nonfinite values, incorrect dimensions/units/sign conventions.
4. Ambiguous procedures, contradictory domains, overlapping records that yield conflicting values, duplicate identifiers or wrongly merged edition/amendment data.
5. Out-of-range parameter inputs and extrapolation, unless a procedure-specific, independently reviewed explicit rule authorizes it.
6. Unsupported interpolation between records or across categorical boundaries, wind angles, direction cases, pitch cases, zones or procedure regimes.
7. MWFRS-versus-C&C conflation, enclosure/internal pressure not modeled as separate +/- alternatives, inappropriate limit-state combination, and unlabeled spatial orientation or normal direction.
8. Attempts to use test/demo data as production-verified normative input.
9. Missing independent reference calculation approval or unmet geometry/pressure-mapping prerequisites for future production execution.

Provide strict validation APIs usable without WPF and without CSI installation. Distinguish `InvalidData`, `NotApplicable`, `UnqualifiedData`, `MissingReferenceEvidence`, and `ReadyForIndependentReview`. A dataset with partially populated records must never become `ReadyForProductionCalculation` on the strength of a coarse manifest flag.

A reviewed numeric interpolation rule may be represented as policy metadata with method, bounds, dimensions, and review trace; this sprint may test infrastructure using synthetic numbers only. Do not implement normative pressure calculation from invented data.

## 5. Reference-calculation fixture contract and synthetic tests

Create a test fixture schema for future independent calculations including:

- Standard/procedure/version/amendment and individual coefficient IDs.
- Complete input scenario (SI, risk/design level, wind direction, internal-pressure alternative, case/zone, geometry), and applicability evidence.
- Expected individual intermediate factors, exact source references, interpolation steps, external/internal pressure sign interpretation, combined pressure, and allowed absolute/relative tolerances.
- Who created the calculation, who checked it independently, evidence file hash, and engineering sign-off state.
- Separation of expected analytical values from application-generated actuals; no self-reference from the engine under test.

For this sprint, publish **synthetic** fixtures only to test schema and gate behavior. Include meaningful negative mutation tests: remove a mandatory row, swap editions, delete a reviewer artifact, corrupt a file hash, duplicate a key, switch MWFRS/C&C, create incompatible amendments, alter units/sign, exceed applicability range, request unauthorized interpolation, and attempt synthetic-to-production promotion.

A green synthetic infrastructure test is **not** proof of ASCE/AS-NZS compliance. No expectation that ASCE or AS/NZS production calculated pressures are produced in Sprint 4A.

## 6. Hook into present workflow without enabling real calculations

Preserve the design distinction among:

1. Licensed-source identification and rights verification.
2. Dataset schema/integrity validation.
3. Independent normative coefficient/review qualification.
4. Procedure/site/building applicability.
5. Actual selected-shell geometry and source qualification.
6. Geometric polygon classification and engineer geometry review.
7. Future engineering pressure proposal/review/approval.
8. Future CSI write authorization and rollback qualification.

Retain the existing per-wind-state `WX+`, `WX-`, `WY+`, `WY-` mapping identity, plus positive/negative internal-pressure alternatives and roof cases/limit states as independent axes, not flattened strings or labels. Distinguish geometric roof plane outward normal from CSI Local 3. Keep structured zone geometry independent of normative coefficient determination.

RealReadOnly must continue to disable production wind calculation, preview/approval, and all live CSI assignments. Existing synthetic demo calculations and fake CSI writes may stay only behind obvious demo controls; no new path may promote a synthetic dataset to real use.

Do not break SQLite historical drafts, model-scoped save/restore, append-only provenance and CAS, or established WPF tests. Avoid introducing a native CSI SDK compile-time dependency.

## 7. Qualification ledger and protective benchmark housekeeping

Update engineering-status documentation to distinguish the **agent-reported** 125/125 private ETABS 23 physical benchmark outcome from externally reviewed evidence. Do not claim independent human approval without review of raw GUI/table exports and signed reference notes. Keep SAP2000 27 physical qualification Pending. Preserve fixture-directed roof plane grouping scope.

Note the following issue for a *separate* small safety follow-up, not a reason to derail this sprint: `scripts/create-benchmark-model.ps1` currently targets a fixed output path without an obvious explicit fail-if-file-exists guard and chooses the first newly appearing ETABS PID. Do not rerun that creation script against an existing benchmark or protected model. If you touch it at all, require no-overwrite semantics and positive new-process identity proof; do not expand production CSI write capabilities.

Also update historical stale statements in `Docs/OPEN_ENGINEERING_ITEMS.md`: differentiate older milestones from current ETABS 23 proof while retaining pending standards and SAP qualification.

## 8. Engineering test matrix and acceptance

Minimum deterministic coverage:

- Dataset family/edition/amendment and component SHA256 validation.
- Value-level provenance with exact source locator and source-review evidence.
- Public/private rights and synthetic-data blocking.
- Procedure applicability and unsupported methods.
- Coverage completeness for an explicitly selected procedure.
- Conflicting/duplicate rows, missing coefficient records, invalid dimensions and units.
- Interpolation boundary and no-extrapolation policy.
- C&C versus MWFRS separation and ASCE versus AS/NZS isolation.
- Internal-pressure +/- alternatives, wind direction and case isolation.
- Synthetic reference fixture qualification and negative tamper tests.
- Old `StandardCalculators` signatures and their non-production gates remain backward compatible.
- SQLite draft/revision persistence and CSI read-only behavior unaffected.

Run locked restore, Debug and Release solution builds, all `Category!=PhysicalCsi` tests, and six existing WPF acceptance processes on Windows. Inspect actual test results and warnings. Physical CSI tests remain opt-in and must **not** be rerun automatically. No actual ETABS model, shell, selection, unit, file or active process changes are required by this sprint.

If a licensed exact standard revision or reviewed reference cannot be accessed, continue with the infrastructure and explicitly record the normative gate as **Blocked**. Do not make up clause numbers, amendments, coefficients, interpolation rules, dynamic factors or examples.

## 9. Git, documentation and reporting

Work directly on `main`. Do not create a PR or use force push. Commit and push only passing public-safe changes.

Update at least:

- Engineering standards architecture / schema reference documentation.
- `Docs/OPEN_ENGINEERING_ITEMS.md` and `Docs/IMPLEMENTATION_STATUS.md`.
- QA checklist and standards data / legal rights guidance.
- Rights-cleared synthetic fixtures and unit tests.

Do not bundle copyrighted standards, private ETABS `.EDB` files, old `.xlsm` workbooks, proprietary CSI binaries, raw private screenshots, real project data or local DBs. Verify `git status` and the commit diff before pushing.

**Final handoff must report:** actual `git rev-parse HEAD`, GitHub Actions run URL and head SHA, Debug/Release build/test and WPF results, created types/files and schema version, synthetic test count, data-rights status, fail-closed gate evidence, missing engineering decisions, and precise private licensed inputs / independently reviewed reference examples required for Sprint 4B.

## Exit criteria

Sprint 4A is complete only if a reviewer can inspect a dataset, trace its source and revision, detect tampering or missing records, and prove that an unreviewed or synthetic bundle **cannot** initiate production wind calculations or pressure approvals.

**Begin the implementation now. Do not claim a numerical wind-standard engine was qualified in this sprint.**
