using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Fail-closed identity check shared by all report builders: a calculation run
/// whose standard or project contradicts the report model's manifest/project
/// must never be rendered. (Extracted from DetailedReportBuilder, where this
/// gate originally lived inline, so the summary builder enforces it too.)
/// </summary>
internal static class ReportIdentityGuard
{
    public static void ThrowIfMismatch(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        if (calcRun == null) return;

        string runFamily = calcRun.Request.StandardEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) ? "AS/NZS" : "ASCE";
        string manifestEdition = model.Manifest.Edition;
        bool manifestIsAsNz = manifestEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) || manifestEdition.Contains("1170", StringComparison.OrdinalIgnoreCase);
        bool manifestIsAsce = manifestEdition.Contains("ASCE", StringComparison.OrdinalIgnoreCase);
        if ((runFamily == "AS/NZS" && !manifestIsAsNz) || (runFamily == "ASCE" && !manifestIsAsce))
        {
            throw new InvalidOperationException($"Report identity mismatch: Calculation run standard '{calcRun.Request.StandardEdition}' does not match model manifest edition '{manifestEdition}'.");
        }
        if (!string.IsNullOrEmpty(calcRun.Request.ProjectId) && !string.Equals(calcRun.Request.ProjectId, model.Project.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Report identity mismatch: Calculation run project '{calcRun.Request.ProjectId}' does not match model project '{model.Project.Id}'.");
        }
    }
}
