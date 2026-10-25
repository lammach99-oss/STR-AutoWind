using StructuralLoadAssistant.Csi.Fake;
using System;
using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class WindPdfReportBuilderTests
{
    private static EngineeringCalculationRun CreateAsceRun(int extraTraceNodes = 0)
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Illustrative review fixture", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            "PRJ-ASCE-GABLE", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        var run = asce.Execute(req);

        if (extraTraceNodes > 0)
        {
            var nodes = run.TraceNodes.ToList();
            for (int i = 0; i < extraTraceNodes; i++)
            {
                nodes.Add(new CalculationTraceNode(
                    $"Node-Extra-{i:D3}", "ExtraStage", $"Extra Quantity {i}", "X = A + B",
                    $"X = {i} + 1 = {i + 1}", $"{i + 1} Pa", i + 1, "Pa", "Ref Clause 99",
                    $"Explanatory note for node {i}", [], []));
            }
            run = run with { TraceNodes = nodes.ToImmutableArray() };
        }

        return run;
    }

    private static EngineeringCalculationRun CreateAsNzSlopeRun()
    {
        var asnz = new AsNz1170_2_2021CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Illustrative review fixture", DateTimeOffset.UtcNow));
        Add("Md", 1.0); Add("Mz_cat", 0.91); Add("Ms", 1.0); Add("Mt", 1.0); Add("Cpi", 0.20);
        Add("Cpe_Windward", 0.7); Add("Cpe_Leeward", -0.3); Add("Cpe_Side", -0.65); Add("Cpe_Roof_Upwind", -0.9); Add("Cpe_Roof_Downwind", -0.5);

        var req = new EngineeringCalculationRequest(
            "PRJ-ASNZ-SLOPE", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1"),
            "Section 5: Analytical Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "TC2", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        return asnz.Execute(req);
    }

    private static EngineeringCalculationRun CreateAsNzFlatRun()
    {
        var asnz = new AsNz1170_2_2021CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Illustrative review fixture", DateTimeOffset.UtcNow));
        Add("Md", 1.0); Add("Mz_cat", 0.91); Add("Ms", 1.0); Add("Mt", 1.0); Add("Cpi", 0.20);
        Add("Cpe_Windward", 0.7); Add("Cpe_Leeward", -0.3); Add("Cpe_Side", -0.65); Add("Cpe_Roof_Zone1", -0.9); Add("Cpe_Roof_Zone2", -0.5); Add("Cpe_Roof_Zone3", -0.3);

        var req = new EngineeringCalculationRequest(
            "PRJ-ASNZ-FLAT", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1"),
            "Section 5: Analytical Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Flat", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 6.0, 0.0, 0.0, "TC2", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        return asnz.Execute(req);
    }

    private static ReportModel CreateModel(string projectId, string edition)
    {
        return new ReportModel(
            new Project(projectId, "Test Engineering Project", "SI"),
            DemoFixtures.Calculation(edition),
            DemoFixtures.Manifest(edition),
            DemoFixtures.Model(),
            ImmutableArray<FacadeReportRow>.Empty,
            ImmutableArray<WallReportRow>.Empty,
            null, null, null,
            new ReportDefinition("rep-test"));
    }

    [Fact]
    public void LayoutProfileSelector_CorrectlyIdentifiesTemplates()
    {
        Assert.Equal(WindReportLayoutProfile.AsceImperial, WindReportLayoutProfileSelector.Select("ASCE/SEI 7-22", "Gable", 15.0));
        Assert.Equal(WindReportLayoutProfile.AsNzSlopeRoof, WindReportLayoutProfileSelector.Select("AS/NZS 1170.2:2021", "Gable", 15.0));
        Assert.Equal(WindReportLayoutProfile.AsNzFlatRoof, WindReportLayoutProfileSelector.Select("AS/NZS 1170.2:2021", "Flat", 0.0));
        Assert.Equal(WindReportLayoutProfile.AsNzFlatRoof, WindReportLayoutProfileSelector.Select("AS/NZS 1170.2:2021", "Gable", 0.5));
    }

    [Fact]
    public void AsceImperial_Html_ContainsExpectedWorkbookStructureAndUnits()
    {
        var run = CreateAsceRun();
        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        Assert.Equal(WindReportLayoutProfile.AsceImperial, doc.LayoutProfile);
        Assert.Equal("lb/ft2", doc.UnitProfile.PressureUnit);
        Assert.Equal("lb", doc.UnitProfile.ForceUnit);
        Assert.Equal("ft", doc.UnitProfile.LengthUnit);
        Assert.Equal("mph", doc.UnitProfile.SpeedUnit);

        // Sections
        Assert.Contains("A. REFERENCE STANDARDS &amp; GOVERNING CODES", html);
        Assert.Contains("B. BASIC VELOCITY PRESSURE CALCULATION", html);
        Assert.Contains("C. DESIGN WIND PRESSURE ON SURFACES", html);
        Assert.Contains("APPENDIX: COMPLETE STEP-BY-STEP CALCULATION TRACE LEDGER", html);

        // Units present in HTML
        Assert.Contains("lb/ft2", html);
        Assert.Contains("mph", html);
        Assert.Contains("ft", html);

        // SVG diagrams present
        Assert.Contains("<svg", html);
        Assert.Contains("Building Plan &amp; Wind Vector", html);
        Assert.Contains("Building Elevation &amp; Roof Profile", html);

        // Offline requirement: no external font / CDN links
        Assert.DoesNotContain("<link", html); Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("src='http", html); Assert.DoesNotContain("src='https", html);
    }

    [Fact]
    public void AsNzSlopeRoof_Html_ContainsExpectedWorkbookStructureAndUnits()
    {
        var run = CreateAsNzSlopeRun();
        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        Assert.Equal(WindReportLayoutProfile.AsNzSlopeRoof, doc.LayoutProfile);
        Assert.Equal("kN/m2", doc.UnitProfile.PressureUnit);
        Assert.Equal("kN", doc.UnitProfile.ForceUnit);
        Assert.Equal("m", doc.UnitProfile.LengthUnit);
        Assert.Equal("m/s", doc.UnitProfile.SpeedUnit);

        Assert.Contains("1. BASIC PARAMETERS &amp; REGIONAL MULTIPLIERS", html);
        Assert.Contains("2. SLOPE ROOF &amp; WALL DESIGN PRESSURES", html);
        Assert.Contains("kN/m2", html);
        Assert.Contains("m/s", html);
    }

    [Fact]
    public void AsNzFlatRoof_Html_ContainsExpectedWorkbookStructureAndUnits()
    {
        var run = CreateAsNzFlatRun();
        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        Assert.Equal(WindReportLayoutProfile.AsNzFlatRoof, doc.LayoutProfile);
        Assert.Contains("1. BASIC PARAMETERS &amp; FLAT ROOF SITE GEOMETRY", html);
        Assert.Contains("2. FLAT ROOF ZONE PRESSURES", html);
        Assert.Contains("Flat Roof", html);
    }

    [Fact]
    public void HtmlReport_Preserves100PercentTraceNodes()
    {
        var run = CreateAsceRun();
        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        foreach (var node in run.TraceNodes)
        {
            Assert.Contains($"[{node.NodeId}]", html);
        }
    }

    [Fact]
    public void MultiPageTraceLedger_With150Steps_GeneratesWithoutClipping()
    {
        var run = CreateAsceRun(extraTraceNodes: 160);
        Assert.True(run.TraceNodes.Length > 160);

        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        for (int i = 0; i < 160; i++)
        {
            Assert.Contains($"[Node-Extra-{i:D3}]", html);
        }

        Assert.Contains("APPENDIX: COMPLETE STEP-BY-STEP CALCULATION TRACE LEDGER", html);
    }

    [Fact]
    public void HtmlEscaping_NeutralizesMaliciousInputAndScripts()
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Illustrative review fixture", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var maliciousReq = new EngineeringCalculationRequest(
            "PRJ-<script>alert('pwn')</script>", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1"),
            "Chapter 27: <b onmouseover=alert(1)>MWFRS</b>", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());

        var run = asce.Execute(maliciousReq);
        var model = new ReportModel(
            new Project("PRJ-<script>alert('pwn')</script>", "Danger <img src=x onerror=alert(2) /> Project", "SI"),
            DemoFixtures.Calculation("ASCE/SEI 7-22"),
            DemoFixtures.Manifest("ASCE/SEI 7-22"),
            DemoFixtures.Model(),
            ImmutableArray<FacadeReportRow>.Empty,
            ImmutableArray<WallReportRow>.Empty,
            null, null, null,
            new ReportDefinition("rep-test"));

        var doc = WindPdfReportDocumentBuilder.Build(model, run);
        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);

        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("<b onmouseover", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void IdentityGuard_MismatchBetweenModelAndCalculation_Throws()
    {
        var run = CreateAsceRun();
        var mismatchedModel = CreateModel(run.Request.ProjectId, "AS/NZS 1170.2:2021");

        Assert.Throws<InvalidOperationException>(() => WindPdfReportDocumentBuilder.Build(mismatchedModel, run));
    }

    [Fact]
    public void PdfRenderService_RendersValidPdf_WhenRendererAvailable()
    {
        if (!WindPdfRenderService.IsRendererAvailable) return;

        var run = CreateAsceRun();
        var model = CreateModel(run.Request.ProjectId, run.Request.StandardEdition);
        var doc = WindPdfReportDocumentBuilder.Build(model, run);

        var service = new WindPdfRenderService();
        byte[] pdfBytes = service.RenderDocumentBytes(doc);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }
}
