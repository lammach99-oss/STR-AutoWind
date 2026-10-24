using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

/// <summary>
/// Phase 0 report-integrity regressions: a blocked or unverified run must
/// never be presented as calculated, verified, or as a numeric 0.00 Pa;
/// document hashes must be deterministic; PDFs must not silently lose text.
/// </summary>
public class Phase0ReportIntegrityTests
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

    private static EngineeringCalculationRun CreateBlockedAsceRun()
    {
        var asce = new Asce7_22CalculationStrategy();
        var req = new EngineeringCalculationRequest(
            "PRJ-BLOCKED", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C",
            ImmutableDictionary<string, CoefficientInputEvidence>.Empty,
            ImmutableDictionary<string, string>.Empty);
        var run = asce.Execute(req);
        Assert.False(run.NumericalComputationPossible);
        return run;
    }

    private static ReportModel CreateAsceModel(string projectId = "PRJ-SYNTH-GABLE") => new(
        new Project(projectId, "Synthetic ASCE Gable Roof Building", "SI"),
        DemoFixtures.Calculation("ASCE/SEI 7-22"),
        DemoFixtures.Manifest("ASCE/SEI 7-22"),
        DemoFixtures.Model(),
        [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
        [new("B1", "WALL", "Illustrative", 3, -750)],
        null, null, null,
        new ReportDefinition("report-asce-gable")
    );

    private static string ReadEntry(byte[] xlsx, string entryName)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void BlockedRun_Markdown_ManualVerification_IsBlockedNotZero()
    {
        var blockedRun = CreateBlockedAsceRun();
        var model = CreateAsceModel("PRJ-BLOCKED");
        var builder = new DetailedReportBuilder();

        string md = builder.BuildMarkdown(model, blockedRun);

        Assert.Contains("| Velocity / Dynamic Pressure | BLOCKED / NOT COMPUTED |", md);
        Assert.DoesNotContain("0.00 Pa", md);
    }

    [Fact]
    public void BlockedRun_Excel_CaseMatrix_IsBlockedNotVerified()
    {
        var blockedRun = CreateBlockedAsceRun();
        var model = CreateAsceModel("PRJ-BLOCKED");
        var builder = new DetailedReportBuilder();

        string sheet4 = ReadEntry(builder.BuildExcel(model, blockedRun), "xl/worksheets/sheet4.xml");

        Assert.Contains("BLOCKED / NOT COMPUTED", sheet4);
        Assert.DoesNotContain("CALCULATED & VERIFIED", sheet4);
        Assert.DoesNotContain("0.00 Pa", sheet4);
    }

    [Fact]
    public void SuccessfulRun_Excel_CaseMatrix_IsNotClaimedVerifiedWithoutIndependentReview()
    {
        var run = CreateAsceRun();
        Assert.True(run.NumericalComputationPossible);
        Assert.False(run.IndependentEngineerReviewed);
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        string sheet4 = ReadEntry(builder.BuildExcel(model, run), "xl/worksheets/sheet4.xml");

        Assert.Contains("CALCULATED - NOT INDEPENDENTLY VERIFIED", sheet4);
        Assert.DoesNotContain("CALCULATED & VERIFIED", sheet4);
    }

    [Fact]
    public void Excel_Workbook_UsesValidCalcPr_AndNoConstantFormulaFallback()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();
        byte[] xlsx = builder.BuildExcel(model, run);

        string workbook = ReadEntry(xlsx, "xl/workbook.xml");
        Assert.DoesNotContain("forceFullCalc", workbook);
        Assert.Contains("fullCalcOnLoad", workbook);

        // A run whose edition matches neither implemented formula branch must
        // still not emit a constant inside an <f> element as a fake formula.
        var customRun = run with
        {
            Request = run.Request with { StandardEdition = "CUSTOM STANDARD 1.0" }
        };
        string sheet1 = ReadEntry(builder.BuildExcel(model, customRun), "xl/worksheets/sheet1.xml");
        Assert.DoesNotContain("<f>833", sheet1);
        Assert.Contains("Evaluated Dynamic Pressure", sheet1);
    }

    [Fact]
    public void Excel_FormulaCachedValues_MatchEngineResults()
    {
        // Recalc-parity (cached-value level): every formula cell's cached <v>
        // must equal the engine's own result, so what a reader sees before
        // Excel recalculates is the number the engine actually produced.
        var run = CreateAsceRun();
        var model = CreateAsceModel();
        var builder = new DetailedReportBuilder();

        string sheet1 = ReadEntry(builder.BuildExcel(model, run), "xl/worksheets/sheet1.xml");

        double qh = run.ComputedQuantities["qh"];
        string qhF2 = qh.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"<f>0.613", sheet1);
        Assert.Contains($"<v>{qhF2}</v>", sheet1);

        foreach (var s in run.SurfaceResults)
        {
            string netF2 = s.NetPressurePa.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains($"<v>{netF2}</v>", sheet1);
        }
    }

    [Fact]
    public void AuditDocument_ContentHash_IsStableAcrossRegeneration()
    {
        var run = CreateAsceRun();
        var model = CreateAsceModel();

        var doc1 = AuditReportDocumentBuilder.BuildDocument(model, run);
        var doc2 = AuditReportDocumentBuilder.BuildDocument(model, run);

        // GeneratedAt differs between the two builds, but it is generation
        // metadata, not content: the document hash must not change.
        Assert.Equal(doc1.ContentHash, doc2.ContentHash);

        var builder = new DetailedReportBuilder();
        string json1 = builder.BuildJson(model, run);
        string json2 = builder.BuildJson(model, run);
        Assert.Contains(doc1.ContentHash, json1);
        Assert.Contains(doc1.ContentHash, json2);
    }

    [Fact]
    public void Pdf_PreservesAccentedText_BinaryMarker_AndTitle()
    {
        var pdf = new SimplePdfWriter { Title = "Báo cáo gió Đà Nẵng" };
        pdf.AddFlowingSection("Dự án tại Đà Nẵng của Lâm — hướng gió → mặt trăng");
        byte[] bytes = pdf.Build();
        string text = Encoding.ASCII.GetString(bytes);

        // Accented Latin-script text is transliterated, not blanked to spaces.
        Assert.Contains("Du an tai Da Nang cua Lam", text);
        Assert.Contains("huong gio -> mat trang", text);
        // Info dictionary now carries the title (transliterated like all text).
        Assert.Contains("/Title (Bao cao gio Da Nang)", text);
        // Binary marker comment is written as real high bytes, not '?'.
        Assert.Equal(0x25, bytes[9]);
        Assert.Equal(0xE2, bytes[10]);
        Assert.Equal(0xE3, bytes[11]);
    }

    [Fact]
    public void Pdf_LongHeadings_WrapBeforeOverflowingPage()
    {
        var pdf = new SimplePdfWriter();
        // An 80-character heading renders at 12pt Courier (7.2pt/char = 576pt,
        // overflowing the 515pt usable width) unless wrapped at heading width.
        pdf.AddFlowingSection("# " + new string('A', 80));
        byte[] bytes = pdf.Build();
        string text = Encoding.ASCII.GetString(bytes);

        int tjCount = text.Split(new[] { " Tj" }, StringSplitOptions.None).Length - 1;
        // Watermark + page label + at least two heading fragments.
        Assert.True(tjCount >= 4, $"Expected heading to wrap into multiple lines, found {tjCount} text-show operations.");
    }
}
