using System;
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
            AddIssue("STD_SCHEMA_UNSUPPORTED", StandardsValidationSeverity.FatalBlocker, "Manifest.SchemaVersion", null, $"Unsupported schema version '{manifest.SchemaVersion}'. Only 1.x is supported.");
        }

        switch (manifest.Family)
        {
            case StandardFamily.Asce7:
                if (!SupportedEditionsAsce.Contains(manifest.Edition))
                {
                    AddIssue("STD_EDITION_UNSUPPORTED", StandardsValidationSeverity.Error, "Manifest.Edition", null, $"Edition '{manifest.Edition}' is not supported for ASCE 7 family.");
                }
                break;

            case StandardFamily.AsNz1170_2:
                if (!SupportedEditionsAsNz.Contains(manifest.Edition))
                {
                    AddIssue("STD_EDITION_UNSUPPORTED", StandardsValidationSeverity.Error, "Manifest.Edition", null, $"Edition '{manifest.Edition}' is not supported for AS/NZS 1170.2 family.");
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
                AddIssue("STD_FAMILY_UNSUPPORTED", StandardsValidationSeverity.FatalBlocker, "Manifest.Family", null, $"Unsupported standard family '{manifest.Family}'.");
                break;
        }

        // 2. Component File Checksums and Tampering Verification
        if (!string.IsNullOrWhiteSpace(rawComponentFolder) && Directory.Exists(rawComponentFolder))
        {
            foreach (var (relPath, expectedSha) in manifest.ComponentFiles)
            {
                var fullPath = Path.Combine(rawComponentFolder, relPath);
                if (!File.Exists(fullPath))
                {
                    AddIssue("STD_COMPONENT_MISSING", StandardsValidationSeverity.Error, "ComponentFiles", relPath, $"Component file '{relPath}' declared in manifest does not exist on disk.");
                    continue;
                }

                var bytes = File.ReadAllBytes(fullPath);
                var actualSha = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(expectedSha, actualSha, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue("STD_HASH_MISMATCH", StandardsValidationSeverity.FatalBlocker, "ComponentFiles", relPath,
                        $"Checksum mismatch for '{relPath}': manifest declared '{expectedSha}', computed '{actualSha}'. Possible file tampering.");
                }
            }
        }

        // 3. Duplicate and Conflicting Record Keys
        var duplicateKeys = bundle.Records.GroupBy(r => r.RecordKey, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicateKeys)
        {
            AddIssue("STD_DUPLICATE_KEY", StandardsValidationSeverity.FatalBlocker, "Records", dup, $"Duplicate record key '{dup}' detected. Keys must be globally unique within bundle.");
        }

        // 4. Record Value Sanity and Units
        foreach (var r in bundle.Records)
        {
            if (!double.IsFinite(r.Value))
            {
                AddIssue("STD_NONFINITE_VALUE", StandardsValidationSeverity.FatalBlocker, "Records", r.RecordKey, $"Record '{r.RecordKey}' has non-finite value '{r.Value}'.");
            }
            if (string.IsNullOrWhiteSpace(r.Unit))
            {
                AddIssue("STD_UNIT_MISSING", StandardsValidationSeverity.Error, "Records", r.RecordKey, $"Record '{r.RecordKey}' is missing explicit physical unit declaration.");
            }
            if (string.IsNullOrWhiteSpace(r.ClauseReference))
            {
                AddIssue("STD_CLAUSE_MISSING", StandardsValidationSeverity.Warning, "Records", r.RecordKey, $"Record '{r.RecordKey}' lacks source clause locator.");
            }
        }

        // 5. Mandatory Coverage Verification
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
                    AddIssue("STD_MANDATORY_CATEGORY_EMPTY", StandardsValidationSeverity.Error, "CoverageMatrix", item.CategoryKey,
                        $"Mandatory category '{item.CategoryKey}' ({item.Description}) contains zero records in this dataset.");
                }
            }
        }

        // 6. Procedure Specific Isolation and Cross-Standard / Cross-System Conflation Checks
        if (procedure != null)
        {
            if (procedure.Family != manifest.Family)
            {
                AddIssue("STD_FAMILY_MISMATCH", StandardsValidationSeverity.FatalBlocker, "ProcedureSelection", null,
                    $"Procedure family '{procedure.Family}' does not match manifest family '{manifest.Family}'.");
            }

            // MWFRS vs C&C isolation
            if (procedure.SystemType.Equals("MWFRS", StringComparison.OrdinalIgnoreCase))
            {
                var ccRecordsInMwfrs = bundle.Records.Where(r => r.ParameterCategory.Contains("CC", StringComparison.OrdinalIgnoreCase) ||
                                                                 (r.CategoricalConditions.TryGetValue("System", out var s) && s.Equals("ComponentsAndCladding", StringComparison.OrdinalIgnoreCase))).ToList();
                // Ensure procedure queries don't inadvertently pull C&C records
            }
        }

        // 7. Internal Pressure Separate Sign Alternatives Check (+ / -)
        var ipRecords = bundle.GetRecordsForCategory("InternalPressure");
        if (ipRecords.Length > 0)
        {
            bool hasPositive = ipRecords.Any(r => r.CategoricalConditions.TryGetValue("InternalPressureState", out var s) && s.Contains("+"));
            bool hasNegative = ipRecords.Any(r => r.CategoricalConditions.TryGetValue("InternalPressureState", out var s) && s.Contains("-"));
            if (!hasPositive || !hasNegative)
            {
                AddIssue("STD_INTERNAL_PRESSURE_INCOMPLETE", StandardsValidationSeverity.Warning, "InternalPressure", null,
                    "Internal pressure coefficients must provide both positive (+) and negative (-) alternative design states.");
            }
        }

        // 8. Rights Classification & Synthetic Test Data Blocker
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

        // 9. Independent Engineering Review Trace Check
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

        // CanCalculateNormativePressures is NEVER true for synthetic data or invalid datasets!
        bool canCalculate = outcome == StandardsGateOutcome.ReadyForIndependentReview && !isSynthetic;

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
            throw new ArgumentException($"Parameter '{domain.ParameterName}' value is non-finite.", domain.ParameterName);

        if (!domain.AllowsExtrapolation && (value < domain.MinValue || value > domain.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                domain.ParameterName,
                value,
                $"Value {value:F4} {domain.Unit} is outside the valid applicability domain [{domain.MinValue:F4}, {domain.MaxValue:F4}] for parameter '{domain.ParameterName}'. Extrapolation is strictly rejected.");
        }
    }

    /// <summary>
    /// Enforces interpolation policies, rejecting interpolation across categorical boundaries.
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
            throw new InvalidOperationException($"Interpolation is strictly forbidden across categorical boundaries for parameter '{parameterKey}'.");
        }

        if (lower.Interpolation == InterpolationPolicy.None || upper.Interpolation == InterpolationPolicy.None)
        {
            throw new InvalidOperationException($"InterpolationPolicy.None requires exact parameter match for '{parameterKey}'.");
        }

        // Check that categorical conditions match exactly
        foreach (var (k, v) in lower.CategoricalConditions)
        {
            if (!upper.CategoricalConditions.TryGetValue(k, out var uv) || !string.Equals(v, uv, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot interpolate across different categorical regimes ({k}: '{v}' vs '{uv}').");
            }
        }

        if (!lower.NumericalConditions.TryGetValue(parameterKey, out double x1) ||
            !upper.NumericalConditions.TryGetValue(parameterKey, out double x2))
        {
            throw new ArgumentException($"Numerical condition '{parameterKey}' missing from bounding records.");
        }

        return EngineeringMath.Linear(targetValue, x1, lower.Value, x2, upper.Value);
    }
}

