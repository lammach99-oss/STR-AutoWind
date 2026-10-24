using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// The exported report (product decision, 2026-10-24): a SUMMARY, PDF only.
/// The full step-by-step calculation (formula ledger, coefficient provenance,
/// substituted equations) lives in the application UX via
/// <see cref="DetailedReportBuilder"/>; it is not exported. This builder emits
/// identity, design basis, key inputs, headline results and audit hashes —
/// in the governing standard's units (ASCE imperial / AS-NZS metric kilo).
/// Phase 0 integrity rules apply unchanged: a blocked run is rendered
/// BLOCKED / NOT COMPUTED with no numeric pressure, and "VERIFIED" is only
/// claimed when an independent engineer review is actually recorded.
/// </summary>
public sealed class SummaryReportBuilder
{
    public DetailedReportOptions Options { get; set; } = new();

    public byte[] BuildSummaryPdf(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ReportIdentityGuard.ThrowIfMismatch(model, calcRun);

        var units = ReportUnitProfile.ForStandard(calcRun?.Request.StandardEdition ?? model.Manifest.Edition);
        var sb = new StringBuilder();
        void Line(string s = "") => sb.Append(s).Append('\n');

        string status = calcRun?.QualificationStatus ?? "NO ENGINEERING CALCULATION RUN SELECTED - SYNTHETIC WORKSPACE ONLY";

        Line("# STRUCTURAL WIND LOAD - SUMMARY REPORT");
        Line($"**STATUS:** {status}");
        Line($"**PROJECT:** {model.Project.Name} (ID: {model.Project.Id})");
        Line($"**AUTHOR:** {Options.Author}");
        Line($"**OWNER REVIEWER:** {Options.OwnerReviewer}");
        Line($"**DATE:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Line($"**REVISION:** {Options.Revision}");
        Line();
        Line("This is a summary report. The full step-by-step calculation is available in the application.");
        Line();

        Line("## 1. DESIGN BASIS");
        Line($"- Standard Edition: {model.Manifest.Edition}");
        if (calcRun != null) Line($"- Procedure: {calcRun.Request.ProcedureName}");
        Line($"- Verification Status: {model.Manifest.Status}");
        Line($"- Units: pressures in {units.PressureUnit}, line loads in {units.LineLoadUnit}, lengths in {units.LengthUnit}, wind speed in {units.SpeedUnit} ({units.SystemName})");
        Line($"- Active Wind State: {model.Calculation.State.Id}");
        Line();

        if (calcRun != null)
        {
            var r = calcRun.Request;
            Line("## 2. KEY INPUTS");
            Line($"- Basic Wind Speed: {units.Speed(r.BasicWindSpeed)}");
            Line($"- Building: {units.Length(r.BuildingLengthM)} x {units.Length(r.BuildingWidthM)}, eave {units.Length(r.EaveHeightM)}, ridge {units.Length(r.RidgeHeightM)}");
            Line($"- Roof: {r.RoofForm}, slope {r.RoofSlopeDegrees:F1} deg");
            Line($"- Enclosure: {r.EnclosureClassification}; Exposure/Terrain: {r.ExposureOrTerrain}");
            Line($"- Limit State: {r.LimitState}; Wind Direction: {r.WindDirection} ({r.WindAngleDegrees:F0} deg)");
            Line($"- Explicit Coefficients Supplied: {r.ExplicitCoefficients.Count} (full register in the application)");
            Line();
        }

        Line("## 3. RESULTS");
        if (calcRun == null)
        {
            Line("No engineering calculation run selected. No results computed.");
        }
        else if (!calcRun.NumericalComputationPossible)
        {
            Line("**BLOCKED / NOT COMPUTED**");
            Line("Required normative tables or explicit engineer coefficients are missing. Surface pressures were not computed.");
            foreach (var issue in calcRun.Issues)
            {
                Line($"- [{issue.Severity}] ({issue.Stage}) {issue.Code}: {issue.Message}");
            }
        }
        else
        {
            bool hasQ = calcRun.ComputedQuantities.TryGetValue("qh", out var q) || calcRun.ComputedQuantities.TryGetValue("q_des", out q);
            if (hasQ) Line($"- Velocity / Dynamic Pressure: {units.Pressure(q)}");
            Line();
            Line($"| Surface / Zone | Description | Cp | Net Pressure ({units.PressureUnit}) |");
            Line("| :--- | :--- | :--- | :--- |");
            foreach (var s in calcRun.SurfaceResults)
            {
                Line($"| {s.SurfaceOrZoneId} | {s.Description} | {s.ExternalCoefficient:F2} | {units.PressureValue(s.NetPressurePa):F2} |");
            }
        }
        Line();

        Line("## 4. STATUS & AUDIT");
        if (calcRun == null)
        {
            Line("- Calculation Status: NOT CALCULATED");
        }
        else if (!calcRun.NumericalComputationPossible)
        {
            Line("- Calculation Status: BLOCKED / NOT COMPUTED");
        }
        else if (calcRun.IndependentEngineerReviewed)
        {
            Line("- Calculation Status: CALCULATED & VERIFIED");
        }
        else
        {
            Line("- Calculation Status: CALCULATED - NOT INDEPENDENTLY VERIFIED");
        }
        Line($"- Calculation Run ID: {calcRun?.Id ?? model.Calculation.Id}");
        Line($"- Calculation Provenance: {model.Calculation.Provenance}");
        Line($"- Geometry Fingerprint: {model.Model.GeometryFingerprint}");
        if (calcRun != null) Line($"- Run Content Hash: {calcRun.ContentHash}");
        var doc = AuditReportDocumentBuilder.BuildDocument(model, calcRun, Options);
        Line($"- Audit Document Hash: {doc.ContentHash}");
        Line("- Live CSI Model Mutation: Strictly disabled (CanCalculateNormativePressures = false).");

        var pdf = new SimplePdfWriter
        {
            Title = $"{model.Project.Name} - Wind Load Summary",
            Watermark = status
        };
        var sections = sb.ToString().Split(new[] { "\n## " }, StringSplitOptions.None);
        pdf.AddFlowingSection(sections[0]);
        for (int i = 1; i < sections.Length; i++)
        {
            pdf.AddFlowingSection("## " + sections[i]);
        }
        return pdf.Build();
    }
}
