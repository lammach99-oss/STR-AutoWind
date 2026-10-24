using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Standalone OpenXML Spreadsheet (.xlsx) generator using pure .NET System.IO.Compression.
/// Creates native Excel calculation workbooks with formulas, cell formatting, freeze panes, and print setups.
/// No third-party or locked dependencies required.
/// </summary>
public sealed class ExcelReportBuilder
{
    public byte[] Build(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            AddEntry(archive, "[Content_Types].xml", ContentTypesXml());
            AddEntry(archive, "_rels/.rels", RootRelsXml());
            AddEntry(archive, "xl/workbook.xml", WorkbookXml(model, calcRun));
            AddEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(model, calcRun));
            AddEntry(archive, "xl/styles.xml", StylesXml());

            // Sheet 1: Wind Load Calculation Worksheet (Workbook style)
            AddEntry(archive, "xl/worksheets/sheet1.xml", CalculationSheetXml(model, calcRun));
            
            // Sheet 2: Formula Ledger & Trace
            AddEntry(archive, "xl/worksheets/sheet2.xml", TraceSheetXml(calcRun));

            // Sheet 3: Manual Verification
            AddEntry(archive, "xl/worksheets/sheet3.xml", VerificationSheetXml(calcRun));
        }

        return ms.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static string ContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\r\n" +
        "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\r\n" +
        "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\r\n" +
        "  <Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>\r\n" +
        "  <Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>\r\n" +
        "  <Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
        "  <Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
        "  <Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
        "</Types>";

    private static string RootRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
        "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>\r\n" +
        "</Relationships>";

    private static string WorkbookRelsXml(ReportModel model, EngineeringCalculationRun? calcRun) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\r\n" +
        "  <Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\r\n" +
        "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>\r\n" +
        "  <Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>\r\n" +
        "  <Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/>\r\n" +
        "</Relationships>";

    private static string WorkbookXml(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        string sheet1Name = calcRun != null && calcRun.Request.StandardEdition.Contains("AS/NZS")
            ? (calcRun.Request.RoofForm.Contains("Flat", StringComparison.OrdinalIgnoreCase) ? "Flat roof" : "Slope Roof")
            : "Windload";

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
               "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\r\n" +
               "  <sheets>\r\n" +
               $"    <sheet name=\"{Escape(sheet1Name)}\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
               "    <sheet name=\"Formula Trace\" sheetId=\"2\" r:id=\"rId2\"/>\r\n" +
               "    <sheet name=\"Manual Verification\" sheetId=\"3\" r:id=\"rId3\"/>\r\n" +
               "  </sheets>\r\n" +
               "</workbook>";
    }

    private static string StylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n" +
        "  <fonts count=\"4\">\r\n" +
        "    <font><sz val=\"10\"/><name val=\"Calibri\"/></font>\r\n" +
        "    <font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>\r\n" +
        "    <font><b/><sz val=\"12\"/><name val=\"Calibri\"/><color rgb=\"FF002060\"/></font>\r\n" +
        "    <font><sz val=\"10\"/><name val=\"Calibri\"/><color rgb=\"FF0070C0\"/></font>\r\n" + // Blue for user input
        "  </fonts>\r\n" +
        "  <fills count=\"3\">\r\n" +
        "    <fill><patternFill patternType=\"none\"/></fill>\r\n" +
        "    <fill><patternFill patternType=\"gray125\"/></fill>\r\n" +
        "    <fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCE6F1\"/></patternFill></fill>\r\n" +
        "  </fills>\r\n" +
        "  <borders count=\"2\">\r\n" +
        "    <border><left/><right/><top/><bottom/><diagonal/></border>\r\n" +
        "    <border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/></border>\r\n" +
        "  </borders>\r\n" +
        "  <cellStyleXfs count=\"1\">\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>\r\n" +
        "  </cellStyleXfs>\r\n" +
        "  <cellXfs count=\"5\">\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\r\n" + // 1: Bold
        "    <xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\"/>\r\n" + // 2: Section Header
        "    <xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\"/>\r\n" + // 3: Blue User Input
        "    <xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\"/>\r\n" + // 4: Number 2 decimal
        "  </cellXfs>\r\n" +
        "</styleSheet>";

    private static string CalculationSheetXml(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
        sb.Append("  <sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews>\r\n");
        sb.Append("  <sheetFormatPr defaultRowHeight=\"15\"/>\r\n");
        sb.Append("  <cols>\r\n");
        sb.Append("    <col min=\"1\" max=\"1\" width=\"35\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"20\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"15\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"12\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"30\"/>\r\n");
        sb.Append("  </cols>\r\n");
        sb.Append("  <sheetData>\r\n");

        int r = 1;
        void Row(string c1, string c2 = "", string c3 = "", string c4 = "", string c5 = "", int s = 0)
        {
            sb.Append($"    <row r=\"{r}\">\r\n");
            if (!string.IsNullOrEmpty(c1)) sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c1)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c2)) sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c2)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c3)) sb.Append($"      <c r=\"C{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c3)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c4)) sb.Append($"      <c r=\"D{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c4)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c5)) sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c5)}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
        }

        string qual = calcRun?.QualificationStatus ?? "NO ENGINEERING RUN SELECTED";
        Row("STRUCTURAL WIND LOAD CALCULATION REPORT", "", "", "", "", 2);
        Row("Status / Provenance:", qual, "", "", "", 1);
        Row("Project:", model.Project.Name, "ID:", model.Project.Id, "", 0);
        Row("Standard / Procedure:", calcRun != null ? $"{calcRun.Request.StandardEdition} - {calcRun.Request.ProcedureName}" : model.Manifest.Edition, "", "", "", 0);
        Row("Calculation Run ID:", calcRun?.Id ?? "N/A", "Run Hash:", calcRun?.ContentHash ?? "N/A", "", 0);
        Row("", "", "", "", "");

        if (calcRun == null)
        {
            Row("NO ENGINEERING CALCULATION RUN SELECTED", "Configure review inputs and calculate before generating worksheet.", "", "", "", 1);
            sb.Append("  </sheetData>\r\n</worksheet>");
            return sb.ToString();
        }

        Row("A. BASIC PARAMETERS & FACTORS", "", "", "", "", 2);
        Row("Description", "Symbol / Variable", "Value", "Unit", "Standard Reference / Clause", 1);
        Row("Basic Wind Speed", "V / VR", calcRun.Request.BasicWindSpeed.ToString("F2"), "m/s", "Design Wind Speed", 3);
        Row("Mean Roof Height", "h", calcRun.Request.ReferenceHeightM.ToString("F2"), "m", "Building Geometry", 3);
        Row("Building Dimensions", "L x B", $"{calcRun.Request.BuildingLengthM:F1} x {calcRun.Request.BuildingWidthM:F1}", "m", "Footprint", 3);
        Row("Roof Slope / Pitch", "theta", calcRun.Request.RoofSlopeDegrees.ToString("F1"), "deg", "Roof Geometry", 3);

        foreach (var kvp in calcRun.Request.ExplicitCoefficients)
        {
            var c = kvp.Value;
            Row(c.CoefficientName, c.CoefficientName, c.Value.ToString("F3"), c.Unit, $"{c.Document} {c.ClauseOrTable}", 3);
        }

        Row("", "", "", "", "");
        Row("B. VELOCITY / DYNAMIC PRESSURE", "", "", "", "", 2); double qVal = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) ? qh : (calcRun.ComputedQuantities.TryGetValue("q_des", out var qdes) ? qdes : 0.0); Row("Velocity Pressure (qh / q_des)", "qh", qVal.ToString("F2"), "Pa", "Evaluated dynamic pressure", 1); Row("", "", "", "", ""); Row("C. DESIGN WIND PRESSURE SUMMARY", "", "", "", "", 2);
        Row("Surface / Zone", "External Coeff", "Net Pressure (Pa)", "Internal Pressure (Pa)", "Global Force Vector (Pa)", 1);

        foreach (var s in calcRun.SurfaceResults)
        {
            Row(s.Description, s.ExternalCoefficient.ToString("F2"), s.NetPressurePa.ToString("F2"), s.InternalPressurePa.ToString("F2"),
                $"({s.GlobalPressureVectorPa.X:F1}, {s.GlobalPressureVectorPa.Y:F1}, {s.GlobalPressureVectorPa.Z:F1})", 0);
        }

        sb.Append("  </sheetData>\r\n</worksheet>");
        return sb.ToString();
    }

    private static string TraceSheetXml(EngineeringCalculationRun? calcRun)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
        sb.Append("  <sheetFormatPr defaultRowHeight=\"15\"/>\r\n");
        sb.Append("  <cols>\r\n");
        sb.Append("    <col min=\"1\" max=\"1\" width=\"15\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"20\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"25\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"45\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"20\"/>\r\n");
        sb.Append("    <col min=\"6\" max=\"6\" width=\"25\"/>\r\n");
        sb.Append("  </cols>\r\n");
        sb.Append("  <sheetData>\r\n");

        int r = 1;
        void Row(string c1, string c2 = "", string c3 = "", string c4 = "", string c5 = "", string c6 = "", int s = 0)
        {
            sb.Append($"    <row r=\"{r}\">\r\n");
            if (!string.IsNullOrEmpty(c1)) sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c1)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c2)) sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c2)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c3)) sb.Append($"      <c r=\"C{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c3)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c4)) sb.Append($"      <c r=\"D{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c4)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c5)) sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c5)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c6)) sb.Append($"      <c r=\"F{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c6)}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
        }

        Row("FORMULA AUDIT LEDGER & STEP-BY-STEP NUMERICAL TRACE", "", "", "", "", "", 2);
        Row("Node ID", "Stage", "Symbolic Formula", "Substituted Expression", "Computed Result", "Source Reference", 1);

        if (calcRun != null)
        {
            foreach (var node in calcRun.TraceNodes)
            {
                Row(node.NodeId, node.Stage, node.SymbolicEquation, node.SubstitutedEquation, node.ResultWithUnit, node.SourceReference, 0);
            }
        }
        else
        {
            Row("N/A", "N/A", "No engineering run loaded", "N/A", "N/A", "N/A", 0);
        }

        sb.Append("  </sheetData>\r\n</worksheet>");
        return sb.ToString();
    }

    private static string VerificationSheetXml(EngineeringCalculationRun? calcRun)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
        sb.Append("  <sheetFormatPr defaultRowHeight=\"15\"/>\r\n");
        sb.Append("  <cols>\r\n");
        sb.Append("    <col min=\"1\" max=\"1\" width=\"30\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"20\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"20\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"15\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"15\"/>\r\n");
        sb.Append("    <col min=\"6\" max=\"6\" width=\"20\"/>\r\n");
        sb.Append("  </cols>\r\n");
        sb.Append("  <sheetData>\r\n");

        int r = 1;
        void Row(string c1, string c2 = "", string c3 = "", string c4 = "", string c5 = "", string c6 = "", int s = 0)
        {
            sb.Append($"    <row r=\"{r}\">\r\n");
            if (!string.IsNullOrEmpty(c1)) sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c1)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c2)) sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c2)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c3)) sb.Append($"      <c r=\"C{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c3)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c4)) sb.Append($"      <c r=\"D{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c4)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c5)) sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c5)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c6)) sb.Append($"      <c r=\"F{r}\" t=\"inlineStr\" s=\"{s}\"><is><t>{Escape(c6)}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
        }

        Row("MANUAL VERIFICATION & INDEPENDENT CHECK REGISTER", "", "", "", "", "", 2);
        Row("Verified Quantity", "Software Result", "Hand-Check Value", "Tolerance", "Difference", "Status", 1);

        if (calcRun != null)
        {
            double qVal = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) ? qh :
                (calcRun.ComputedQuantities.TryGetValue("q_des", out var qdes) ? qdes : 0.0);
            Row("Velocity / Dynamic Pressure", $"{qVal:F2} Pa", "", "1.0%", "", "NOT CHECKED", 0);

            foreach (var s in calcRun.SurfaceResults)
            {
                Row($"{s.Description} p_net", $"{s.NetPressurePa:F2} Pa", "", "1.0%", "", "NOT CHECKED", 0);
            }
        }
        else
        {
            Row("No engineering calculation loaded for verification", "", "", "", "", "NOT CHECKED", 0);
        }

        sb.Append("  </sheetData>\r\n</worksheet>");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

