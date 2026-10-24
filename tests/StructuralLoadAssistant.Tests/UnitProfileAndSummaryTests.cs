using System.Collections.Immutable;
using System.Text;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

/// <summary>
/// Units-by-standard and summary-PDF-only export (product decision,
/// 2026-10-24): ASCE renders imperial (lb/ft2, lb/ft, ft, mph), AS/NZS
/// renders metric kilo (kN/m2, kN/m, m, m/s); engines stay SI internally.
/// The exported report is a summary PDF: no formula ledger, no trace detail,
/// and Phase 0 integrity rules (blocked / verification wording) still apply.
/// </summary>
public class UnitProfileAndSummaryTests
{
    private static EngineeringCalculationRun CreateAsceRun()
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);
        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-GABLE", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "false" }.ToImmutableDictionary());
        return asce.Execute(req);
    }

    private static EngineeringCalculationRun CreateAsNzRun()
    {
        var strategy = new AsNz1170_2_2021CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Md", 1.0); Add("Mz_cat", 0.95); Add("Ms", 1.0); Add("Mt", 1.0); Add("Mc", 1.0);
        Add("Cpe_Windward", 0.7); Add("Cpe_Leeward", -0.5); Add("Cpe_Side", -0.65); Add("Cpe_Roof_Windward", -0.9); Add("Cpe_Roof_Leeward", -0.5); Add("Cpi", 0.2);
        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-ASNZ", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1:2023"),
            "Section 3: Design Wind Speed and Dynamic Pressure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "TC2", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());
        return strategy.Execute(req);
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
        return asce.Execute(req);
    }

    private static ReportModel CreateModel(string projectId, string edition) => new(
        new Project(projectId, "Synthetic Building", "SI"),
        DemoFixtures.Calculation(edition),
        DemoFixtures.Manifest(edition),
        DemoFixtures.Model(),
        [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
        [new("B1", "WALL", "Illustrative", 3, -750)],
        null, null, null,
        new ReportDefinition("report-units"));

    private static string PdfText(byte[] pdf) => Encoding.ASCII.GetString(pdf);

    [Fact]
    public void Profile_Selection_ByStandard()
    {
        Assert.Same(ReportUnitProfile.ImperialAsce, ReportUnitProfile.ForStandard("ASCE/SEI 7-22"));
        Assert.Same(ReportUnitProfile.MetricKiloAsNz, ReportUnitProfile.ForStandard("AS/NZS 1170.2:2021"));
        Assert.Same(ReportUnitProfile.MetricKiloAsNz, ReportUnitProfile.ForStandard("AS/NZS 1170"));
    }

    [Fact]
    public void Profile_Conversions_AreCorrect()
    {
        var imp = ReportUnitProfile.ImperialAsce;
        Assert.Equal("17.41 lb/ft2", imp.Pressure(833.68));       // qh benchmark
        Assert.Equal("143.90 lb/ft", imp.LineLoad(2100));
        Assert.Equal("89.48 mph", imp.Speed(40.0));
        Assert.Equal("98.43 ft", imp.Length(30.0));

        var met = ReportUnitProfile.MetricKiloAsNz;
        Assert.Equal("0.83 kN/m2", met.Pressure(833.68));
        Assert.Equal("1.10 kN/m2", met.Pressure(1096.54));        // AS/NZS q_des benchmark
        Assert.Equal("2.10 kN/m", met.LineLoad(2100));
        Assert.Equal("45.00 m/s", met.Speed(45.0));
    }

    [Fact]
    public void DetailedMarkdown_Asce_RendersImperialUnits()
    {
        var run = CreateAsceRun();
        string md = new DetailedReportBuilder().BuildMarkdown(CreateModel("PRJ-SYNTH-GABLE", "ASCE/SEI 7-22"), run);

        Assert.Contains("Net Pressure (lb/ft2)", md);
        Assert.Contains("| Velocity / Dynamic Pressure | 17.41 lb/ft2 |", md);
        Assert.Contains("143.90", md); // facade 2100 N/m rendered in lb/ft
        Assert.Contains("lb/ft2, line loads in lb/ft", md);
    }

    [Fact]
    public void DetailedMarkdown_AsNz_RendersMetricKiloUnits()
    {
        var run = CreateAsNzRun();
        Assert.True(run.NumericalComputationPossible);
        string md = new DetailedReportBuilder().BuildMarkdown(CreateModel("PRJ-SYNTH-ASNZ", "AS/NZS 1170.2:2021"), run);

        Assert.Contains("Net Pressure (kN/m2)", md);
        Assert.Contains("| Velocity / Dynamic Pressure | 1.10 kN/m2 |", md);
        Assert.Contains("kN/m2, line loads in kN/m", md);
    }

    [Fact]
    public void SummaryPdf_Asce_HasResultsInImperial_AndNoTraceDetail()
    {
        var run = CreateAsceRun();
        var model = CreateModel("PRJ-SYNTH-GABLE", "ASCE/SEI 7-22");
        string text = PdfText(new DetailedReportBuilder().BuildSummaryPdf(model, run));

        Assert.Contains("SUMMARY REPORT", text);
        Assert.Contains("17.41 lb/ft2", text);
        Assert.Contains("89.48 mph", text);
        Assert.Contains("CALCULATED - NOT INDEPENDENTLY VERIFIED", text);
        Assert.DoesNotContain("CALCULATED & VERIFIED", text);
        // Summary only: the step-by-step calculation stays in the UX.
        Assert.DoesNotContain("FORMULA LEDGER", text);
        Assert.DoesNotContain("INPUT REGISTER", text);
        Assert.DoesNotContain(run.TraceNodes[0].NodeId, text);
        Assert.Contains(run.Id, text);
        Assert.Contains(run.ContentHash, text);
    }

    [Fact]
    public void SummaryPdf_AsNz_HasResultsInMetricKilo()
    {
        var run = CreateAsNzRun();
        var model = CreateModel("PRJ-SYNTH-ASNZ", "AS/NZS 1170.2:2021");
        string text = PdfText(new SummaryReportBuilder().BuildSummaryPdf(model, run));

        Assert.Contains("1.10 kN/m2", text);
        Assert.Contains("45.00 m/s", text);
        Assert.Contains("kN/m2", text);
    }

    [Fact]
    public void SummaryPdf_BlockedRun_IsBlockedNotComputed_WithNoNumericPressure()
    {
        var run = CreateBlockedAsceRun();
        Assert.False(run.NumericalComputationPossible);
        var model = CreateModel("PRJ-BLOCKED", "ASCE/SEI 7-22");
        string text = PdfText(new SummaryReportBuilder().BuildSummaryPdf(model, run));

        Assert.Contains("BLOCKED / NOT COMPUTED", text);
        Assert.DoesNotContain("CALCULATED", text.Replace("BLOCKED / NOT COMPUTED", ""));
        Assert.DoesNotContain("0.00 lb/ft2", text);
    }

    [Fact]
    public void SummaryPdf_IdentityMismatch_Throws()
    {
        var run = CreateAsceRun();
        var wrongModel = CreateModel("PRJ-SYNTH-GABLE", "AS/NZS 1170.2:2021");
        Assert.Throws<InvalidOperationException>(() => new SummaryReportBuilder().BuildSummaryPdf(wrongModel, run));
    }
}
