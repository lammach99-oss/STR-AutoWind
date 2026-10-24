using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
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
        ArgumentNullException.ThrowIfNull(model);
        if (calcRun != null)
        {
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

            // Sheet 4: Load Case Matrix
            AddEntry(archive, "xl/worksheets/sheet4.xml", CaseMatrixSheetXml(calcRun));
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
        "  <Override PartName=\"/xl/worksheets/sheet4.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>\r\n" +
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
        "  <Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet4.xml\"/>\r\n" +
        "</Relationships>";

    private static string WorkbookXml(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        string sheet1Name = calcRun != null && calcRun.Request.StandardEdition.Contains("AS/NZS")
            ? (calcRun.Request.RoofForm.Contains("Flat", StringComparison.OrdinalIgnoreCase) ? "Flat roof" : "Slope Roof")
            : "Windload";

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
               "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">\r\n" +
               "  <calcPr calcId=\"124519\" fullCalcOnLoad=\"1\"/>\r\n" +
               "  <sheets>\r\n" +
               $"    <sheet name=\"{Escape(sheet1Name)}\" sheetId=\"1\" r:id=\"rId1\"/>\r\n" +
               "    <sheet name=\"Formula Trace\" sheetId=\"2\" r:id=\"rId2\"/>\r\n" +
               "    <sheet name=\"Manual Verification\" sheetId=\"3\" r:id=\"rId3\"/>\r\n" +
               "    <sheet name=\"Load Case Matrix\" sheetId=\"4\" r:id=\"rId4\"/>\r\n" +
               "  </sheets>\r\n" +
               "</workbook>";
    }

    private static string StylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n" +
        "  <numFmts count=\"1\">\r\n" +
        "    <numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/>\r\n" +
        "  </numFmts>\r\n" +
        "  <fonts count=\"5\">\r\n" +
        "    <font><sz val=\"10\"/><name val=\"Calibri\"/></font>\r\n" +
        "    <font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>\r\n" +
        "    <font><b/><sz val=\"11\"/><name val=\"Calibri\"/><color rgb=\"FF002060\"/></font>\r\n" +
        "    <font><sz val=\"10\"/><name val=\"Calibri\"/><color rgb=\"FF0070C0\"/></font>\r\n" +
        "    <font><b/><sz val=\"10\"/><name val=\"Calibri\"/><color rgb=\"FFC00000\"/></font>\r\n" +
        "  </fonts>\r\n" +
        "  <fills count=\"4\">\r\n" +
        "    <fill><patternFill patternType=\"none\"/></fill>\r\n" +
        "    <fill><patternFill patternType=\"gray125\"/></fill>\r\n" +
        "    <fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCE6F1\"/></patternFill></fill>\r\n" +
        "    <fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFCE4D6\"/></patternFill></fill>\r\n" +
        "  </fills>\r\n" +
        "  <borders count=\"2\">\r\n" +
        "    <border><left/><right/><top/><bottom/><diagonal/></border>\r\n" +
        "    <border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/></border>\r\n" +
        "  </borders>\r\n" +
        "  <cellStyleXfs count=\"1\">\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>\r\n" +
        "  </cellStyleXfs>\r\n" +
        "  <cellXfs count=\"7\">\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>\r\n" +
        "    <xf numFmtId=\"164\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>\r\n" +
        "    <xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>\r\n" +
        "    <xf numFmtId=\"164\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>\r\n" +
        "    <xf numFmtId=\"0\" fontId=\"4\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>\r\n" +
        "  </cellXfs>\r\n" +
        "</styleSheet>";

    private static string CalculationSheetXml(ReportModel model, EngineeringCalculationRun? calcRun)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
        sb.Append("  <sheetViews>\r\n");
        sb.Append("    <sheetView tabSelected=\"1\" workbookViewId=\"0\">\r\n");
        sb.Append("      <pane ySplit=\"6\" topLeftCell=\"A7\" activePane=\"bottomLeft\" state=\"frozen\"/>\r\n");
        sb.Append("    </sheetView>\r\n");
        sb.Append("  </sheetViews>\r\n");
        sb.Append("  <sheetFormatPr defaultRowHeight=\"15\"/>\r\n");
        sb.Append("  <cols>\r\n");
        sb.Append("    <col min=\"1\" max=\"1\" width=\"36\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"22\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"14\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"38\" customWidth=\"1\"/>\r\n");
        sb.Append("  </cols>\r\n");
        sb.Append("  <sheetData>\r\n");

        int r = 1;
        void RowStr(string c1, string c2 = "", string c3 = "", string c4 = "", string c5 = "", int s = 0)
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

        void RowNum(string c1, string c2, double val, string c4, string c5, int s = 3)
        {
            string valStr = val.ToString("G17", CultureInfo.InvariantCulture);
            sb.Append($"    <row r=\"{r}\">\r\n");
            if (!string.IsNullOrEmpty(c1)) sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\"><is><t>{Escape(c1)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c2)) sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\"><is><t>{Escape(c2)}</t></is></c>\r\n");
            sb.Append($"      <c r=\"C{r}\" s=\"{s}\"><v>{valStr}</v></c>\r\n");
            if (!string.IsNullOrEmpty(c4)) sb.Append($"      <c r=\"D{r}\" t=\"inlineStr\"><is><t>{Escape(c4)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c5)) sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\"><is><t>{Escape(c5)}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
        }

        void RowFormula(string c1, string c2, string formula, double computedVal, string c4, string c5, int s = 5)
        {
            string valStr = computedVal.ToString("F2", CultureInfo.InvariantCulture);
            sb.Append($"    <row r=\"{r}\">\r\n");
            if (!string.IsNullOrEmpty(c1)) sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\"><is><t>{Escape(c1)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c2)) sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\"><is><t>{Escape(c2)}</t></is></c>\r\n");
            sb.Append($"      <c r=\"C{r}\" s=\"{s}\"><f>{EscapeFormula(formula)}</f><v>{valStr}</v></c>\r\n");
            if (!string.IsNullOrEmpty(c4)) sb.Append($"      <c r=\"D{r}\" t=\"inlineStr\"><is><t>{Escape(c4)}</t></is></c>\r\n");
            if (!string.IsNullOrEmpty(c5)) sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\"><is><t>{Escape(c5)}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
        }

        string qual = calcRun?.QualificationStatus ?? "NO ENGINEERING RUN SELECTED";
        RowStr("STRUCTURAL WIND LOAD CALCULATION REPORT", "", "", "", "", 2);
        RowStr("Status / Provenance:", qual, "", "", "", 1);
        RowStr("Project:", model.Project.Name, "ID:", model.Project.Id, "", 0);
        RowStr("Standard / Procedure:", calcRun != null ? $"{calcRun.Request.StandardEdition} - {calcRun.Request.ProcedureName}" : model.Manifest.Edition, "", "", "", 0);
        RowStr("Calculation Run ID:", calcRun?.Id ?? "N/A", "Run Hash:", calcRun?.ContentHash ?? "N/A", "", 0);
        RowStr("", "", "", "", "");

        if (calcRun == null)
        {
            RowStr("NO ENGINEERING CALCULATION RUN SELECTED", "Configure review inputs and calculate before generating worksheet.", "", "", "", 1);
            sb.Append("  </sheetData>\r\n</worksheet>");
            return sb.ToString();
        }

        RowStr("A. BASIC PARAMETERS & FACTORS", "", "", "", "", 2);
        RowStr("Description", "Symbol / Variable", "Value", "Unit", "Standard Reference / Clause", 1);

        int rowSpeed = r;
        RowNum("Basic Wind Speed", "V / VR", calcRun.Request.BasicWindSpeed, "m/s", "Design Wind Speed", 3);
        int rowHeight = r;
        RowNum("Mean Roof Height", "h", calcRun.Request.ReferenceHeightM, "m", "Building Geometry", 3);
        int rowSlope = r;
        RowNum("Roof Slope / Pitch", "theta", calcRun.Request.RoofSlopeDegrees, "deg", "Roof Geometry", 3);

        var coeffRowMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in calcRun.Request.ExplicitCoefficients)
        {
            var c = kvp.Value;
            coeffRowMap[c.CoefficientName] = r;
            RowNum(c.CoefficientName, c.CoefficientName, c.Value, c.Unit, $"{c.Document} {c.ClauseOrTable}", 3);
        }

        RowStr("", "", "", "", "");
        RowStr("B. VELOCITY / DYNAMIC PRESSURE", "", "", "", "", 2);

        int rowQ = r;
        bool hasQ = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) || calcRun.ComputedQuantities.TryGetValue("q_des", out qh);

        if (!hasQ || !calcRun.NumericalComputationPossible)
        {
            RowStr("Velocity / Dynamic Pressure", "q", "BLOCKED / NOT COMPUTED", "N/A", calcRun.Issues.FirstOrDefault()?.Message ?? "Blocked by missing normative data", 6);
        }
        else if (calcRun.Request.StandardEdition.Contains("ASCE", StringComparison.OrdinalIgnoreCase) && coeffRowMap.TryGetValue("Kd", out int rKd) && coeffRowMap.TryGetValue("Kh", out int rKh))
        {
            string keTerm = coeffRowMap.TryGetValue("Ke", out int rKe) ? $"*C{rKe}" : "";
            string kztTerm = coeffRowMap.TryGetValue("Kzt", out int rKzt) ? $"*C{rKzt}" : "";
            string qFormula = $"0.613{keTerm}*C{rKh}{kztTerm}*C{rKd}*(C{rowSpeed}^2)";
            RowFormula("Velocity Pressure at Height h", "qh", qFormula, qh, "Pa", "ASCE 7-22 Section 26.10.1 (Eq. 26.10-1)", 5);
        }
        else if (calcRun.Request.StandardEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) && coeffRowMap.TryGetValue("Md", out int rMd) && coeffRowMap.TryGetValue("Mz_cat", out int rMz))
        {
            string msTerm = coeffRowMap.TryGetValue("Ms", out int rMs) ? $"*C{rMs}" : "";
            string mtTerm = coeffRowMap.TryGetValue("Mt", out int rMt) ? $"*C{rMt}" : "";
            string mcTerm = coeffRowMap.TryGetValue("Mc", out int rMc) ? $"*C{rMc}" : "";
            string qFormula = $"0.6*(C{rowSpeed}*C{rMd}*C{rMz}{msTerm}{mtTerm}{mcTerm})^2";
            RowFormula("Design Dynamic Pressure", "q_des", qFormula, qh, "Pa", "AS/NZS 1170.2:2021 Section 2.4", 5);
        }
        else
        {
            // Coefficient rows are insufficient to reconstruct a cell-referencing
            // formula, so emit the evaluated value as a typed number. A constant
            // inside an <f> element would falsely present it as a live formula.
            RowNum("Velocity / Dynamic Pressure", "q", qh, "Pa", "Evaluated Dynamic Pressure", 4);
        }

        RowStr("", "", "", "", "");
        RowStr("C. DESIGN WIND PRESSURE SUMMARY", "", "", "", "", 2);
        RowStr("Surface / Zone", "External Coeff", "Net Pressure (Pa)", "Internal Pressure (Pa)", "Global Force Vector (Pa)", 1);

        int rowGust = coeffRowMap.TryGetValue("G", out int rg) ? rg : 0;

        foreach (var s in calcRun.SurfaceResults)
        {
            string ecStr = s.ExternalCoefficient.ToString("G17", CultureInfo.InvariantCulture);
            string npStr = s.NetPressurePa.ToString("F2", CultureInfo.InvariantCulture);
            string ipStr = s.InternalPressurePa.ToString("G17", CultureInfo.InvariantCulture);
            string vecStr = string.Format(CultureInfo.InvariantCulture, "({0:F1}, {1:F1}, {2:F1})", s.GlobalPressureVectorPa.X, s.GlobalPressureVectorPa.Y, s.GlobalPressureVectorPa.Z);

            sb.Append($"    <row r=\"{r}\">\r\n");
            sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\"><is><t>{Escape(s.Description)}</t></is></c>\r\n");
            sb.Append($"      <c r=\"B{r}\" s=\"4\"><v>{ecStr}</v></c>\r\n");

            if (hasQ && calcRun.NumericalComputationPossible)
            {
                string netFormula = rowGust > 0
                    ? $"C{rowQ}*C{rowGust}*B{r}-D{r}"
                    : $"C{rowQ}*B{r}-D{r}";

                sb.Append($"      <c r=\"C{r}\" s=\"5\"><f>{EscapeFormula(netFormula)}</f><v>{npStr}</v></c>\r\n");
            }
            else
            {
                sb.Append($"      <c r=\"C{r}\" t=\"inlineStr\" s=\"6\"><is><t>BLOCKED</t></is></c>\r\n");
            }

            sb.Append($"      <c r=\"D{r}\" s=\"4\"><v>{ipStr}</v></c>\r\n");
            sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\"><is><t>{vecStr}</t></is></c>\r\n");
            sb.Append("    </row>\r\n");
            r++;
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
        sb.Append("    <col min=\"1\" max=\"1\" width=\"15\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"20\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"25\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"45\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"20\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"6\" max=\"6\" width=\"30\" customWidth=\"1\"/>\r\n");
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
        sb.Append("    <col min=\"1\" max=\"1\" width=\"32\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"14\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"6\" max=\"6\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("  </cols>\r\n");
        sb.Append("  <sheetData>\r\n");

        int r = 1;
        void RowHdr(string c1, string c2 = "", string c3 = "", string c4 = "", string c5 = "", string c6 = "", int s = 0)
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

        RowHdr("MANUAL VERIFICATION & INDEPENDENT CHECK REGISTER", "", "", "", "", "", 2);
        RowHdr("Verified Quantity", "Software (Pa)", "Hand-Check (Pa)", "Tolerance", "Difference (Pa)", "Status", 1);

        if (calcRun != null)
        {
            void AddCheckRow(string name, double? softVal, bool blocked = false)
            {
                sb.Append($"    <row r=\"{r}\">\r\n");
                sb.Append($"      <c r=\"A{r}\" t=\"inlineStr\"><is><t>{Escape(name)}</t></is></c>\r\n");
                if (blocked || !softVal.HasValue)
                {
                    sb.Append($"      <c r=\"B{r}\" t=\"inlineStr\" s=\"6\"><is><t>BLOCKED</t></is></c>\r\n");
                    sb.Append($"      <c r=\"C{r}\" s=\"3\"/>\r\n");
                    sb.Append($"      <c r=\"D{r}\" s=\"4\"><v>0.01</v></c>\r\n");
                    sb.Append($"      <c r=\"E{r}\" t=\"inlineStr\" s=\"6\"><is><t>N/A</t></is></c>\r\n");
                    sb.Append($"      <c r=\"F{r}\" t=\"inlineStr\" s=\"6\"><is><t>BLOCKED / NOT COMPUTED</t></is></c>\r\n");
                }
                else
                {
                    string svStr = softVal.Value.ToString("G17", CultureInfo.InvariantCulture);
                    sb.Append($"      <c r=\"B{r}\" s=\"4\"><v>{svStr}</v></c>\r\n");
                    sb.Append($"      <c r=\"C{r}\" s=\"3\"/>\r\n");
                    sb.Append($"      <c r=\"D{r}\" s=\"4\"><v>0.01</v></c>\r\n");
                    string diffFormula = string.Format(CultureInfo.InvariantCulture, "IF(ISBLANK(C{0}),\"\",ABS(B{0}-C{0}))", r);
                    sb.Append($"      <c r=\"E{r}\" s=\"4\"><f>{EscapeFormula(diffFormula)}</f></c>\r\n");
                    string statusFormula = string.Format(CultureInfo.InvariantCulture, "IF(ISBLANK(C{0}),\"NOT CHECKED\",IF(ABS(B{0}-C{0})<=ABS(B{0}*D{0}),\"PASS\",\"FAIL\"))", r);
                    sb.Append($"      <c r=\"F{r}\" t=\"str\"><f>{EscapeFormula(statusFormula)}</f><v>NOT CHECKED</v></c>\r\n");
                }
                sb.Append("    </row>\r\n");
                r++;
            }

            bool hasQ = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) || calcRun.ComputedQuantities.TryGetValue("q_des", out qh);
            AddCheckRow("Velocity / Dynamic Pressure", hasQ ? qh : null, !hasQ || !calcRun.NumericalComputationPossible);

            foreach (var s in calcRun.SurfaceResults)
            {
                AddCheckRow($"{s.Description} p_net", s.NetPressurePa, !calcRun.NumericalComputationPossible);
            }

            RowHdr("", "", "", "", "", "", 0);
            RowHdr("REVIEW SIGN-OFF REGISTER", "", "", "", "", "", 2);
            RowHdr("Role", "Designation", "Status", "Reviewer Name", "Date (UTC)", "Hash", 1);
            RowHdr("Owner / Reviewing Engineer", "Self-Review", calcRun.OwnerEngineerReviewed ? "REVIEWED" : "PENDING", Environment.UserName, DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), calcRun.ContentHash[..8], 0);
            RowHdr("Independent Verification Engineer", "Third-Party Audit", "PENDING / REQUIRED", "Unassigned", "Pending Commissioning", "N/A", 0);
        }
        else
        {
            RowHdr("No engineering calculation loaded for verification", "", "", "", "", "NOT CHECKED", 0);
        }

        sb.Append("  </sheetData>\r\n</worksheet>");
        return sb.ToString();
    }

    private static string CaseMatrixSheetXml(EngineeringCalculationRun? calcRun)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">\r\n");
        sb.Append("  <sheetFormatPr defaultRowHeight=\"15\"/>\r\n");
        sb.Append("  <cols>\r\n");
        sb.Append("    <col min=\"1\" max=\"1\" width=\"20\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"4\" max=\"4\" width=\"22\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"5\" max=\"5\" width=\"22\" customWidth=\"1\"/>\r\n");
        sb.Append("    <col min=\"6\" max=\"6\" width=\"30\" customWidth=\"1\"/>\r\n");
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

        Row("LOAD CASE MATRIX & MULTI-DIRECTION STATE SUMMARY", "", "", "", "", "", 2);
        Row("Direction", "Internal Pressure", "Limit State", "Velocity Pressure", "Governing Surface", "Status", 1);

        if (calcRun != null)
        {
            var activeDir = calcRun.Request.WindDirection.ToString();
            var activeIp = calcRun.Request.Options.TryGetValue("InternalPressure", out var ip) ? ip : "IP_POS";
            var activeLs = calcRun.Request.LimitState.ToString();
            bool hasQ = calcRun.ComputedQuantities.TryGetValue("qh", out var qh) || calcRun.ComputedQuantities.TryGetValue("q_des", out qh);
            bool blocked = !calcRun.NumericalComputationPossible || !hasQ;

            string[] directions = ["PositiveX", "NegativeX", "PositiveY", "NegativeY"];
            string[] ips = ["IP_POS", "IP_NEG"];

            foreach (var d in directions)
            {
                foreach (var p in ips)
                {
                    bool isActive = d == activeDir && p == activeIp;
                    if (isActive && blocked)
                    {
                        // A blocked run computed nothing: never present 0.00 Pa
                        // or a calculated/verified status for it.
                        Row(d, p, activeLs, "BLOCKED / NOT COMPUTED", "N/A", "BLOCKED / NOT COMPUTED", 6);
                    }
                    else if (isActive)
                    {
                        var maxSurf = calcRun.SurfaceResults.OrderByDescending(s => Math.Abs(s.NetPressurePa)).FirstOrDefault();
                        // "VERIFIED" is reserved for runs an independent engineer
                        // has actually reviewed; a self-computed run is not verified.
                        string status = calcRun.IndependentEngineerReviewed
                            ? "CALCULATED & VERIFIED"
                            : "CALCULATED - NOT INDEPENDENTLY VERIFIED";
                        Row(d, p, activeLs, $"{qh:F2} Pa", maxSurf != null ? $"{maxSurf.Description} ({maxSurf.NetPressurePa:F2} Pa)" : "Evaluated", status, 0);
                    }
                    else
                    {
                        Row(d, p, "ULS", "N/A", "N/A", "NOT CALCULATED - RUN REQUIRED", 0);
                    }
                }
            }
        }
        else
        {
            Row("N/A", "N/A", "N/A", "N/A", "N/A", "No engineering run loaded", 0);
        }

        sb.Append("  </sheetData>\r\n</worksheet>");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string EscapeFormula(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
