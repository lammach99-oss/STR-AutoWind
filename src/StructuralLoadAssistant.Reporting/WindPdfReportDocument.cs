using StructuralLoadAssistant.Csi.Fake;
using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Immutable, typed presentation projection for wind calculation PDF reporting.
/// Holds the canonical engineering calculation run, model snapshots, layout profiles,
/// output units, and verification metadata needed for deterministic workbook-style reporting.
/// Distinguishes calculation run content hash from report document hash.
/// </summary>
public sealed record WindPdfReportDocument(
    string ReportId,
    Project Project,
    StandardManifest Manifest,
    EngineeringCalculationRun? CalculationRun,
    CsiModelSnapshot ModelSnapshot,
    ImmutableArray<FacadeReportRow> FacadeRows,
    ImmutableArray<WallReportRow> WallRows,
    RoofMappingApproval? Approval,
    AssignmentRun? Assignment,
    ReadbackVerification? Readback,
    ReportDefinition Definition,
    DetailedReportOptions Options,
    WindReportLayoutProfile LayoutProfile,
    ReportUnitProfile UnitProfile,
    DateTimeOffset GeneratedAt,
    bool NumericalComputationPossible,
    bool OwnerReviewed,
    bool IndependentlyVerified,
    bool LicensedSourceQualified,
    bool ProductionAuthorized,
    string QualificationStatus)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string RunContentHash => CalculationRun?.ContentHash ?? "NO_RUN";

    [System.Text.Json.Serialization.JsonIgnore]
    public string ReportDocumentHash => SnapshotJson.Hash(new
    {
        ReportId,
        Project,
        Manifest,
        RunHash = RunContentHash,
        ModelGeometry = ModelSnapshot.GeometryFingerprint,
        FacadeRows,
        WallRows,
        Definition,
        Options,
        LayoutProfile,
        UnitProfile
    });

    [System.Text.Json.Serialization.JsonIgnore]
    public string ContentHash => ReportDocumentHash;
}

public static class WindPdfReportDocumentBuilder
{
    public static WindPdfReportDocument Build(
        ReportModel model,
        EngineeringCalculationRun? calcRun,
        DetailedReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ReportIdentityGuard.ThrowIfMismatch(model, calcRun);

        options ??= new DetailedReportOptions();
        string std = calcRun?.Request.StandardEdition ?? model.Manifest.Edition;
        var unitProfile = ReportUnitProfile.ForStandard(std);
        string roofForm = calcRun?.Request.RoofForm ?? "Gable";
        double slope = calcRun?.Request.RoofSlopeDegrees ?? 15.0;
        var layoutProfile = WindReportLayoutProfileSelector.Select(std, roofForm, slope);

        bool numOk = calcRun?.NumericalComputationPossible ?? false;
        bool ownerReviewed = calcRun?.OwnerEngineerReviewed ?? false;
        bool indepVerified = calcRun?.IndependentEngineerReviewed ?? false;
        bool licensed = calcRun?.StandardsDataIntegrityVerified ?? false;
        bool prodAuth = false; // Live CSI writes / normative production authorization remain disabled

        string qualStatus = calcRun?.QualificationStatus ?? "NO ENGINEERING CALCULATION RUN SELECTED - SYNTHETIC WORKSPACE ONLY";
        string reportId = "wind-pdf-" + (calcRun != null ? calcRun.Id : (model.Calculation != null ? model.Calculation.Id : "legacy"));

        return new WindPdfReportDocument(
            reportId,
            model.Project,
            model.Manifest,
            calcRun,
            model.Model,
            model.Facade,
            model.Walls,
            model.Approval,
            model.Assignment,
            model.Readback,
            model.Definition,
            options,
            layoutProfile,
            unitProfile,
            DateTimeOffset.UtcNow,
            numOk,
            ownerReviewed,
            indepVerified,
            licensed,
            prodAuth,
            qualStatus);
    }
}
