using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

/// <summary>
/// Strict fail-closed validator for standards dataset bundles, enforcing provenance, checksums,
/// rights clearance, mandatory coverage, interpolation constraints, and non-extrapolation policies.
/// </summary>
public static class StandardsDataBundleValidator
{
    private static readonly ImmutableHashSet<string> SupportedEditionsAsce = ["7-22", "ASCE/SEI 7-22"];
    private static readonly ImmutableHashSet<string> SupportedEditionsAsNz = ["2021", "AS/NZS 1170.2:2021"];

    /// <summary>
    /// Validates an in-memory or directory-loaded standards bundle against all fail-closed qualification gates.
    /// </summary>
    public static StandardsValidationReport Validate(
        StandardsDataBundle bundle,
        ProcedureSelection? procedure = null,
        DataProvenance provenance = DataProvenance.Unverified,
        string rawComponentFolder = "")
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var issues = ImmutableArray.CreateBuilder<StandardsValidationIssue>();
        var manifest = bundle.Manifest;

        void AddIssue(string code, StandardsValidationSeverity severity, string location, string? recordKey, string message, string? remediation = null)
        {
            issues.Add(new StandardsValidationIssue(code, severity, location, recordKey, message, remediation));
        }

        // 1. Manifest and Edition / Amendment Check
        if (string.IsNullOrWhiteSpace(manifest.BundleId))
        {
            AddIssue("STD_MANIFEST_ID_MISSING", StandardsValidationSeverity.FatalBlocker, "Manifest", null, "Manifest is missing a required BundleId.");
        }

        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion) || !manifest.SchemaVersion.StartsWith("1.", StringComparison.Ordinal))
        {
            AddIssue("STD_SCHEMA_UNSUPPORTED", StandardsValidationSeverity.FatalBlocker, "Manifest.SchemaVersion", null, $"Unsupported schema version ''{manifest.SchemaVersion}''. Only 1.x is supported.");
        }

        bool isHashVersionSupported = string.Equals(manifest.CanonicalHashAlgorithmVersion, "1.0", StringComparison.Ordinal) ||
            string.Equals(manifest.CanonicalHashAlgorithmVersion, "2.0", StringComparison.Ordinal) ||
            string.Equals(manifest.CanonicalHashAlgorithmVersion, "3.0", StringComparison.Ordinal);

        if (!isHashVersionSupported)
        {
            AddIssue("STD_HASH_VERSION_UNSUPPORTED", StandardsValidationSeverity.FatalBlocker, "Manifest.CanonicalHashAlgorithmVersion", null,
                $"Unsupported canonical hash version ''{manifest.CanonicalHashAlgorithmVersion}''. Only 1.0, 2.0, and 3.0 are supported.");
        }

        switch (manifest.Family)
        {
            case StandardFamily.Asce7:
                if (!SupportedEditionsAsce.Contains(manifest.Edition))
                {
                    AddIssue("STD_EDITION_UNSUPPORTED", StandardsValidationSeverity.Error, "Manifest.Edition", null, $"Edition ''{manifest.Edition}'' is not supported for ASCE 7 family.");
                }
                break;

            case StandardFamily.AsNz1170_2:
                if (!SupportedEditionsAsNz.Contains(manifest.Edition))
                {
                    AddIssue("STD_EDITION_UNSUPPORTED", StandardsValidationSeverity.Error, "Manifest.Edition", null, $"Edition ''{manifest.Edition}'' is not supported for AS/NZS 1170.2 family.");
                }
                // Verify required amendments
                if (!manifest.Amendments.Contains("Amendment 1:2023", StringComparer.OrdinalIgnoreCase) ||
                    !manifest.Amendments.Contains("Amendment 2:2024", StringComparer.OrdinalIgnoreCase))
                {
                    AddIssue("STD_AMENDMENT_UNRESOLVED", StandardsValidationSeverity.Error, "Manifest.Amendments", null,
                        "AS/NZS 1170.2:2021 datasets must declare explicit resolution of Amendment 1:2023 and Amendment 2:2024.");
                }
                break;

            default:
                AddIssue("STD_FAMILY_UNSUPPORTED", StandardsValidationSeverity.FatalBlocker, "Manifest.Family", null, $"Unsupported standard family ''{manifest.Family}''.");
                break;
        }

        if (manifest.ComponentFiles == null || manifest.ComponentFiles.Count == 0)
        {
            AddIssue("STD_COMPONENT_FILES_EMPTY", StandardsValidationSeverity.FatalBlocker, "Manifest.ComponentFiles", null,
                "Manifest does not declare any component files. At least one component file is required.");
        }

        var componentFiles = manifest.ComponentFiles ?? ImmutableDictionary<string, string>.Empty;
        // 2. Component File Checksums, Safe Paths, and Tampering Verification
        var verifiedBytes = new Dictionary<string, ImmutableArray<byte>>(StringComparer.OrdinalIgnoreCase);

        // Populate from bundle''s loaded raw bytes
        foreach (var (k, v) in bundle.RawComponentBytes)
        {
            verifiedBytes[k] = v;
        }

        // If rawComponentFolder is provided, read any files from disk
        if (!string.IsNullOrWhiteSpace(rawComponentFolder) && Directory.Exists(rawComponentFolder))
        {
            foreach (var (relPath, _) in componentFiles)
            {
                try
                {
                    StandardsDataBundle.ValidateSafeRelativePath(relPath);
                    var fullPath = Path.Combine(rawComponentFolder, relPath);
                    if (File.Exists(fullPath))
                    {
                        verifiedBytes[relPath] = File.ReadAllBytes(fullPath).ToImmutableArray();
                    }
                }
                catch (ArgumentException ex)
                {
                    AddIssue("STD_UNSAFE_PATH", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", relPath, ex.Message);
                }
            }
        }

        // If no component bytes exist, fail-closed immediately
        if (verifiedBytes.Count == 0 && componentFiles.Count > 0)
        {
            AddIssue("STD_INTEGRITY_UNVERIFIED", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", null,
                "Component file bytes were not supplied or could not be verified. Integrity verification is mandatory.");
        }
        else
        {
            foreach (var (relPath, expectedSha) in componentFiles)
            {
                try
                {
                    StandardsDataBundle.ValidateSafeRelativePath(relPath);
                }
                catch (ArgumentException ex)
                {
                    AddIssue("STD_UNSAFE_PATH", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", relPath, ex.Message);
                    continue;
                }

                if (!verifiedBytes.TryGetValue(relPath, out var bytes) || bytes.IsDefaultOrEmpty)
                {
                    AddIssue("STD_COMPONENT_MISSING", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", relPath,
                        $"Component file ''{relPath}'' declared in manifest was not provided or is empty.");
                    continue;
                }

                var actualSha = Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
                if (!string.Equals(expectedSha, actualSha, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue("STD_HASH_MISMATCH", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", relPath,
                        $"Checksum mismatch for ''{relPath}'': manifest declared ''{expectedSha}'', computed ''{actualSha}''. Possible file tampering.");
                }
            }

            // Check for undeclared extra component files
            foreach (var extraKey in verifiedBytes.Keys)
            {
                if (!componentFiles.ContainsKey(extraKey))
                {
                    AddIssue("STD_EXTRA_COMPONENT", StandardsValidationSeverity.Error, "ComponentFiles", extraKey,
                        $"Component file ''{extraKey}'' was provided but is not declared in manifest.");
                }
            }

            // Cryptographically bind in-memory records to parsed bytes
            if (verifiedBytes.Count > 0)
            {
                ImmutableArray<CoefficientRecord> deserializedRecords = ImmutableArray<CoefficientRecord>.Empty;
                bool deserializationFailed = false;
                try
                {
                    deserializedRecords = StandardsDataBundle.DeserializeRecordsFromBytes(
                        verifiedBytes.ToImmutableDictionary(), componentFiles.Keys);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    deserializationFailed = true;
                    AddIssue("STD_COMPONENT_JSON_MALFORMED", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", null,
                        $"Failed to deserialize component JSON bytes: {ex.Message}");
                }
                catch (Exception ex)
                {
                    deserializationFailed = true;
                    AddIssue("STD_COMPONENT_LOAD_FAILED", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", null,
                        $"Failed to parse component bytes: {ex.Message}");
                }
                if (!deserializationFailed)
                {

                var duplicateRawKeys = deserializedRecords.GroupBy(r => r.RecordKey, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateRawKeys.Count > 0)
                {
                    foreach (var dup in duplicateRawKeys)
                    {
                        AddIssue("STD_DUPLICATE_SOURCE_KEY", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", dup,
                            $"Duplicate record key ''{dup}'' detected in raw component bytes.");
                    }
                }

                if (bundle.Records.Length != deserializedRecords.Length)
                {
                    AddIssue("STD_RECORD_BYTES_MISMATCH", StandardsValidationSeverity.FatalBlocker, "Records", null,
                        $"In-memory records count ({bundle.Records.Length}) does not match records parsed from verified raw component bytes ({deserializedRecords.Length}). Possible record manipulation.");
                }
                else
                {
                    var bytesRecordsMap = new Dictionary<string, List<CoefficientRecord>>(StringComparer.Ordinal);
                    foreach (var r in deserializedRecords)
                    {
                        if (!bytesRecordsMap.TryGetValue(r.RecordKey, out var list))
                        {
                            list = new List<CoefficientRecord>();
                            bytesRecordsMap[r.RecordKey] = list;
                        }
                        list.Add(r);
                    }

                    foreach (var r in bundle.Records)
                    {
                        if (!bytesRecordsMap.TryGetValue(r.RecordKey, out var matchingList) || !matchingList.Any(br => RecordsMatch(r, br)))
                        {
                            AddIssue("STD_RECORD_BYTES_MISMATCH", StandardsValidationSeverity.FatalBlocker, "Records", r.RecordKey,
                                $"In-memory record ''{r.RecordKey}'' data differs from verified raw component bytes. Stale or manipulated records detected.");
                        }
                    }
                }
                }
            }
        }

        // 3. Record Value Sanity, Units, and Non-Finite Checks
        bool hasNonFiniteValues = false;
        foreach (var r in bundle.Records)
        {
            if (string.IsNullOrWhiteSpace(r.RecordKey))
            {
                AddIssue("STD_RECORD_KEY_MISSING", StandardsValidationSeverity.FatalBlocker, "Records", null, "Record is missing RecordKey.");
            }
            if (!double.IsFinite(r.Value))
            {
                hasNonFiniteValues = true;
                AddIssue("STD_NONFINITE_VALUE", StandardsValidationSeverity.FatalBlocker, "Records", r.RecordKey, $"Record ''{r.RecordKey}'' has non-finite value ''{r.Value}''.");
            }
            foreach (var (k, v) in r.NumericalConditions)
            {
                if (!double.IsFinite(v))
                {
                    hasNonFiniteValues = true;
                    AddIssue("STD_NONFINITE_NUMERICAL_CONDITION", StandardsValidationSeverity.FatalBlocker, "Records", r.RecordKey,
                        $"Record ''{r.RecordKey}'' has non-finite numerical condition ''{k}'' with value ''{v}''.");
                }
            }
            if (string.IsNullOrWhiteSpace(r.Unit))
            {
                AddIssue("STD_UNIT_MISSING", StandardsValidationSeverity.Error, "Records", r.RecordKey, $"Record ''{r.RecordKey}'' is missing explicit physical unit declaration.");
            }
            if (string.IsNullOrWhiteSpace(r.ClauseReference))
            {
                AddIssue("STD_CLAUSE_MISSING", StandardsValidationSeverity.Warning, "Records", r.RecordKey, $"Record ''{r.RecordKey}'' lacks source clause locator.");
            }
        }

        // 4. Duplicate and Conflicting Record Keys
        var duplicateKeys = bundle.Records.GroupBy(r => r.RecordKey, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicateKeys)
        {
            AddIssue("STD_DUPLICATE_KEY", StandardsValidationSeverity.FatalBlocker, "Records", dup, $"Duplicate record key ''{dup}'' detected. Keys must be globally unique within bundle.");
        }

        // 5. Canonical Semantic Content Hash Verification
        if (isHashVersionSupported && !hasNonFiniteValues)
        {
            try
            {
                var computedCanonicalHash = bundle.ComputeCanonicalHash();
                if (string.IsNullOrWhiteSpace(manifest.ContentHash) ||
                    !string.Equals(computedCanonicalHash, manifest.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue("STD_CANONICAL_HASH_MISMATCH", StandardsValidationSeverity.FatalBlocker, "Manifest.ContentHash", null,
                        $"Computed canonical semantic hash ''{computedCanonicalHash}'' does not match manifest ContentHash ''{manifest.ContentHash}''. Possible metadata modification or tampering.");
                }
            }
            catch (Exception ex)
            {
                AddIssue("STD_CANONICAL_HASH_FAILED", StandardsValidationSeverity.FatalBlocker, "Manifest.ContentHash", null,
                    $"Failed to compute canonical semantic hash: {ex.Message}");
            }
        }
        else if (string.IsNullOrWhiteSpace(manifest.ContentHash))
        {
            AddIssue("STD_MANIFEST_HASH_MISSING", StandardsValidationSeverity.FatalBlocker, "Manifest.ContentHash", null,
                "Manifest is missing a required ContentHash.");
        }

        // 6. Mandatory Coverage Verification
        if (manifest.CoverageMatrix.IsDefaultOrEmpty)
        {
            AddIssue("STD_COVERAGE_UNDECLARED", StandardsValidationSeverity.Error, "Manifest.CoverageMatrix", null, "Manifest does not declare a mandatory procedure coverage matrix.");
        }
        else
        {
            foreach (var item in manifest.CoverageMatrix)
            {
                var available = bundle.GetRecordsForCategory(item.CategoryKey).Length;
                if (item.IsRequired && available == 0)
                {
                    AddIssue("STD_MANDATORY_CATEGORY_EMPTY", StandardsValidationSeverity.FatalBlocker, "CoverageMatrix", item.CategoryKey,
                        $"Mandatory category ''{item.CategoryKey}'' ({item.Description}) contains zero records in this dataset.");
                }
                else if (item.AvailableRecordCount > 0 && available != item.AvailableRecordCount)
                {
                    AddIssue("STD_COVERAGE_COUNT_MISMATCH", StandardsValidationSeverity.Error, "CoverageMatrix", item.CategoryKey,
                        $"Coverage declared {item.AvailableRecordCount} records for ''{item.CategoryKey}'', but dataset contains {available}.");
                }
            }
        }

        // 7. Procedure Specific Isolation and Cross-Standard / Cross-System Conflation Checks
        if (procedure != null)
        {
            if (procedure.Family != manifest.Family)
            {
                AddIssue("STD_FAMILY_MISMATCH", StandardsValidationSeverity.FatalBlocker, "ProcedureSelection", null,
                    $"Procedure family ''{procedure.Family}'' does not match manifest family ''{manifest.Family}''.");
            }
        }

        // 8. Internal Pressure Separate Sign Alternatives Check (+ / -)
        var ipRecords = bundle.GetRecordsForCategory("InternalPressure");
        if (ipRecords.Length > 0)
        {
            bool hasPositive = ipRecords.Any(r => r.CategoricalConditions.TryGetValue("InternalPressureState", out var s) && s.Contains("+"));
            bool hasNegative = ipRecords.Any(r => r.CategoricalConditions.TryGetValue("InternalPressureState", out var s) && s.Contains("-"));
            if (!hasPositive || !hasNegative)
            {
                AddIssue("STD_INTERNAL_PRESSURE_INCOMPLETE", StandardsValidationSeverity.Error, "InternalPressure", null,
                    "Internal pressure coefficients must provide both positive (+) and negative (-) alternative design states.");
            }
        }

        // 9. Rights Classification & Synthetic Test Data Blocker
        bool isSynthetic = manifest.Rights == DataRightsClassification.SyntheticTestData || provenance == DataProvenance.SyntheticDemoFixture;

        if (isSynthetic)
        {
            AddIssue("STD_SYNTHETIC_PROMOTION_BLOCKED", StandardsValidationSeverity.FatalBlocker, "Rights", null,
                "Synthetic/test-only standards data cannot be promoted to production calculation or used for engineering design.");
        }

        if (manifest.Rights == DataRightsClassification.Unknown)
        {
            AddIssue("STD_RIGHTS_UNKNOWN", StandardsValidationSeverity.Error, "Rights", null,
                "Dataset intellectual property rights status is Unknown. Rights must be positively resolved.");
        }

        // 10. Independent Engineering Review Trace Check
        var review = manifest.ReviewTrace;
        if (review == null)
        {
            AddIssue("STD_REVIEW_TRACE_MISSING", StandardsValidationSeverity.FatalBlocker, "ReviewTrace", null, "Manifest missing human engineering review trace.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(review.ReviewerName) || string.IsNullOrWhiteSpace(review.ReviewerOrganization))
            {
                AddIssue("STD_REVIEWER_UNIDENTIFIED", StandardsValidationSeverity.Error, "ReviewTrace.Reviewer", null, "Independent reviewer or organization is unidentified.");
            }
            if (string.Equals(review.PreparerName?.Trim(), review.ReviewerName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                AddIssue("STD_SELF_APPROVAL_REJECTED", StandardsValidationSeverity.FatalBlocker, "ReviewTrace.Reviewer", null,
                    "Independent review cannot be performed by data preparer. Self-approval is strictly rejected.");
            }
            if (!review.IsIndependentReview)
            {
                AddIssue("STD_SELF_APPROVAL_REJECTED", StandardsValidationSeverity.Error, "ReviewTrace.IsIndependentReview", null,
                    "Independent review must be attested by a reviewer distinct from data preparer. Self-approval is rejected.");
            }
            if (string.IsNullOrWhiteSpace(review.EvidenceDocumentHash) && !isSynthetic)
            {
                AddIssue("STD_EVIDENCE_HASH_MISSING", StandardsValidationSeverity.Error, "ReviewTrace.EvidenceDocumentHash", null,
                    "Production qualification requires a cryptographic hash of the reviewed evidence document.");
            }
            else if (!isSynthetic && !string.IsNullOrWhiteSpace(review.EvidenceDocumentHash))
            {
                var hash = review.EvidenceDocumentHash.Trim();
                if (hash.Length != 64 || !hash.All(c => Uri.IsHexDigit(c)))
                {
                    AddIssue("STD_EVIDENCE_HASH_INVALID", StandardsValidationSeverity.Error, "ReviewTrace.EvidenceDocumentHash", null,
                        "Evidence document hash must be a valid 64-character hexadecimal SHA-256 digest.");
                }
            }
        }

        // Gate Outcome Determination
        bool hasFatal = issues.Any(i => i.Severity == StandardsValidationSeverity.FatalBlocker);
        bool hasError = issues.Any(i => i.Severity == StandardsValidationSeverity.Error);

        StandardsGateOutcome outcome;
        StandardDataLifecycleState state;
        string summary;

        if (hasFatal || hasError)
        {
            if (isSynthetic)
            {
                outcome = StandardsGateOutcome.UnqualifiedData;
                state = StandardDataLifecycleState.Unreviewed;
                summary = "Dataset is SYNTHETIC TEST FIXTURE. Strictly blocked from engineering calculation.";
            }
            else
            {
                outcome = StandardsGateOutcome.InvalidData;
                state = StandardDataLifecycleState.Invalid;
                summary = $"Standards data validation FAILED: {issues.Count(i => i.Severity >= StandardsValidationSeverity.Error)} errors/blockers found.";
            }
        }
        else if (review != null && !review.IsIndependentReview)
        {
            outcome = StandardsGateOutcome.MissingReferenceEvidence;
            state = StandardDataLifecycleState.Unreviewed;
            summary = "Dataset lacks verified independent engineering review evidence.";
        }
        else if (isSynthetic)
        {
            outcome = StandardsGateOutcome.UnqualifiedData;
            state = StandardDataLifecycleState.Unreviewed;
            summary = "Dataset is SYNTHETIC TEST FIXTURE. Passed schema checks, but blocked from production.";
        }
        else
        {
            outcome = StandardsGateOutcome.ReadyForIndependentReview;
            state = StandardDataLifecycleState.ReadyForQualification;
            summary = "Dataset passed schema, integrity, coverage, and review checks; ready for reference benchmark evaluation.";
        }

        // In Sprint 4A.1, CanCalculateNormativePressures is STRICTLY FALSE across the board!
        // No code path exists to authorize normative pressure calculations prior to Sprint 4B.
        bool canCalculate = false;

        return new StandardsValidationReport(
            outcome,
            canCalculate,
            issues.ToImmutable(),
            summary,
            manifest.BundleId,
            manifest.ContentHash,
            state);
    }

    /// <summary>
    /// Validates parameter input values against an explicit domain, rejecting extrapolation.
    /// </summary>
    public static void ValidateDomain(ParameterDomain domain, double value)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (!double.IsFinite(value))
            throw new ArgumentException($"Parameter ''{domain.ParameterName}'' value is non-finite.", domain.ParameterName);

        if (!double.IsFinite(domain.MinValue) || !double.IsFinite(domain.MaxValue) || domain.MinValue > domain.MaxValue)
            throw new InvalidOperationException($"Parameter domain ''{domain.ParameterName}'' has invalid bounds [{domain.MinValue}, {domain.MaxValue}].");

        if (value < domain.MinValue || value > domain.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                domain.ParameterName,
                value,
                $"Value {value:F4} {domain.Unit} is outside the valid applicability domain [{domain.MinValue:F4}, {domain.MaxValue:F4}] for parameter ''{domain.ParameterName}''. Extrapolation is strictly rejected.");
        }
    }

    /// <summary>
    /// Enforces interpolation policies, rejecting interpolation across categorical boundaries, units, sign conventions, or extrapolation.
    /// </summary>
    public static double Interpolate1D(
        CoefficientRecord lower,
        CoefficientRecord upper,
        string parameterKey,
        double targetValue)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);

        if (lower.Interpolation == InterpolationPolicy.Categorical || upper.Interpolation == InterpolationPolicy.Categorical)
        {
            throw new InvalidOperationException($"Interpolation is strictly forbidden across categorical boundaries for parameter ''{parameterKey}''.");
        }

        if (lower.Interpolation == InterpolationPolicy.None || upper.Interpolation == InterpolationPolicy.None)
        {
            throw new InvalidOperationException($"InterpolationPolicy.None requires exact parameter match for ''{parameterKey}''.");
        }

        if (lower.Interpolation == InterpolationPolicy.Bilinear2D || upper.Interpolation == InterpolationPolicy.Bilinear2D)
        {
            throw new InvalidOperationException($"Bilinear2D interpolation is not supported by Interpolate1D for ''{parameterKey}''.");
        }

        // Same category
        if (!string.Equals(lower.ParameterCategory, upper.ParameterCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot interpolate across different parameter categories (''{lower.ParameterCategory}'' vs ''{upper.ParameterCategory}'').");
        }

        // Same unit
        if (!string.Equals(lower.Unit, upper.Unit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot interpolate across differing physical units (''{lower.Unit}'' vs ''{upper.Unit}'').");
        }

        // Same sign convention
        if (!string.Equals(lower.SignConvention, upper.SignConvention, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot interpolate across differing sign conventions (''{lower.SignConvention}'' vs ''{upper.SignConvention}'').");
        }

        // Same table or figure
        if (!string.Equals(lower.TableOrFigure, upper.TableOrFigure, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot interpolate across differing source tables (''{lower.TableOrFigure}'' vs ''{upper.TableOrFigure}'').");
        }

        // Check that categorical conditions match exactly in BOTH directions (bidirectional exact match)
        if (lower.CategoricalConditions.Count != upper.CategoricalConditions.Count)
        {
            throw new InvalidOperationException("Cannot interpolate across asymmetric categorical regimes.");
        }

        foreach (var (k, v) in lower.CategoricalConditions)
        {
            if (!upper.CategoricalConditions.TryGetValue(k, out var uv) || !string.Equals(v, uv, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot interpolate across different categorical regimes ({k}: ''{v}'' vs ''{uv}'').");
            }
        }

        if (!lower.NumericalConditions.TryGetValue(parameterKey, out double x1) ||
            !upper.NumericalConditions.TryGetValue(parameterKey, out double x2))
        {
            throw new ArgumentException($"Numerical condition ''{parameterKey}'' missing from bounding records.");
        }

        if (!double.IsFinite(x1) || !double.IsFinite(x2) || Math.Abs(x1 - x2) < 1e-9)
        {
            throw new InvalidOperationException($"Bounding points for ''{parameterKey}'' are non-finite or degenerate ({x1}, {x2}).");
        }

        // Check that all OTHER numerical conditions match exactly within tolerance
        foreach (var (nk, nv) in lower.NumericalConditions)
        {
            if (string.Equals(nk, parameterKey, StringComparison.OrdinalIgnoreCase)) continue;
            if (!upper.NumericalConditions.TryGetValue(nk, out var unv) || Math.Abs(nv - unv) > 1e-6)
            {
                throw new InvalidOperationException($"Cannot interpolate when other numerical regime condition ''{nk}'' differs ({nv} vs {unv}).");
            }
        }

        // Reject extrapolation
        double minX = Math.Min(x1, x2);
        double maxX = Math.Max(x1, x2);
        if (targetValue < minX || targetValue > maxX)
        {
            throw new ArgumentOutOfRangeException(
                parameterKey,
                targetValue,
                $"Target value {targetValue:F4} is outside bounding range [{minX:F4}, {maxX:F4}]. Extrapolation is strictly rejected.");
        }

        return EngineeringMath.Linear(targetValue, x1, lower.Value, x2, upper.Value);
    }

    private static bool RecordsMatch(CoefficientRecord a, CoefficientRecord b)
    {
        if (!string.Equals(a.RecordKey, b.RecordKey, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.ParameterCategory, b.ParameterCategory, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.TableOrFigure, b.TableOrFigure, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.ClauseReference, b.ClauseReference, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.SignConvention, b.SignConvention, StringComparison.Ordinal)) return false;
        if (!string.Equals(a.Unit, b.Unit, StringComparison.Ordinal)) return false;
        if (a.Interpolation != b.Interpolation) return false;
        if (!string.Equals(a.ApplicabilityNotes ?? string.Empty, b.ApplicabilityNotes ?? string.Empty, StringComparison.Ordinal)) return false;
        if (BitConverter.DoubleToInt64Bits(a.Value) != BitConverter.DoubleToInt64Bits(b.Value)) return false;

        if (a.CategoricalConditions.Count != b.CategoricalConditions.Count) return false;
        foreach (var (k, v) in a.CategoricalConditions)
        {
            if (!b.CategoricalConditions.TryGetValue(k, out var bv) || !string.Equals(v, bv, StringComparison.Ordinal)) return false;
        }

        if (a.NumericalConditions.Count != b.NumericalConditions.Count) return false;
        foreach (var (k, v) in a.NumericalConditions)
        {
            if (!b.NumericalConditions.TryGetValue(k, out var bv) || BitConverter.DoubleToInt64Bits(v) != BitConverter.DoubleToInt64Bits(bv)) return false;
        }

        return true;
    }

    /// <summary>
    /// Optional local preflight check verifying that a private evidence file exists and its
    /// computed SHA-256 matches the manifest's declared EvidenceDocumentHash.
    /// Does not print or expose file contents.
    /// </summary>
    public static (bool Valid, string Message) VerifyEvidenceDocument(string evidenceFilePath, StandardsDataBundleManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceFilePath);
        ArgumentNullException.ThrowIfNull(manifest);

        if (!File.Exists(evidenceFilePath))
            return (false, $"Evidence file not found at '{evidenceFilePath}'.");

        using var stream = File.OpenRead(evidenceFilePath);
        var computedHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(computedHash, manifest.ReviewTrace.EvidenceDocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Evidence file SHA-256 mismatch: computed {computedHash}, expected {manifest.ReviewTrace.EvidenceDocumentHash}.");
        }
        return (true, "Evidence file SHA-256 verified successfully.");
    }

    /// <summary>
    /// Generates an unambiguous standards intake preflight report distinguishing cryptographic integrity,
    /// local evidence file hash verification, human reviewer role separation, and calculation authorization.
    /// </summary>
    public static StandardsIntakePreflightReport GenerateIntakePreflight(
        StandardsDataBundle bundle,
        string? evidenceFilePath = null,
        StandardReferenceCalculationFixture? referenceFixture = null,
        ProcedureSelection? procedure = null,
        string rawComponentFolder = "")
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var validationReport = Validate(bundle, procedure, rawComponentFolder: rawComponentFolder);
        var manifest = bundle.Manifest;
        var blockers = ImmutableArray.CreateBuilder<string>();

        bool hasSemanticHashIssue = validationReport.Issues.Any(i =>
            i.Code == "STD_CANONICAL_HASH_MISMATCH" ||
            i.Code == "STD_CANONICAL_HASH_FAILED" ||
            i.Code == "STD_HASH_VERSION_UNSUPPORTED" ||
            i.Code == "STD_MANIFEST_HASH_MISSING" ||
            i.Code == "STD_NONFINITE_VALUE" ||
            i.Code == "STD_NONFINITE_NUMERICAL_CONDITION" ||
            i.Code == "STD_RECORD_KEY_MISSING");
        bool semanticHashValid = !string.IsNullOrWhiteSpace(manifest.ContentHash) && !hasSemanticHashIssue;

        bool hasComponentBytesIssue = validationReport.Issues.Any(i =>
            i.Code == "STD_RECORD_BYTES_MISMATCH" ||
            i.Code == "STD_COMPONENT_MISSING" ||
            i.Code == "STD_HASH_MISMATCH" ||
            i.Code == "STD_INTEGRITY_UNVERIFIED" ||
            i.Code == "STD_COMPONENT_FILES_EMPTY" ||
            i.Code == "STD_COMPONENT_JSON_MALFORMED" ||
            i.Code == "STD_COMPONENT_LOAD_FAILED" ||
            i.Code == "STD_EXTRA_COMPONENT" ||
            i.Code == "STD_DUPLICATE_SOURCE_KEY" ||
            i.Code == "STD_UNSAFE_PATH");
        bool componentBytesCoherent = bundle.IsBytesVerified && !hasComponentBytesIssue;

        bool hasProcedureCoverageIssue = validationReport.Issues.Any(i =>
            i.Code == "STD_COVERAGE_UNDECLARED" ||
            i.Code == "STD_COVERAGE_COUNT_MISMATCH" ||
            i.Code == "STD_MANDATORY_CATEGORY_EMPTY" ||
            i.Code == "STD_INTERNAL_PRESSURE_INCOMPLETE" ||
            i.Code == "STD_FAMILY_MISMATCH" ||
            i.Code == "STD_EDITION_UNSUPPORTED" ||
            i.Code == "STD_AMENDMENT_UNRESOLVED");
        bool procedureCoverageValid = !manifest.CoverageMatrix.IsDefaultOrEmpty && !hasProcedureCoverageIssue;

        EvidenceVerificationStatus evidenceStatus = EvidenceVerificationStatus.NotPerformed;
        if (!string.IsNullOrWhiteSpace(evidenceFilePath))
        {
            if (!File.Exists(evidenceFilePath))
            {
                evidenceStatus = EvidenceVerificationStatus.MissingFile;
                blockers.Add($"Evidence document not found at '{evidenceFilePath}'.");
            }
            else
            {
                using var stream = File.OpenRead(evidenceFilePath);
                var computedSha = Convert.ToHexString(SHA256.HashData(stream));
                if (string.Equals(computedSha, manifest.ReviewTrace?.EvidenceDocumentHash, StringComparison.OrdinalIgnoreCase))
                {
                    evidenceStatus = EvidenceVerificationStatus.Verified;
                }
                else
                {
                    evidenceStatus = EvidenceVerificationStatus.HashMismatch;
                    blockers.Add($"Evidence document SHA-256 mismatch (computed {computedSha}, declared {manifest.ReviewTrace?.EvidenceDocumentHash}).");
                }
            }
        }
        else
        {
            blockers.Add("Local review evidence document not provided (verification NotPerformed).");
        }

        HumanReviewStatus reviewStatus = HumanReviewStatus.Unverified;
        if (manifest.ReviewTrace != null)
        {
            bool isSelfReview = string.Equals(
                manifest.ReviewTrace.PreparerName?.Trim(),
                manifest.ReviewTrace.ReviewerName?.Trim(),
                StringComparison.OrdinalIgnoreCase);

            if (!isSelfReview)
            {
                if (evidenceStatus == EvidenceVerificationStatus.Verified && manifest.ReviewTrace.IsIndependentReview)
                {
                    reviewStatus = HumanReviewStatus.EvidenceHashVerified;
                }
                else
                {
                    reviewStatus = HumanReviewStatus.SelfDeclaredMetadata;
                }
            }
        }

        if (reviewStatus != HumanReviewStatus.EvidenceHashVerified && reviewStatus != HumanReviewStatus.IndependentEngineerReviewed)
        {
            blockers.Add("Independent engineering review trace unverified.");
        }

        bool refCalcAvailable = false;
        if (referenceFixture == null)
        {
            blockers.Add("Non-synthetic independent reference calculation benchmark not supplied.");
        }
        else if (referenceFixture.IsSynthetic || referenceFixture.RightsClassification == DataRightsClassification.SyntheticTestData)
        {
            blockers.Add("Supplied reference calculation fixture is synthetic test data; cannot qualify production intake.");
        }
        else if (referenceFixture.Family != manifest.Family ||
                 !string.Equals(referenceFixture.Edition, manifest.Edition, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add($"Supplied reference calculation fixture family/edition ({referenceFixture.Family} {referenceFixture.Edition}) does not match manifest ({manifest.Family} {manifest.Edition}).");
        }
        else if (procedure != null && !string.IsNullOrWhiteSpace(procedure.ProcedureId) &&
                 !string.Equals(referenceFixture.ProcedureId, procedure.ProcedureId, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add($"Supplied reference calculation fixture procedure ({referenceFixture.ProcedureId}) does not match requested procedure ({procedure.ProcedureId}).");
        }
        else if (referenceFixture.ExpectedFactors.IsEmpty && referenceFixture.ExpectedPressures.IsEmpty)
        {
            blockers.Add("Supplied reference calculation fixture contains zero expected factor and pressure assertions.");
        }
        else
        {
            bool hasInvalidTolerance = false;
            foreach (var ef in referenceFixture.ExpectedFactors)
            {
                if (!double.IsFinite(ef.ExpectedValue) || !double.IsFinite(ef.Tolerance) || ef.Tolerance < 0)
                {
                    hasInvalidTolerance = true;
                    break;
                }
            }
            if (!hasInvalidTolerance)
            {
                foreach (var ep in referenceFixture.ExpectedPressures)
                {
                    if (!double.IsFinite(ep.ExpectedNetPressurePa) ||
                        !double.IsFinite(ep.AbsoluteTolPa) || ep.AbsoluteTolPa < 0 ||
                        !double.IsFinite(ep.RelativeTol) || ep.RelativeTol < 0)
                    {
                        hasInvalidTolerance = true;
                        break;
                    }
                }
            }

            if (hasInvalidTolerance)
            {
                blockers.Add("Supplied reference calculation fixture contains non-finite values or invalid negative tolerances.");
            }
            else if (referenceFixture.ReviewTrace == null || !referenceFixture.ReviewTrace.IsIndependentReview ||
                     string.Equals(referenceFixture.ReviewTrace.PreparerName?.Trim(), referenceFixture.ReviewTrace.ReviewerName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                blockers.Add("Supplied reference calculation fixture lacks verified independent engineering review attestation (self-review or unverified reviewer).");
            }
            else
            {
                refCalcAvailable = true;
            }
        }

        if (manifest.Rights == DataRightsClassification.SyntheticTestData)
        {
            blockers.Add("Synthetic test data strictly blocked from engineering calculation.");
        }

        if (!string.Equals(manifest.CanonicalHashAlgorithmVersion, "3.0", StringComparison.Ordinal))
        {
            blockers.Add($"Legacy canonical hash version '{manifest.CanonicalHashAlgorithmVersion}' cannot qualify new candidates; structural V3 is required.");
        }

        // Aggregate all validation report errors and fatal blockers into blockers list
        foreach (var iss in validationReport.Issues)
        {
            if (iss.Severity >= StandardsValidationSeverity.Error)
            {
                blockers.Add($"[{iss.Severity}] {iss.Code}: {iss.Message}");
            }
        }

        IntakeQualificationStatus overallStatus;
        bool hasFatalBlocker = validationReport.Issues.Any(i => i.Severity == StandardsValidationSeverity.FatalBlocker);
        bool hasError = validationReport.Issues.Any(i => i.Severity == StandardsValidationSeverity.Error);

        if (manifest.Rights == DataRightsClassification.SyntheticTestData || !string.Equals(manifest.CanonicalHashAlgorithmVersion, "3.0", StringComparison.Ordinal))
        {
            overallStatus = IntakeQualificationStatus.Blocked;
        }
        else if (hasFatalBlocker || hasError || !semanticHashValid || !componentBytesCoherent)
        {
            overallStatus = IntakeQualificationStatus.Invalid;
        }
        else if (reviewStatus == HumanReviewStatus.EvidenceHashVerified && refCalcAvailable && procedureCoverageValid)
        {
            overallStatus = IntakeQualificationStatus.ReadyForIndependentReview;
        }
        else
        {
            overallStatus = IntakeQualificationStatus.Unverified;
        }

        return new StandardsIntakePreflightReport(
            manifest.BundleId,
            manifest.Family,
            manifest.Edition,
            manifest.Rights,
            manifest.CanonicalHashAlgorithmVersion,
            semanticHashValid,
            componentBytesCoherent,
            procedureCoverageValid,
            evidenceStatus,
            reviewStatus,
            refCalcAvailable,
            overallStatus,
            CanCalculateNormativePressures: false,
            blockers.ToImmutable(),
            DateTimeOffset.UtcNow);
    }
}
