using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class ReportWiringRegressionTests
{
    [Fact]
    public void Export_Requires_Selected_Engineering_Run_To_Include_Worksheet_Calculations()
    {
        // When EngineeringCalculationRun is null, the report must state NO ENGINEERING RUN SELECTED
        // and must NOT include misleading synthetic calculation rows.
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();
        string mdNoRun = builder.BuildMarkdown(model, null);

        Assert.Contains("NO ENGINEERING CALCULATION RUN SELECTED", mdNoRun);
        Assert.DoesNotContain("833.68", mdNoRun);
    }

    [Fact]
    public void Export_With_Selected_Run_Includes_Exact_Run_Id_And_Calculated_Pressures()
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            "PRJ-01", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        var run = asce.Execute(req);
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();
        string md = builder.BuildMarkdown(model, run);

        Assert.Contains(run.Id, md);
        Assert.Contains("833.68", md);
        Assert.DoesNotContain("demo-pressure-ASCE7_22-missing", md);
    }

    [Fact]
    public void Manual_Verification_Worksheet_Contains_Actual_Software_Values_And_Editable_Placeholders()
    {
        var asce = new Asce7_22CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Kd", 0.85); Add("Kzt", 1.0); Add("Ke", 1.0); Add("Kh", 1.0); Add("G", 0.85); Add("GCpi", 0.18);
        Add("Cp_Windward", 0.8); Add("Cp_Leeward", -0.5); Add("Cp_Side", -0.7); Add("Cp_Roof_Windward", -0.7); Add("Cp_Roof_Leeward", -0.6);

        var req = new EngineeringCalculationRequest(
            "PRJ-01", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        var run = asce.Execute(req);
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();
        string md = builder.BuildMarkdown(model, run);

        // Must bind actual 833.68 and not generic placeholder "As calculated"
        Assert.Contains("833.68 Pa", md);
        Assert.Contains("NOT CHECKED", md);
    }
}

