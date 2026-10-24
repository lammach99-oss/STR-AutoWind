using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Immutable, typed audit report document that unifies all report formats (Markdown, PDF, HTML, Excel, JSON).
/// Enforces identity coherence across project, standard edition, calculation run, geometry, and review status.
/// </summary>
public sealed record EngineeringAuditReportDocument(
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
    DateTimeOffset GeneratedAt,
    bool NumericalComputationPossible,
    bool OwnerReviewed,
    bool IndependentlyVerified,
    bool LicensedSourceQualified,
    bool ProductionAuthorized,
    string QualificationStatus)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ContentHash => SnapshotJson.Hash(this);
}

public static class AuditReportDocumentBuilder
{
    public static EngineeringAuditReportDocument BuildDocument(
        ReportModel model,
        EngineeringCalculationRun? calcRun,
        DetailedReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        options ??= new DetailedReportOptions();

        if (calcRun != null)
        {
            // P0-C: Fail closed on contradictory standard / manifest identities
            string runFamily = calcRun.Request.StandardEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) ? "AS/NZS" : "ASCE";
            string manifestEdition = model.Manifest.Edition;
            bool manifestIsAsNz = manifestEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) || manifestEdition.Contains("1170", StringComparison.OrdinalIgnoreCase);
            bool manifestIsAsce = manifestEdition.Contains("ASCE", StringComparison.OrdinalIgnoreCase);
            if ((runFamily == "AS/NZS" && !manifestIsAsNz) || (runFamily == "ASCE" && !manifestIsAsce))
            {
                throw new InvalidOperationException($"Report identity mismatch: Calculation run standard '{calcRun.Request.StandardEdition}' does not match model manifest edition '{manifestEdition}'.");
            }

            // Project ID coherence
            if (!string.IsNullOrEmpty(calcRun.Request.ProjectId) &&
                !string.Equals(calcRun.Request.ProjectId, model.Project.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Report identity mismatch: Calculation run project '{calcRun.Request.ProjectId}' does not match model project '{model.Project.Id}'.");
            }

            // Active wind state coherence
            if (model.Calculation != null && model.Calculation.Provenance != DataProvenance.SyntheticDemoFixture)
            {
                if (calcRun.Request.WindDirection != model.Calculation.State.Direction)
                {
                    throw new InvalidOperationException($"Report identity mismatch: Calculation run direction '{calcRun.Request.WindDirection}' does not match model state direction '{model.Calculation.State.Direction}'.");
                }
            }
        }

        string qual = calcRun?.QualificationStatus ?? "SYNTHETIC DEMO - NOT FOR DESIGN";
        string reportId = "audit-report-" + (calcRun != null ? calcRun.Id : (model.Calculation != null ? model.Calculation.Id : "legacy"));

        return new EngineeringAuditReportDocument(
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
            DateTimeOffset.UtcNow,
            calcRun?.NumericalComputationPossible ?? false,
            calcRun?.OwnerEngineerReviewed ?? false,
            false, // IndependentlyVerified: false pending external commissioning
            false, // LicensedSourceQualified: false pending licensed sources
            false, // ProductionAuthorized: false (CanCalculateNormativePressures = false)
            qual
        );
    }
}
