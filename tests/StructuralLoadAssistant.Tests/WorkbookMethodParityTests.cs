using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

/// <summary>
/// Workbook parity: the engine's workbook methods and workbook-mode runs
/// must reproduce the owner reference workbooks' own cached results
/// (ASCE_Wind_load.xlsm "Windload (Imperial)" example and
/// AS.NZS_Wind_load.xlsm "Slope Roof" example) to rounding tolerance.
/// </summary>
public sealed class WorkbookMethodParityTests
{
    private static void Near(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected} +/- {tolerance}, got {actual}");

    private static CoefficientInputEvidence Coeff(string name, double value) =>
        new(name, value, "-", DataProvenance.Unverified, "Test", "Test", "Workbook parity fixture", DateTimeOffset.UtcNow);

    private static EngineeringCalculationRequest Request(
        string standard, Dictionary<string, double> coeffs, Dictionary<string, string> options,
        double v = 40, double refH = 10, double len = 20, double wid = 15,
        double eave = 8, double ridge = 10, double slope = 15,
        string enclosure = "Enclosed", string exposure = "C", LimitState ls = LimitState.Uls, double angle = 0) =>
        new("P-WB", standard, ImmutableArray<string>.Empty, "Workbook", ls,
            WindDirection.PositiveX, angle, "Gable", enclosure, v, refH, len, wid, eave, ridge, slope, 0, exposure,
            coeffs.ToImmutableDictionary(kv => kv.Key, kv => Coeff(kv.Key, kv.Value)),
            options.ToImmutableDictionary());

    // ---------------- Workbook table / formula parity ----------------

    [Fact]
    public void Asce_Kz_MatchesWorkbookCell()
    {
        // Workbook K35: z = 33 ft, Exposure C: Kz = 0.9997473778944684.
        Assert.Equal(0.9997473778944684, WorkbookMethods.AsceKz(33, "C"), 6);
        Assert.Equal(1.0, WorkbookMethods.AsceKzt(0, 0, 0), 9);
    }

    [Fact]
    public void Asce_Qh_MatchesWorkbookUlsAndSls()
    {
        double kz = WorkbookMethods.AsceKz(33, "C");
        // Workbook K39 (V = 130 mph): 36.76510997364234 psf; K41 (V = 67 mph): 9.765596371105353 psf.
        Assert.Equal(36.76510997364234, WorkbookMethods.AsceQhPsf(130, kz, 1, 0.85, 1), 4);
        Assert.Equal(9.765596371105353, WorkbookMethods.AsceQhPsf(67, kz, 1, 0.85, 1), 4);
    }

    [Fact]
    public void Asce_LeewardCp_InterpolatesOnLengthBreadth()
    {
        Assert.Equal(-0.5, WorkbookMethods.AsceLeewardWallCp(1.0), 9);   // workbook D128 cached -0.5
        Assert.Equal(-0.25, WorkbookMethods.AsceLeewardWallCp(3.0), 9);  // midway (2,-0.3)-(4,-0.2)
        Assert.Equal(-0.2, WorkbookMethods.AsceLeewardWallCp(9.0), 9);   // clamped
    }

    [Fact]
    public void Asce_RoofZoneCp_MatchesWorkbookArrayFormula()
    {
        // Workbook H133 cached Cp: -0.90773033707865169 at h/L = 0.5096629213483146.
        var (cp1, cp2) = WorkbookMethods.AsceRoofZoneCp(1, 0.5096629213483146);
        Assert.Equal(-0.90773033707865169, cp1, 6);
        Assert.Equal(-0.18, cp2, 9);
    }

    [Fact]
    public void AsNz_RegionalWindSpeed_MatchesDataSheet()
    {
        Assert.Equal(80, WorkbookMethods.AsNzRegionalWindSpeed("D", 500));   // Slope Roof VRu
        Assert.Equal(53, WorkbookMethods.AsNzRegionalWindSpeed("D", 25));    // Slope Roof VRs
        Assert.Equal(45, WorkbookMethods.AsNzRegionalWindSpeed("A", 500));   // Data Sheet C16
        Assert.Equal(57, WorkbookMethods.AsNzRegionalWindSpeed("B", 500));   // Data Sheet D16
        Assert.Equal(66, WorkbookMethods.AsNzRegionalWindSpeed("C", 500));   // Data Sheet E16
        Assert.Equal(73, WorkbookMethods.AsNzRegionalWindSpeed("C", 2000));  // Data Sheet E18
    }

    [Fact]
    public void AsNz_MzCat_MatchesTable41()
    {
        Assert.Equal(0.91, WorkbookMethods.AsNzMzCat(3.75, 2, false), 6);  // Slope Roof K28 = 0.91
        Assert.Equal(1.12, WorkbookMethods.AsNzMzCat(10, 1, false), 6);
        Assert.Equal(0.95, WorkbookMethods.AsNzMzCat(15, 3, true), 6);      // Table 4.1(B), typo-corrected 0.95
        Assert.Equal(1.08, WorkbookMethods.AsNzMzCat(20, 2, false), 6);     // Table 4.1(A), typo-corrected 1.08
    }

    [Fact]
    public void AsNz_LeewardCp_MatchesWorkbookFormula()
    {
        Assert.Equal(-0.3, WorkbookMethods.AsNzLeewardWallCp(13, 1.667), 9);  // pitch > 10 -> -0.3 (Slope Roof J51 via J50)
        Assert.Equal(-0.5, WorkbookMethods.AsNzLeewardWallCp(5, 0.8), 9);
        Assert.Equal(-0.4, WorkbookMethods.AsNzLeewardWallCp(5, 1.5), 6);
        Assert.Equal(-0.2, WorkbookMethods.AsNzLeewardWallCp(5, 6), 9);
    }

    [Fact]
    public void AsNz_RoofCp_MatchesWorkbookUdfs()
    {
        // Slope Roof: h/d = 0.197368, pitch = 13 deg.
        Assert.Equal(-0.58, WorkbookMethods.AsNzUpwindRoofCp(0.19736842105263158, 13, 1), 4);   // F66 cached -0.58
        Assert.Equal(-0.12, WorkbookMethods.AsNzUpwindRoofCp(0.19736842105263158, 13, 2), 4);   // F67 cached -0.12
        Assert.Equal(-0.42, WorkbookMethods.AsNzDownwindRoofCp(0.19736842105263158, 13, 19.0 / 11.4), 4); // F68 cached -0.42
    }

    [Fact]
    public void AsNz_FlatRoofZones_MatchWorkbook()
    {
        // Flat roof sheet: h/d = 0.3059 -> clamped to the h/d = 0.5 column.
        Assert.Equal(-0.9, WorkbookMethods.AsNzFlatRoofZoneCp(1, 0.3059, 1));  // F67 cached -0.9
        Assert.Equal(-0.4, WorkbookMethods.AsNzFlatRoofZoneCp(1, 0.3059, 2));  // F76 cached -0.4
        Assert.Null(WorkbookMethods.AsNzFlatRoofZoneCp(4, 1.0, 1));            // workbook "-"
    }

    // ---------------- Workbook-mode run parity ----------------

    [Fact]
    public void Asce_WorkbookModeRun_ReproducesWorkbookExample()
    {
        // Workbook example: Vuls 130 mph, Vsls 67 mph, Kd 0.85, Ke 1, G 0.85,
        // Exposure C, z 33 ft, enclosed (Ri 1), B = L = 37.0833 ft, h 18.9 ft, theta 10 deg.
        double ft = 0.3048;
        var coeffs = new Dictionary<string, double>
        {
            ["Kd"] = 0.85, ["Ke"] = 1.0, ["G"] = 0.85,
            ["V_SLS"] = 67 * 0.44704, ["Z_FT"] = 33,
            ["Cp_Roof_Leeward"] = -0.5, // explicit wins over the workbook zone-3 derivation
        };
        var req = Request("ASCE 7-22", coeffs,
            new() { ["WorkbookMethod"] = "ASCE7" },
            v: 130 * 0.44704, refH: 33 * ft, len: 37.0833333 * ft, wid: 37.0833333 * ft,
            eave: 18.9 * ft, ridge: 20 * ft, slope: 10, enclosure: "Enclosed building", exposure: "C");

        var run = new Asce7_22CalculationStrategy().Execute(req);
        Assert.True(run.NumericalComputationPossible);

        double psf = 47.88025898033584; // 1 lb/ft2 in Pa
        // Workbook K39: qh,uls = 36.76511 psf (engine works in Pa internally).
        Near(36.76510997364234 * psf, run.ComputedQuantities["qh"], 2.0); // imperial 0.00256 vs SI 0.613 constants differ ~0.05%
        // Workbook K41: qh,sls = 9.76560 psf.
        Near(9.765596371105353 * psf, run.ComputedQuantities["qh_sls"], 1.0);
        // Derived coefficients match the workbook derivation chain.
        Assert.Equal(0.9997473778944684, run.ComputedQuantities["Kh"], 4);
        Assert.Equal(0.18, run.ComputedQuantities["GCpi"], 6);

        var ww = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "WindwardWall");
        // Workbook: external 25.00027 psf; internal +/-6.61772 psf; governing net
        // is the -GCpi case: 25.00027 + 6.61772 = 31.61799 psf.
        Near(25.000274782076794 * psf, ww.ExternalPressurePa, 1.5);
        Near(31.617994577332415 * psf, ww.NetPressurePa, 1.5);
        Assert.Equal("NEGATIVE", ww.GoverningInternalPressureCase);
        // SLS net scales by the workbook ratio K43 = 0.26562...
        Near(31.617994577332415 * psf * (9.765596371105353 / 36.76510997364234), ww.AlternateLimitStateNetPressurePa, 1.0);

        var lw = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "LeewardWall");
        Assert.Equal(-0.5, lw.ExternalCoefficient, 6); // L/B = 1 -> -0.5 (workbook D128)
    }

    [Fact]
    public void AsNz_WorkbookModeRun_ReproducesSlopeRoofExample()
    {
        // Slope Roof sheet: Region D, ARI 500/25, TC2, Md 0.9, Mc 1.05, Ms = Mt = 1,
        // Ka = Kc = Kl = Kp = Cdyn = 1, L 19 m, W 11.4 m, h 3.75 m, pitch 13 deg.
        var coeffs = new Dictionary<string, double>
        {
            ["Md"] = 0.9, ["Ms"] = 1.0, ["Mt"] = 1.0, ["Mc"] = 1.05, ["Cpi"] = 0.0,
        };
        var req = Request("AS/NZS 1170.2:2021", coeffs,
            new() { ["WorkbookMethod"] = "ASNZS", ["Region"] = "D" },
            v: 1, refH: 3.75, len: 19, wid: 11.4, eave: 3.0, ridge: 4.5, slope: 13,
            enclosure: "Enclosed", exposure: "TC2");

        var run = new AsNz1170_2_2021CalculationStrategy().Execute(req);
        Assert.True(run.NumericalComputationPossible);

        // Workbook K29/K34: Vsit,u = 68.796 m/s; basic ULS pressure 2.83973 kN/m2.
        Near(68.796, run.ComputedQuantities["V_sit"], 0.01);
        Near(2839.73, run.ComputedQuantities["q_des"], 0.5);

        var ww = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "WindwardWall");
        Near(2271.79, ww.NetPressurePa, 0.5);            // workbook J46: 2.27179 kN/m2 ULS
        Near(997.10, ww.AlternateLimitStateNetPressurePa, 0.5); // workbook J47: SLS

        var lw = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "LeewardWall");
        Near(-851.92, lw.NetPressurePa, 0.5);            // workbook J52: -0.85192 kN/m2

        var rw = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "RoofWindward");
        Near(-1647.05, rw.NetPressurePa, 0.5);           // workbook I66: -1.64705 kN/m2 (Case 1 governs)

        var rl = run.SurfaceResults.Single(s => s.SurfaceOrZoneId == "RoofLeeward");
        Near(-1192.69, rl.NetPressurePa, 0.5);          // workbook I68: -1.19269 kN/m2
    }

    [Fact]
    public void WorkbookMode_ExplicitCoefficientsAlwaysWin()
    {
        var coeffs = new Dictionary<string, double>
        {
            ["Kd"] = 0.85, ["Ke"] = 1.0, ["G"] = 0.85, ["Kh"] = 0.77, ["Kzt"] = 1.0, ["GCpi"] = 0.18,
            ["Cp_Roof_Leeward"] = -0.5,
        };
        var req = Request("ASCE 7-22", coeffs, new() { ["WorkbookMethod"] = "ASCE7" }, slope: 10);
        var run = new Asce7_22CalculationStrategy().Execute(req);
        Assert.True(run.NumericalComputationPossible);
        Assert.Equal(0.77, run.ComputedQuantities["Kh"], 9); // supplied Kh not overwritten by Kz derivation
    }

    [Fact]
    public void WorkbookMode_MissingRequiredExplicit_StillBlocks()
    {
        // AS/NZS workbook mode derives Cpe but never Cpi (the workbook has no
        // internal-pressure section): without Cpi the run must still block.
        var coeffs = new Dictionary<string, double> { ["Md"] = 0.9, ["Ms"] = 1, ["Mt"] = 1, ["Mc"] = 1.05 };
        var req = Request("AS/NZS 1170.2:2021", coeffs,
            new() { ["WorkbookMethod"] = "ASNZS", ["Region"] = "D" }, exposure: "TC2");
        var run = new AsNz1170_2_2021CalculationStrategy().Execute(req);
        Assert.False(run.NumericalComputationPossible);
    }
}
