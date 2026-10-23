# Sprint 4B.0.1 — Intake Readiness Truthfulness & Owner Evidence Handoff

**Scope:** A single, bounded qualification-preflight correctness closure. **No normative formulas, no production unlock, no new architectural sprint.**

## 0. Repository and verified baseline

- Repository: `https://github.com/lammach99-oss/STR-AutoWind`
- Start from the actual latest `origin/main`, with independently checked baseline `61cd3f2d64ab0c765feca889ae8dbb836a389597`.
- CI baseline: `https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39894875734` — successful; 315 non-physical automated tests passed, 0 failed, 0 skipped; six WPF smoke scenarios reported successful.
- The prior agent's live ETABS 23.2.0 geometry benchmark is **agent-reported** as 125/125 physical checks. It is not independent external engineering sign-off, nor normative wind-standards qualification.
- ASCE/SEI 7-22 and AS/NZS 1170.2:2021 still lack installed, appropriately licensed, independently reviewed normative coefficient datasets and certified calculation reference cases. Do not claim otherwise.

Fetch and inspect current main before editing. If it already contains an equivalent verified fix, do not duplicate it.

## 1. Mission and constraints

Fix the remaining misleading readout and procedure-scope gaps in:

- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataBundleValidator.cs`
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataBundle.cs`
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataContracts.cs` only if absolutely needed for a backward-compatible report improvement.
- Relevant xUnit tests, `Docs/STANDARDS_DATA_INTAKE_AND_ENGINEER_REVIEW.md`, `Docs/OPEN_ENGINEERING_ITEMS.md`, `Docs/IMPLEMENTATION_STATUS.md`, and QA checklist.

**Do not** enable wind-pressure calculations, simulation of normative values, engineering sign-offs, production pressure previews, CSI load assignments, or other CSI writes. Preserve `CanCalculateNormativePressures == false` for **all** rights classifications, paths and statuses; preserve `RealReadOnly` behavior and existing data schema compatibility.

Treat a syntactically plausible manifest, valid file SHA-256, or a matching reference-fixture label as **data integrity / metadata**, not proof of licensed normative correctness or independent engineering approval.

## 2. Confirmed source-review issues to reproduce BEFORE fixing

### P0 — `GenerateIntakePreflight()` can overstate readiness

At the baseline, `GenerateIntakePreflight()` derives the overall status essentially as follows:

- `Blocked` for synthetic or non-V3 hash;
- `Invalid` if any **FatalBlocker** exists;
- `ReadyForIndependentReview` if `EvidenceHashVerified` and `ReferenceCalculationAvailable` are true;
- otherwise `Unverified`.

It does **not** reject `ReadyForIndependentReview` when `Validate()` returned one or more issues at severity **Error** (for example invalid edition/amendment, missing mandatory coverage, invalid unit, or incomplete internal pressure data). This is a misleading qualification status even while the separate production-calculation boolean remains false.

**Required fix:** Decide readiness from the **complete** `StandardsValidationReport`, with no Error or FatalBlocker; align `Outcome`, `State`, readiness flags and blockers. No invalid dataset may be labeled `ReadyForIndependentReview`. Preserve distinct `Missing`, `Invalid`, `Unverified`, `Blocked`, `ReadyForIndependentReview` semantics; do not label `EngineerReviewed` from self-declared metadata or file hashes. If the procedure is absent or its applicability has not been checked, report that gap rather than assuming procedure coverage.

### P1 — Report booleans omit serious failure issue codes

Current flags are inferred from short **negative lists**:

- `SemanticHashValid` ignores `STD_MANIFEST_HASH_MISSING` and situations where hashing is skipped after non-finite data, so it can report true when no successful comparison occurred.
- `ComponentBytesCoherent` ignores codes such as `STD_COMPONENT_JSON_MALFORMED`, `STD_COMPONENT_LOAD_FAILED`, `STD_COMPONENT_FILES_EMPTY`, `STD_EXTRA_COMPONENT`, and `STD_DUPLICATE_SOURCE_KEY`.
- `ProcedureCoverageValid` ignores `STD_MANDATORY_CATEGORY_EMPTY` and procedure/applicability mismatch cases, and can be reported true without any procedure qualification.

**Required fix:** Prefer positive, explicit proof-of-check results (or a complete, documented issue-code taxonomy) over brittle absence-of-a-few-issues tests. A check not executed must never render as `true`. Preserve compatibility for consumers; add explicitly named `NotChecked`/`NotQualified` diagnostics or fields if necessary rather than silently altering the meaning of existing values. Keep a complete blocker list corresponding to all invalid/error conditions without exposing private coefficient content.

### P1 — `ReferenceCalculationAvailable` is only a shallow label match

At baseline, a supplied `StandardReferenceCalculationFixture` is considered available if it is non-synthetic and has matching standard family and edition. The code does not establish that:

- Expected factors and pressures are present and finite;
- absolute/relative tolerances are valid and assertion keys unique;
- procedure, method, system, limit state and amendment applicability match the intended scope;
- referenced evidence actually exists and has been checked;
- reviewer attestation is independent and verifiable;
- the expected numbers were obtained from an independent licensed reference rather than typed into the fixture.

**Required fix:** Distinguish **file/object supplied**, **structurally eligible**, **independently qualified** and **production-authorized**. Do not allow an empty or malformed, self-declared, contradictory or synthetic-looking fixture to contribute to `ReadyForIndependentReview`. Reuse `ReferenceCalculationValidator`'s existing structural checks where appropriate, but do not pretend that comparing supplied expected values to arbitrary actual dictionaries establishes independent engineering qualification. For absent primary sources, retain an explicit external-evidence blocker. Avoid inventing reviewer credentials or normative data.

### P1 — Procedure-scoped lookup still accepts a record missing `LimitState`

`StandardsDataBundle.MatchesProcedureMetadata()` verifies a declared `LimitState` **if present**, but allows the record through when that category key is missing. This contradicts the claimed requirement that each procedure-scoped coefficient must have an explicit procedure ID, method **and limit-state applicability**.

**Required fix:** Require an explicit reviewed limit-state scope before returning a record. Continue to reject mismatched family, edition, procedure ID, method, system and numerical conditions. Only permit intentional `Both`/`All` semantics where explicitly reviewed and appropriate; do not silently infer ULS/SLS compatibility. Keep low-level synthetic inspection APIs separate from qualification-authorized lookup. Preserve combined MWFRS/C&C bundles while isolating retrieved records.

## 3. Bounded test-first regression matrix

Create failing tests, then make the smallest feasible production changes. Cover at minimum:

1. V3 non-synthetic bundle with matching local evidence SHA and family/edition-matching reference fixture **plus** a `StandardsValidationSeverity.Error` must **not** become `ReadyForIndependentReview` (multiple representative Error issue codes).
2. Missing or empty manifest `ContentHash`: `SemanticHashValid == false`, blocked/invalid overall status.
3. Non-finite value or numerical condition preventing hash computation: no `SemanticHashValid == true` and no accidental exception.
4. Invalid raw component JSON, undeclared component, duplicate raw key, or empty component file list: `ComponentBytesCoherent == false`.
5. Mandatory coverage category absent, procedure absent/mismatched, or incomplete internal-pressure states: `ProcedureCoverageValid == false` when applicable and no premature readiness.
6. Non-synthetic reference fixture with no expected assertions, duplicate keys, invalid tolerance, mismatched procedure/amendment, unverified evidence or self-review: must not qualify readiness.
7. A valid *synthetic* reference fixture stays clearly synthetic and non-production even when numeric matching succeeds.
8. `FindRecordForProcedure()` rejects a coefficient missing `LimitState` and rejects incompatible explicitly declared `LimitState`; separately test explicitly permitted reviewed alternatives where representable.
9. Genuine structurally valid, V3, checksum-verified non-synthetic metadata with no private reviewer document reports `Unverified`/`Blocked` as appropriate, **not** `ReadyForIndependentReview` or `EngineerReviewed`.
10. `CanCalculateNormativePressures` remains false in every negative and positive metadata-only test.

Use **rights-cleared synthetic / mock tests only** in source control. Tests must not need installed ETABS, licensed standards PDFs, private local models, or live network access. Do not rewrite geometry algorithms or pressure mathematics.

## 4. External owner handoff — concrete licensed data intake (no fabrication)

After these bounded correctness fixes, prepare a single clear local intake handoff (public-safe instructions only) explaining exactly what the owner must obtain outside Git:

**ASCE/SEI 7-22:** legally accessible primary edition plus applicable supplements/errata, applicable jurisdiction and risk/wind-speed basis, reviewed procedure and table/figure locators, method-specific MWFRS and C&C data, and independently reviewed flat/gable reference examples.

**AS/NZS 1170.2:2021:** legally accessible primary edition, jurisdiction-specific applicability and actual governing amendments checked against primary sources, wind-region/terrain/topography/shielding/directivity/data coverage, and independently reviewed flat/gable reference examples.

For **each** family, specify:

- Permitted private untracked path, e.g. `fixtures/standards/private/<family>/<dataset-id>/` and `artifacts/standards-private/`.
- Dataset manifest, full scope, exact procedure and applicable limit states, source-document identifiers, rights declaration, raw component byte hashes and V3 canonical hash.
- Preparers and genuinely independent reviewer, scope-specific review memo, locally verified SHA-256, actual reviewer sign-off and separate benchmark verification.
- At least one complete, externally independently calculated flat-roof and one gable-roof case per approved method; signed intermediate factors, pressure-zone/wind-state outputs, units, sign and normal conventions, interpolation bounds, tolerance rationale, coefficient record IDs, amendment and edition information.
- A strictly local/opt-in preflight command or reproducible developer call. If no CLI exists, document the actual callable API; do not imply a CLI exists. No licensed rows, private paths, PII, memo contents or raw PDF bytes in public test output or CI logs.
- Unambiguous labels: `IntegrityVerified`, `EvidenceFileHashVerified`, `IndependentReviewPending`, `ReferenceQualificationPending`, `ProductionAuthorizationDisabled` (or existing equivalent typed states).

Audit documentation for false assurances: `.gitignore` currently excludes `*.pdf` and specific private directories, while the intake guide also mentions `*.csv` and `*.docx` as protected file patterns. Clarify the actual scope accurately; do not globally ignore useful rights-cleared public fixtures without justification. Never rely on `.gitignore` alone to prevent tracking of licensed content; verify staged changes and tracked files explicitly.

**Stop condition:** If the licensed source/reviewer package is absent, produce the actionable owner handoff and an explicitly blocked status. **Do not start numerical Sprint 4B or create fabricated coefficients/signatures.**

## 5. Verification and repository workflow

- Work directly from actual latest `origin/main`; no new feature branch, pull request or force-push.
- Run locked dependency restore, Debug and Release solution builds, all non-physical automated tests, and all six WPF smoke checks. Maintain Windows CI pass.
- Confirm physical CSI tests remain opt-in, that all real CSI writes and engineering approval remain disabled, and that the fixture-directed ETABS 125/125 outcome is still **agent-reported** rather than independent standards qualification.
- Update only necessary architecture, QA and intake documents; do not run another broad refactoring pass.
- Check `git status`, `git diff --cached`, tracked file inventory and newly added files for proprietary material, private paths and temporary patch scripts.
- Commit and push only verified public-safe changes directly to `main`.

## 6. Final deliverables — evidence, not claims

Provide the actual `git rev-parse HEAD` SHA and GitHub Actions run URL; Debug/Release results; exact non-physical test count and six WPF results; list of regression tests demonstrating each corrected status problem; an example **synthetic** intake report with all flags and blockers; written owner checklist for licensed dataset and independent engineer memo; and a truthful matrix for ASCE 7-22 / AS/NZS 1170.2:2021 showing what is still absent.

**Acceptance:** No combination of verified metadata bytes, a matching PDF hash or an unreviewed reference-fixture object can yield an unjustified `ReadyForIndependentReview`, `EngineerReviewed` or calculation authorization. Invalid/Error/NotChecked results must be reflected accurately. The repository remains a read-only, non-normative-calculation system.

**Execute this final bounded preflight correction and then stop implementation pending externally supplied licensed evidence.**
