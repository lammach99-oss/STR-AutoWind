# Standards Data Intake and Independent Engineering Review Guide

## 1. Objective and Boundary

This document defines the strict, fail-closed intake process for licensed building standards data (ASCE 7-22 and AS/NZS 1170.2:2021) and independent engineering reference calculations.

**Safety Invariant:**
- No licensed standards text, copyrighted coefficient tables, purchased PDFs, proprietary spreadsheets, or unredacted client project models are permitted in Git, pull requests, or CI build artifacts.
- Production wind-pressure calculation (`CanCalculateNormativePressures`), engineering approval workflows, RealReadOnly pressure previews, and live CSI write operations remain **strictly disabled** across all modules until independently reviewed licensed source data and reference calculations are qualified.

---

## 2. Local Private Dataset Storage and .gitignore Rules

All real, licensed, or proprietary standards data must reside strictly in local, untracked private directories:

- **Local Private Root:** `fixtures/standards/private/`
- **Private Artifacts:** `artifacts/standards-private/`
- **Protected File Patterns:** `*.pdf`, `*.xlsx`, `*.csv`, `*.docx`

These paths are explicitly excluded in the repository's root `.gitignore`:
```gitignore
# Private licensed standards datasets and review evidence documents
fixtures/standards/private/
artifacts/standards-private/
*.pdf
```

Before every commit, verify via `git status` and `git diff` that no private files, directories, or licensed table excerpts are tracked or staged.

---

## 3. Data Rights and Legal Compliance Checklist

Prior to ingesting any coefficient data into local private storage, the following legal checks must be signed off:

1. **Licensing Authorization:** The practicing organization or engineer holds a valid, legal license to access and use the primary standard text (ASCE 7-22, AS/NZS 1170.2:2021).
2. **Private Local Use Only:** Extracted numerical coefficients are classified strictly as `DataRightsClassification.LocalLicensedOnly` or `Restricted`.
3. **Redistribution Prohibition:** Under no circumstances may licensed coefficient tables be published, committed to a public Git repository, embedded in public test fixtures, or distributed in application binaries.
4. **Synthetic Data Separation:** Public automated test suites and CI runners must execute exclusively against synthetic test fixtures (`DataRightsClassification.SyntheticTestData`).

---

## 4. Dataset Provenance and Integrity Verification

Each private dataset bundle must provide:
- `manifest.json` conforming to `StandardsDataBundleManifest` (Schema 1.0.0).
- Component JSON files declared with exact raw file SHA-256 hashes in `manifest.ComponentFiles`.
- `ContentHash` computed via the versioned **V3 Structural Canonical Semantic Hash** (`CANONICAL_STANDARDS_BUNDLE_V3`).
- Explicit `CanonicalHashAlgorithmVersion: "3.0"`.
- Primary standard citations referencing table, figure, and clause locators without reproducing standard prose or entire copyrighted tables.

### V3 Structural Canonical Semantic Hash Specification
The V3 hash eliminates delimiter-injection ambiguities by length-prefixing every item and field boundary:
- Prefix format: `[byte_length]:[utf8_bytes];`
- Sorted record keys (`StringComparer.Ordinal`), sorted categorical condition pairs, sorted numerical condition pairs.
- IEEE 754 double normalization: finite-only enforcement, normalizing `-0.0` to `+0.0`, round-trip invariant `ToString("R")`.
- Clean domain separation starting with `CANONICAL_STANDARDS_BUNDLE_V3`.

---

## 5. Reviewer and Preparer Separation of Duties

To prevent self-certification and conflicts of interest, the engineering review pipeline enforces four distinct statuses:

| Status | Verification Requirement | Gate Impact |
| :--- | :--- | :--- |
| **1. Self-Declared Metadata** | Preparer populates manifest fields, rights statement, and scope. | Incomplete; cannot calculate. |
| **2. Locally Checked Evidence Hash** | Preflight tool computes SHA-256 of private evidence document and matches `EvidenceDocumentHash`. | Provenance verified; cannot calculate. |
| **3. Independent Engineering Sign-off** | Registered Professional Engineer (PE/SE/CPEng) from an independent organization signs review memo. | Technical validity confirmed. |
| **4. Production Authorization** | Formal software milestone gate (Sprint 4B+) explicitly enabling production pressure calculations. | **Disabled in Sprint 4A.3**. |

**Enforcement Rule:** If `PreparerName` equals `ReviewerName` (case/whitespace insensitive), validation fails immediately with `STD_SELF_APPROVAL_REJECTED`.

---

## 6. Independent Engineering Reference Calculations

Before any standard calculator can be activated in production, it must be benchmarked against independent, hand-checked reference calculations for:
1. **Flat Roof (0° pitch)** — ASCE 7-22 Directional Procedure (Ch. 27) and AS/NZS 1170.2:2021 (Sec. 5).
2. **Gable Roof (e.g. 15°–30° pitch)** — ASCE 7-22 Directional Procedure and AS/NZS 1170.2:2021.

### Reference Calculation Schema
Reference calculation fixtures must specify:
- **Inputs:** Basic wind speed, exposure category, enclosure classification, mean roof height, ground elevation, roof pitch, building dimensions.
- **Expected Intermediate Factors:** `Kz`, `Kd`, `G`, `qz` (ASCE) or `Md`, `Mz,cat`, `Ms`, `Mt`, `Vdes,θ` (AS/NZS) with defined tolerances.
- **Expected Net Pressures:** Per-zone, per-wind-state net design pressures (e.g. `Zone-1:WX+/IP+`, `Zone-2:WY-/IP-`) with absolute (Pa) and relative tolerances.
- **Sign Conventions:** Explicit indication of outward suction (-) vs inward pressure (+).
- **Review Trace:** Full engineer signature, reviewer organization, date, and evidence document SHA-256.

---

## 7. Preflight Verification Tool

Use the built-in `StandardsDataBundleValidator.VerifyEvidenceDocument()` method to verify local evidence documents without exposing file contents:

```csharp
var (valid, message) = StandardsDataBundleValidator.VerifyEvidenceDocument(
    "fixtures/standards/private/asce7-22/review-memo.pdf",
    bundle.Manifest);

if (!valid)
{
    Console.WriteLine($"Preflight check failed: {message}");
}
```

---

## 8. Templates Provided in Repository

Public, rights-safe JSON templates are available in `fixtures/standards/templates/`:
- `template-manifest.json` — Sample manifest structure for licensed dataset intake.
- `template-coefficients.json` — Sample structured coefficient record array.
- `template-reference-calculation.json` — Sample flat-roof reference calculation fixture.

---

## 9. Outstanding Requirements Before Sprint 4B

The following external engineering and legal items must be provided before Sprint 4B (Normative Pressure Calculations) can begin:

1. **Licensed ASCE 7-22 Primary Source:** Verification of Table 26.5-1, Table 26.6-1, Table 26.10-1, Table 26.13-1, Figure 27.3-1, and Table 27.3-1 against licensed text.
2. **Licensed AS/NZS 1170.2:2021 Primary Source:** Verification of Section 2–5 tables and explicit confirmation of Amendment status from Standards Australia / Standards New Zealand primary sources.
3. **Independent Engineering Sign-off Memo:** PDF review document signed by an independent licensed engineer (PE/SE or CPEng).
4. **Hand Calculation Benchmark Fixtures:** Independently calculated flat and gable roof test cases with intermediate factors and net zone pressures.
