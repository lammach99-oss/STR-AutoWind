using System.Collections.Immutable;
using System.Text;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class DetailedReportBuilderTests
{
    private static EngineeringCalculationRun CreateSyntheticRun()
    {
        var strategy = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-ASCE", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        return strategy.Execute(req);
    }

    [Fact]
    public void DetailedReportBuilder_Generates_Valid_Markdown_With_Watermark_And_Traces()
    {
        var run = CreateSyntheticRun();
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        string md = builder.BuildMarkdown(model, run);

        Assert.Contains("# STRUCTURAL WIND LOAD AUDIT REPORT", md);
        Assert.Contains("SYNTHETIC EXAMPLE - NOT FOR DESIGN", md);
        Assert.Contains("## 1. DESIGN BASIS & GOVERNING STANDARDS", md);
        Assert.Contains("## 2. INPUT REGISTER & COEFFICIENT PROVENANCE", md);
        Assert.Contains("## 3. FORMULA LEDGER & STEP-BY-STEP TRACE", md);
        Assert.Contains("## 4. PER-STANDARD SURFACE PRESSURE WORKSHEET", md);
        Assert.Contains("## 7. MANUAL VERIFICATION WORKSHEET", md);
        Assert.Contains("## APPENDIX A: VALIDATION & BLOCKED PROCEDURES", md);
        Assert.Contains("## APPENDIX B: IMMUTABLE AUDIT TRAIL", md);
        Assert.Contains("833.68", md);
    }

    [Fact]
    public void DetailedReportBuilder_Generates_Valid_Pdf_Bytes()
    {
        var run = CreateSyntheticRun();
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        byte[] pdfBytes = builder.BuildPdf(model, run);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100);
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 8);
        Assert.StartsWith("%PDF-1.4", header);
        string fullPdf = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("%%EOF", fullPdf);
        Assert.Contains("SYNTHETIC EXAMPLE - NOT FOR DESIGN", fullPdf);
    }

    [Fact]
    public void DetailedReportBuilder_Generates_Valid_Html_With_Escaped_Entities()
    {
        var run = CreateSyntheticRun();
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        string html = builder.BuildHtml(model, run);

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<title>Demo &lt;script&gt; - Wind Calculation Audit</title>", html);
        Assert.Contains("class='watermark'>SYNTHETIC EXAMPLE - NOT FOR DESIGN</div>", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void DetailedReportBuilder_Generates_Valid_Deterministic_Json()
    {
        var run = CreateSyntheticRun();
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        string json = builder.BuildJson(model, run);

        Assert.Contains("ReportModel", json);
        Assert.Contains("CalculationRun", json);
        Assert.Contains("Options", json);
        Assert.Contains("SYNTHETIC EXAMPLE - NOT FOR DESIGN", json);
    }

    [Fact]
    public void DetailedReportBuilder_Generates_Valid_Excel_OpenXml_Bytes()
    {
        var run = CreateSyntheticRun();
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        byte[] xlsxBytes = builder.BuildExcel(model, run);

        Assert.NotNull(xlsxBytes);
        Assert.True(xlsxBytes.Length > 200);

        // Verify it is a valid Zip Archive with required OpenXML parts
        using var ms = new MemoryStream(xlsxBytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet2.xml"));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet3.xml"));

        // Verify calculation sheet content has project and watermark
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        string content = reader.ReadToEnd();
        Assert.Contains("STRUCTURAL WIND LOAD CALCULATION REPORT", content);
        Assert.Contains("833.68", content);
    }
}
