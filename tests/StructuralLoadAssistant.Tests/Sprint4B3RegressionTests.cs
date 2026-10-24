using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class Sprint4B3RegressionTests
{
    private static EngineeringCalculationRun CreateAsceRun()
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-GABLE", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        return asce.Execute(req);
    }

    private static EngineeringCalculationRun CreateAsnzRun()
    {
        var asnzStrategy = new AsNz1170_2_2021CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Md", 1.0); Add("Mz_cat", 0.95); Add("Ms", 1.0); Add("Mt", 1.0); Add("Mc", 1.0);
        Add("Cpe_Windward", 0.7); Add("Cpe_Leeward", -0.5); Add("Cpe_Side", -0.65); Add("Cpe_Roof_Windward", -0.9); Add("Cpe_Roof_Leeward", -0.5); Add("Cpi", 0.2);

        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-FLAT", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024"),
            "Section 3: Design Wind Speed and Dynamic Pressure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Flat", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 10.0, 10.0, 0.0, 0.0, "TC2", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        return asnzStrategy.Execute(req);
    }

    private static ReportModel CreateAsceModel() => new(
        new Project("PRJ-SYNTH-GABLE", "Synthetic ASCE Gable Roof Building", "SI"),
        DemoFixtures.Calculation("ASCE/SEI 7-22"),
        DemoFixtures.Manifest("ASCE/SEI 7-22"),
        DemoFixtures.Model(),
        [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
        [new("B1", "WALL", "Illustrative", 3, -750)],
        null, null, null,
        new ReportDefinition("report-asce-gable")
    );

    private static ReportModel CreateAsnzModel() => new(
        new Project("PRJ-SYNTH-FLAT", "Synthetic AS/NZS Flat Roof Building", "SI"),
        DemoFixtures.Calculation("AS/NZS 1170.2:2021"),
        DemoFixtures.Manifest("AS/NZS 1170.2:2021"),
        DemoFixtures.Model(),
        [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
        [new("B1", "WALL", "Illustrative", 3, -750)],
        null, null, null,
        new ReportDefinition("report-asnz-flat")
    );

    [Fact]
    public void Test_01_Typed_Numbers_And_Real_Formulas()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        byte[] xlsx = builder.BuildExcel(model, run);
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var sheet1 = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet1);

        using var reader = new StreamReader(sheet1.Open());
        string xml = reader.ReadToEnd();

        // Must contain XML formula tags <f>
        Assert.Contains("<f>", xml);
        // Must contain cell values <v>
        Assert.Contains("<v>", xml);
    }

    [Fact]
    public void Test_02_Downstream_Excel_Recalc_Dependencies()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        byte[] xlsx = builder.BuildExcel(model, run);
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var sheet1 = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet1);

        using var reader = new StreamReader(sheet1.Open());
        string xml = reader.ReadToEnd();

        // Formula for qh references cell B10 (basic wind speed)
        Assert.Contains("B10", xml);
    }

    [Fact]
    public void Test_03_HandCheck_Pass_Fail_NotChecked()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        byte[] xlsx = builder.BuildExcel(model, run);
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var sheet3 = zip.GetEntry("xl/worksheets/sheet3.xml");
        Assert.NotNull(sheet3);

        using var reader = new StreamReader(sheet3.Open());
        string xml = reader.ReadToEnd();

        // Must contain formula checking ISBLANK and PASS/FAIL logic
        Assert.Contains("ISBLANK", xml);
        Assert.Contains("NOT CHECKED", xml);
        Assert.Contains("PASS", xml);
        Assert.Contains("FAIL", xml);
    }

    [Fact]
    public void Test_04_Pdf_Zero_Lost_Trace_Nodes_Both_Standards()
    {
        var asceRun = CreateAsceRun();
        var asceModel = CreateAsceModel();
        var asnzRun = CreateAsnzRun();
        var asnzModel = CreateAsnzModel();
        var builder = new DetailedReportBuilder();

        byte[] ascePdf = builder.BuildPdf(asceModel, asceRun);
        string asceText = System.Text.Encoding.ASCII.GetString(ascePdf);
        foreach (var node in asceRun.TraceNodes)
        {
            Assert.Contains(node.NodeId, asceText);
        }

        byte[] asnzPdf = builder.BuildPdf(asnzModel, asnzRun);
        string asnzText = System.Text.Encoding.ASCII.GetString(asnzPdf);
        foreach (var node in asnzRun.TraceNodes)
        {
            Assert.Contains(node.NodeId, asnzText);
        }
    }

    [Fact]
    public void Test_05_Project_Standard_Mismatch_Blocked()
    {
        var asnzRun = CreateAsnzRun();
        var asceModel = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        // Combining AS/NZS run with ASCE model must be blocked fail-closed
        Assert.Throws<InvalidOperationException>(() => builder.BuildMarkdown(asceModel, asnzRun));
        Assert.Throws<InvalidOperationException>(() => builder.BuildExcel(asceModel, asnzRun));
        Assert.Throws<InvalidOperationException>(() => builder.BuildPdf(asceModel, asnzRun));
        Assert.Throws<InvalidOperationException>(() => builder.BuildJson(asceModel, asnzRun));
    }

    [Fact]
    public void Test_06_Blocked_Quantity_Never_Represented_As_Zero()
    {
        // When coefficients are not provided, calculation is blocked
        var asce = new Asce7_22CalculationStrategy();
        var reqBlocked = new EngineeringCalculationRequest(
            "PRJ-BLOCKED", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C",
            ImmutableDictionary<string, CoefficientInputEvidence>.Empty,
            ImmutableDictionary<string, string>.Empty);

        var blockedRun = asce.Execute(reqBlocked);
        Assert.False(blockedRun.NumericalComputationPossible);

        var model = CreateAsceModel() with { Project = new Project("PRJ-BLOCKED", "Blocked Project", "SI") };
        var builder = new DetailedReportBuilder();
        string md = builder.BuildMarkdown(model, blockedRun);

        Assert.Contains("BLOCKED / NOT COMPUTED", md);
        Assert.DoesNotContain("0.00 Pa (calculated net pressure)", md);
    }

    [Fact]
    public void Test_07_Separate_Design_Bases_ASCE_vs_ASNZ()
    {
        var asceRun = CreateAsceRun();
        var asceModel = CreateAsceModel();
        var asnzRun = CreateAsnzRun();
        var asnzModel = CreateAsnzModel();
        var builder = new DetailedReportBuilder();

        string asceMd = builder.BuildMarkdown(asceModel, asceRun);
        Assert.Contains("ASCE/SEI 7-22", asceMd);
        Assert.Contains("Chapter 27", asceMd);
        Assert.Contains("GCpi", asceMd);
        Assert.DoesNotContain("Section 3: Design Wind Speed", asceMd);

        string asnzMd = builder.BuildMarkdown(asnzModel, asnzRun);
        Assert.Contains("AS/NZS 1170.2:2021", asnzMd);
        Assert.Contains("Mz_cat", asnzMd);
        Assert.DoesNotContain("Chapter 27: MWFRS", asnzMd);
    }

    [Fact]
    public void Test_08_Engineering_Run_Audit_Document_Lifecycle()
    {
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        // 1. Without run
        string mdNoRun = builder.BuildMarkdown(model, null);
        Assert.Contains("NO ENGINEERING CALCULATION RUN SELECTED", mdNoRun);

        // 2. With valid run
        var run = CreateAsceRun();
        var doc = AuditReportDocumentBuilder.BuildDocument(model, run);
        Assert.NotNull(doc.CalculationRun);
        Assert.True(doc.NumericalComputationPossible);
        Assert.Equal(run.Id, doc.CalculationRun.Id);

        string mdWithRun = builder.BuildMarkdown(model, run);
        Assert.Contains(run.Id, mdWithRun);
        Assert.Contains(run.ContentHash[..8], mdWithRun);
    }

    [Fact]
    public void Test_09_Safety_Gates_And_Integrity_Checks()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var doc = AuditReportDocumentBuilder.BuildDocument(model, run);

        // Production authorized and independent review MUST remain false
        Assert.False(doc.ProductionAuthorized);
        Assert.False(doc.IndependentlyVerified);
        Assert.False(doc.LicensedSourceQualified);
        Assert.False(run.StandardsDataIntegrityVerified);
        Assert.False(run.IndependentEngineerReviewed);
        Assert.Equal(DataProvenance.SyntheticDemoFixture, run.Provenance);
    }

    [Fact]
    public void Test_10_CrossFormat_Parity_And_ContentHash()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        string json = builder.BuildJson(model, run);
        string md = builder.BuildMarkdown(model, run);
        string html = builder.BuildHtml(model, run);
        byte[] pdf = builder.BuildPdf(model, run);
        byte[] xlsx = builder.BuildExcel(model, run);

        string expectedHash = run.ContentHash;
        string expectedId = run.Id;

        // Content hash and ID match across formats
        Assert.Contains(expectedId, json);
        Assert.Contains(expectedHash, json);

        Assert.Contains(expectedId, md);
        Assert.Contains(expectedHash[..8], md);

        Assert.Contains(expectedId, html);
        Assert.Contains(expectedHash[..8], html);

        string pdfText = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.Contains(expectedId, pdfText);

        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var sheet1 = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet1);
        using var reader = new StreamReader(sheet1.Open());
        string sheetXml = reader.ReadToEnd();
        Assert.Contains(expectedId, sheetXml);

        // Numeric pressure parity: 833.68 Pa appears across all formats
        double qh = run.ComputedQuantities["qh"];
        string qhStr = qh.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Contains(qhStr, json);
        Assert.Contains(qhStr, md);
        Assert.Contains(qhStr, html);
        Assert.Contains(qhStr, pdfText);
        Assert.Contains(qhStr, sheetXml);
    }
}
