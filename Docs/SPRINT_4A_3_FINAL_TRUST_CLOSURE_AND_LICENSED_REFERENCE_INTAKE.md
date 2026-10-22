# Sprint 4A.3 — Final Standards-Data Trust Closure & Licensed Reference Intake Preflight

**Role:** Lead C#/.NET 8 structural-engineering software architect, security-minded data-integrity reviewer, and engineering QA lead.

**Repository:** https://github.com/lammach99-oss/STR-AutoWind  
**Branch:** Work from actual latest `origin/main`; direct verified commits to `main` only. Do not create PRs or force-push.  
**Independently verified baseline:** `6fa2b06bce661d015f246f35a668e824eed817fe`  
**CI:** https://github.com/lammach99-oss/STR-AutoWind/actions/runs/39748935682 — success, 279 non-physical automated tests, clean build, WPF smoke evidence.

## 0. Scope and acceptance philosophy

Sprint 4A.2 correctly improved source-byte/in-memory record binding, exact numerical-condition matching, and MWFRS/C&C retrieval isolation. **Do not broadly rewrite these systems.** This is a **bounded final trust-closure sprint** plus a practical, privacy-preserving intake kit for future engineer-reviewed licensed source data. Do not start another general-purpose hardening cycle after this unless a new reproducible P0 issue warrants it.

Source review of the actual HEAD found four remaining, reproducible weaknesses:

1. **P0 — V2 canonical hash is still ambiguous for nested collections.** `WritePrefixed(...)` operates on `string.Join(...)` results, not individual collection items. Different categorical maps can still serialize identically: `{"A":"B,C=D"}` and `{"A":"B", "C":"D"}` both become `A=B,C=D`. Similarly `Amendments=["A,B"]` and `["A","B"]` become `A,B`. Coverage records and numerical-condition keys can have analogous delimiter ambiguities. A length-prefix surrounding an *already ambiguous joined string* does not eliminate this collision class. The existing test only checks pipe delimiters **between record fields** and therefore does not cover this issue.
2. **P0 — byte-to-record coherence omits a field.** `StandardsDataBundleValidator.RecordsMatch()` does not compare `CoefficientRecord.ApplicabilityNotes`, even though it is included in the canonical semantic hash. An attacker or faulty caller could mutate in-memory applicability notes, recompute `Manifest.ContentHash`, retain unchanged verified component bytes, and evade `STD_RECORD_BYTES_MISMATCH`.
3. **P1 — source duplicate keys can throw instead of returning a structured failure.** The validator calls `deserializedRecords.ToDictionary(r => r.RecordKey, StringComparer.Ordinal)` before independently validating uniqueness in source records. Duplicates inside declared raw component bytes may cause an unhandled exception rather than a deterministic `StandardsValidationReport` with an issue code.
4. **P1 — system scope still uses string heuristics and an unknown-type fallback.** `StandardsDataBundle.MatchesSystem()` infers system from category substring and returns `true` for an unrecognized `systemType`. Ambiguous or unlabeled records must never acquire MWFRS/C&C eligibility through fallback string matching. A combined bundle can remain valid; only the queried procedure's eligible subset must be selected.

**Safety invariant:** `CanCalculateNormativePressures == false` for every bundle, regardless of rights, review metadata, hash or synthetic tests. Production wind-pressure calculation, approvals, RealReadOnly pressure previews and all live CSI writes remain disabled. The reported ETABS 125/125 physical checks are separate, agent-reported private qualification evidence; independent human sign-off and SAP2000 physical qualification are still pending.

## 1. Audit before changing code

Inspect the actual newest HEAD, relevant contracts and tests:

- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataBundle.cs`
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataBundleValidator.cs`
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/StandardsDataContracts.cs`
- `src/StructuralLoadAssistant.Engineering/StandardsDataFoundation/ReferenceCalculationContracts.cs`
- `tests/StructuralLoadAssistant.Tests/StandardsBundleCoherenceTests.cs`
- `tests/StructuralLoadAssistant.Tests/StandardsDataIntegrityHardeningTests.cs`
- `fixtures/standards/`, `.gitattributes`, `.gitignore`
- Existing `StandardsGate`, standard calculators, WPF read-only guards and SQLite snapshot interfaces.

Produce failing regression tests first, using rights-cleared synthetic values. Record what failed before correction and why existing tests did not detect it.

## 2. P0 — Proper structural canonical serialization

Implement a versioned **V3 canonical semantic encoding** (or demonstrably equivalent backward-compatible alternative) in which every structural boundary is unambiguous:

- Encode array/map lengths or counts and **every item and every key/value separately**, each with a type/field marker and byte length; do not create delimiter-joined maps before hashing.
- Encode sorted amendment IDs individually, sorted coverage entries field-by-field, sorted categorical-condition pairs field-by-field, and sorted numerical-condition pairs field-by-field using invariant round-trip double representation.
- Encode record count and each record's complete field sequence, including category, record key, table/figure, clause locator, unit, sign, interpolation, value, applicability notes and all condition maps.
- Specify deterministic sort order (`StringComparer.Ordinal`), UTF-8 encoding, null-vs-empty semantics, and numerical normalization rules (including finite-only and signed zero behavior). Include unambiguous algorithm/version domain separation.
- Avoid hash self-reference: `Manifest.ContentHash` is excluded from its own preimage.
- Define the exact manifest fields protected by the semantic hash and fields protected by separately verified raw component hashes; document any manifest/reviewer-evidence fields not covered by semantic hashing and why.
- **Never call V2 collision-proof.** Preserve V1/V2 *read compatibility only* for previously committed synthetic fixtures, with an explicit compatibility policy. New non-synthetic qualification manifests must use the new structural version. Unknown algorithm versions must fail closed. Do not silently relabel an existing manifest or rewrite hashes without an explicit migration step.
- Provide a deterministic offline, rights-safe synthetic fixture migration tool or documented command if V3 fixtures are updated. Preserve exact raw SHA-256 behavior across Windows/Linux checkout and demonstrate `.gitattributes` behavior.

### Required negative tests

1. Categorical map collision: `{"A":"B,C=D"}` versus `{"A":"B","C":"D"}` must produce different V3 digests.
2. Amendment collision: `["A,B"]` versus `["A","B"]` must produce different V3 digests.
3. Coverage category keys containing `:`, `,`, `=` must not collide with a different coverage list.
4. Numerical-condition map keys containing delimiters must not collide.
5. Record order and map insertion order must not affect the V3 digest; record content changes must.
6. Unicode/UTF-8 length accounting, empty/null cases, and unsupported hash versions must have deterministic results.
7. Show V2's actual prior ambiguity in a test or engineering note without weakening V3 acceptance.

## 3. P0 — Make source bytes and in-memory records exactly coherent

Harden `RecordsMatch()` or replace it with an auditable typed structural comparison derived from the actual deserialized source records.

- Compare **every** engineering-significant field, explicitly including `ApplicabilityNotes` (null versus non-null, and documented treatment of empty strings).
- Compare all categorical/numerical map memberships and values in both directions, key names, clause/table locators, categories, physical units, signs, interpolation, and records' stable IDs.
- Prefer exact `StringComparison.Ordinal` for integrity checks; do not treat case-only changes to signed data fields as equivalent unless a carefully justified normalized contract explicitly requires it.
- Check and report unexpected null fields, invalid map values, duplicate source keys, duplicate in-memory keys and empty records without a raw exception leaking as a fake successful validation.
- Avoid trusting `isBytesVerified` as an assertion of correctness without actually validating raw SHA-256 against the manifest.
- Keep `STD_RECORD_BYTES_MISMATCH` a FatalBlocker and preserve raw SHA-256 and semantic content hash checks.

### Required negative tests

- Change **only** `ApplicabilityNotes` in memory, recompute manifest canonical hash to match the mutated memory and leave raw bytes unchanged: validation must reject with `STD_RECORD_BYTES_MISMATCH`.
- Change **only** case in a significant unit, sign or source locator where the contract regards it as distinct: reject or document explicit normalization.
- Duplicate record keys within the raw component JSON (with a matching new raw SHA-256): return a structured validation issue rather than throwing.
- Alter one member of a categorical/numerical condition map while leaving verified source bytes unchanged: reject.
- Missing, extra, empty or unauthorized component-file entries must remain fail-closed.

## 4. P1 — Explicit procedure-scoped coefficient selection

Retain the ability to validate a combined ASCE dataset containing both MWFRS and C&C records; **do not** reject the whole bundle because the other system's records are present.

- Add or strengthen a typed, procedure-aware lookup boundary using `ProcedureSelection` or a specific enum/identifier contract rather than relying on loose `string systemType` and category `Contains("CC")` heuristics.
- Require a recognized system/procedure identity when selecting system-dependent external coefficients. Reject unsupported or unknown system types rather than `return true`.
- Require exact edition, selected procedure, method, limit-state and applicable category/conditions where the selected standard needs them. Do not infer a system from a missing field; treat genuinely common coefficient data through a separately documented category and explicit authorization rule.
- Keep categorical and numerical condition maps complete. Never use a numerical equality check as a substitute for an approved interpolation policy.
- Do not turn this into a family-agnostic normative roof coefficient calculator. ASCE and AS/NZS procedure applicability remains separate.
- Consider preserving the original `FindExactRecord` signature for legacy nonproduction tests while offering a new explicit, safe procedure-bound retrieval method for future numeric-engine consumers. Document that raw lookups alone cannot authorize engineering use.

### Required tests

- Combined MWFRS/C&C bundle validates as structurally intact.
- Valid MWFRS lookup cannot retrieve C&C records and vice versa.
- Unknown/misspelled system identifiers, missing required system tags and conflicting procedure fields fail closed.
- No category-name substring false positives; demonstrate an intentionally misleading category name.
- Wrong family/edition/method/limit state and ambiguous multiple matches are rejected.
- Exact numeric queries remain exact; interpolation remains separately approved.

## 5. Build a private licensed-reference intake preflight (without ingesting licensed data into Git)

The primary blocker after code hardening is **actual licensed source material and independent engineer-reviewed reference cases**. Create a practical, privacy-preserving intake pack instead of fabricating coefficients or opening Sprint 4B production math.

Deliver a public **template only**, e.g. `Docs/STANDARDS_DATA_INTAKE_AND_ENGINEER_REVIEW.md` and empty/example-safe JSON schema templates under a clearly marked synthetic/template-only directory, covering:

- Standards family, edition, amendments/errata/supplements with explicit applicability decision and source evidence; AS/NZS amendment status must be independently confirmed from licensed primary sources, not auto-approved by name.
- Legal right to use licensed extracts locally, restrictions on redistribution and a publication review checklist.
- Local private dataset root and `.gitignore` rules; no private workbooks, purchased PDFs, CSV extracts, screenshots, scanned tables, customer data or evidence documents in Git or CI artifacts.
- Dataset's byte SHA-256, V3 semantic hash, source-document fingerprints, exact clause/table/figure locators (without reproducing licensed content), and component inventory.
- Procedure/zone/case coverage matrix, dimensional units, sign conventions, expected interpolation method and domains.
- Independent reference-calculation template for **flat and gable roofs** for each target standard, containing complete inputs, intermediate factors, coefficient IDs, explicit `ZoneId:WindState` outputs, units, tolerances, source locators, reviewer trace and evidence SHA-256.
- Reviewer/preparer separation; distinguish **self-declared metadata**, **locally checked evidence hashes**, **actual independent engineering sign-off**, and **production authorization** as separate statuses.
- Optional local preflight verifying an evidence file exists and its computed SHA-256 matches the declared evidence hash; no identity-signature verification claim unless actual signature/trust validation has been implemented. Do not upload or print private contents.
- A clear owner handoff listing exactly which private files and independent review decisions must be supplied before numeric pressure implementation.

Do not add any official standard coefficient, unofficial web coefficient, copyrighted table, proprietary workbook or real normative pressure formula to the repository. If real licensed data is unavailable, stop at a complete validated **template/intake kit**, report blocked, and do not pretend it is qualified.

## 6. Engineering and application safety gates

- `CanCalculateNormativePressures` must remain **false for all bundles** in this sprint; there must be tests explicitly proving this for all rights categories and plausible reviewer metadata.
- Preserve separate gates for dataset integrity, license rights, independent reference qualification, procedure applicability, shell geometry, pressure-mapping review and potential future CSI write approval.
- All RealReadOnly operations remain read-only; no production pressure previews, approvals, ETABS/SAP2000 setters or real load assignment.
- Preserve existing WPF geometry/mapping/persistence behavior, immutable SQLite drafts, existing physical benchmark runner and historical reports.
- No live ETABS benchmark rerun is necessary just to prove standards-data structural changes; distinguish that from the agent-reported 125/125 physical benchmark and independent human sign-off status.

## 7. Verification and acceptance

Use the pinned/locked SDK and dependencies. Run:

- `dotnet restore StructuralLoadAssistant.sln --locked-mode -m:1 -nr:false`
- `dotnet build StructuralLoadAssistant.sln --configuration Debug --no-restore -m:1 -nr:false`
- `dotnet build StructuralLoadAssistant.sln --configuration Release --no-restore -m:1 -nr:false`
- `dotnet test tests/StructuralLoadAssistant.Tests --configuration Release --no-build --no-restore -m:1 -nr:false --filter "Category!=PhysicalCsi"`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-wpf.ps1 -Configuration Release`

Add meaningful deterministic red/green tests rather than a single happy-path hash test. Keep all six WPF smoke scenarios passing. Confirm no proprietary files or real datasets appear in `git status`, changed-file diffs, artifacts, or CI upload lists.

**Acceptance gates:**

1. V3 nested-collection hash collisions are eliminated by structural encoding and the supplied counterexamples have distinct hashes.
2. Every field of records parsed from verified source bytes is compared to the in-memory representation, especially `ApplicabilityNotes`.
3. Duplicate keys and malformed raw source records return structured fail-closed errors rather than uncontrolled exceptions.
4. Procedure lookup does not infer system membership by substring and does not accept unrecognized system types.
5. Private licensed-source/reference intake workflow is documented and demonstrably non-distributive.
6. CI, tests and all six WPF smoke processes remain green.
7. `CanCalculateNormativePressures == false` remains invariant, and no real CSI writes or normative coefficients are introduced.

Commit and push only verified, public-safe files directly to `main`; no PR, feature branch or force-push. If any real-world license/reviewer inputs are unavailable, clearly label that task **Blocked—Owner/Independent Reviewer Input Required**, not Completed.

## 8. Final agent handoff

Report:

- Exact `git rev-parse HEAD` SHA, GitHub Actions run URL/status and count of automated tests.
- Files changed and principal engineering decisions, including V1/V2 compatibility and V3 encoding specification.
- Each reproduced original failure and new red/green regression test.
- A concise source-byte/in-memory coherence and procedure-scope risk verdict.
- Owner-ready location of the private licensed-source intake template and reference calculation checklist.
- Explicit list of missing licensed sources, amendment-applicability confirmations, rights permissions and independently reviewed calculations.
- Confirmation of zero published licensed standard tables, zero production normative pressures, zero real engineering approvals and zero live CSI write operations.
- A bounded next-step recommendation: **begin a narrowly scoped Sprint 4B reference-calculation sandbox only when actual rights-cleared, independently reviewed inputs exist**, or continue private evidence collection without pretending those inputs exist.

**Finish the bounded trust closure and prepare for real engineer-reviewed normative data. Do not start an endless series of infrastructure hardening sprints or create a fake standards calculation engine.**
