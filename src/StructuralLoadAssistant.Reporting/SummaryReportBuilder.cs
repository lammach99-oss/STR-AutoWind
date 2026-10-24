using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// The exported report (product decision, 2026-10-24): a SUMMARY, PDF only.
/// The full step-by-step calculation (formula ledger, coefficient provenance,
/// substituted equations) lives in the application UX via
/// <see cref="DetailedReportBuilder"/>; it is not exported.
///
/// Layout follows the project owner's reference calculation workbooks
/// (ASCE_Wind_load.xlsm / AS.NZS_Wind_load.xlsm, adopted 2026-10-24):
/// a LOAD CALCULATION title block (Project / Item / Calc. by / Checked by /
/// Date / Rev), then lettered sections — A. Reference, B. Basic wind
/// pressure calculation, C. Design wind pressure of building calculation,
/// D. Status &amp; audit — with parameter rows in the workbook style
/// "label .... symbol = value unit (clause/table reference)" and ULS/SLS
/// result columns. Phase 0 integrity rules apply unchanged: a blocked run
/// is rendered BLOCKED / NOT COMPUTED with no numeric pressure, and
/// "VERIFIED" is only claimed when an independent engineer review is
/// actually recorded.
/// </summary>
public sealed class SummaryReportBuilder
{
    public DetailedReportOptions Options { get; set; } = new();

    public byte[] BuildSummaryPdf(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ReportIdentityGuard.ThrowIfMismatch(model, calcRun);

        var units = ReportUnitProfile.ForStandard(calcRun?.Request.StandardEdition ?? model.Manifest.Edition);
        bool isAsNz = units.SystemName.Contains("Metric", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        void Line(string s = "") => sb.Append(s).Append('\n');

        string status = calcRun?.QualificationStatus ?? "NO ENGINEERING CALCULATION RUN SELECTED - SYNTHETIC WORKSPACE ONLY";
        bool workbookMode = calcRun != null
            && calcRun.Request.Options.TryGetValue(WorkbookRequestDerivation.MethodKey, out var wm)
            && !string.IsNullOrWhiteSpace(wm);
        var cq = calcRun?.ComputedQuantities;

        // ---- Title block (workbook header grid) ----
        Line("# LOAD CALCULATION");
        Line("WIND LOAD - SUMMARY REPORT");
        Line($"**STATUS:** {status}");
        Line($"Project: {model.Project.Name}");
        Line($"Project Code: {model.Project.Id}");
        Line("Item: Wind load to building calculation");
        Line($"Calc. by: {Options.Author}");
        Line($"Checked by: {Options.OwnerReviewer}");
        Line($"Date: {DateTimeOffset.UtcNow:yyyy-MM-dd}");
        Line($"Rev: {Options.Revision}");
        Line();
        Line("This is a summary report. The full step-by-step calculation is available in the application.");
        Line();

        // ---- A. Reference (workbook section A) ----
        Line("## A. Reference");
        Line(isAsNz
            ? $"- {model.Manifest.Edition}: Structural design actions - Part 2: Wind actions"
            : $"- {model.Manifest.Edition}: Minimum Design Loads and Associated Criteria for Buildings and Other Structures");
        if (calcRun != null) Line($"- Procedure: {calcRun.Request.ProcedureName}");
        Line($"- Verification Status: {model.Manifest.Status}");
        Line($"- Units: pressures in {units.PressureUnit}, line loads in {units.LineLoadUnit}, lengths in {units.LengthUnit}, wind speed in {units.SpeedUnit} ({units.SystemName})");
        Line($"- Active Wind State: {model.Calculation.State.Id}");
        if (workbookMode)
            Line("- Calculation method: owner's reference workbook (coefficients derived from workbook tables; see application trace)");
        Line();

        // ---- B. Basic wind pressure calculation (workbook section B) ----
        Line(isAsNz ? "## B. Basic parameters" : "## B. Basic wind pressure calculation");
        if (calcRun == null)
        {
            Line("No engineering calculation run selected.");
        }
        else
        {
            var r = calcRun.Request;
            void Param(string label, string symbolEq, string? clause)
                => Line(clause is null ? $"- {label} .... {symbolEq}" : $"- {label} .... {symbolEq} ({clause})");
            bool Has(string key) => cq != null && cq.TryGetValue(key, out _);
            double Q(string key) => cq![key];

            Line($"- Location / Exposure: {r.ExposureOrTerrain}; Enclosure: {r.EnclosureClassification}");
            Line($"- Building: {units.Length(r.BuildingLengthM)} x {units.Length(r.BuildingWidthM)}, eave {units.Length(r.EaveHeightM)}, ridge {units.Length(r.RidgeHeightM)}");
            Line($"- Roof: {r.RoofForm}, slope {r.RoofSlopeDegrees:F1} deg; Limit State: {r.LimitState}; Wind Direction: {r.WindDirection} ({r.WindAngleDegrees:F0} deg)");
            if (isAsNz)
            {
                Param("Regional wind speed", $"V_R = {units.Speed(r.BasicWindSpeed)}", "Table 3.1");
                if (Has("Md")) Param("Wind direction multiplier", $"Md = {Q("Md"):F2}", "Section 3.3");
                if (Has("Mz_cat")) Param("Terrain/height multiplier", $"Mz,cat = {Q("Mz_cat"):F2}", "Table 4.1");
                if (Has("Ms")) Param("Shielding multiplier", $"Ms = {Q("Ms"):F2}", "Section 4.3");
                if (Has("Mt")) Param("Topographic multiplier", $"Mt = {Q("Mt"):F2}", "Section 4.4");
                if (Has("Mc")) Param("Climate change multiplier", $"Mc = {Q("Mc"):F2}", "Section 3.4");
                if (Has("V_sit")) Param("Site wind speed", $"V_sit = {units.Speed(Q("V_sit"))}", "Section 2.2");
                if (Has("Cdyn") && Math.Abs(Q("Cdyn") - 1) > 1e-9) Param("Dynamic response factor", $"Cdyn = {Q("Cdyn"):F2}", "Section 6");
                if (Has("q_des")) Param("Design wind pressure (basic)", $"q = {units.Pressure(Q("q_des"))}", "Section 2.4");
                if (Has("q_des_alt")) Param("Design wind pressure, other limit state", $"q = {units.Pressure(Q("q_des_alt"))}", "Section 2.4");
            }
            else
            {
                Param("Basic wind speed", $"V = {units.Speed(r.BasicWindSpeed)}", "Section 26.5.1");
                if (Has("Kd")) Param("Wind directionality factor", $"Kd = {Q("Kd"):F2}", "Table 26.6-1");
                if (Has("Kzt")) Param("Topographic factor", $"Kzt = {Q("Kzt"):F2}", "Section 26.8");
                if (Has("Ke")) Param("Ground elevation factor", $"Ke = {Q("Ke"):F2}", "Section 26.9");
                if (Has("Kh")) Param("Velocity pressure exposure coefficient", $"Kz = {Q("Kh"):F3}", "Section 26.10");
                if (Has("G")) Param("Gust-effect factor", $"G = {Q("G"):F2}", "Section 26.11");
                if (Has("GCpi")) Param("Internal pressure coefficient", $"GCpi = +/-{Math.Abs(Q("GCpi")):F2}", "Table 26.13-1");
                if (Has("qh")) Param("Velocity pressure (ULS)", $"qh = {units.Pressure(Q("qh"))}", "Section 26.10.2");
                if (Has("qh_sls")) Param("Velocity pressure (SLS)", $"qh,sls = {units.Pressure(Q("qh_sls"))}", "Section 26.10.2");
            }
            Line($"- Explicit Coefficients Supplied: {r.ExplicitCoefficients.Count} (full register in the application)");
        }
        Line();

        // ---- C. Design wind pressure (workbook section C) ----
        Line(isAsNz ? "## C. Wind load calculation" : "## C. Design wind pressure of building calculation");
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
            bool hasAlt = calcRun.SurfaceResults.Any(s => Math.Abs(s.AlternateLimitStateNetPressurePa) > 1e-9);
            string altLabel = calcRun.Request.LimitState == LimitState.Uls ? "SLS" : "ULS";
            if (hasAlt)
            {
                Line($"| Surface | Description | Cp | p ULS ({units.PressureUnit}) | p {altLabel} ({units.PressureUnit}) |");
                Line("| :--- | :--- | :--- | :--- | :--- |");
                foreach (var s in calcRun.SurfaceResults)
                {
                    double uls = calcRun.Request.LimitState == LimitState.Uls ? s.NetPressurePa : s.AlternateLimitStateNetPressurePa;
                    double alt = calcRun.Request.LimitState == LimitState.Uls ? s.AlternateLimitStateNetPressurePa : s.NetPressurePa;
                    Line($"| {s.SurfaceOrZoneId} | {s.Description} | {s.ExternalCoefficient:F2} | {units.PressureValue(uls):F2} | {units.PressureValue(alt):F2} |");
                }
            }
            else
            {
                Line($"| Surface | Description | Cp | Net Pressure ({units.PressureUnit}) |");
                Line("| :--- | :--- | :--- | :--- |");
                foreach (var s in calcRun.SurfaceResults)
                {
                    Line($"| {s.SurfaceOrZoneId} | {s.Description} | {s.ExternalCoefficient:F2} | {units.PressureValue(s.NetPressurePa):F2} |");
                }
            }
            Line();
            Line("Net pressures are the governing internal-pressure case per surface (+/- internal pressure enveloped; both cases in the application).");
            if (cq != null && cq.TryGetValue(isAsNz ? "p_i" : "pi", out double pi))
                Line($"Internal wind pressure: +/-{units.Pressure(Math.Abs(pi))}");
        }
        Line();

        // ---- D. Status & audit ----
        Line("## D. Status & audit");
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
