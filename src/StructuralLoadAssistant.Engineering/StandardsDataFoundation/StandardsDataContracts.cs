using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

/// <summary>Identifies the primary wind loading standard family.</summary>
public enum StandardFamily
{
    /// <summary>ASCE / SEI Standard 7 (Minimum Design Loads and Associated Criteria for Buildings and Other Structures).</summary>
    Asce7,

    /// <summary>AS / NZS 1170.2 (Structural design actions - Part 2: Wind actions).</summary>
    AsNz1170_2
}

/// <summary>Classification of intellectual property, copyright, and redistribution rights for wind standards data.</summary>
public enum DataRightsClassification
{
    /// <summary>Unknown rights status; fail-closed by default (treated as non-distributable and unapproved).</summary>
    Unknown = 0,

    /// <summary>Publicly distributable data with positive license verification (e.g. open/permissive government data).</summary>
    RedistributionAllowed = 1,

    /// <summary>Locally licensed normative data for private design use; strictly prohibited from public commit or export.</summary>
    LocalLicensedOnly = 2,

    /// <summary>Restricted commercial or proprietary data requiring specialized licensing terms.</summary>
    Restricted = 3,

    /// <summary>Explicitly synthetic, rights-cleared test fixtures; strictly blocked from production engineering calculation.</summary>
    SyntheticTestData = 4
}

/// <summary>Lifecycle state of a standards dataset bundle within the qualification pipeline.</summary>
public enum StandardDataLifecycleState
{
    /// <summary>Uninitialized or undefined state.</summary>
    Unknown = 0,

    /// <summary>Mandatory manifest or dataset files are missing.</summary>
    Missing = 1,

    /// <summary>The standard family, edition, or amendment is unsupported.</summary>
    Unsupported = 2,

    /// <summary>Dataset parsed successfully but has not undergone independent engineering verification.</summary>
    Unreviewed = 3,

    /// <summary>Dataset failed schema, checksum, or applicability validation.</summary>
    Invalid = 4,

    /// <summary>Dataset passed schema and integrity gates; ready for analytical benchmark qualification.</summary>
    ReadyForQualification = 5,

    /// <summary>Dataset passed independent engineer review and benchmark qualification.</summary>
    EngineerReviewed = 6,

    /// <summary>Dataset is explicitly blocked due to unverified normative source or licensing restriction.</summary>
    Blocked = 7
}

/// <summary>Mathematical interpolation policy for numerical table parameters.</summary>
public enum InterpolationPolicy
{
    /// <summary>Discrete categorical value; mathematical interpolation across categories or zones is strictly forbidden.</summary>
    Categorical = 0,

    /// <summary>Exact numerical match only; no interpolation allowed.</summary>
    None = 1,

    /// <summary>1-dimensional linear interpolation between two adjacent validated entries within range.</summary>
    Linear1D = 2,

    /// <summary>2-dimensional bilinear interpolation within a rectangular bounding domain.</summary>
    Bilinear2D = 3
}

/// <summary>Severity level for standards validation issues.</summary>
public enum StandardsValidationSeverity
{
    Information,
    Warning,
    Error,
    FatalBlocker
}

/// <summary>Outcome of the fail-closed standards validation gate.</summary>
public enum StandardsGateOutcome
{
    /// <summary>Data is fully verified, complete, and ready for independent engineering calculation review.</summary>
    ReadyForIndependentReview,

    /// <summary>Data is synthetic or unverified; blocked from production calculation.</summary>
    UnqualifiedData,

    /// <summary>Independent engineering review evidence is missing or unverifiable.</summary>
    MissingReferenceEvidence,

    /// <summary>Queried scenario falls outside standard or procedure applicability domain.</summary>
    NotApplicable,

    /// <summary>Dataset failed integrity, hash, or schema validation.</summary>
    InvalidData
}

/// <summary>Formal descriptor for a specific edition of a wind loading standard.</summary>
public sealed record StandardEditionDescriptor(
    StandardFamily Family,
    string EditionId,
    string FormalTitle,
    DateTimeOffset PublicationDate,
    ImmutableArray<string> SupportedAmendments,
    string ApplicabilityNotes);

/// <summary>Explicit procedure and methodology selection within a wind standard.</summary>
public sealed record ProcedureSelection(
    StandardFamily Family,
    string Edition,
    string ProcedureId,
    LimitState LimitState,
    string SystemType,
    string Method);

/// <summary>Defines the geometric and structural validity domain for a specific design procedure.</summary>
public sealed record StandardProcedureScope(
    string ProcedureId,
    double MinMeanRoofHeightM,
    double MaxMeanRoofHeightM,
    double MinRoofSlopeDegrees,
    double MaxRoofSlopeDegrees,
    bool AllowsDynamicCalculation,
    bool RequiresRigidBuilding,
    ImmutableArray<string> SupportedBuildingTypes);

/// <summary>Defines the numerical validity domain of a parameter, enforcing no-extrapolation rules.</summary>
public sealed record ParameterDomain(
    string ParameterName,
    double MinValue,
    double MaxValue,
    string Unit,
    bool AllowsExtrapolation = false);

/// <summary>A single aerodynamic factor or pressure coefficient record with complete provenance.</summary>
public sealed record CoefficientRecord(
    string RecordKey,
    string TableOrFigure,
    string ClauseReference,
    string ParameterCategory,
    ImmutableDictionary<string, string> CategoricalConditions,
    ImmutableDictionary<string, double> NumericalConditions,
    double Value,
    string Unit,
    string SignConvention,
    InterpolationPolicy Interpolation,
    string? ApplicabilityNotes = null);

/// <summary>Immutable record of human engineering responsibility and independent review evidence.</summary>
public sealed record EngineeringReviewTrace(
    string PreparerName,
    string PreparerOrganization,
    DateTimeOffset PreparedDate,
    string ReviewerName,
    string ReviewerOrganization,
    DateTimeOffset? ReviewDate,
    string ReviewScope,
    string EvidenceDocumentUri,
    string EvidenceDocumentHash,
    bool IsIndependentReview);

/// <summary>Specifies a mandatory data category for a chosen design procedure.</summary>
public sealed record MandatoryCoverageItem(
    string CategoryKey,
    string Description,
    bool IsRequired,
    int AvailableRecordCount = 0);

/// <summary>Manifest describing a standards dataset bundle, its licensing, checksums, and review trace.</summary>
public sealed record StandardsDataBundleManifest(
    string BundleId,
    string SchemaVersion,
    StandardFamily Family,
    string Edition,
    ImmutableArray<string> Amendments,
    DataRightsClassification Rights,
    string RightsStatement,
    string ContentHash,
    ImmutableDictionary<string, string> ComponentFiles,
    EngineeringReviewTrace ReviewTrace,
    ImmutableArray<MandatoryCoverageItem> CoverageMatrix,
    string LicenseNotice,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastValidatedAt);

/// <summary>Individual finding produced during standards data validation.</summary>
public sealed record StandardsValidationIssue(
    string Code,
    StandardsValidationSeverity Severity,
    string Location,
    string? RecordKey,
    string Message,
    string? SuggestedRemediation = null);

/// <summary>Structured report detailing the outcome of the standards validation gate.</summary>
public sealed record StandardsValidationReport(
    StandardsGateOutcome Outcome,
    bool CanCalculateNormativePressures,
    ImmutableArray<StandardsValidationIssue> Issues,
    string Summary,
    string BundleId,
    string ContentHash,
    StandardDataLifecycleState State);

