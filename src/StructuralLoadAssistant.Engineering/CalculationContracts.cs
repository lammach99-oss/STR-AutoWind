using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public enum CalculationIssueSeverity { Information, Warning, Error }

public sealed record CalculationIssue(
    string Code,
    CalculationIssueSeverity Severity,
    string Stage,
    string Message);

public sealed record CoefficientInputEvidence(
    string CoefficientName,
    double Value,
    string Unit,
    DataProvenance Provenance,
    string Document,
    string ClauseOrTable,
    string SourceNotes,
    DateTimeOffset Timestamp);

public sealed record CalculationTraceNode(
    string NodeId,
    string Stage,
    string QuantityName,
    string SymbolicEquation,
    string SubstitutedEquation,
    string ResultWithUnit,
    double NumericValue,
    string OutputUnit,
    string SourceReference,
    string ExplanatoryNote,
    ImmutableArray<string> Dependencies,
    ImmutableArray<CalculationIssue> Issues);

public sealed record EngineeringCalculationRequest(
    string ProjectId,
    string StandardEdition,
    ImmutableArray<string> SelectedAmendments,
    string ProcedureName,
    LimitState LimitState,
    WindDirection WindDirection,
    double WindAngleDegrees,
    string RoofForm,
    string EnclosureClassification,
    double BasicWindSpeed,
    double ReferenceHeightM,
    double BuildingLengthM,
    double BuildingWidthM,
    double EaveHeightM,
    double RidgeHeightM,
    double RoofSlopeDegrees,
    double GroundElevationM,
    string ExposureOrTerrain,
    ImmutableDictionary<string, CoefficientInputEvidence> ExplicitCoefficients,
    ImmutableDictionary<string, string> Options);

public sealed record SurfacePressureResult(
    string SurfaceOrZoneId,
    string Description,
    double ExternalCoefficient,
    double ExternalPressurePa,
    double InternalCoefficient,
    double InternalPressurePa,
    double NetPressurePa,
    Vec3 OutwardNormal,
    Vec3 GlobalPressureVectorPa,
    // Phase 1 both-sign internal-pressure envelope (Option A): the primary
    // Internal*/NetPressurePa values above are the GOVERNING case (larger
    // absolute net pressure); both case values are carried here so the
    // envelope is auditable without doubling the surface list.
    double NetPressurePositiveInternalPa = 0,
    double NetPressureNegativeInternalPa = 0,
    string GoverningInternalPressureCase = "",
    // Workbook-method dual limit states: net pressure at the OTHER limit
    // state (SLS when the run is ULS and vice versa), scaled by the
    // velocity-pressure ratio. Zero when no second limit state was derived.
    double AlternateLimitStateNetPressurePa = 0);

public sealed record EngineeringCalculationRun(
    string Id,
    DateTimeOffset CreatedAt,
    EngineeringCalculationRequest Request,
    DataProvenance Provenance,
    bool NumericalComputationPossible,
    bool StandardsDataIntegrityVerified,
    bool PrimarySourceApplicabilityVerified,
    bool OwnerEngineerReviewed,
    bool IndependentEngineerReviewed,
    string QualificationStatus,
    ImmutableArray<PressureSample> VelocityPressureProfile,
    ImmutableArray<SurfacePressureResult> SurfaceResults,
    ImmutableArray<CalculationTraceNode> TraceNodes,
    ImmutableArray<CalculationIssue> Issues,
    ImmutableDictionary<string, double> ComputedQuantities)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ContentHash => SnapshotJson.Hash(this);

    public WindCalculationRun ToWindCalculationRun(WindState state, StandardManifest manifest)
    {
        var legacyTrace = TraceNodes.Select(t => new CalculationTraceItem(
            t.NodeId,
            t.SubstitutedEquation,
            t.ResultWithUnit,
            t.SourceReference,
            t.Issues.FirstOrDefault()?.Message ?? t.ExplanatoryNote
        )).ToImmutableArray();

        return new WindCalculationRun(
            Id,
            CreatedAt,
            manifest.Hash,
            Provenance,
            state,
            VelocityPressureProfile,
            legacyTrace
        );
    }
}

