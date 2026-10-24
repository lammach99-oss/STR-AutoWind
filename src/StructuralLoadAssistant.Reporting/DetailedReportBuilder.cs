using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

public sealed record DetailedReportOptions(
    string Author = "Structural Engineer",
    string OwnerReviewer = "Project Owner / Reviewer",
    string Revision = "Rev 0",
    bool IncludeCover = true,
    bool IncludeDesignBasis = true,
    bool IncludeInputRegister = true,
    bool IncludeFormulaLedger = true,
    bool IncludeWorksheets = true,
    bool IncludeRoofFacadeSheets = true,
    bool IncludeWallLoads = true,
    bool IncludeManualVerification = true,
    bool IncludeValidationAppendix = true,
    bool IncludeAuditAppendix = true);

public sealed class DetailedReportBuilder
{
    public DetailedReportOptions Options { get; set; } = new();

    public string BuildMarkdown(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        ArgumentNullException.ThrowIfNull(model);
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
            if (!string.IsNullOrEmpty(calcRun.Request.ProjectId) && !string.Equals(calcRun.Request.ProjectId, model.Project.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Report identity mismatch: Calculation run project '{calcRun.Request.ProjectId}' does not match model project '{model.Project.Id}'.");
            }
        }
        var sb = new StringBuilder();
        void Line(string s = "") => sb.Append(s).Append('\n');

        string watermark = calcRun?.QualificationStatus ?? "NO ENGINEERING CALCULATION RUN SELECTED - SYNTHETIC WORKSPACE ONLY";

        if (Options.IncludeCover)
        {
            Line("# STRUCTURAL WIND LOAD AUDIT REPORT");
            Line($"**STATUS:** {watermark}");
            Line($"**PROJECT:** {model.Project.Name} (ID: {model.Project.Id})");
            Line($"**AUTHOR:** {Options.Author}");
            Line($"**OWNER REVIEWER:** {Options.OwnerReviewer}");
            Line($"**DATE:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Line($"**REVISION:** {Options.Revision}");
            Line($"**MODEL GEOMETRY FINGERPRINT:** {model.Model.GeometryFingerprint}");
            Line("---");
            Line();
        }

        if (Options.IncludeDesignBasis)
        {
            Line("## 1. DESIGN BASIS & GOVERNING STANDARDS");
            Line($"- Standard Edition: {model.Manifest.Edition}");
            if (calcRun != null) Line($"- Procedure: {calcRun.Request.ProcedureName}");
            Line($"- Verification Status: {model.Manifest.Status}");
            Line($"- Standard Manifest Hash: {model.Manifest.Hash}");
            foreach (var r in model.Manifest.Revisions) Line($"- Revision / Amendment: {r}");
            foreach (var refItem in model.Manifest.References) Line($"- Source Reference: {refItem.Document} / {refItem.Clause} / {refItem.Source}");
            Line($"- Active Wind State: {model.Calculation.State.Id}");
            Line();
        }

        if (Options.IncludeInputRegister && calcRun != null)
        {
            Line("## 2. INPUT REGISTER & COEFFICIENT PROVENANCE");
            Line("| Parameter / Coefficient | Value | Unit | Provenance | Source Document & Clause | Notes |");
            Line("| :--- | :--- | :--- | :--- | :--- | :--- |");
            foreach (var kvp in calcRun.Request.ExplicitCoefficients)
            {
                var c = kvp.Value;
                Line($"| {c.CoefficientName} | {c.Value:F3} | {c.Unit} | {c.Provenance} | {c.Document} {c.ClauseOrTable} | {c.SourceNotes} |");
            }
            Line();
        }

        if (Options.IncludeFormulaLedger && calcRun != null)
        {
            Line("## 3. FORMULA LEDGER & STEP-BY-STEP TRACE");
            foreach (var node in calcRun.TraceNodes)
            {
                Line($"### [{node.NodeId}] {node.QuantityName} ({node.Stage})");
                Line($"- Symbolic: `{node.SymbolicEquation}`");
                Line($"- Substituted: `{node.SubstitutedEquation}` = **{node.ResultWithUnit}**");
                Line($"- Reference: {node.SourceReference}");
                Line($"- Explanation: {node.ExplanatoryNote}");
                Line();
            }
        }

        if (Options.IncludeWorksheets && calcRun != null)
        {
            Line("## 4. PER-STANDARD SURFACE PRESSURE WORKSHEET");
            if (!calcRun.NumericalComputationPossible)
            {
                Line("**NUMERICAL CALCULATION BLOCKED / NOT COMPUTED**");
                Line("Required normative tables or explicit engineer coefficients are missing. Surface pressures were not computed.");
            }
            else
            {
                Line("| Surface / Zone ID | Description | External Coeff | Net Pressure (Pa) | Global Force Vector (Pa) |");
                Line("| :--- | :--- | :--- | :--- | :--- |");
                foreach (var s in calcRun.SurfaceResults)
                {
                    Line($"| {s.SurfaceOrZoneId} | {s.Description} | {s.ExternalCoefficient:F2} | {s.NetPressurePa:F2} | ({s.GlobalPressureVectorPa.X:F1}, {s.GlobalPressureVectorPa.Y:F1}, {s.GlobalPressureVectorPa.Z:F1}) |");
                }
            }
            Line();
        }

        if (Options.IncludeRoofFacadeSheets)
        {
            Line("## 5. ROOF & FACADE TRIBUTARY DISTRIBUTION");
            if (model.Definition.IncludeFacade)
            {
                Line("### Facade Line Loads (N/m)");
                Line("| Panel | Frame | Method | Start (m) | End (m) | Load (N/m) |");
                Line("| :--- | :--- | :--- | :--- | :--- | :--- |");
                foreach (var f in model.Facade)
                {
                    Line($"| {f.Panel} | {f.Frame} | {f.Method} | {f.StartM:F2} | {f.EndM:F2} | {f.LineLoadNPerM:F2} |");
                }
            }
            if (model.Definition.IncludeRoof && model.Approval is { } a)
            {
                Line("### Roof Zone Area Load Proposals");
                Line("| Shell ID | Zone ID | Pattern | Net Pressure (Pa) | Local 3 Direction | Csi Area Load (Pa) |");
                Line("| :--- | :--- | :--- | :--- | :--- | :--- |");
                foreach (var p in a.Proposals)
                {
                    Line($"| {p.ShellId} | {p.ZoneId} | {p.Pattern} | {p.NetPressurePa:F2} | {p.ManualLocal3Direction} | {p.SignedCsiAreaLoadPa:F2} |");
                }
            }
            Line();
        }

        if (Options.IncludeWallLoads && model.Definition.IncludeWall)
        {
            Line("## 6. WALL GRAVITY LOADS (Global -Z)");
            Line("| Frame | Pattern | Wall Type | Height (m) | Line Load (N/m) |");
            Line("| :--- | :--- | :--- | :--- | :--- |");
            foreach (var w in model.Walls)
            {
                Line($"| {w.Frame} | {w.Pattern} | {w.WallType} | {w.HeightM:F2} | {w.GravityNPerM:F2} |");
            }
            Line();
        }

        if (Options.IncludeManualVerification)
        {
            Line("## 7. MANUAL VERIFICATION WORKSHEET");
            Line("Independent engineering check register (to be filled by reviewing engineer):");
            Line("| Quantity | Software Value | Hand-Check Value | Tolerance | Difference | Check Result | Reviewer |");
            Line("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
            if (calcRun != null) {
                double qVal = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) ? qh : (calcRun.ComputedQuantities.TryGetValue("q_des", out var qdes) ? qdes : 0.0);
                Line($"| Velocity / Dynamic Pressure | {qVal:F2} Pa | ____________ | 1.0% | ______ | NOT CHECKED | ____________ |");
                foreach (var s in calcRun.SurfaceResults) {
                    Line($"| {s.Description} p_net | {s.NetPressurePa:F2} Pa | ____________ | 1.0% | ______ | NOT CHECKED | ____________ |");
                }
            } else {
                Line("| No engineering run loaded | N/A | ____________ | 1.0% | ______ | NOT CHECKED | ____________ |");
            }
            Line();
        }

        if (Options.IncludeValidationAppendix)
        {
            Line("## APPENDIX A: VALIDATION & BLOCKED PROCEDURES");
            if (calcRun != null && calcRun.Issues.Length > 0)
            {
                foreach (var issue in calcRun.Issues)
                {
                    Line($"- [{issue.Severity}] ({issue.Stage}) {issue.Code}: {issue.Message}");
                }
            }
            else
            {
                Line("No validation blockers flagged for currently supplied input scope.");
            }
            Line("- Live CSI Model Mutation: Strictly disabled (CanCalculateNormativePressures = false).");
            Line();
        }

        if (Options.IncludeAuditAppendix)
        {
            Line("## APPENDIX B: IMMUTABLE AUDIT TRAIL");
            Line($"- Calculation Run ID: {calcRun?.Id ?? model.Calculation.Id}");
            Line($"- Calculation Provenance: {model.Calculation.Provenance}");
            Line($"- Model Geometry Fingerprint: {model.Model.GeometryFingerprint}");
            Line($"- Model Existing Loads Fingerprint: {model.Model.ExistingLoadsFingerprint}");
            Line($"- Calculation Snapshot Hash: {SnapshotJson.Hash(model.Calculation)}");
            if (calcRun != null) Line($"- Full Engineering Run Content Hash: {calcRun.ContentHash}");
            Line($"- Report Generated At: {DateTimeOffset.UtcNow:O}");
        }

        return sb.ToString();
    }

    public byte[] BuildPdf(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        string md = BuildMarkdown(model, calcRun);
        var pdf = new SimplePdfWriter
        {
            Title = $"{model.Project.Name} - Wind Load Audit",
            Watermark = calcRun?.QualificationStatus ?? "SYNTHETIC EXAMPLE - NOT FOR DESIGN"
        };

        var sections = md.Split(new[] { "\n## " }, StringSplitOptions.None);
        if (sections.Length > 0)
        {
            pdf.AddFlowingSection(sections[0]);
            for (int i = 1; i < sections.Length; i++)
            {
                pdf.AddFlowingSection("## " + sections[i]);
            }
        }
        else
        {
            pdf.AddFlowingSection(md);
        }

        return pdf.Build();
    }

    public string BuildHtml(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        string md = BuildMarkdown(model, calcRun);
        string E(string s) => System.Net.WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang='en'><meta charset='utf-8'>");
        sb.Append($"<title>{E(model.Project.Name)} - Wind Calculation Audit</title>");
        sb.Append("<style>");
        sb.Append("body { font-family: Segoe UI, sans-serif; margin: 40px; color: #1e293b; background: #f8fafc; }");
        sb.Append(".watermark { background: #fee2e2; color: #991b1b; padding: 12px; font-weight: bold; border: 1px solid #f87171; margin-bottom: 24px; border-radius: 4px; }");
        sb.Append("pre { background: white; padding: 24px; border: 1px solid #cbd5e1; border-radius: 6px; white-space: pre-wrap; font-family: Consolas, monospace; line-height: 1.5; }");
        sb.Append("</style>");
        sb.Append("<body>");
        sb.Append($"<div class='watermark'>{E(calcRun?.QualificationStatus ?? "SYNTHETIC EXAMPLE - NOT FOR DESIGN")}</div>");
        sb.Append("<pre>");
        sb.Append(E(md));
        sb.Append("</pre>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public byte[] BuildExcel(ReportModel model, EngineeringCalculationRun? calcRun = null) => new ExcelReportBuilder().Build(model, calcRun);
    public string BuildJson(ReportModel model, EngineeringCalculationRun? calcRun = null)
    {
        var doc = AuditReportDocumentBuilder.BuildDocument(model, calcRun, Options);
        return SnapshotJson.Serialize(new
        {
            ReportModel = model,
            CalculationRun = calcRun,
            CalculationRunHash = calcRun?.ContentHash,
            DocumentHash = doc.ContentHash,
            Options = Options,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }
}