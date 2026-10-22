# Sprint 4A.1 - Standards Data Integrity, Provenance, and Qualification-Gate Closure

## Role, repository, and verified baseline

Act as the lead structural engineering software architect, independent QA reviewer, and senior C#/.NET 8 developer for **Structural Load Assistant / AutoWind**.

Repository: https://github.com/lammach99-oss/STR-AutoWind

Work from the latest `origin/main`. The independently confirmed Sprint 4A baseline is:

- Commit: `19e955b76a6fcd98c788269737f49106dc8df8bf`
- CI: https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39728598138
- CI evidence: Windows Release build with zero warnings/errors; **250 non-physical automated tests passed**; WPF smoke job succeeded.

The previous implementation added `StandardsDataContracts.cs`, `StandardsDataBundle.cs`, `StandardsDataBundleValidator.cs`, `ReferenceCalculationContracts.cs`, and synthetic ASCE/AS-NZS fixtures. This is real progress, but an independent source review found integrity and promotion-gate defects that must be resolved **before any Sprint 4B numerical standards engine**.

Important engineering state:

- ETABS 23.2.0 private benchmark: agent reports 125/125 physical comparisons passed. This is not an independently audited external engineer sign-off; the `.EDB` and raw qualification evidence are private.
- SAP2000 27 physical qualification: pending.
- Licensed, independently reviewed ASCE/SEI 7-22 and AS/NZS 1170.2:2021 coefficient datasets: not installed and not qualified.
- Normative wind calculations, engineering approval, load proposals, and live CSI writes in RealReadOnly: remain disabled.

## Objective

**Harden and complete the Sprint 4A foundation, not the normative calculator.** Fix the concrete defects below using failing regression tests first. Preserve existing public contracts where possible, clearly version any breaking persistence/schema change, and maintain strict separation of ASCE and AS/NZS procedures.

**Non-negotiable result:** no path from mutable, unsigned, partially checked, synthetic, or metadata-only standards records to a production-pressure authorization flag.

## Source-reviewed defects to reproduce

1. **Incomplete canonical hash.** `StandardsDataBundle.ComputeCanonicalHash()` hashes only `BundleId`, family, edition, and each record's `RecordKey`, category, value, unit, and interpolation type. It omits categorical/numerical conditions, clause/table locators, sign convention, applicability, amendments and other engineering-significant metadata. Modifying those fields can leave this hash unchanged. It also currently has no matching validation against `Manifest.ContentHash`.
2. **Optional component verification.** `StandardsDataBundleValidator.Validate()` checks raw component SHA256 only if `rawComponentFolder` is nonempty and exists. The default call can therefore omit source-byte integrity evidence without a blocking integrity issue. `StandardsDataBundle.LoadFromMemory()` consumes arbitrary supplied files rather than requiring exact manifest-declared components and verified bytes.
3. **Premature calculation eligibility.** `CanCalculateNormativePressures` is computed as `ReadyForIndependentReview && !isSynthetic`. A non-synthetic, superficially valid manifest could yield `true` even with `DataProvenance.Unverified`, no executed independent reference calculations, no verified reviewer artifact and no normative implementation. This violates the current project safety boundary.
4. **Review metadata is treated too optimistically.** A supplied `IsIndependentReview=true`, reviewer text, and nonempty evidence hash are not cryptographic verification of an independent engineer's review. Preparer/reviewer identity equality is not directly checked; evidence file bytes are not independently verified by this validator.
5. **Incomplete procedure and coverage gating.** Mandatory coverage currently checks mainly for one record per required category; it does not validate exact per-procedure, per-regime, per-zone, per-wind-state and +/- internal-pressure coverage. The `MWFRS`/`C&C` isolation branch computes `ccRecordsInMwfrs` but does not use it. An internal-pressure alternative missing its sign is currently only a Warning.
6. **Ambiguous coefficient retrieval/interpolation.** `FindExactRecord()` permits partial categorical-condition matching and ignores numerical conditions; `Interpolate1D()` compares categorical keys only in one direction and does not verify all applicable table, procedure, unit, sign, source, and other numerical-regime identities. Incorrect regime mixing remains possible.
7. **Reference fixture can falsely pass.** `ReferenceCalculationValidator.Evaluate()` can return `Passed=true` with zero expected comparisons. It also falls back from exact `ZoneId:StateId` to zone-only pressure, which may silently mix WX/WY and internal-pressure +/- states. The numeric comparison result is not distinct from engineer-reviewed normative qualification.
8. **Amendment claims lack applicability evidence.** The AS/NZS validator requires the two named amendments merely by label. Their governing applicability must ultimately come from licensed, independently reviewed primary documents. Do not claim labels alone prove applicability or compliance.

Confirm these issues against HEAD before editing. If a later HEAD has fixed one, retain its regression and do not reintroduce work.

## Task A - Integrity envelope and deterministic cryptographic coverage (P0)

1. Define and document **two distinct hashes**:
   - **Raw file SHA256** for each exact component file's bytes; changing whitespace/order in the file changes its raw digest.
   - **Canonical semantic bundle SHA256** for all engineering-significant normalized data. Equivalent JSON key order and whitespace do not change the semantic digest, but changing any semantic field must change it.
2. Canonical data must include at minimum family/edition/revisions/amendments/procedure scope/rights where relevant, plus every coefficient record's exact key, category, clause/table/figure, all categorical conditions, all numerical conditions, value, unit, sign convention, interpolation, domain/applicability, and any relevant supersession metadata. Sort objects/maps deterministically, serialize numbers invariantly, reject NaN/Infinity, and define UTF-8 policy. Avoid hashing a field into itself: exclude `Manifest.ContentHash` from its own canonical preimage.
3. Compare the computed semantic hash with `Manifest.ContentHash`. Missing/malformed/mismatching hashes must block any qualified/readiness claim. Version the canonicalization algorithm and provide a documented migration for old synthetic manifests.
4. Verify **every manifest-declared component** against original bytes. Detect missing, extra, duplicate, mismatched, and unlisted input components. `LoadFromMemory()` must not silently bypass verification. If bytes are absent, return explicit `IntegrityUnverified`/`MissingEvidence`, never ready.
5. Bind validated parsed records to the exact verified source bytes. Avoid time-of-check/time-of-use races between loading, hashing, and reading by using a single immutable snapshot of the verified bytes.
6. Enforce safe component paths: reject rooted paths, traversal (`..`), paths escaping the bundle root, and ambiguous case-colliding components. Handle symlinks/reparse points conservatively and document the trust boundary. Keep licensed data private.
7. Remove any claim that a *canonical* hash changes on whitespace or JSON order. Explain byte-exact versus semantic hashes accurately.

**Required mutation tests:** change only a zone category, roof pitch condition, sign convention, clause locator, units, amendment identity, reviewer evidence reference, or procedure scope without modifying the numeric `Value`. A semantic change must fail hash validation. Also test whitespace-only JSON change (raw hash changes, canonical hash stable), wrong component bytes, missing/extra file, memory-only bypass, and traversal paths.

## Task B - Fail-closed qualification and separate readiness states (P0)

1. Keep the public `StandardCalculators.Asce7_22()` and `.AsNz1170_2_2021()` metadata-only; no production numeric result. Keep all RealReadOnly engineering approvals/writes disabled.
2. Separate:
   - manifest/schema validity;
   - component byte and semantic hash integrity;
   - licensing/rights resolution;
   - procedure/edition/amendment applicability;
   - independent reviewed coefficient coverage;
   - independent reference-calculation execution and results;
   - authorization to use a future normative pressure engine.
3. Until a future explicitly approved production engine and data qualification stage exists, ensure **`CanCalculateNormativePressures == false` for every bundle**, including non-synthetic bundles with apparently valid metadata. The current Sprint 4A.1 may report `ReadyForIndependentReview` or `ReadyForQualification` for data checks, but must not claim `EngineerReviewed`, `ProductionQualified`, or calculation authorization from metadata alone.
4. Ensure an unverified or unresolvable data source, absent checked review evidence, missing procedure-specific coverage, or an unavailable fixture produces a blocking result rather than a warning-only promotion path.
5. Validate preparer and reviewer are meaningfully distinct based on normalized identity and organization; do not trust `IsIndependentReview` by itself. Hash the actual review-evidence file if authorized and accessible; an asserted hash/URI without the file is **unverified metadata**, not an attestation. Do not fabricate a digital signature or a human approval.
6. Add a future approval token/contract only if its input state is clearly unforgeable by a casual `with` copy or arbitrary JSON property; for this sprint there is no code path that issues a production token.
7. Require explicit positive rights evidence for public redistribution. `LocalLicensedOnly` may be used privately only and must not be exported into CI artifacts, Git history, or reports containing licensed coefficients.

**Required negative tests:** construct a non-synthetic in-memory manifest with `Rights=LocalLicensedOnly`, `IsIndependentReview=true`, nonempty fake evidence hash, plausible record counts, and no verified bytes or reviewer evidence. Validation must **not** authorize normative calculations. Repeat with `provenance=Unverified` and with a copied reviewer's name.

## Task C - Exact procedure coverage, lookup, and interpolation (P1)

1. Replace coarse category count as proof of coverage with typed, applicable **per-procedure coverage requirements**: standard family, edition/amendments, procedure ID, MWFRS vs C&C, wall vs roof, enclosure/internal-pressure +/-, wind direction, limit state, roof type/pitch/wind-angle regime, and zone requirements when relevant. Keep standard-specific rules distinct.
2. Verify declared `CoverageMatrix.AvailableRecordCount` against actual records (or explicitly redefine it as a required minimum with a schema revision). Reject empty required domains, duplicates/conflicts, or only a single representative row claiming full coverage.
3. Correct `MWFRS` versus `C&C` selection: a bundle may legitimately contain both sets, but a given procedure lookup must never return the wrong system's records. The unused `ccRecordsInMwfrs` variable is not a guard; implement and test real isolation.
4. `FindExactRecord()` must use exact applicable categorical and numeric conditions and procedure identity. Define explicit behavior for irrelevant extra query conditions, missing record dimensions, ambiguous matching, and allowed wildcards. Do not use labels or partial substring matching as engineering evidence.
5. `Interpolate1D()` must require compatible source/edition/procedure/table/category/system, identical full categorical sets in *both* directions, compatible units and sign conventions, identical remaining numerical-regime conditions, finite bounds, an independently authorized interpolation policy, and in-range target. Reject unsupported bilinear interpolation rather than silently approximating it.
6. `ParameterDomain` should validate finite and ordered bounds and must not permit extrapolation merely because a JSON boolean is `true`; require explicit reviewed procedure permission, or conservatively reject all extrapolation in Sprint 4A.1.
7. For AS/NZS amendments, record `Applicable`, `NotApplicable`, `Unresolved` with independent source provenance for each potentially relevant amendment, instead of assuming naming both proves applicability. If unresolved, block promotion. Do not reinterpret licensed clauses without review.

**Required tests:** retrieve the same numeric value under two different wind states, two pressure zones, MWFRS/C&C, roof pitches, and enclosure cases; only the fully matching record may be selected. Reject cross-standards/edition interpolation, asymmetric categorical sets, differing sign/unit/source tables, extrapolation without provenance, and unresolved amendments.

## Task D - Independent reference-fixture completeness (P1)

1. `ReferenceCalculationValidator.Evaluate()` must never return a successful *qualification* for empty `ExpectedFactors` and `ExpectedPressures`, or for missing required states and expected stage outputs. Introduce explicit `NotExecuted`/`Incomplete` or equivalent outcome when appropriate.
2. Match pressures by **exact `(ZoneId, StateId)` identity**. Remove the zone-only fallback unless an independently reviewed fixture explicitly defines a single-state unambiguous alias. Prefer no fallback.
3. Detect duplicate expected result keys, duplicate actual normalized keys, malformed sign, nonfinite expected/actual values, invalid negative/nonfinite tolerances, inconsistent units, missing stage/source traces, and out-of-range relative tolerances.
4. Report numerical `Matched` separately from `EngineerReviewed`/`NormativelyQualified`. Synthetic fixtures may pass numerical infrastructure tests, but remain unqualified for production and cannot be used to authorize calculations.
5. Require independent reference fixtures to bind to the exact standard edition/amendment, procedure, dataset hash, coefficient IDs and review evidence. Do not accept an application-generated copy of expected results as independent verification.

**Required tests:** empty fixture; a pressure value for `WX+/IP+` supplied only as zone-only or `WX-/IP-`; duplicate factors; invalid tolerance; incomplete internal-pressure alternatives; synthetic fixture numerically matching but production-ineligible; mismatched dataset revision; missing external reference evidence.

## Task E - Testing, documentation, and release safety

1. Use red-green-refactor TDD on the P0 cases first. Preserve the 250-test baseline and WPF compatibility. Test both `LoadFromDirectory` and `LoadFromMemory` integrity paths; avoid tests that prove only an ad hoc copy of a PowerShell/C# condition rather than actual production code.
2. Use rights-cleared, clearly labeled `SYNTHETIC / TEST ONLY / NOT STANDARDS-COMPLIANT` fixtures only. Update their manifest semantic hashes through the documented canonicalization procedure; don't smuggle real coefficients into a test fixture.
3. Update `Docs/SPRINT_4A_STANDARDS_DATA_FOUNDATION.md`, `Docs/OPEN_ENGINEERING_ITEMS.md`, `Docs/IMPLEMENTATION_STATUS.md`, and QA documentation with an exact qualification-state matrix. Clarify that ETABS's private 125/125 physical benchmark is agent-reported, independent external sign-off is pending, and automatic arbitrary-plane clustering is not physically qualified by the fixture-directed test.
4. Ensure no broad claim that ASCE/SEI 7-22 or AS/NZS 1170.2:2021 calculations are verified. Applicability and amendment interpretation require licensed primary sources and an independent engineering review.
5. Run locked restore, Debug and Release zero-warning builds, `dotnet test --filter Category!=PhysicalCsi`, and all six real WPF smoke processes. Physical CSI remains separately opt-in.
6. Work **directly on `main`**, commit/push only verified, public-safe source and synthetic fixtures to `origin/main`. No PR, feature branch, force push, or destructive history rewrite.

## Acceptance and evidence checklist

The sprint is complete only when:

- Canonical semantic hash changes for every engineering-significant mutation, while JSON whitespace/order changes remain semantically stable.
- Missing/unverifiable component bytes and unverified memory-only bundles cannot claim full integrity/readiness.
- `CanCalculateNormativePressures` is false for all Sprint 4A.1 validation scenarios, including apparently reviewed non-synthetic metadata.
- `IsIndependentReview=true` alone cannot imply independent verification.
- Procedure lookup cannot cross MWFRS/C&C, wind state, zone, limit state or edition boundaries.
- Interpolation cannot cross categorical/sign/unit/procedure regimes or extrapolate without approved authorization.
- Empty or state-ambiguous reference fixtures do not produce a qualified Passed result.
- No normative coefficient tables, proprietary CSI files, private engineering notes, customer models or licensed workbook data are published.
- CI, Debug/Release builds and the WPF restart smoke suite remain green; live CSI write operations and production normative calculation gates remain blocked.

## Final handoff from coding agent

Provide:

- Exact `git rev-parse HEAD` SHA and GitHub Actions URL/status.
- Changed-file list and summary of original P0/P1 reproductions.
- Test count, red/green evidence, locked restore, Debug/Release and six-process WPF smoke results.
- Canonical hash specification/version and raw-file SHA behavior.
- Negative-test matrix, especially the **non-synthetic metadata-only false-positive** regression.
- Actual gate/status for each synthetic and unreviewed dataset scenario.
- Remaining legal/licensing, amendment-applicability, independent coefficient-reference, physical sign-off and SAP2000 blockers.
- Confirmation that no production normative pressure calculation, engineering approval or live CSI writes were enabled.

**Start implementing Sprint 4A.1 now. Do not begin Sprint 4B production coefficient calculations until these integrity and qualification gates are secure and independently reviewed normative inputs exist.**
