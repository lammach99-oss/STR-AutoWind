# Sprint 4A.2 — Standards Bundle Coherence & Procedure-Safe Coefficient Retrieval

**Role:** Lead structural engineering software architect, independent QA engineer, and senior C#/.NET 8 developer.

**Repository:** https://github.com/lammach99-oss/STR-AutoWind  
**Branch:** Work directly on the current `origin/main`. No feature branch, PR, force-push, or history rewrite.  
**Independently verified starting HEAD:** `8bc569aedf3bf7afd861bdac13a24ab57ef38ba6`  
**Verified GitHub Actions run:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39734981715 — success, **271/271** nonphysical automated tests, zero failed/skipped, and WPF smoke evidence.  
**Scope:** Focused closure of standards data integrity and procedure-specific lookup before any normative calculation implementation.

## 0. Safety and evidence baseline

Sprint 4A.1 improved canonical semantic hashing, mandatory component SHA-256 verification, synthetic-data blockers, reviewer metadata checks, numerical matching, interpolation safety, and reference-calculation validation. Nevertheless, independent review of the actual pushed source identified several residual weaknesses below. Treat them as **source-review findings to reproduce with failing regression tests**, not as claims that they have already been exploited in production.

The previous agent reports **125/125** physical benchmark checks passed against an authorized, private ETABS 23.2.0 disposable gable model. The private `.EDB`, raw physical comparison JSON, independent GUI review, and signed external engineering approval were not provided to this reviewing assistant; **do not claim external engineering sign-off**. Benchmark roof-plane grouping is fixture-directed, not unguided general roof-plane discovery. SAP2000 physical qualification remains pending.

**Non-negotiable gates:** No licensed normative coefficient tables may be published; no real ASCE/SEI 7-22 or AS/NZS 1170.2:2021 production pressures; no pressure approval; no real CSI writes. `CanCalculateNormativePressures` must remain **false for every bundle**, even if all data checks pass. Keep `StandardCalculators` metadata-only and `RealReadOnly` strictly read-only. Do not use metadata, hash values, CI success, or a generated review name as evidence of engineering approval.

## 1. Reproduce and close the source-byte / record-coherence gap — P0

**Source:** `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataBundle.cs` and `StandardsDataBundleValidator.cs`.

**Current behavior observed:** A bundle exposes a public constructor accepting `Manifest`, `Records`, `RawComponentBytes`, and `IsBytesVerified`. The validator compares declared component SHA-256 values against `RawComponentBytes` and separately compares `Manifest.ContentHash` with `ComputeCanonicalHash()` of the in-memory `Records`. It does not presently demonstrate that the **records being used** are precisely the records parsed from those **verified component bytes**.

**Required failing regression:** Load a valid synthetic fixture; retain its verified raw bytes, change an in-memory coefficient/condition while keeping the raw bytes untouched, recompute/set the manifest semantic hash to match the mutated in-memory records, and call the validator. This must be rejected as `STD_RECORD_BYTES_MISMATCH` or equivalent; a matching raw-file SHA and a separately matching semantic hash must never suffice when they describe different content. Include changed record counts, swapped component assignments, duplicated records, and a public constructor misrepresenting `IsBytesVerified=true`.

**Implement:**

1. Derive the authoritative record set from the *same exact component bytes* that have passed the manifest checksum checks, or compare canonical parsed records from those bytes against the in-memory record set and block any mismatch.
2. Treat `IsBytesVerified` as **derived evidence from validation**, not a trustworthy caller-supplied attestation. Preserve test construction APIs where necessary, but do not let arbitrary public construction grant readiness.
3. Bind each record to its declared component (or retain a deterministic per-component record partition) so a swapped record/component cannot be accepted merely because the flattened set is unchanged.
4. Verify byte presence, SHA-256, record deserialization, unique identifiers, and correspondence before permitting any ready-for-review state. Handle missing and extra components, duplicate manifest paths, BOM and malformed JSON deterministically.
5. If loading from disk, validate paths before reads and prevent symlink/reparse-point escape from the declared dataset root where applicable. Avoid TOCTOU assumptions; if the folder changes during verification, fail closed or rely solely on the immutable captured bytes already verified.
6. Keep all synthetic fixtures clearly labeled test-only and update their hashes only through a reviewed, reproducible fixture-generation procedure.

**Acceptance:** No combination of stale raw bytes, mutated in-memory records, recomputed semantic hash, misleading `IsBytesVerified`, or substituted component filenames can reach the dataset readiness outcome.

## 2. Remove ambiguous canonical hash serialization — P1

**Current behavior observed:** `ComputeCanonicalHash()` concatenates string fields with `|`, `,`, `=`, and newlines without length-prefixing or escaping every field. Even with more fields included than Sprint 4A, this encoding is not guaranteed collision-free for arbitrary legal identifiers, source locators, and condition strings.

**Implement a robust, versioned canonical encoding** (e.g. length-prefixed UTF-8 segments or strictly specified canonical JSON with deterministic property/order/number/Unicode handling). Protect against delimiter-injection collisions, ambiguous null versus empty string, case/culture differences, duplicate keys, and nonfinite numbers. Cover all engineering-significant record fields plus the relevant manifest lineage, edition/amendment identity, and declared component inventory. Exclude the canonical digest field itself from its preimage.

State the supported hash-format version explicitly. Provide a safe and documented transition for existing **synthetic v1** fixture hashes; do not silently accept an obsolete hash scheme as sufficient for future non-synthetic production datasets. Preserve a distinction between raw component-byte SHA-256 and canonical semantic content hashing.

**Required tests:** Hash difference for semantic changes; stable hash for harmless JSON whitespace/key ordering; attempted collisions using separators inside text; invariant results across process culture and record ordering; unknown canonical version fails closed.

## 3. Enforce truly exact numerical record matching — P0/P1

**Source:** `StandardsDataBundle.FindExactRecord()` and `MatchesNumerical()`.

**Current behavior observed:** `MatchesNumerical(...)` immediately returns `true` when requested numerical conditions are `null` or empty, **even when a coefficient record has numerical dimensions** such as pitch, height, or effective area. When a query provides numerical conditions, only queried keys are checked; undeclared extra record conditions are not rejected. A fixed `1e-6` difference is applied without reference to the parameter's dimension or an authorized lookup tolerance.

**Required behavior:**

1. For `FindExactRecord`, require a **complete bidirectional match** of numerical condition keysets as well as categorical keysets. A query without pitch must not retrieve a pitch-dependent coefficient.
2. Require finite query/record condition values; reject missing dimensions, extra dimensions, ambiguous duplicate matches, and unknown units.
3. For true exact lookup, use exact numerical equality or a consciously designed, explicit, unit-aware precision policy. Do not silently assume `1e-6` is valid for every dimensional parameter.
4. If approximate matching is required, expose a **separate, explicit method** with fully documented tolerances and procedure provenance; do not silently broaden `FindExactRecord`.
5. Preserve existing public API call sites only if they can be made safe; otherwise add an explicit migration path with regression coverage.

**Required tests:** A record at pitch 10° queried without pitch, with only part of the required numerical map, or at pitch 10.0000005° must not accidentally appear as an exact match. An exact complete query must return only the unique intended record.

## 4. Scope MWFRS versus C&C to the selected procedure, not to the entire bundle — P0/P1

**Source:** `StandardsDataBundleValidator.Validate(..., ProcedureSelection?)` and `StandardsDataBundle.MatchesSystem()`.

**Current behavior observed:** When the selected procedure is `MWFRS`, the validator scans **all** bundle records and raises `STD_SYSTEM_MISMATCH` if any C&C record exists. A legitimate standards bundle can intentionally contain MWFRS, Components & Cladding, and separate internal-pressure records together. It must not be rejected merely for containing categories used by other procedures. Conversely, `MatchesSystem` infers system membership using string fragments such as `ParameterCategory.Contains("CC")` or allows an unscoped call.

**Implement:**

1. Define explicit, typed procedure/system identity and applicability metadata: ASCE/SEI 7-22 MWFRS versus C&C, or appropriate separate AS/NZS procedure identity. Preserve shared/internal-pressure data with explicit applicability rather than treating it as C&C or MWFRS indiscriminately.
2. Separate **bundle integrity validation** from **selected-procedure record resolution**. The bundle can contain multiple procedures; only records returned or applied for the queried procedure must satisfy its system scope.
3. Enforce family, edition, amendment applicability status, procedure ID, selected system, wind state, surface and zone when resolving an engineering coefficient. Missing/unknown metadata must block a safety-critical lookup, not default to another system.
4. Remove string-fragment classification (`Contains("CC")`) as an authority for production-relevant retrieval. For backwards compatibility, keep explicitly labeled legacy/demo helpers segregated and unqualified.
5. Detect actual cross-procedure contamination by asserting that MWFRS lookups never return C&C-only records and vice versa. A combined synthetic bundle with both categories must not produce a false `STD_SYSTEM_MISMATCH` merely because it contains both.
6. Ensure positive/negative internal-pressure alternatives remain distinct **within the applicable procedure and enclosure case** rather than accepting unrelated `+`/`-` strings elsewhere in the bundle.

**Required tests:** Combined MWFRS+C&C bundle valid for schema/integrity; MWFRS-only retrieval returns only MWFRS records; C&C-only retrieval returns only C&C; ambiguous/no system selection returns no normative record; unsupported procedure is blocked; external versus internal-pressure states remain independent; wind direction and load state cannot be silently conflated.

## 5. Review, provenance, and amendment applicability boundaries — P1

Retain independent reviewer checks but be explicit about the distinction between a **syntactically valid review trace** and **authentic human engineering attestation**.

1. Validate evidence hash **format and linkage**, not just nonblank presence. A syntactically valid 64-character SHA-256 and a URL are still metadata, not independent evidence that the file exists, is authorized, or was signed by a separate engineer.
2. If review evidence is locally provided and authorized, compute its SHA-256 and compare it with the trace; classify missing/unavailable verification separately. Never fetch private licensed documents into CI or publish their raw content.
3. Keep rights to **read/use locally** separate from rights to **redistribute**. Unknown rights block public export; local use requires affirmative license metadata and review of terms. No automatic assumption that access to a workbook grants extraction/redistribution rights.
4. Represent ASCE errata/supplement and AS/NZS amendments with edition-scoped applicability decisions plus source/provenance. The existing unconditional AS/NZS amendment-name requirement is a conservative temporary gate, not proven universal applicability. **Do not loosen it** until licensed primary-source review establishes the correct applicability; otherwise mark unresolved and block.
5. No `EngineerReviewed` or `CanCalculateNormativePressures=true` state from a filename, a forged reviewer name, synthetic data, or a numerically matching self-generated example.

**Required tests:** Malformed SHA, absent locally referenced evidence, mismatched evidence hash, fake review identities, unknown rights, prohibited redistribution, edition/amendment contradiction, and unresolved applicability. All must leave production pressure calculation disabled.

## 6. Reference-calculation qualification semantics — P1

Keep `ReferenceCalculationValidator` strictly numerical and independent of engineering authorization. Tests with synthetic fixtures may report `IsNumericalMatch=true` but must never authorize standards compliance.

Ensure a reference fixture is explicit about selected family, edition, amendments, procedure/system type, wind state including `WX+`, `WX-`, `WY+`, `WY-`, each internal-pressure sign alternative, and the exact zone/pressure-surface mapping for its declared scenario. A zone-only key is prohibited. The fixture may support factor-only unit benchmarks if deliberately marked as such; a complete engineering pressure benchmark must require its declared mandatory intermediate factors and expected pressures.

Add tests for duplicate `(ZoneId, StateId)`, nonfinite actual factors/pressures, missing required expected pressures, mismatched amendments/system, and any attempt to treat a synthetic numerical match as reviewer approval.

**Do not invent normative pressure coefficients or independent reviewer signatures.** All new public fixtures must be obviously synthetic or derived from explicitly redistribution-cleared examples.

## 7. Scope, verification, and publication

Start by running the existing locked build/test baseline. Add failing negative regressions that reproduce each source-reviewed weakness before modifying implementation; distinguish true reproductions from speculative failure modes. Minimize changes outside the StandardsDataFoundation and test modules. Maintain compatibility with the WPF roof mapping, SQLite drafts, previous ETABS geometry qualification, and public standard metadata-only API.

**Required QA:**

- Locked NuGet restore.
- Debug and Release solution builds with warnings-as-errors.
- All deterministic unit/integration tests using `Category!=PhysicalCsi` (report actual totals, 0 failed/skipped).
- Six WPF smoke processes with exit codes and artifacts.
- Mutation tests proving record/byte divergence, missing numerical dimensions, and cross-procedure queries fail closed.
- Verify that `CanCalculateNormativePressures` is **always false** and no real CSI setter / approval pathway has been enabled.
- Physical ETABS tests remain strictly opt-in; **do not repeat them just for this standards-data sprint**.
- Confirm `.gitignore` and tracked artifacts do not leak proprietary libraries, private ETABS models, standards tables, reviewed workbooks, credentials, or licensed PDFs.

Update `Docs/OPEN_ENGINEERING_ITEMS.md`, `Docs/IMPLEMENTATION_STATUS.md`, `Docs/QA_CHECKLIST.md`, and add a focused Sprint 4A.2 qualification note documenting actual tested assurances and remaining unqualified areas. Avoid obsolete historical status statements.

Push passing changes directly to `origin/main` (no PR, no force-push). Report exact `git rev-parse HEAD`, GitHub Actions run URL and exact run HEAD, code changes, failing-before/passing-after cases, test counts, and residual gates.

## 8. Engineering exit criteria and next decision

Sprint 4A.2 is complete only if:

1. Verified raw component bytes and parsed engineering records are cryptographically and semantically bound.
2. Record queries cannot match missing numerical conditions or the wrong procedure/system.
3. Combined multi-procedure standards bundles do not fail incorrectly, while genuinely cross-procedure coefficient use is blocked.
4. Canonical hashing has a collision-resistant unambiguous serialization with a versioned compatibility story.
5. Data rights, evidence and amendment applicability remain explicitly unqualified where not independently established.
6. CI and UI regressions pass with engineering calculations, approvals and live CSI writes still disabled.

After this closure, **do not automatically proceed to unrestricted Sprint 4B numerical engine development**. The next responsible work is a *licensed normative dataset intake and independent engineering reference review*; implementation of production normative pressure formulas requires rights-cleared primary sources, actual applicability/interpretation decisions, signed independent calculation fixtures, and a separate authorization milestone.

**Begin implementation now. Deliver verified fixes, not merely architecture documents.**
