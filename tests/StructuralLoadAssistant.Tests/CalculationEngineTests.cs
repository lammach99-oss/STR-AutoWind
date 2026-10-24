using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class CalculationEngineTests
{
    [Fact]
    public void Asce7_22_Blocks_When_Coefficients_Missing()
    {
        var strategy = new Asce7_22CalculationStrategy();
        var req = new EngineeringCalculationRequest(
            "PRJ-01", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C",
            ImmutableDictionary<string, CoefficientInputEvidence>.Empty, ImmutableDictionary<string, string>.Empty);

        var run = strategy.Execute(req);
        Assert.False(run.NumericalComputationPossible);
        Assert.False(run.IndependentEngineerReviewed);
        Assert.Contains(run.Issues, i => i.Code == "BLOCKED_REQUIRED_NORMATIVE_DATA");
        Assert.Contains("BLOCKED", run.QualificationStatus);
    }

    [Fact]
    public void Asce7_22_Synthetic_HandCalculation_Benchmark()
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

        var run = strategy.Execute(req);
        Assert.True(run.NumericalComputationPossible);
        Assert.True(run.OwnerEngineerReviewed);
        Assert.False(run.IndependentEngineerReviewed);
        Assert.Equal("SYNTHETIC EXAMPLE - NOT FOR DESIGN", run.QualificationStatus);
        Assert.Equal(833.68, run.ComputedQuantities["qh"], 2);
        // Phase 1 envelope: the hand-calculated +GCpi case (416.84) is carried
        // as the positive-internal case; the governing (primary) value is the
        // -GCpi case, 716.96. Leeward is governed by the +GCpi case (-504.38).
        var asceWw = Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "WindwardWall");
        Assert.Equal(416.84, asceWw.NetPressurePositiveInternalPa, 2);
        Assert.Equal(716.96, asceWw.NetPressureNegativeInternalPa, 2);
        Assert.Equal(716.96, asceWw.NetPressurePa, 2);
        Assert.Equal("NEGATIVE", asceWw.GoverningInternalPressureCase);
        Assert.Equal(-504.38, Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "LeewardWall").NetPressurePa, 2);
    }

    [Fact]
    public void AsNz1170_2_2021_Synthetic_HandCalculation_Benchmark()
    {
        var strategy = new AsNz1170_2_2021CalculationStrategy();
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void Add(string k, double v) => b.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        Add("Md", 1.0); Add("Mz_cat", 0.95); Add("Ms", 1.0); Add("Mt", 1.0); Add("Mc", 1.0);
        Add("Cpe_Windward", 0.7); Add("Cpe_Leeward", -0.5); Add("Cpe_Side", -0.65); Add("Cpe_Roof_Windward", -0.9); Add("Cpe_Roof_Leeward", -0.5); Add("Cpi", 0.2);

        var req = new EngineeringCalculationRequest(
            "PRJ-SYNTH-ASNZ", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024"),
            "Section 3: Design Wind Speed and Dynamic Pressure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "TC2", b.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        var run = strategy.Execute(req);
        Assert.True(run.NumericalComputationPossible);
        Assert.Equal(1096.54, run.ComputedQuantities["q_des"], 2);
        // Phase 1 envelope: +Cpi case 548.27 is the hand-calculated value;
        // the governing (primary) windward value is the -Cpi case, 986.88.
        // RoofWindward is governed by the +Cpi case (-1206.19, unchanged).
        var asnzWw = Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "WindwardWall");
        Assert.Equal(548.27, asnzWw.NetPressurePositiveInternalPa, 2);
        Assert.Equal(986.88, asnzWw.NetPressureNegativeInternalPa, 2);
        Assert.Equal(986.88, asnzWw.NetPressurePa, 2);
        Assert.Equal("NEGATIVE", asnzWw.GoverningInternalPressureCase);
        Assert.Equal(-1206.19, Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "RoofWindward").NetPressurePa, 2);
    }
}
