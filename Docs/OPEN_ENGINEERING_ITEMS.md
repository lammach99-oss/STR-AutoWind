# Open engineering items

Sprint 4A through 4B.0.1 establish the hardened versioned data foundation, manifest provenance, rights classification, component byte SHA256 verification, record-to-byte cryptographic coherence, structural canonical semantic hashing (V3; legacy V1/V2 for backward compatibility only), and fail-closed validation gates for ASCE 7-22 and AS/NZS 1170.2:2021. However, no licensed normative dataset is installed. Demo pressures and wall materials remain synthetic/illustrative. Both standard calculation engines and real engineering approval/assignment remain strictly blocked.

## Status baseline and remaining engineering tasks

- **Software automated verification:** 325 unit and integration tests pass in both Debug and Release configurations with zero failures and zero skips. All six WPF smoke processes pass cleanly.
- **Physical ETABS benchmark execution:** Agent-reported Passed on private local model `artifacts/physical-benchmark-private/ETABS-BENCHMARK.EDB` in ETABS 23.2.0 (125/125 physical checks passed, exit code 0). The benchmark geometry pipeline is *fixture-directed* for roof-plane grouping and does not prove automatic roof-plane discovery on arbitrary real models.
- **Independent external engineering sign-off:** Not established by the public repository; remains a separate required verification gate.
- **SAP2000 physical qualification:** Remains Pending (no disposable SAP2000 27 model was qualified).
- **Standards normative data qualification:** Blocked / Not Started until licensed applicable datasets and independently checked reference fixtures are provided.

## Sprint 4A, 4A.1 & 4A.2 deliverables and gates

- Versioned, strongly typed standards data contracts (`StandardsDataContracts.cs`) separating ASCE 7-22 and AS/NZS 1170.2:2021 domain models without false shared abstractions.
- Comprehensive canonical semantic hashing (`StandardsDataBundle.ComputeCanonicalHash()`) incorporating all engineering-significant metadata (locators, conditions, units, sign conventions, amendments, coverage matrix) while remaining invariant to JSON formatting/whitespace on disk.
- Structural canonical semantic hashing algorithm V3 (`CANONICAL_STANDARDS_BUNDLE_V3`) with length-prefixed encoding of individual items/fields preventing delimiter injection; legacy V1/V2 preserved for fixture compatibility only.
- Source-byte and in-memory record coherence validation: rejects bundles where in-memory records differ from records parsed from verified raw component bytes (`STD_RECORD_BYTES_MISMATCH`).
- Exact bidirectional numerical matching in `FindExactRecord()`, rejecting partial numerical condition maps and off-value queries.
- Procedure-scoped system isolation: bundles legitimately containing both MWFRS and C&C records pass schema validation; system isolation is enforced at record lookup time.
- Mandatory component byte presence and path traversal validation (`StandardsDataBundleValidator.cs`), enforcing raw SHA256 matching and safe relative paths.
- Strict fail-closed validation engine (`StandardsDataBundleValidator.cs`) enforcing all 9 fail-closed validation rules: manifest integrity, SHA256 checksums, component file verification, mandatory coverage completeness, no-extrapolation, no cross-categorical interpolation, explicit amendment applicability for AS/NZS 1170.2:2021, and independent reviewer attestation (self-approval rejected).
- Strict promotion prevention: `CanCalculateNormativePressures` is strictly `false` across the board, preventing premature calculation eligibility before Sprint 4B.
- Reviewer independence check: verifies that preparer and reviewer identities are distinct; self-approval is strictly rejected.
- Reference calculation benchmark contracts (`ReferenceCalculationContracts.cs`) with strict absolute and relative tolerances for intermediate factors and net pressures, requiring exact `(ZoneId:StateId)` keys with no zone-only fallback.
- Licensed applicable normative datasets for ASCE/SEI 7-22 and AS/NZS 1170.2:2021 must be installed before engineering calculation and approval can be enabled.
- SAP2000 physical qualification remains pending.

## Standards review

- ASCE/SEI 7-22: licensed applicable amendments/supplements/errata manifest; risk/design speed source and strength/service distinction; exposure/height and ground-elevation factors; directionality/topography; reference-height rules; gust/dynamic effects; external wall/roof zones and coefficients; enclosure/internal-pressure states; combination algebra; interpolation and out-of-range policy; independently signed regression examples. Use exact governing clauses from licensed sources before implementation. Do not reuse mixed 7-16/7-22 workbook constants.
- AS/NZS 1170.2:2021: confirm Amendment 1:2023 and Amendment 2:2024 applicability; regional speed/return period; directionality/climate; terrain categories including TC2.5; current terrain/height table; shielding/topography; dynamics; wall/roof aerodynamic factors; internal-pressure cases; zone dimensions/reference heights; interpolation. Do not reuse pitch>10 -> -0.3 shortcut.
- Signed engineer-reviewed normative fixtures and redistribution rights are required before any standards compliance claim.

## Workbook baseline and risks

Private reference workbooks were inspected structurally without modification or execution. Hash/sheet/VBA inventories and read-only copies remain local and excluded from public source. One workbook differs from its earlier baseline, so parity cannot be assumed. Full cell/VBA audit and independent recalculation remain outstanding.

Known AS/NZS risks from contract: older/mixed terrain layouts; incomplete categories; suspicious multiplier literals; pitch shortcut; incomplete internal-pressure engine; VBA/interpolation verification. ASCE risks: 7-16 labels; mixed-edition exposure constants; algebraically misleading internal-pressure labels; Wind-Y roof references to Wind-X cells. These are recorded audit leads, not independently qualified corrections.

## Application scope

- Read-only installed CSI adapter implemented and physically qualified on ETABS 23.2.0 for geometry and load readback (agent-reported 125/125 checks on private benchmark model; independent engineering sign-off pending).
- Real CSI write operations remain strictly disabled; no production load assignment, model mutation, unit change, or shell subdivision is implemented.
- Real writes need backup/SaveAs, current geometry/load preflight, ownership, conflicts, partial-failure recovery, rollback and readback qualification.
- Arbitrary facade UI frame selection/top/bottom tributary boundaries, advanced geometry and local sloping-ground strategy are not implemented. Current demo assumes explicit complete horizontal pairs and a datum of zero.
- Wall preview only; nonuniform openings need explicit geometry or reviewed override model.
- Roof vertical/degenerate projected polygons are unsupported; manual normal ambiguity blocks mapping until resolved.
- Project draft-resume, persistent fake ledgers, multi-record transaction orchestration, concurrency audit, PDF/DOCX exporters and 3D view remain future work.
- NuGet network access was needed for initial restore and succeeded through approved escalation. No runtime proprietary dependency is required.
