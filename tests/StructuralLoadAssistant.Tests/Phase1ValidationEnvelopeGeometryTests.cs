using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

/// <summary>
/// Phase 1 (decisions confirmed by the project owner, 2026-10-24):
/// invalid coefficients BLOCK the run; internal pressure is a both-sign
/// envelope with the governing case as the primary result (Option A);
/// |GCpi| is validated against the enclosure classification; roof normals
/// derive from RoofSlopeDegrees; ASCE mean roof height follows the
/// Section 26.2 rule (eave height at slope &lt;= 10 deg).
/// </summary>
public class Phase1ValidationEnvelopeGeometryTests
{
    private static CoefficientInputEvidence Ev(string k, double v) =>
        new(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow);

    private static ImmutableDictionary<string, CoefficientInputEvidence> AsceCoeffs(
        double gcpi = 0.18, double kd = 0.85, double kzt = 1.0, double g = 0.85)
    {
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        b.Add("Kd", Ev("Kd", kd)); b.Add("Kzt", Ev("Kzt", kzt)); b.Add("Ke", Ev("Ke", 1.0)); b.Add("Kh", Ev("Kh", 1.0));
        b.Add("G", Ev("G", g)); b.Add("GCpi", Ev("GCpi", gcpi));
        b.Add("Cp_Windward", Ev("Cp_Windward", 0.8)); b.Add("Cp_Leeward", Ev("Cp_Leeward", -0.5)); b.Add("Cp_Side", Ev("Cp_Side", -0.7));
        b.Add("Cp_Roof_Windward", Ev("Cp_Roof_Windward", -0.7)); b.Add("Cp_Roof_Leeward", Ev("Cp_Roof_Leeward", -0.6));
        return b.ToImmutable();
    }

    private static EngineeringCalculationRequest AsceRequest(
        ImmutableDictionary<string, CoefficientInputEvidence> coeffs,
        string enclosure = "Enclosed", double slopeDeg = 15.0, double eave = 6.0, double ridge = 9.0) =>
        new("PRJ-P1", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", enclosure,
            40.0, 10.0, 30.0, 20.0, eave, ridge, slopeDeg, 0.0, "C", coeffs,
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS" }.ToImmutableDictionary());

    private static ImmutableDictionary<string, CoefficientInputEvidence> AsNzCoeffs(double md = 1.0, double mc = 1.0, double cpi = 0.2)
    {
        var b = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        b.Add("Md", Ev("Md", md)); b.Add("Mz_cat", Ev("Mz_cat", 0.95)); b.Add("Ms", Ev("Ms", 1.0)); b.Add("Mt", Ev("Mt", 1.0)); b.Add("Mc", Ev("Mc", mc));
        b.Add("Cpe_Windward", Ev("Cpe_Windward", 0.7)); b.Add("Cpe_Leeward", Ev("Cpe_Leeward", -0.5)); b.Add("Cpe_Side", Ev("Cpe_Side", -0.65));
        b.Add("Cpe_Roof_Windward", Ev("Cpe_Roof_Windward", -0.9)); b.Add("Cpe_Roof_Leeward", Ev("Cpe_Roof_Leeward", -0.5)); b.Add("Cpi", Ev("Cpi", cpi));
        return b.ToImmutable();
    }

    private static EngineeringCalculationRequest AsNzRequest(
        ImmutableDictionary<string, CoefficientInputEvidence> coeffs, double slopeDeg = 15.0) =>
        new("PRJ-P1-NZ", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024"),
            "Section 3: Design Wind Speed and Dynamic Pressure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 6.0, 9.0, slopeDeg, 0.0, "TC2", coeffs,
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS" }.ToImmutableDictionary());

    // ---- 1a: validation blocks ----

    [Fact]
    public void NaN_Gcpi_Blocks_AsceRun()
    {
        var run = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(gcpi: double.NaN)));
        Assert.False(run.NumericalComputationPossible);
        Assert.Contains(run.Issues, i => i.Code == "INVALID_COEFF_GCpi");
        Assert.Empty(run.SurfaceResults);
    }

    [Fact]
    public void OutOfRange_Factors_Block_AsceRun()
    {
        var kd = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(kd: 1.2)));
        Assert.False(kd.NumericalComputationPossible);
        Assert.Contains(kd.Issues, i => i.Code == "INVALID_COEFF_Kd");

        var kzt = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(kzt: 0.9)));
        Assert.False(kzt.NumericalComputationPossible);
        Assert.Contains(kzt.Issues, i => i.Code == "INVALID_COEFF_Kzt");

        var g = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(g: 0)));
        Assert.False(g.NumericalComputationPossible);
        Assert.Contains(g.Issues, i => i.Code == "INVALID_COEFF_G");
    }

    [Fact]
    public void Invalid_Multipliers_Block_AsNzRun()
    {
        var md = new AsNz1170_2_2021CalculationStrategy().Execute(AsNzRequest(AsNzCoeffs(md: 0)));
        Assert.False(md.NumericalComputationPossible);
        Assert.Contains(md.Issues, i => i.Code == "INVALID_COEFF_Md");

        var mc = new AsNz1170_2_2021CalculationStrategy().Execute(AsNzRequest(AsNzCoeffs(mc: double.NaN)));
        Assert.False(mc.NumericalComputationPossible);
        Assert.Contains(mc.Issues, i => i.Code == "INVALID_COEFF_Mc");
    }

    // ---- 1b: GCpi vs enclosure + envelope ----

    [Fact]
    public void Gcpi_Mismatching_Enclosure_Blocks()
    {
        var run = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(gcpi: 0.55), enclosure: "Enclosed"));
        Assert.False(run.NumericalComputationPossible);
        Assert.Contains(run.Issues, i => i.Code == "GCPI_ENCLOSURE_MISMATCH");
    }

    [Fact]
    public void Gcpi_Matching_Enclosure_Computes()
    {
        var partial = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(gcpi: 0.55), enclosure: "Partially Enclosed"));
        Assert.True(partial.NumericalComputationPossible);

        var open = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(gcpi: 0.0), enclosure: "Open"));
        Assert.True(open.NumericalComputationPossible);
        Assert.Equal(0, open.ComputedQuantities["pi_pos"], 2);
    }

    [Fact]
    public void Unrecognised_Enclosure_Warns_But_Computes()
    {
        var run = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), enclosure: "Unknown"));
        Assert.True(run.NumericalComputationPossible);
        Assert.Contains(run.Issues, i => i.Code == "GCPI_ENCLOSURE_UNRECOGNISED" && i.Severity == CalculationIssueSeverity.Warning);
    }

    [Fact]
    public void Envelope_EverySurface_CarriesBothCases_AndGoverningIsPrimary()
    {
        var asce = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs()));
        Assert.True(asce.NumericalComputationPossible);
        Assert.All(asce.SurfaceResults, s =>
        {
            Assert.True(s.GoverningInternalPressureCase is "POSITIVE" or "NEGATIVE");
            double governing = s.GoverningInternalPressureCase == "NEGATIVE" ? s.NetPressureNegativeInternalPa : s.NetPressurePositiveInternalPa;
            Assert.Equal(governing, s.NetPressurePa, 6);
            Assert.Equal(Math.Max(Math.Abs(s.NetPressurePositiveInternalPa), Math.Abs(s.NetPressureNegativeInternalPa)), Math.Abs(s.NetPressurePa), 6);
        });

        var asnz = new AsNz1170_2_2021CalculationStrategy().Execute(AsNzRequest(AsNzCoeffs()));
        Assert.True(asnz.NumericalComputationPossible);
        var ww = Assert.Single(asnz.SurfaceResults, s => s.SurfaceOrZoneId == "WindwardWall");
        Assert.Equal("NEGATIVE", ww.GoverningInternalPressureCase);
        Assert.Equal(986.88, ww.NetPressurePa, 2);
    }

    [Fact]
    public void Envelope_Ignores_Legacy_NegFlag()
    {
        // The same run is produced whether the legacy text flag says POS or
        // NEG: both cases are always computed and enveloped.
        var posReq = AsceRequest(AsceCoeffs());
        var negReq = posReq with
        {
            Options = new Dictionary<string, string> { ["InternalPressure"] = "IP_NEG" }.ToImmutableDictionary()
        };
        var strategy = new Asce7_22CalculationStrategy();
        var posRun = strategy.Execute(posReq);
        var negRun = strategy.Execute(negReq);
        Assert.Equal(
            posRun.SurfaceResults.Select(s => s.NetPressurePa),
            negRun.SurfaceResults.Select(s => s.NetPressurePa));
    }

    // ---- 1c: geometry ----

    [Fact]
    public void RoofNormals_DeriveFromSlope()
    {
        var run = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: 15.0));
        var rw = Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "RoofWindward");
        Assert.Equal(0, rw.OutwardNormal.X, 3);
        Assert.Equal(-0.259, rw.OutwardNormal.Y, 3);
        Assert.Equal(0.966, rw.OutwardNormal.Z, 3);
        var rl = Assert.Single(run.SurfaceResults, s => s.SurfaceOrZoneId == "RoofLeeward");
        Assert.Equal(0.259, rl.OutwardNormal.Y, 3);
        Assert.Equal(0.966, rl.OutwardNormal.Z, 3);

        var flat = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: 0.0));
        var flatRw = Assert.Single(flat.SurfaceResults, s => s.SurfaceOrZoneId == "RoofWindward");
        Assert.Equal(0, flatRw.OutwardNormal.Y, 3);
        Assert.Equal(1, flatRw.OutwardNormal.Z, 3);

        // 36.87 deg reproduces the legacy hard-coded (0, -0.6, 0.8) vector.
        double legacy = Math.Atan(0.75) * 180.0 / Math.PI;
        var legacyRun = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: legacy));
        var legacyRw = Assert.Single(legacyRun.SurfaceResults, s => s.SurfaceOrZoneId == "RoofWindward");
        Assert.Equal(-0.6, legacyRw.OutwardNormal.Y, 3);
        Assert.Equal(0.8, legacyRw.OutwardNormal.Z, 3);
    }

    [Fact]
    public void Invalid_RoofSlope_Blocks()
    {
        var run = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: 90.0));
        Assert.False(run.NumericalComputationPossible);
        Assert.Contains(run.Issues, i => i.Code == "INVALID_ROOF_SLOPE");
    }

    [Fact]
    public void MeanRoofHeight_Follows_Section26_2_Rule()
    {
        var lowSlope = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: 5.0, eave: 6.0, ridge: 9.0));
        Assert.Equal(6.0, lowSlope.ComputedQuantities["h"], 2);

        var steep = new Asce7_22CalculationStrategy().Execute(AsceRequest(AsceCoeffs(), slopeDeg: 15.0, eave: 6.0, ridge: 9.0));
        Assert.Equal(7.5, steep.ComputedQuantities["h"], 2);
    }
}
