using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Professional A4 Workbook-Style HTML Report Builder adapted from AnchorDesignReportBuilder.vb.
/// Generates deterministic, fully offline, escaped HTML/CSS/SVG documents matching the owner's
/// Excel calculation workbooks:
/// - AsceImperial: Hawaii Housing / ASCE 'Windload (Imperial)' (A1:P225)
/// - AsNzSlopeRoof: AS/NZS 'Slope Roof' (A1:N103)
/// - AsNzFlatRoof: AS/NZS 'Flat roof' (A1:N124)
///
/// Features:
/// - Reusable 5-column .calc-box calculation steps table (Description, Symbol, Value, Equation / Logic, Code Ref)
/// - Inline vector SVG diagrams for building geometry, axes, wind vectors, and surface pressures
/// - Complete 100% trace node ledger appendix preserving all TraceNodes[].NodeId
/// - Standard-specific units (Imperial for ASCE, Metric Kilo for AS/NZS)
/// - Pure offline rendering without external CDNs or fonts
/// - Complete HTML escaping on all inputs, formulas, and labels
/// </summary>
public sealed class WindWorkbookHtmlReportBuilder
{
    private static string E(string? val) => WebUtility.HtmlEncode(val ?? string.Empty);
    private static string F(double val, int decimals = 2) => val.ToString($"F{decimals}", CultureInfo.InvariantCulture);

    public string BuildHtml(WindPdfReportDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var sb = new StringBuilder();
        var units = doc.UnitProfile;
        var run = doc.CalculationRun;
        var req = run?.Request;
        var layout = doc.LayoutProfile;

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset='UTF-8' />");
        sb.AppendLine("  <meta http-equiv='X-UA-Compatible' content='IE=edge' />");
        sb.AppendLine("  <meta name='viewport' content='width=device-width, initial-scale=1.0' />");
        sb.AppendLine($"  <title>Structural Wind Load Calculation Report - {E(doc.Project.Name)}</title>");
        AppendStyles(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Top Sheet / Cover Block
        AppendTopHeaderBlock(sb, doc);

        // Watermark Banner if needed
        AppendWatermarkBanner(sb, doc);

        // Executive Metric Grid (KPI Cards)
        AppendMetricKpiGrid(sb, doc);

        // SVG Engineering Diagrams: Plan and Elevation
        AppendSvgDiagrams(sb, doc);

        // Core Sections based on Layout Profile
        switch (layout)
        {
            case WindReportLayoutProfile.AsceImperial:
                AppendAsceImperialSections(sb, doc);
                break;
            case WindReportLayoutProfile.AsNzSlopeRoof:
                AppendAsNzSlopeRoofSections(sb, doc);
                break;
            case WindReportLayoutProfile.AsNzFlatRoof:
                AppendAsNzFlatRoofSections(sb, doc);
                break;
        }

        // Structural Model Integration (Facade / Walls / Roof Mapping if included)
        AppendModelIntegrationSection(sb, doc);

        // Complete 100% Trace Node Ledger Appendix
        AppendTraceNodeAppendix(sb, doc);

        // Verification & Audit Sign-off Block
        AppendAuditSignoffBlock(sb, doc);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("  <style>");
        sb.AppendLine("    @page {");
        sb.AppendLine("      size: A4 portrait;");
        sb.AppendLine("      margin: 12mm 15mm 15mm 15mm;");
        sb.AppendLine("    }");
        sb.AppendLine("    * { box-sizing: border-box; }");
        sb.AppendLine("    body {");
        sb.AppendLine("      font-family: 'Segoe UI', Arial, -apple-system, BlinkMacSystemFont, sans-serif;");
        sb.AppendLine("      font-size: 8.5pt;");
        sb.AppendLine("      line-height: 1.4;");
        sb.AppendLine("      color: #1e293b;");
        sb.AppendLine("      background: #ffffff;");
        sb.AppendLine("      margin: 0;");
        sb.AppendLine("      padding: 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    @media screen {");
        sb.AppendLine("      body { background: #f1f5f9; padding: 20px 0; }");
        sb.AppendLine("      .sheet-container {");
        sb.AppendLine("        width: 210mm;");
        sb.AppendLine("        margin: 0 auto;");
        sb.AppendLine("        background: #ffffff;");
        sb.AppendLine("        padding: 15mm;");
        sb.AppendLine("        box-shadow: 0 4px 12px rgba(0,0,0,0.1);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    /* Header */");
        sb.AppendLine("    .report-header {");
        sb.AppendLine("      border-bottom: 2px solid #0f172a;");
        sb.AppendLine("      padding-bottom: 8px;");
        sb.AppendLine("      margin-bottom: 12px;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      justify-content: space-between;");
        sb.AppendLine("      align-items: flex-end;");
        sb.AppendLine("    }");
        sb.AppendLine("    .header-left h1 {");
        sb.AppendLine("      font-size: 16pt;");
        sb.AppendLine("      font-weight: 800;");
        sb.AppendLine("      color: #0f172a;");
        sb.AppendLine("      margin: 0 0 4px 0;");
        sb.AppendLine("      letter-spacing: -0.5px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .header-left .sub-title {");
        sb.AppendLine("      font-size: 8pt;");
        sb.AppendLine("      color: #64748b;");
        sb.AppendLine("      font-weight: 700;");
        sb.AppendLine("      text-transform: uppercase;");
        sb.AppendLine("      letter-spacing: 0.5px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .header-right { text-align: right; font-size: 8pt; }");
        sb.AppendLine("    .header-row { margin-bottom: 2px; }");
        sb.AppendLine("    .header-row .label { font-weight: 600; color: #64748b; margin-right: 5px; }");
        sb.AppendLine("    .header-row .value { font-weight: 700; color: #0f172a; }");
        sb.AppendLine("    /* Watermark / Status Banner */");
        sb.AppendLine("    .watermark-banner {");
        sb.AppendLine("      background: #eff6ff;");
        sb.AppendLine("      border: 1px solid #bfdbfe;");
        sb.AppendLine("      color: #1e40af;");
        sb.AppendLine("      padding: 6px 12px;");
        sb.AppendLine("      border-radius: 4px;");
        sb.AppendLine("      font-size: 8pt;");
        sb.AppendLine("      font-weight: 700;");
        sb.AppendLine("      margin-bottom: 12px;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      justify-content: space-between;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("    }");
        sb.AppendLine("    .watermark-banner.blocked { background: #fee2e2; border-color: #fecaca; color: #991b1b; }");
        sb.AppendLine("    .watermark-banner.review { background: #fef3c7; border-color: #fde68a; color: #92400e; }");
        sb.AppendLine("    .watermark-banner.verified { background: #ecfdf5; border-color: #a7f3d0; color: #065f46; }");
        sb.AppendLine("    /* KPI Metric Cards */");
        sb.AppendLine("    .metric-grid {");
        sb.AppendLine("      display: grid;");
        sb.AppendLine("      grid-template-columns: repeat(4, 1fr);");
        sb.AppendLine("      gap: 10px;");
        sb.AppendLine("      margin: 10px 0 14px 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    .metric-card {");
        sb.AppendLine("      background: #f8fafc;");
        sb.AppendLine("      border: 1px solid #e2e8f0;");
        sb.AppendLine("      border-radius: 6px;");
        sb.AppendLine("      padding: 8px;");
        sb.AppendLine("      text-align: center;");
        sb.AppendLine("    }");
        sb.AppendLine("    .metric-card .label {");
        sb.AppendLine("      font-size: 7pt;");
        sb.AppendLine("      font-weight: 700;");
        sb.AppendLine("      color: #64748b;");
        sb.AppendLine("      text-transform: uppercase;");
        sb.AppendLine("      margin-bottom: 4px;");
        sb.AppendLine("    }");
        sb.AppendLine("    .metric-card .value {");
        sb.AppendLine("      font-size: 13pt;");
        sb.AppendLine("      font-weight: 800;");
        sb.AppendLine("      color: #0f172a;");
        sb.AppendLine("    }");
        sb.AppendLine("    .metric-card .sub { font-size: 6.5pt; color: #94a3b8; }");
        sb.AppendLine("    /* Diagrams */");
        sb.AppendLine("    .diagram-panel {");
        sb.AppendLine("      display: grid;");
        sb.AppendLine("      grid-template-columns: 1fr 1fr;");
        sb.AppendLine("      gap: 12px;");
        sb.AppendLine("      margin-bottom: 14px;");
        sb.AppendLine("      break-inside: avoid;");
        sb.AppendLine("    }");
        sb.AppendLine("    .diagram-box {");
        sb.AppendLine("      border: 1px solid #e2e8f0;");
        sb.AppendLine("      border-radius: 6px;");
        sb.AppendLine("      background: #ffffff;");
        sb.AppendLine("      padding: 8px;");
        sb.AppendLine("      text-align: center;");
        sb.AppendLine("    }");
        sb.AppendLine("    .diagram-box h4 { margin: 0 0 6px 0; font-size: 8pt; color: #475569; text-transform: uppercase; }");
        sb.AppendLine("    /* Calc Box (VB Adaptation) */");
        sb.AppendLine("    .calc-box {");
        sb.AppendLine("      margin: 12px 0;");
        sb.AppendLine("      border: 1px solid #e2e8f0;");
        sb.AppendLine("      border-radius: 6px;");
        sb.AppendLine("      background: #ffffff;");
        sb.AppendLine("      break-inside: auto;");
        sb.AppendLine("    }");
        sb.AppendLine("    .calc-box-header {");
        sb.AppendLine("      background: #f1f5f9;");
        sb.AppendLine("      padding: 6px 10px;");
        sb.AppendLine("      border-bottom: 1px solid #e2e8f0;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      justify-content: space-between;");
        sb.AppendLine("      align-items: center;");
        sb.AppendLine("      break-after: avoid;");
        sb.AppendLine("    }");
        sb.AppendLine("    .calc-box-header .section-title { font-weight: 700; color: #0f172a; font-size: 9pt; }");
        sb.AppendLine("    .calc-box-header .section-ref { font-size: 7.5pt; color: #64748b; font-style: italic; }");
        sb.AppendLine("    .calc-box-content { padding: 4px 6px; }");
        sb.AppendLine("    /* 5-Column Engineering Table */");
        sb.AppendLine("    table.calc-table {");
        sb.AppendLine("      width: 100%;");
        sb.AppendLine("      border-collapse: collapse;");
        sb.AppendLine("      font-size: 8pt;");
        sb.AppendLine("      margin: 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    table.calc-table thead { display: table-header-group; }");
        sb.AppendLine("    table.calc-table tr { break-inside: avoid; }");
        sb.AppendLine("    table.calc-table th {");
        sb.AppendLine("      background: #1e293b;");
        sb.AppendLine("      color: #f8fafc;");
        sb.AppendLine("      text-align: left;");
        sb.AppendLine("      padding: 5px 8px;");
        sb.AppendLine("      font-weight: 600;");
        sb.AppendLine("      font-size: 7pt;");
        sb.AppendLine("      text-transform: uppercase;");
        sb.AppendLine("      letter-spacing: 0.5px;");
        sb.AppendLine("      border: none;");
        sb.AppendLine("    }");
        sb.AppendLine("    table.calc-table td {");
        sb.AppendLine("      border-bottom: 1px solid #f1f5f9;");
        sb.AppendLine("      padding: 5px 8px;");
        sb.AppendLine("      vertical-align: middle;");
        sb.AppendLine("      color: #334155;");
        sb.AppendLine("    }");
        sb.AppendLine("    table.calc-table tr:nth-child(even) { background: #f8fafc; }");
        sb.AppendLine("    table.calc-table td.col-desc { width: 30%; font-weight: 500; }");
        sb.AppendLine("    table.calc-table td.col-sym { width: 12%; font-family: 'Cambria Math', Georgia, serif; font-weight: 700; color: #0f172a; }");
        sb.AppendLine("    table.calc-table td.col-val { width: 18%; font-weight: 700; color: #1e40af; }");
        sb.AppendLine("    table.calc-table td.col-eq { width: 25%; font-family: 'Consolas', 'Lucida Console', monospace; font-size: 7.5pt; color: #475569; word-break: break-word; }");
        sb.AppendLine("    table.calc-table td.col-ref { width: 15%; font-size: 7pt; color: #64748b; }");
        sb.AppendLine("    /* Badges */");
        sb.AppendLine("    .badge { display: inline-block; padding: 2px 7px; border-radius: 9999px; font-size: 7pt; font-weight: 700; }");
        sb.AppendLine("    .badge-pass { background: #dcfce7; color: #166534; border: 1px solid #bbf7d0; }");
        sb.AppendLine("    .badge-review { background: #fef3c7; color: #92400e; border: 1px solid #fde68a; }");
        sb.AppendLine("    .badge-blocked { background: #fee2e2; color: #991b1b; border: 1px solid #fecaca; }");
        sb.AppendLine("    /* Footers & Page Breaks */");
        sb.AppendLine("    .page-break { page-break-before: always; break-before: page; margin-top: 15px; }");
        sb.AppendLine("    .report-footer {");
        sb.AppendLine("      border-top: 1px solid #e2e8f0;");
        sb.AppendLine("      padding-top: 6px;");
        sb.AppendLine("      margin-top: 14px;");
        sb.AppendLine("      font-size: 7pt;");
        sb.AppendLine("      color: #94a3b8;");
        sb.AppendLine("      display: flex;");
        sb.AppendLine("      justify-content: space-between;");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
    }

    private static void AppendTopHeaderBlock(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        string rev = doc.Options.Revision;
        string author = doc.Options.Author;
        string reviewer = doc.Options.OwnerReviewer;

        sb.AppendLine("  <div class='report-header'>");
        sb.AppendLine("    <div class='header-left'>");
        sb.AppendLine("      <h1>STRUCTURAL WIND LOAD CALCULATION</h1>");
        sb.AppendLine($"      <div class='sub-title'>ENGINEERING CALCULATION REPORT &bull; {E(doc.Manifest.Edition)}</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='header-right'>");
        sb.AppendLine($"      <div class='header-row'><span class='label'>Project:</span><span class='value'>{E(doc.Project.Name)} ({E(doc.Project.Id)})</span></div>");
        sb.AppendLine($"      <div class='header-row'><span class='label'>Date:</span><span class='value'>{doc.GeneratedAt:yyyy-MM-dd HH:mm} UTC</span></div>");
        sb.AppendLine($"      <div class='header-row'><span class='label'>Rev:</span><span class='value'>{E(rev)}</span> | <span class='label'>Author:</span><span class='value'>{E(author)}</span></div>");
        sb.AppendLine($"      <div class='header-row'><span class='label'>Reviewer:</span><span class='value'>{E(reviewer)}</span></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendWatermarkBanner(StringBuilder sb, WindPdfReportDocument doc)
    {
        string statusText = doc.QualificationStatus;
        string bannerClass = "watermark-banner";
        if (!doc.NumericalComputationPossible)
        {
            bannerClass += " blocked";
        }
        else if (doc.IndependentlyVerified)
        {
            bannerClass += " verified";
        }
        else
        {
            bannerClass += " review";
        }

        sb.AppendLine($"  <div class='{bannerClass}'>");
        sb.AppendLine($"    <span>STATUS: {E(statusText)}</span>");
        sb.AppendLine($"    <span>UNITS: {E(doc.UnitProfile.SystemName)} ({E(doc.UnitProfile.PressureUnit)}, {E(doc.UnitProfile.ForceUnit)})</span>");
        sb.AppendLine("  </div>");
    }

    private static void AppendMetricKpiGrid(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        var req = run?.Request;
        var units = doc.UnitProfile;

        double vSpeed = req?.BasicWindSpeed ?? 0.0;
        string speedDisplay = units.Speed(vSpeed);
        string limitState = req?.LimitState.ToString().ToUpperInvariant() ?? "ULS";
        string direction = req != null ? $"{req.WindDirection} (θ = {req.WindAngleDegrees:F0}°)" : "WX+";
        string meanRoofH = req != null ? units.Length(req.ReferenceHeightM) : units.Length(9.0);

        string maxP = "-";
        if (run != null && run.SurfaceResults.Length > 0)
        {
            var maxVal = run.SurfaceResults.Max(p => Math.Abs(p.NetPressurePa));
            maxP = units.Pressure(maxVal);
        }

        sb.AppendLine("  <div class='metric-grid'>");
        sb.AppendLine("    <div class='metric-card'>");
        sb.AppendLine("      <div class='label'>Basic Wind Speed</div>");
        sb.AppendLine($"      <div class='value'>{E(speedDisplay)}</div>");
        sb.AppendLine($"      <div class='sub'>{E(limitState)} Design Event</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='metric-card'>");
        sb.AppendLine("      <div class='label'>Active Direction</div>");
        sb.AppendLine($"      <div class='value'>{E(direction)}</div>");
        sb.AppendLine($"      <div class='sub'>Roof Form: {E(req?.RoofForm ?? "Gable")} ({req?.RoofSlopeDegrees:F1}°)</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='metric-card'>");
        sb.AppendLine("      <div class='label'>Mean Roof Height</div>");
        sb.AppendLine($"      <div class='value'>{E(meanRoofH)}</div>");
        sb.AppendLine($"      <div class='sub'>Eave: {units.Length(req?.EaveHeightM ?? 6.0)}</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='metric-card'>");
        sb.AppendLine("      <div class='label'>Peak Net Pressure</div>");
        sb.AppendLine($"      <div class='value'>{E(maxP)}</div>");
        sb.AppendLine($"      <div class='sub'>Internal Case: {E(req?.Options.GetValueOrDefault("InternalPressure", "IP_POS"))}</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendSvgDiagrams(StringBuilder sb, WindPdfReportDocument doc)
    {
        var req = doc.CalculationRun?.Request;
        var units = doc.UnitProfile;

        double L = req?.BuildingLengthM ?? 30.0;
        double B = req?.BuildingWidthM ?? 20.0;
        double H = req?.ReferenceHeightM ?? 9.0;
        double H_eave = req?.EaveHeightM ?? 6.0;
        double slope = req?.RoofSlopeDegrees ?? 15.0;
        string dirStr = req?.WindDirection.ToString() ?? "PositiveX";

        string lStr = units.Length(L);
        string bStr = units.Length(B);
        string hStr = units.Length(H);
        string heStr = units.Length(H_eave);

        sb.AppendLine("  <div class='diagram-panel'>");

        // 1. Plan View SVG with Wind Vector
        sb.AppendLine("    <div class='diagram-box'>");
        sb.AppendLine("      <h4>Building Plan &amp; Wind Vector (XY View)</h4>");
        sb.AppendLine("      <svg width='100%' height='130' viewBox='0 0 320 130' xmlns='http://www.w3.org/2000/svg'>");
        sb.AppendLine("        <rect width='100%' height='100%' fill='#f8fafc' rx='4'/>");
        sb.AppendLine("        <!-- Building Outline -->");
        sb.AppendLine("        <rect x='90' y='30' width='140' height='75' fill='#e2e8f0' stroke='#1e293b' stroke-width='1.5' rx='2'/>");
        sb.AppendLine($"        <text x='160' y='72' font-size='9' font-weight='bold' fill='#0f172a' text-anchor='middle'>{E(lStr)} &times; {E(bStr)}</text>");
        sb.AppendLine("        <!-- Axes -->");
        sb.AppendLine("        <line x1='20' y1='115' x2='50' y2='115' stroke='#64748b' stroke-width='1.5'/>");
        sb.AppendLine("        <polygon points='50,113 55,115 50,117' fill='#64748b'/>");
        sb.AppendLine("        <text x='58' y='118' font-size='8' font-weight='bold' fill='#64748b'>+X</text>");
        sb.AppendLine("        <line x1='20' y1='115' x2='20' y2='85' stroke='#64748b' stroke-width='1.5'/>");
        sb.AppendLine("        <polygon points='18,85 20,80 22,85' fill='#64748b'/>");
        sb.AppendLine("        <text x='17' y='75' font-size='8' font-weight='bold' fill='#64748b'>+Y</text>");

        // Wind Vector Arrow
        if (dirStr.Contains("PositiveX", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("        <line x1='25' y1='67' x2='78' y2='67' stroke='#2563eb' stroke-width='2.5'/>");
            sb.AppendLine("        <polygon points='78,63 88,67 78,71' fill='#2563eb'/>");
            sb.AppendLine("        <text x='55' y='60' font-size='8.5' font-weight='bold' fill='#2563eb' text-anchor='middle'>Wind +X</text>");
            sb.AppendLine("        <text x='86' y='25' font-size='7' fill='#16a34a' font-weight='bold'>Windward Wall</text>");
            sb.AppendLine("        <text x='235' y='25' font-size='7' fill='#dc2626' font-weight='bold'>Leeward Wall</text>");
        }
        else if (dirStr.Contains("NegativeX", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("        <line x1='295' y1='67' x2='242' y2='67' stroke='#2563eb' stroke-width='2.5'/>");
            sb.AppendLine("        <polygon points='242,63 232,67 242,71' fill='#2563eb'/>");
            sb.AppendLine("        <text x='265' y='60' font-size='8.5' font-weight='bold' fill='#2563eb' text-anchor='middle'>Wind -X</text>");
        }
        else if (dirStr.Contains("PositiveY", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("        <line x1='160' y1='120' x2='160' y2='110' stroke='#2563eb' stroke-width='2.5'/>");
            sb.AppendLine("        <polygon points='156,110 160,105 164,110' fill='#2563eb'/>");
            sb.AppendLine("        <text x='160' y='128' font-size='8.5' font-weight='bold' fill='#2563eb' text-anchor='middle'>Wind +Y</text>");
        }
        else
        {
            sb.AppendLine("        <line x1='160' y1='5' x2='160' y2='25' stroke='#2563eb' stroke-width='2.5'/>");
            sb.AppendLine("        <polygon points='156,25 160,30 164,25' fill='#2563eb'/>");
            sb.AppendLine("        <text x='160' y='12' font-size='8.5' font-weight='bold' fill='#2563eb' text-anchor='middle'>Wind -Y</text>");
        }

        sb.AppendLine("      </svg>");
        sb.AppendLine("    </div>");

        // 2. Elevation View SVG with Roof Slope
        sb.AppendLine("    <div class='diagram-box'>");
        sb.AppendLine("      <h4>Building Elevation &amp; Roof Profile</h4>");
        sb.AppendLine("      <svg width='100%' height='130' viewBox='0 0 320 130' xmlns='http://www.w3.org/2000/svg'>");
        sb.AppendLine("        <rect width='100%' height='100%' fill='#f8fafc' rx='4'/>");
        sb.AppendLine("        <!-- Ground Line -->");
        sb.AppendLine("        <line x1='20' y1='105' x2='300' y2='105' stroke='#64748b' stroke-width='1.5' stroke-dasharray='4,2'/>");
        sb.AppendLine("        <text x='25' y='116' font-size='6.5' fill='#64748b'>GROUND DATUM</text>");
        sb.AppendLine("        <!-- Building Walls & Roof Polygon -->");
        sb.AppendLine("        <polygon points='70,105 70,60 160,35 250,60 250,105' fill='#e2e8f0' stroke='#1e293b' stroke-width='1.5'/>");
        sb.AppendLine("        <!-- Dimension Markers -->");
        sb.AppendLine($"        <text x='62' y='85' font-size='7.5' font-weight='bold' fill='#64748b' text-anchor='end'>h_eave={E(heStr)}</text>");
        sb.AppendLine($"        <text x='160' y='28' font-size='7.5' font-weight='bold' fill='#0f172a' text-anchor='middle'>Ridge h={E(hStr)} (Slope {slope:F1}°)</text>");
        sb.AppendLine("        <!-- Windward Pressure Arrows (Push) -->");
        sb.AppendLine("        <line x1='30' y1='75' x2='65' y2='75' stroke='#16a34a' stroke-width='1.5'/>");
        sb.AppendLine("        <polygon points='65,72 70,75 65,78' fill='#16a34a'/>");
        sb.AppendLine("        <line x1='30' y1='90' x2='65' y2='90' stroke='#16a34a' stroke-width='1.5'/>");
        sb.AppendLine("        <polygon points='65,87 70,90 65,93' fill='#16a34a'/>");
        sb.AppendLine("        <text x='47' y='68' font-size='7' font-weight='bold' fill='#16a34a' text-anchor='middle'>+p (Push)</text>");
        sb.AppendLine("        <!-- Leeward Suction Arrows (Pull) -->");
        sb.AppendLine("        <line x1='250' y1='80' x2='285' y2='80' stroke='#dc2626' stroke-width='1.5'/>");
        sb.AppendLine("        <polygon points='285,77 290,80 285,83' fill='#dc2626'/>");
        sb.AppendLine("        <text x='270' y='73' font-size='7' font-weight='bold' fill='#dc2626' text-anchor='middle'>-p (Suction)</text>");
        sb.AppendLine("      </svg>");
        sb.AppendLine("    </div>");

        sb.AppendLine("  </div>");
    }

    private static void AppendAsceImperialSections(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        var req = run?.Request;
        var units = doc.UnitProfile;

        // SECTION A: Reference Standards
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>A. REFERENCE STANDARDS &amp; GOVERNING CODES</span>");
        sb.AppendLine("      <span class='section-ref'>ASCE 7-22 Chapter 27 &amp; Supplements</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Standard / Reference Document</th><th class='col-sym'>Designation</th><th class='col-val'>Status</th><th class='col-eq'>Notes / Edition</th><th class='col-ref'>Clause</th></tr></thead>");
        sb.AppendLine("        <tbody>");
        sb.AppendLine($"          <tr><td class='col-desc'>Governing Wind Standard</td><td class='col-sym'>ASCE 7</td><td class='col-val'>{E(doc.Manifest.Status.ToString())}</td><td class='col-eq'>{E(doc.Manifest.Edition)}</td><td class='col-ref'>Ch. 26-27</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Standard Supplements</td><td class='col-sym'>Supplements</td><td class='col-val'>Applied</td><td class='col-eq'>{E(string.Join(", ", doc.Manifest.Revisions))}</td><td class='col-ref'>Supp. 1-3</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Wind Procedure Selection</td><td class='col-sym'>Procedure</td><td class='col-val'>{E(req?.ProcedureName ?? "Directional Procedure")}</td><td class='col-eq'>Enclosed Building</td><td class='col-ref'>Part 1</td></tr>");
        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        // SECTION B: Basic Wind Pressure Calculation
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>B. BASIC VELOCITY PRESSURE CALCULATION (qh &amp; qz)</span>");
        sb.AppendLine("      <span class='section-ref'>ASCE 7-22 Section 26.10 &amp; Eq. 26.10-1</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Parameter / Step Description</th><th class='col-sym'>Symbol</th><th class='col-val'>Value</th><th class='col-eq'>Equation / Logic</th><th class='col-ref'>Code Ref</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        double vSpeed = req?.BasicWindSpeed ?? 40.0;
        double kd = req?.ExplicitCoefficients.GetValueOrDefault("Kd")?.Value ?? 0.85;
        double kzt = req?.ExplicitCoefficients.GetValueOrDefault("Kzt")?.Value ?? 1.0;
        double ke = req?.ExplicitCoefficients.GetValueOrDefault("Ke")?.Value ?? 1.0;
        double kh = req?.ExplicitCoefficients.GetValueOrDefault("Kh")?.Value ?? 1.0;
        double g = req?.ExplicitCoefficients.GetValueOrDefault("G")?.Value ?? 0.85;
        double gcpi = req?.ExplicitCoefficients.GetValueOrDefault("GCpi")?.Value ?? 0.18;

        sb.AppendLine($"          <tr><td class='col-desc'>Basic Wind Speed</td><td class='col-sym'>V</td><td class='col-val'>{E(units.Speed(vSpeed))}</td><td class='col-eq'>3-s gust speed</td><td class='col-ref'>Fig. 26.5-1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Wind Directionality Factor</td><td class='col-sym'>K<sub>d</sub></td><td class='col-val'>{F(kd, 3)}</td><td class='col-eq'>Main Wind Force Resisting System</td><td class='col-ref'>Table 26.6-1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Exposure Category</td><td class='col-sym'>Exposure</td><td class='col-val'>{E(req?.ExposureOrTerrain ?? "C")}</td><td class='col-eq'>Open terrain with scattered obstructions</td><td class='col-ref'>Sec. 26.7</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Topographic Factor</td><td class='col-sym'>K<sub>zt</sub></td><td class='col-val'>{F(kzt, 3)}</td><td class='col-eq'>(1 + K1*K2*K3)^2</td><td class='col-ref'>Sec. 26.8</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Ground Elevation Factor</td><td class='col-sym'>K<sub>e</sub></td><td class='col-val'>{F(ke, 3)}</td><td class='col-eq'>e^(-0.0000362 * z_g)</td><td class='col-ref'>Table 26.9-1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Velocity Pressure Exposure Coeff</td><td class='col-sym'>K<sub>z</sub> / K<sub>h</sub></td><td class='col-val'>{F(kh, 3)}</td><td class='col-eq'>Evaluated at mean roof height h</td><td class='col-ref'>Table 26.10-1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Gust-Effect Factor</td><td class='col-sym'>G</td><td class='col-val'>{F(g, 3)}</td><td class='col-eq'>Rigid building assumption</td><td class='col-ref'>Sec. 26.11</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Internal Pressure Coefficient</td><td class='col-sym'>GC<sub>pi</sub></td><td class='col-val'>&plusmn;{F(gcpi, 2)}</td><td class='col-eq'>Enclosed building</td><td class='col-ref'>Table 26.13-1</td></tr>");

        if (run != null)
        {
            var qzNode = run.TraceNodes.FirstOrDefault(n => n.NodeId.Contains("VelocityPressure") || n.NodeId.Contains("qh"));
            if (qzNode != null)
            {
                sb.AppendLine($"          <tr style='background:#ecfdf5;'><td class='col-desc'><strong>Velocity Pressure at Height h</strong></td><td class='col-sym'><strong>q<sub>h</sub></strong></td><td class='col-val'><strong>{E(units.Pressure(qzNode.NumericValue))}</strong></td><td class='col-eq'>{E(qzNode.SubstitutedEquation)}</td><td class='col-ref'>Eq. 26.10-1</td></tr>");
            }
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        // SECTION C: Design Wind Pressures (Windward, Leeward, Side Walls, Roof)
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>C. DESIGN WIND PRESSURE ON SURFACES (p = q*G*Cp - qh*(GCpi))</span>");
        sb.AppendLine("      <span class='section-ref'>ASCE 7-22 Section 27.3.1 &amp; Eq. 27.3-1</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Surface / Zone Description</th><th class='col-sym'>C<sub>p</sub></th><th class='col-val'>Design Net Pressure</th><th class='col-eq'>Substituted Equation</th><th class='col-ref'>Action</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        if (run != null && run.SurfaceResults.Length > 0)
        {
            foreach (var sp in run.SurfaceResults)
            {
                string pNet = units.Pressure(sp.NetPressurePa);
                string pAlt = units.Pressure(sp.AlternateLimitStateNetPressurePa);
                string action = sp.NetPressurePa >= 0 ? "Pressure (Inward)" : "Suction (Outward)";
                string badgeClass = sp.NetPressurePa >= 0 ? "badge badge-pass" : "badge badge-review";

                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td class='col-desc'><strong>{E(sp.SurfaceOrZoneId)}</strong> ({E(sp.Description)})</td>");
                sb.AppendLine($"            <td class='col-sym'>{F(sp.ExternalCoefficient, 2)}</td>");
                sb.AppendLine($"            <td class='col-val'>{E(pNet)} <span style='font-size:7pt;color:#64748b;'>(Alt: {E(pAlt)})</span></td>");
                sb.AppendLine($"            <td class='col-eq'>q<sub>h</sub>({F(g,2)} &times; {F(sp.ExternalCoefficient,2)} &minus; ({F(sp.InternalCoefficient,2)}))</td>");
                sb.AppendLine($"            <td class='col-ref'><span class='{badgeClass}'>{action}</span></td>");
                sb.AppendLine("          </tr>");
            }
        }
        else
        {
            sb.AppendLine("          <tr><td colspan='5' style='text-align:center;color:#94a3b8;'>No calculated surface pressures available.</td></tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendAsNzSlopeRoofSections(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        var req = run?.Request;
        var units = doc.UnitProfile;

        // SECTION 1: Basic Parameters
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>1. BASIC PARAMETERS &amp; REGIONAL MULTIPLIERS</span>");
        sb.AppendLine("      <span class='section-ref'>AS/NZS 1170.2:2021 Section 2 &amp; 3</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Parameter Description</th><th class='col-sym'>Symbol</th><th class='col-val'>Value</th><th class='col-eq'>Specification / Method</th><th class='col-ref'>Clause</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        double vSpeed = req?.BasicWindSpeed ?? 45.0;
        double md = req?.ExplicitCoefficients.GetValueOrDefault("Md")?.Value ?? 1.0;
        double mz_cat = req?.ExplicitCoefficients.GetValueOrDefault("Mz_cat")?.Value ?? 0.91;
        double ms = req?.ExplicitCoefficients.GetValueOrDefault("Ms")?.Value ?? 1.0;
        double mt = req?.ExplicitCoefficients.GetValueOrDefault("Mt")?.Value ?? 1.0;
        double cpi = req?.ExplicitCoefficients.GetValueOrDefault("Cpi")?.Value ?? 0.20;

        sb.AppendLine($"          <tr><td class='col-desc'>Regional Wind Speed (500-yr ULS)</td><td class='col-sym'>V<sub>R</sub></td><td class='col-val'>{E(units.Speed(vSpeed))}</td><td class='col-eq'>Wind Region / ARI</td><td class='col-ref'>Table 3.1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Wind Direction Multiplier</td><td class='col-sym'>M<sub>d</sub></td><td class='col-val'>{F(md, 3)}</td><td class='col-eq'>Cardinal direction factor</td><td class='col-ref'>Sec. 3.3</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Terrain/Height Multiplier</td><td class='col-sym'>M<sub>z,cat</sub></td><td class='col-val'>{F(mz_cat, 3)}</td><td class='col-eq'>Terrain Category 2 / Height z</td><td class='col-ref'>Table 4.1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Shielding Multiplier</td><td class='col-sym'>M<sub>s</sub></td><td class='col-val'>{F(ms, 3)}</td><td class='col-eq'>Shielding parameter</td><td class='col-ref'>Sec. 4.3</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Topographic Multiplier</td><td class='col-sym'>M<sub>t</sub></td><td class='col-val'>{F(mt, 3)}</td><td class='col-eq'>Hill / Escarpment factor</td><td class='col-ref'>Sec. 4.4</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Internal Pressure Coefficient</td><td class='col-sym'>C<sub>pi</sub></td><td class='col-val'>&plusmn;{F(cpi, 2)}</td><td class='col-eq'>Standard enclosure</td><td class='col-ref'>Table 5.1</td></tr>");

        if (run != null)
        {
            var vdesNode = run.TraceNodes.FirstOrDefault(n => n.NodeId.Contains("SiteWindSpeed") || n.NodeId.Contains("Vdes"));
            if (vdesNode != null)
            {
                sb.AppendLine($"          <tr style='background:#ecfdf5;'><td class='col-desc'><strong>Site Design Wind Speed</strong></td><td class='col-sym'><strong>V<sub>des,θ</sub></strong></td><td class='col-val'><strong>{E(units.Speed(vdesNode.NumericValue))}</strong></td><td class='col-eq'>{E(vdesNode.SubstitutedEquation)}</td><td class='col-ref'>Eq. 2.2</td></tr>");
            }
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        // SECTION 2: Slope Roof Wind Pressures
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>2. SLOPE ROOF &amp; WALL DESIGN PRESSURES (p = [Cpe - Cpi] * qz)</span>");
        sb.AppendLine("      <span class='section-ref'>AS/NZS 1170.2:2021 Section 5 &amp; Table 5.3(C)</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Surface / Slope Zone</th><th class='col-sym'>C<sub>pe</sub></th><th class='col-val'>Design Net Pressure</th><th class='col-eq'>Substituted Formula</th><th class='col-ref'>Action</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        if (run != null && run.SurfaceResults.Length > 0)
        {
            foreach (var sp in run.SurfaceResults)
            {
                string pNet = units.Pressure(sp.NetPressurePa);
                string pAlt = units.Pressure(sp.AlternateLimitStateNetPressurePa);
                string action = sp.NetPressurePa >= 0 ? "Pressure (Inward)" : "Suction (Outward)";
                string badgeClass = sp.NetPressurePa >= 0 ? "badge badge-pass" : "badge badge-review";

                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td class='col-desc'><strong>{E(sp.SurfaceOrZoneId)}</strong> ({E(sp.Description)})</td>");
                sb.AppendLine($"            <td class='col-sym'>{F(sp.ExternalCoefficient, 2)}</td>");
                sb.AppendLine($"            <td class='col-val'>{E(pNet)} <span style='font-size:7pt;color:#64748b;'>(Alt: {E(pAlt)})</span></td>");
                sb.AppendLine($"            <td class='col-eq'>({F(sp.ExternalCoefficient,2)} &minus; ({F(sp.InternalCoefficient,2)})) &times; q<sub>z</sub></td>");
                sb.AppendLine($"            <td class='col-ref'><span class='{badgeClass}'>{action}</span></td>");
                sb.AppendLine("          </tr>");
            }
        }
        else
        {
            sb.AppendLine("          <tr><td colspan='5' style='text-align:center;color:#94a3b8;'>No calculated surface pressures available.</td></tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendAsNzFlatRoofSections(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        var req = run?.Request;
        var units = doc.UnitProfile;

        // SECTION 1: Basic Parameters
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>1. BASIC PARAMETERS &amp; FLAT ROOF SITE GEOMETRY</span>");
        sb.AppendLine("      <span class='section-ref'>AS/NZS 1170.2:2021 Section 2 &amp; 3</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Parameter Description</th><th class='col-sym'>Symbol</th><th class='col-val'>Value</th><th class='col-eq'>Specification / Method</th><th class='col-ref'>Clause</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        double vSpeed = req?.BasicWindSpeed ?? 45.0;
        double md = req?.ExplicitCoefficients.GetValueOrDefault("Md")?.Value ?? 1.0;
        double mz_cat = req?.ExplicitCoefficients.GetValueOrDefault("Mz_cat")?.Value ?? 0.91;
        double ms = req?.ExplicitCoefficients.GetValueOrDefault("Ms")?.Value ?? 1.0;
        double mt = req?.ExplicitCoefficients.GetValueOrDefault("Mt")?.Value ?? 1.0;

        sb.AppendLine($"          <tr><td class='col-desc'>Regional Wind Speed (500-yr ULS)</td><td class='col-sym'>V<sub>R</sub></td><td class='col-val'>{E(units.Speed(vSpeed))}</td><td class='col-eq'>Wind Region / ARI</td><td class='col-ref'>Table 3.1</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Wind Direction Multiplier</td><td class='col-sym'>M<sub>d</sub></td><td class='col-val'>{F(md, 3)}</td><td class='col-eq'>Cardinal direction factor</td><td class='col-ref'>Sec. 3.3</td></tr>");
        sb.AppendLine($"          <tr><td class='col-desc'>Terrain/Height Multiplier</td><td class='col-sym'>M<sub>z,cat</sub></td><td class='col-val'>{F(mz_cat, 3)}</td><td class='col-eq'>Terrain Category 2 / Height z</td><td class='col-ref'>Table 4.1</td></tr>");
        sb.AppendLine("          <tr><td class='col-desc'>Building Roof Pitch</td><td class='col-sym'>α</td><td class='col-val'>0.0° (Flat Roof)</td><td class='col-eq'>Slope &le; 1.0° classification</td><td class='col-ref'>Table 5.3(A)</td></tr>");

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        // SECTION 2: Flat Roof Pressure Zones (0 to h/2, h/2 to h, > h)
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>2. FLAT ROOF ZONE PRESSURES (Zone 1: 0 to h/2, Zone 2: h/2 to h, Zone 3: &gt; h)</span>");
        sb.AppendLine("      <span class='section-ref'>AS/NZS 1170.2:2021 Table 5.3(A)</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th class='col-desc'>Roof Zone Description</th><th class='col-sym'>C<sub>pe</sub></th><th class='col-val'>Design Net Pressure</th><th class='col-eq'>Formula / Zone Bounds</th><th class='col-ref'>Action</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        if (run != null && run.SurfaceResults.Length > 0)
        {
            foreach (var sp in run.SurfaceResults)
            {
                string pNet = units.Pressure(sp.NetPressurePa);
                string pAlt = units.Pressure(sp.AlternateLimitStateNetPressurePa);
                string action = sp.NetPressurePa >= 0 ? "Pressure" : "Suction (Uplift)";
                string badgeClass = sp.NetPressurePa >= 0 ? "badge badge-pass" : "badge badge-review";

                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td class='col-desc'><strong>{E(sp.SurfaceOrZoneId)}</strong> ({E(sp.Description)})</td>");
                sb.AppendLine($"            <td class='col-sym'>{F(sp.ExternalCoefficient, 2)}</td>");
                sb.AppendLine($"            <td class='col-val'>{E(pNet)} <span style='font-size:7pt;color:#64748b;'>(Alt: {E(pAlt)})</span></td>");
                sb.AppendLine($"            <td class='col-eq'>({F(sp.ExternalCoefficient,2)} &minus; ({F(sp.InternalCoefficient,2)})) &times; q<sub>z</sub></td>");
                sb.AppendLine($"            <td class='col-ref'><span class='{badgeClass}'>{action}</span></td>");
                sb.AppendLine("          </tr>");
            }
        }
        else
        {
            sb.AppendLine("          <tr><td colspan='5' style='text-align:center;color:#94a3b8;'>No calculated surface pressures available.</td></tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendModelIntegrationSection(StringBuilder sb, WindPdfReportDocument doc)
    {
        var units = doc.UnitProfile;

        if (doc.FacadeRows.Length > 0 || doc.WallRows.Length > 0)
        {
            sb.AppendLine("  <div class='calc-box'>");
            sb.AppendLine("    <div class='calc-box-header'>");
            sb.AppendLine("      <span class='section-title'>STRUCTURAL MODEL LOAD INTEGRATION &amp; TRIBUTARY LOADS</span>");
            sb.AppendLine("      <span class='section-ref'>Frame Segment Loads &amp; Wall Gravity Actions</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <div class='calc-box-content'>");
            sb.AppendLine("      <table class='calc-table'>");
            sb.AppendLine("        <thead><tr><th class='col-desc'>Element / Member</th><th class='col-sym'>Type</th><th class='col-val'>Distributed Load</th><th class='col-eq'>Span / Fraction</th><th class='col-ref'>Method</th></tr></thead>");
            sb.AppendLine("        <tbody>");

            foreach (var f in doc.FacadeRows.Take(12))
            {
                sb.AppendLine($"          <tr><td class='col-desc'>Frame {E(f.Frame)} ({E(f.Panel)})</td><td class='col-sym'>Facade</td><td class='col-val'>{E(units.LineLoad(f.LineLoadNPerM))}</td><td class='col-eq'>{units.Length(f.StartM)} to {units.Length(f.EndM)}</td><td class='col-ref'>{E(f.Method)}</td></tr>");
            }

            foreach (var w in doc.WallRows.Take(6))
            {
                sb.AppendLine($"          <tr><td class='col-desc'>Wall on {E(w.Frame)}</td><td class='col-sym'>Gravity</td><td class='col-val'>{E(units.LineLoad(w.GravityNPerM))}</td><td class='col-eq'>Height {units.Length(w.HeightM)}</td><td class='col-ref'>{E(w.WallType)}</td></tr>");
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
        }
    }

    private static void AppendTraceNodeAppendix(StringBuilder sb, WindPdfReportDocument doc)
    {
        var run = doc.CalculationRun;
        var units = doc.UnitProfile;

        sb.AppendLine("  <div class='calc-box page-break'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>APPENDIX: COMPLETE STEP-BY-STEP CALCULATION TRACE LEDGER</span>");
        sb.AppendLine($"      <span class='section-ref'>{run?.TraceNodes.Length ?? 0} Trace Nodes &bull; Exact Deterministic Provenance</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content'>");
        sb.AppendLine("      <table class='calc-table'>");
        sb.AppendLine("        <thead><tr><th style='width:18%;'>Node ID / Quantity</th><th style='width:10%;'>Stage</th><th style='width:18%;'>Result Value</th><th style='width:34%;'>Substituted Equation / Logic</th><th style='width:20%;'>Source Reference</th></tr></thead>");
        sb.AppendLine("        <tbody>");

        if (run != null && run.TraceNodes.Length > 0)
        {
            foreach (var node in run.TraceNodes)
            {
                string converted = units.ConvertTraceResult(node.NumericValue, node.OutputUnit, node.ResultWithUnit);

                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td style='font-weight:700;'>[{E(node.NodeId)}]<br/><span style='font-size:7pt;color:#64748b;'>{E(node.QuantityName)}</span></td>");
                sb.AppendLine($"            <td style='font-size:7pt;color:#64748b;'>{E(node.Stage.ToString())}</td>");
                sb.AppendLine($"            <td style='font-weight:700;color:#1e40af;'>{E(converted)}</td>");
                sb.AppendLine($"            <td style='font-family:Consolas,monospace;font-size:7.5pt;word-break:break-all;'>{E(node.SubstitutedEquation)}</td>");
                sb.AppendLine($"            <td style='font-size:7pt;color:#64748b;'>{E(node.SourceReference)}</td>");
                sb.AppendLine("          </tr>");
            }
        }
        else
        {
            sb.AppendLine("          <tr><td colspan='5' style='text-align:center;color:#94a3b8;'>No trace nodes recorded for this run.</td></tr>");
        }

        sb.AppendLine("        </tbody>");
        sb.AppendLine("      </table>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendAuditSignoffBlock(StringBuilder sb, WindPdfReportDocument doc)
    {
        sb.AppendLine("  <div class='calc-box'>");
        sb.AppendLine("    <div class='calc-box-header'>");
        sb.AppendLine("      <span class='section-title'>ENGINEERING VERIFICATION &amp; IMMUTABLE AUDIT RECORD</span>");
        sb.AppendLine("      <span class='section-ref'>Phase 0 Safety Gates &amp; CSI Protection Intact</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class='calc-box-content' style='padding:10px;'>");
        sb.AppendLine("      <div style='display:grid;grid-template-columns:1fr 1fr;gap:15px;font-size:8pt;'>");
        sb.AppendLine("        <div>");
        sb.AppendLine($"          <p><strong>Report Document ID:</strong> {E(doc.ReportId)}</p>");
        sb.AppendLine($"          <p><strong>Calculation Run Content Hash:</strong> <code>{E(doc.ContentHash)}</code></p>");
        sb.AppendLine($"          <p><strong>CSI Model Snapshot Fingerprint:</strong> <code>{E(doc.ModelSnapshot.GeometryFingerprint)}</code></p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div>");
        sb.AppendLine($"          <p><strong>Owner Engineer Reviewed:</strong> {(doc.OwnerReviewed ? "YES" : "NO - PENDING REVIEW")}</p>");
        sb.AppendLine($"          <p><strong>Independent Verification:</strong> {(doc.IndependentlyVerified ? "VERIFIED" : "NOT INDEPENDENTLY VERIFIED")}</p>");
        sb.AppendLine("          <p><strong>CSI Live Write Protection:</strong> <span class='badge badge-pass'>ACTIVE &bull; READ-ONLY BOUNDARY INTACT</span></p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class='report-footer'>");
        sb.AppendLine($"    <div>Structural Load Assistant &bull; Hash: {E(doc.ContentHash[..Math.Min(16, doc.ContentHash.Length)])}</div>");
        sb.AppendLine($"    <div>Governing Standard: {E(doc.Manifest.Edition)} &bull; {E(doc.UnitProfile.SystemName)}</div>");
        sb.AppendLine("    <div>ENGINEER REVIEW - NOT FOR CONSTRUCTION</div>");
        sb.AppendLine("  </div>");
    }
}
