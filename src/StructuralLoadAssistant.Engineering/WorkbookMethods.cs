using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

/// <summary>
/// Calculation methods and data tables transcribed from the project owner's
/// reference workbooks (supplied 2026-10-24):
///   - "ASCE_Wind_load.xlsm"  (sheet "Windload (Imperial)" + "DATA"):
///     ASCE/SEI 7-16 Chapter 27 Directional Procedure as localised by the
///     Hawaii IBC 2018 wind provisions referenced in the workbook.
///   - "AS.NZS_Wind_load.xlsm" (sheets "Slope Roof" / "Flat roof" / "Data Sheet"):
///     AS/NZS 1170.2:2021 Sections 2-5.
///
/// Provenance matters: every table below is the WORKBOOK's data, quoted with
/// its source cells, not a licensed copy of the standards. Two obvious
/// decimal typos in the workbook's Table 4.1 grids (108 for 1.08, 118 for
/// 1.18, 95 for 0.95) are corrected here and flagged to the owner.
/// The workbook INTERPOLATE / InterpolateTable5_3B / InterpolateTable5_3C
/// VBA functions are linear / bilinear interpolation; they are reproduced
/// as <see cref="Interpolate"/> and <see cref="Bilinear"/>.
/// </summary>
public static class WorkbookMethods
{
    // ------------------------------------------------------------------
    // Interpolation helpers (workbook INTERPOLATE UDF equivalents)
    // ------------------------------------------------------------------

    /// <summary>Linear interpolation on a monotone point table, clamped at the ends (workbook INTERPOLATE).</summary>
    public static double Interpolate(IReadOnlyList<(double X, double Y)> points, double x)
    {
        if (points.Count == 0) throw new ArgumentException("Interpolation table is empty.", nameof(points));
        if (x <= points[0].X) return points[0].Y;
        if (x >= points[^1].X) return points[^1].Y;
        for (int i = 1; i < points.Count; i++)
        {
            if (x <= points[i].X)
            {
                var (x0, y0) = points[i - 1];
                var (x1, y1) = points[i];
                return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
            }
        }
        return points[^1].Y;
    }

    /// <summary>Bilinear interpolation on a rectangular grid, clamped at the edges.</summary>
    public static double Bilinear(IReadOnlyList<double> xs, IReadOnlyList<double> ys, double[,] grid, double x, double y)
    {
        double cx = ClampToAxis(xs, x, out int ix, out double fx);
        double cy = ClampToAxis(ys, y, out int iy, out double fy);
        _ = cx; _ = cy;
        double v00 = grid[iy, ix], v01 = grid[iy, ix + 1], v10 = grid[iy + 1, ix], v11 = grid[iy + 1, ix + 1];
        return v00 * (1 - fx) * (1 - fy) + v01 * fx * (1 - fy) + v10 * (1 - fx) * fy + v11 * fx * fy;
    }

    private static double ClampToAxis(IReadOnlyList<double> axis, double v, out int index, out double fraction)
    {
        if (v <= axis[0]) { index = 0; fraction = 0; return axis[0]; }
        if (v >= axis[^1]) { index = axis.Count - 2; fraction = 1; return axis[^1]; }
        for (int i = 1; i < axis.Count; i++)
        {
            if (v <= axis[i])
            {
                index = i - 1;
                fraction = (v - axis[i - 1]) / (axis[i] - axis[i - 1]);
                return v;
            }
        }
        index = axis.Count - 2; fraction = 1; return axis[^1];
    }

    // ------------------------------------------------------------------
    // ASCE workbook (ASCE/SEI 7-16, Hawaii provisions, imperial)
    // ------------------------------------------------------------------

    /// <summary>Exposure alpha / zg (ft) from DATA!T6:V9 of the ASCE workbook.</summary>
    public static bool TryExposureAlphaZg(string exposure, out double alpha, out double zgFt)
    {
        switch ((exposure ?? "").Trim().ToUpperInvariant())
        {
            case "B": alpha = 7.5; zgFt = 3280; return true;
            case "C": alpha = 9.8; zgFt = 2460; return true;
            case "D": alpha = 11.5; zgFt = 1935; return true;
            default: alpha = 0; zgFt = 0; return false;
        }
    }

    /// <summary>
    /// Velocity pressure exposure coefficient, workbook cell K35:
    /// Kz = 2.41 * (z/zg)^(2/alpha), with z floored at 15 ft.
    /// </summary>
    public static double AsceKz(double zFt, string exposure)
    {
        if (!TryExposureAlphaZg(exposure, out double alpha, out double zg)) return double.NaN;
        double z = Math.Max(zFt, 15.0);
        return 2.41 * Math.Pow(z / zg, 2.0 / alpha);
    }

    /// <summary>Topographic factor, workbook cell K27: Kzt = (1 + K1*K2*K3)^2 (Section 26.8).</summary>
    public static double AsceKzt(double k1, double k2, double k3) => Math.Pow(1 + k1 * k2 * k3, 2);

    /// <summary>Velocity pressure in psf, workbook cells K39/K41: qh = 0.00256 * Kz * Kzt * Kd * Ke * V^2 (V in mph).</summary>
    public static double AsceQhPsf(double vMph, double kz, double kzt, double kd, double ke)
        => 0.00256 * kz * kzt * kd * ke * vMph * vMph;

    private static readonly (double, double)[] LeewardWallCpTable = [(0, -0.5), (1, -0.5), (2, -0.3), (4, -0.2)];

    /// <summary>Leeward wall Cp from Figure 27.3-1 via L/B (workbook cells R121:S124 + INTERPOLATE in D128).</summary>
    public static double AsceLeewardWallCp(double lengthOverBreadth) => Interpolate(LeewardWallCpTable, lengthOverBreadth);

    /// <summary>|GCpi| by enclosure classification (workbook DATA!X2:Y5, Table 26.13-1). Null when unrecognised.</summary>
    public static double? AsceGcpiMagnitude(string enclosureClassification)
    {
        string cls = enclosureClassification ?? "";
        if (cls.Contains("Partially enclosed", StringComparison.OrdinalIgnoreCase)) return 0.55;
        if (cls.Contains("Partially open", StringComparison.OrdinalIgnoreCase)) return 0.18;
        if (cls.Contains("Open", StringComparison.OrdinalIgnoreCase)) return 0.0;
        if (cls.Contains("Enclosed", StringComparison.OrdinalIgnoreCase)) return 0.18;
        return null;
    }

    // Roof windward-slope Cp by distance zone (workbook DATA!AC2:AF9):
    // per zone, Cp pair (primary, alternate) at h/L = 0.5 and h/L = 1.0,
    // linearly interpolated on h/L and clamped outside [0.5, 1.0].
    private static readonly (double Cp1, double Cp2)[][] RoofZones =
    [
        [( -0.9, -0.18), (-1.3, -0.18)], // Zone 1: 0 to h/2
        [( -0.9, -0.18), (-0.7, -0.18)], // Zone 2: h/2 to h
        [( -0.5, -0.18), (-0.7, -0.18)], // Zone 3: h to 2h
        [( -0.3, -0.18), (-0.7, -0.18)], // Zone 4: beyond 2h
    ];

    /// <summary>Roof Cp pair for distance zone 1..4 at the given h/L (workbook DATA table + array formulas in H133:H136).</summary>
    public static (double CpPrimary, double CpAlternate) AsceRoofZoneCp(int zone, double hOverL)
    {
        if (zone < 1 || zone > 4) throw new ArgumentOutOfRangeException(nameof(zone));
        var row = RoofZones[zone - 1];
        double t = Math.Clamp((hOverL - 0.5) / 0.5, 0, 1);
        return (row[0].Cp1 + (row[1].Cp1 - row[0].Cp1) * t,
                row[0].Cp2 + (row[1].Cp2 - row[0].Cp2) * t);
    }

    // ------------------------------------------------------------------
    // AS/NZS workbook (AS/NZS 1170.2:2021)
    // ------------------------------------------------------------------

    /// <summary>
    /// Regional wind speed V_R (m/s) from the workbook Data Sheet Table 3.1
    /// equations, by region group and average recurrence interval R (years):
    /// A: 67 - 41 R^-0.1; B: 106 - 92 R^-0.1; C: 122 - 104 R^-0.1;
    /// D: 156 - 142 R^-0.1; each rounded to the nearest integer, as in the
    /// workbook cells (the Data Sheet's extra x1.1 cyclonic factor column is
    /// NOT used by the workbook's own calculation sheets and is not applied).
    /// </summary>
    public static double AsNzRegionalWindSpeed(string region, double ariYears)
    {
        if (ariYears <= 0) return double.NaN;
        double rPow = Math.Pow(ariYears, -0.1);
        double v = (region ?? "").Trim().ToUpperInvariant() switch
        {
            var s when s.StartsWith("A") => 67 - 41 * rPow,
            var s when s.StartsWith("B") => 106 - 92 * rPow,
            var s when s.StartsWith("C") => 122 - 104 * rPow,
            var s when s.StartsWith("D") => 156 - 142 * rPow,
            _ => double.NaN,
        };
        return double.IsNaN(v) ? v : Math.Round(v);
    }

    private static readonly double[] MzHeightsA = [3, 5, 10, 15, 20, 30, 40, 50, 75, 100, 150, 200];
    // Table 4.1(A) - SLS all regions; ULS regions A, B, W (workbook Data Sheet J5:N22).
    // Workbook typos corrected: TC2@20m 108 -> 1.08; TC2@50m 118 -> 1.18.
    private static readonly double[,] MzTableA =
    {
        { 0.99, 0.91, 0.83, 0.75 },
        { 1.05, 0.91, 0.83, 0.75 },
        { 1.12, 1.00, 0.83, 0.75 },
        { 1.16, 1.05, 0.89, 0.75 },
        { 1.19, 1.08, 0.94, 0.75 },
        { 1.22, 1.12, 1.00, 0.80 },
        { 1.24, 1.16, 1.04, 0.85 },
        { 1.25, 1.18, 1.07, 0.90 },
        { 1.27, 1.22, 1.12, 0.98 },
        { 1.29, 1.24, 1.16, 1.03 },
        { 1.31, 1.27, 1.21, 1.11 },
        { 1.32, 1.29, 1.24, 1.16 },
    };
    private static readonly double[] MzHeightsB = [3, 5, 10, 15, 20, 30, 40, 50, 75, 100];
    // Table 4.1(B) - ULS regions C and D only (workbook Data Sheet J25:N41).
    // Workbook typos corrected: TC3/TC4@15m 95 -> 0.95.
    private static readonly double[,] MzTableB =
    {
        { 0.90, 0.90, 0.80, 0.80 },
        { 0.95, 0.95, 0.80, 0.80 },
        { 1.00, 1.00, 0.89, 0.89 },
        { 1.07, 1.07, 0.95, 0.95 },
        { 1.13, 1.13, 1.05, 1.05 },
        { 1.20, 1.20, 1.15, 1.15 },
        { 1.25, 1.25, 1.25, 1.25 },
        { 1.29, 1.29, 1.29, 1.29 },
        { 1.35, 1.35, 1.35, 1.35 },
        { 1.40, 1.40, 1.40, 1.40 },
    };

    /// <summary>
    /// Terrain/height multiplier Mz,cat from Table 4.1 (workbook Data Sheet),
    /// linearly interpolated on height and clamped. Table (B) applies to ULS
    /// in regions C and D; Table (A) otherwise.
    /// </summary>
    public static double AsNzMzCat(double heightM, int terrainCategory, bool ultimateCyclonic)
    {
        if (terrainCategory < 1 || terrainCategory > 4) return double.NaN;
        var heights = ultimateCyclonic ? MzHeightsB : MzHeightsA;
        var table = ultimateCyclonic ? MzTableB : MzTableA;
        var points = new (double, double)[heights.Length];
        for (int i = 0; i < heights.Length; i++) points[i] = (heights[i], table[i, terrainCategory - 1]);
        return Interpolate(points, heightM);
    }

    private static readonly (double, double)[] AsNzLeewardDbTable = [(1, -0.5), (2, -0.3), (4, -0.2)];

    /// <summary>
    /// Leeward wall Cp,e (workbook array formula in J50, Table 5.2B):
    /// roof pitch above 10 deg flattens to -0.3; otherwise -0.5 for d/b &lt;= 1,
    /// -0.2 for d/b &gt;= 4, interpolated between.
    /// </summary>
    public static double AsNzLeewardWallCp(double roofPitchDeg, double depthOverBreadth)
    {
        if (roofPitchDeg > 10) return -0.3;
        if (depthOverBreadth <= 1) return -0.5;
        if (depthOverBreadth >= 4) return -0.2;
        return Interpolate(AsNzLeewardDbTable, depthOverBreadth);
    }

    /// <summary>Side wall Cp,e zones by distance from the windward edge (workbook Slope Roof rows 57-60, Table 5.2).</summary>
    public static readonly (double FromH, double ToH, double Cp)[] AsNzSideWallZones =
        [(0, 1, -0.65), (1, 2, -0.5), (2, 3, -0.3), (3, double.PositiveInfinity, -0.2)];

    private static readonly double[] RoofPitchRows = [10, 15, 20, 25, 30, 35];
    private static readonly double[] HOverDRows = [0.25, 0.5, 1.0];
    // Table 5.3(B) upwind slope, Case 1 (workbook Data Sheet C53:G58 even columns C,E,G).
    private static readonly double[,] UpwindCase1 =
    {
        { -0.7, -0.9, -1.3 },
        { -0.5, -0.7, -1.0 },
        { -0.3, -0.4, -0.7 },
        { -0.2, -0.3, -0.5 },
        { -0.2, -0.2, -0.3 },
        {  0.0, -0.2, -0.2 },
    };
    // Table 5.3(B) upwind slope, Case 2 (columns D,F,H).
    private static readonly double[,] UpwindCase2 =
    {
        { -0.3, -0.4, -0.6 },
        {  0.0, -0.3, -0.5 },
        {  0.2,  0.0, -0.3 },
        {  0.3,  0.2,  0.0 },
        {  0.4,  0.3,  0.2 },
        {  0.5,  0.4,  0.3 },
    };

    /// <summary>
    /// Upwind roof slope Cp,e from Table 5.3(B) (workbook VBA
    /// InterpolateTable5_3B): bilinear on (roof pitch, h/d), pitch clamped to
    /// the tabulated 10-35 deg range (the workbook's 45 deg row is a note,
    /// "0, 0.8 sin alpha", not a numeric row).
    /// </summary>
    public static double AsNzUpwindRoofCp(double hOverD, double roofPitchDeg, int caseNumber)
        => Bilinear(HOverDRows, RoofPitchRows, caseNumber == 2 ? UpwindCase2 : UpwindCase1, hOverD, roofPitchDeg);

    private static readonly double[] DownwindPitchRows = [10, 15, 20];
    // Table 5.3(C) downwind slope (workbook Data Sheet C68:E70).
    private static readonly double[,] DownwindGrid =
    {
        { -0.3, -0.5, -0.7 },
        { -0.5, -0.5, -0.6 },
        { -0.6, -0.6, -0.6 },
    };

    /// <summary>
    /// Downwind roof slope Cp,e from Table 5.3(C) (workbook VBA
    /// InterpolateTable5_3C): bilinear on (roof pitch, h/d) up to 20 deg;
    /// at 25 deg and above the workbook's b/d rule applies:
    /// -0.6 (b/d &lt;= 3), -0.06*(7 + b/d) (3 &lt; b/d &lt; 8), -0.9 (b/d &gt;= 8).
    /// Between 20 and 25 deg the grid value and the rule are interpolated.
    /// </summary>
    public static double AsNzDownwindRoofCp(double hOverD, double roofPitchDeg, double breadthOverDepth)
    {
        double grid = Bilinear(HOverDRows, DownwindPitchRows, DownwindGrid, hOverD, Math.Min(roofPitchDeg, 20));
        if (roofPitchDeg <= 20) return grid;
        double rule = breadthOverDepth <= 3 ? -0.6
            : breadthOverDepth >= 8 ? -0.9
            : -0.06 * (7 + breadthOverDepth);
        if (roofPitchDeg >= 25) return rule;
        return grid + (rule - grid) * (roofPitchDeg - 20) / 5.0;
    }

    // Flat roof zones (workbook Data Sheet A28:E32 + interpolation rows 34-37):
    // per zone, Cp at h/d = 0.5 and h/d = 1.0 for Case 1 and Case 2.
    private static readonly (double C1Lo, double? C1Hi, double C2Lo, double? C2Hi)[] FlatRoofZones =
    [
        (-0.9, -1.3, -0.4, -0.6), // 0 to 0.5h
        (-0.9, -0.7, -0.4, -0.3), // 0.5h to 1h
        (-0.5, -0.7,  0.0, -0.3), // 1h to 2h
        (-0.3,  null,  0.1,  null), // 2h to 3h
        (-0.2,  null,  0.2,  null), // beyond 3h
    ];

    /// <summary>
    /// Flat roof zone Cp,e (workbook Flat roof F67:F80 formulas): zone 1..5,
    /// linear on h/d between 0.5 and 1.0, clamped; zones 4-5 have no h/d = 1
    /// value in the workbook ("-") and return null there.
    /// </summary>
    public static double? AsNzFlatRoofZoneCp(int zone, double hOverD, int caseNumber)
    {
        if (zone < 1 || zone > 5) throw new ArgumentOutOfRangeException(nameof(zone));
        var z = FlatRoofZones[zone - 1];
        double lo = caseNumber == 2 ? z.C2Lo : z.C1Lo;
        double? hi = caseNumber == 2 ? z.C2Hi : z.C1Hi;
        if (hOverD <= 0.5) return lo;
        if (hi is null) return hOverD >= 1.0 ? null : lo;
        if (hOverD >= 1.0) return hi.Value;
        return lo + (hi.Value - lo) * (hOverD - 0.5) / 0.5;
    }
}

/// <summary>
/// Workbook-method request derivation. When a request carries
/// Options["WorkbookMethod"] = "ASCE7" or "ASNZS", the strategies call the
/// matching Derive method first: coefficients the workbook DERIVES from its
/// tables/formulas are injected as <see cref="CoefficientInputEvidence"/>
/// (provenance Unverified, document = the reference workbook) for any key
/// the engineer did not supply explicitly. Explicit inputs always win.
/// </summary>
public static class WorkbookRequestDerivation
{
    public const string MethodKey = "WorkbookMethod";
    private const string AsceWorkbook = "ASCE_Wind_load.xlsm (owner reference workbook, ASCE/SEI 7-16 Ch.27 + Hawaii provisions)";
    private const string AsNzWorkbook = "AS.NZS_Wind_load.xlsm (owner reference workbook, AS/NZS 1170.2:2021)";

    public static bool IsWorkbookMode(EngineeringCalculationRequest request, string method)
        => request.Options.TryGetValue(MethodKey, out var m) && string.Equals(m, method, StringComparison.OrdinalIgnoreCase);

    private static CoefficientInputEvidence Derived(string name, double value, string unit, string doc, string clause, string notes)
        => new(name, value, unit, DataProvenance.Unverified, doc, clause, notes, DateTimeOffset.UtcNow);

    private static void Inject(ImmutableDictionary<string, CoefficientInputEvidence>.Builder b, CoefficientInputEvidence ev)
    {
        if (!b.ContainsKey(ev.CoefficientName)) b.Add(ev.CoefficientName, ev);
    }

    private static double OptionalCoeff(EngineeringCalculationRequest r, string name, double fallback)
        => r.ExplicitCoefficients.TryGetValue(name, out var c) && double.IsFinite(c.Value) ? c.Value : fallback;

    /// <summary>
    /// ASCE workbook derivation ("Windload (Imperial)" sheet): Kzt from
    /// K1/K2/K3, Kh = Kz at z (Section B), GCpi from the enclosure table
    /// times the Ri reduction factor, and Figure 27.3-1 wall/roof Cp values
    /// (leeward interpolated on L/B; roof windward = zone 1 primary Cp;
    /// roof leeward = zone 3 primary Cp per the workbook's distance zones).
    /// Returns the augmented request plus workbook extras for the run
    /// (SLS velocity pressure ratio and the full roof-zone table).
    /// </summary>
    public static (EngineeringCalculationRequest Request, double SlsQRatio, ImmutableArray<CalculationTraceNode> ExtraTraces) DeriveAsce(EngineeringCalculationRequest request)
    {
        var b = request.ExplicitCoefficients.ToBuilder();
        var traces = ImmutableArray.CreateBuilder<CalculationTraceNode>();

        string exposure = "C";
        foreach (char c in (request.ExposureOrTerrain ?? "").ToUpperInvariant())
        {
            if (c is 'B' or 'C' or 'D') { exposure = c.ToString(); break; }
        }

        double zFt = OptionalCoeff(request, "Z_FT", request.ReferenceHeightM * 3.28084);
        double kz = WorkbookMethods.AsceKz(zFt, exposure);
        if (double.IsFinite(kz))
            Inject(b, Derived("Kh", kz, "-", AsceWorkbook, "Section 26.10 (workbook K35)",
                $"Derived: Kz = 2.41*(z/zg)^(2/alpha) at z = {zFt:F1} ft, Exposure {exposure} (workbook DATA alpha/zg)."));

        double k1 = OptionalCoeff(request, "K1", 0), k2 = OptionalCoeff(request, "K2", 0), k3 = OptionalCoeff(request, "K3", 0);
        Inject(b, Derived("Kzt", WorkbookMethods.AsceKzt(k1, k2, k3), "-", AsceWorkbook, "Section 26.8 (workbook K27)",
            $"Derived: Kzt = (1 + K1*K2*K3)^2 with K1 = {k1}, K2 = {k2}, K3 = {k3}."));

        double ri = OptionalCoeff(request, "Ri", 1.0);
        if (WorkbookMethods.AsceGcpiMagnitude(request.EnclosureClassification) is double mag)
            Inject(b, Derived("GCpi", mag * ri, "-", AsceWorkbook, "Table 26.13-1 (workbook K101)",
                $"Derived: |GCpi| from enclosure table x Ri reduction factor ({ri})."));

        bool alongX = Math.Abs(Math.Sin(request.WindAngleDegrees * Math.PI / 180.0)) < 0.5;
        double len = alongX ? request.BuildingLengthM : request.BuildingWidthM;
        double brd = alongX ? request.BuildingWidthM : request.BuildingLengthM;
        double h = request.EaveHeightM > 0 ? request.EaveHeightM : request.ReferenceHeightM;
        double loverB = len > 0 && brd > 0 ? len / brd : 1;
        double hOverL = len > 0 ? h / len : 0.5;

        Inject(b, Derived("Cp_Windward", 0.8, "-", AsceWorkbook, "Figure 27.3-1 (workbook D127)", "Workbook wall table: windward Cp = +0.8."));
        Inject(b, Derived("Cp_Leeward", WorkbookMethods.AsceLeewardWallCp(loverB), "-", AsceWorkbook, "Figure 27.3-1 (workbook D128)",
            $"Derived: leeward Cp interpolated on L/B = {loverB:F3}."));
        Inject(b, Derived("Cp_Side", -0.7, "-", AsceWorkbook, "Figure 27.3-1 (workbook D129)", "Workbook wall table: side wall Cp = -0.7."));
        var zone1 = WorkbookMethods.AsceRoofZoneCp(1, hOverL);
        Inject(b, Derived("Cp_Roof_Windward", zone1.CpPrimary, "-", AsceWorkbook, "Figure 27.3-1 (workbook H133)",
            $"Derived: roof zone 1 (0 to h/2) primary Cp at h/L = {hOverL:F3}; alternate Cp {zone1.CpAlternate:F2}. Full zone table in trace TR-WB-ASCE-ZONES."));
        var zone3 = WorkbookMethods.AsceRoofZoneCp(3, hOverL);
        Inject(b, Derived("Cp_Roof_Leeward", zone3.CpPrimary, "-", AsceWorkbook, "Figure 27.3-1 (workbook H135)",
            "Workbook convention: the distance zones span the whole roof; the leeward slope takes the zone 3 (h to 2h) primary Cp."));

        var zoneLines = new List<string>();
        for (int z = 1; z <= 4; z++)
        {
            var (cp1, cp2) = WorkbookMethods.AsceRoofZoneCp(z, hOverL);
            zoneLines.Add($"Zone {z}: Cp {cp1:F3} / alternate {cp2:F2}");
        }
        traces.Add(new CalculationTraceNode(
            "TR-WB-ASCE-ZONES", "Workbook Roof Zones", "Roof Cp zones",
            "Figure 27.3-1 distance zones, interpolated on h/L",
            string.Join("; ", zoneLines), "see note", 0, "-",
            "ASCE workbook DATA!AC2:AF9",
            "Workbook roof table (both Cp values per zone). The engine's RoofWindward surface uses zone 1 primary Cp; RoofLeeward uses zone 3 primary Cp.",
            [], []));

        double slsRatio = 1;
        if (request.ExplicitCoefficients.TryGetValue("V_SLS", out var vsls) && double.IsFinite(vsls.Value) && request.BasicWindSpeed > 0)
            slsRatio = Math.Pow(vsls.Value / request.BasicWindSpeed, 2);

        return (request with { ExplicitCoefficients = b.ToImmutable() }, slsRatio, traces.ToImmutable());
    }

    /// <summary>
    /// AS/NZS workbook derivation ("Slope Roof" / "Flat roof" sheets):
    /// V_R from the Table 3.1 equations (Region + ARI for the request's
    /// limit state), Mz,cat from Table 4.1 at the reference height, and
    /// Cpe values folded with the Cfig factor chain (Ka*Kc*Kl*Kp) exactly
    /// as the workbook computes Cfig = Cp,e*Ka*Kc*Kl*Kp. Cdyn (default 1)
    /// and the SLS pressure ratio are returned for run post-processing.
    /// </summary>
    public static (EngineeringCalculationRequest Request, double SlsQRatio, double Cdyn, ImmutableArray<CalculationTraceNode> ExtraTraces) DeriveAsNz(EngineeringCalculationRequest request)
    {
        var b = request.ExplicitCoefficients.ToBuilder();
        var traces = ImmutableArray.CreateBuilder<CalculationTraceNode>();
        var derived = request;

        string region = request.Options.TryGetValue("Region", out var reg) ? reg : "";
        double ariUls = OptionalCoeff(request, "ARI_ULS", 500);
        double ariSls = OptionalCoeff(request, "ARI_SLS", 25);
        double vrUls = WorkbookMethods.AsNzRegionalWindSpeed(region, ariUls);
        double vrSls = WorkbookMethods.AsNzRegionalWindSpeed(region, ariSls);
        bool isUls = request.LimitState == LimitState.Uls;
        double vrActive = isUls ? vrUls : vrSls;
        if (double.IsFinite(vrActive) && vrActive > 0)
        {
            derived = derived with { BasicWindSpeed = vrActive };
            traces.Add(new CalculationTraceNode(
                "TR-WB-ASNZ-VR", "Workbook Regional Wind Speed", "V_R",
                "Table 3.1 equations (workbook Data Sheet)",
                $"Region {region}: V_R(ULS, R={ariUls}) = {vrUls:F0} m/s; V_R(SLS, R={ariSls}) = {vrSls:F0} m/s",
                $"{vrActive:F0} m/s", vrActive, "m/s",
                "AS/NZS 1170.2:2021 Table 3.1 (workbook equations)",
                "Derived: regional wind speed for the active limit state replaces the request's basic wind speed in workbook mode.",
                [], []));
        }

        int tc = 2;
        foreach (char c in request.ExposureOrTerrain ?? "")
        {
            if (c is >= '1' and <= '4') { tc = c - '0'; break; }
        }
        // Table choice: the workbook's Data Sheet labels Table 4.1(B) as
        // "ULS regions C and D only", yet its own Slope Roof worked example
        // (Region D, ULS) uses the Table 4.1(A) value 0.91. The worked
        // example governs the default; Options["MzTable"] = "B" selects
        // the C/D ultimate table explicitly.
        bool useTableB = request.Options.TryGetValue("MzTable", out var mtTab)
            && string.Equals(mtTab, "B", StringComparison.OrdinalIgnoreCase);
        double mz = WorkbookMethods.AsNzMzCat(request.ReferenceHeightM, tc, useTableB);
        if (double.IsFinite(mz))
            Inject(b, Derived("Mz_cat", mz, "-", AsNzWorkbook, "Table 4.1 (workbook Data Sheet)",
                $"Derived: Mz,cat at z = {request.ReferenceHeightM:F2} m, Terrain Category {tc}, Table {(useTableB ? "4.1(B)" : "4.1(A)")}, interpolated on height."));

        // Cfig factor chain (workbook J45: Cfig = Cp,e*Ka*Kc*Kl*Kp).
        double ka = OptionalCoeff(request, "Ka", 1), kc = OptionalCoeff(request, "Kc", 1),
               kl = OptionalCoeff(request, "Kl", 1), kp = OptionalCoeff(request, "Kp", 1);
        double cdyn = OptionalCoeff(request, "Cdyn", 1);
        bool factorsValid = ka > 0 && kc > 0 && kl > 0 && kp > 0 && cdyn > 0
            && double.IsFinite(ka) && double.IsFinite(kc) && double.IsFinite(kl) && double.IsFinite(kp) && double.IsFinite(cdyn);
        double chain = ka * kc * kl * kp;

        if (factorsValid)
        {
            bool alongX = Math.Abs(Math.Sin(request.WindAngleDegrees * Math.PI / 180.0)) < 0.5;
            double depth = alongX ? request.BuildingLengthM : request.BuildingWidthM;
            double breadth = alongX ? request.BuildingWidthM : request.BuildingLengthM;
            double dOverB = depth > 0 && breadth > 0 ? depth / breadth : 1;
            double hRef = request.ReferenceHeightM;
            double hOverD = depth > 0 ? hRef / depth : 0.5;
            double pitch = request.RoofSlopeDegrees;

            Inject(b, Derived("Cpe_Windward", 0.8 * chain, "-", AsNzWorkbook, "Table 5.2A (workbook J44/J45)",
                $"Workbook: windward wall Cp,e = +0.8; Cfig = Cp,e*Ka*Kc*Kl*Kp = {0.8 * chain:F3}."));
            double lwCp = WorkbookMethods.AsNzLeewardWallCp(pitch, dOverB);
            Inject(b, Derived("Cpe_Leeward", lwCp * chain, "-", AsNzWorkbook, "Table 5.2B (workbook J50)",
                $"Derived: leeward Cp,e = {lwCp:F3} at pitch {pitch:F1} deg, d/b = {dOverB:F3}; folded with Cfig chain {chain:F3}."));
            Inject(b, Derived("Cpe_Side", -0.65 * chain, "-", AsNzWorkbook, "Table 5.2 (workbook F57)",
                "Workbook side wall zones: -0.65 (0 to h), -0.5 (h to 2h), -0.3 (2h to 3h), -0.2 (beyond 3h); governing first zone folded with Cfig chain."));
            double up1 = WorkbookMethods.AsNzUpwindRoofCp(hOverD, pitch, 1);
            double up2 = WorkbookMethods.AsNzUpwindRoofCp(hOverD, pitch, 2);
            double upGov = Math.Abs(up1) >= Math.Abs(up2) ? up1 : up2;
            Inject(b, Derived("Cpe_Roof_Windward", upGov * chain, "-", AsNzWorkbook, "Table 5.3B (workbook F66/F67)",
                $"Derived: upwind slope Cp,e Case 1 = {up1:F3}, Case 2 = {up2:F3} at h/d = {hOverD:F3}, pitch {pitch:F1} deg; governing case folded with Cfig chain {chain:F3}."));
            double dnCp = WorkbookMethods.AsNzDownwindRoofCp(hOverD, pitch, dOverB > 0 ? 1.0 / dOverB : 1);
            Inject(b, Derived("Cpe_Roof_Leeward", dnCp * chain, "-", AsNzWorkbook, "Table 5.3C (workbook F68)",
                $"Derived: downwind slope Cp,e = {dnCp:F3} at h/d = {hOverD:F3}, pitch {pitch:F1} deg; folded with Cfig chain {chain:F3}. Crosswind slope takes the same value in the workbook."));
        }

        double slsRatio = 1;
        if (double.IsFinite(vrUls) && double.IsFinite(vrSls) && vrUls > 0)
            slsRatio = isUls ? Math.Pow(vrSls / vrUls, 2) : Math.Pow(vrUls / vrSls, 2);

        derived = derived with { ExplicitCoefficients = b.ToImmutable() };
        return (derived, slsRatio, factorsValid ? cdyn : 1.0, traces.ToImmutable());
    }
}

/// <summary>
/// Post-processing shared by both workbook modes: folds Cdyn into every
/// pressure quantity (the workbook multiplies Cdyn into the design
/// pressure; the core strategies compute with Cdyn = 1) and fills each
/// surface's alternate-limit-state net pressure from the velocity
/// pressure ratio, which is exact because pressures scale linearly
/// with q for fixed coefficients.
/// </summary>
public static class WorkbookRunPostProcess
{
    public static EngineeringCalculationRun Apply(
        EngineeringCalculationRun run,
        double alternateLimitStateQRatio,
        double cdyn,
        ImmutableArray<CalculationTraceNode> extraTraces,
        string qKey,
        string qAltKey)
    {
        var computed = run.ComputedQuantities.ToBuilder();
        if (run.ComputedQuantities.TryGetValue(qKey, out double q))
            computed[qAltKey] = q * alternateLimitStateQRatio;
        computed["Cdyn"] = cdyn;

        var traces = run.TraceNodes.AddRange(extraTraces);
        if (!run.NumericalComputationPossible || (cdyn == 1.0 && alternateLimitStateQRatio == 1.0))
        {
            return run with { TraceNodes = traces, ComputedQuantities = computed.ToImmutable() };
        }

        var surfaces = run.SurfaceResults.Select(s =>
        {
            double net = s.NetPressurePa * cdyn;
            return s with
            {
                ExternalPressurePa = s.ExternalPressurePa * cdyn,
                InternalPressurePa = s.InternalPressurePa * cdyn,
                NetPressurePa = net,
                NetPressurePositiveInternalPa = s.NetPressurePositiveInternalPa * cdyn,
                NetPressureNegativeInternalPa = s.NetPressureNegativeInternalPa * cdyn,
                GlobalPressureVectorPa = EngineeringMath.GlobalForce(net, s.OutwardNormal),
                AlternateLimitStateNetPressurePa = net * alternateLimitStateQRatio,
            };
        }).ToImmutableArray();

        var profile = run.VelocityPressureProfile.Select(p => new PressureSample(p.ElevationM, p.PressurePa * cdyn)).ToImmutableArray();
        if (computed.TryGetValue(qKey, out double qNow)) computed[qKey] = qNow * cdyn;
        if (computed.TryGetValue(qAltKey, out double qAlt)) computed[qAltKey] = qAlt * cdyn;

        return run with
        {
            TraceNodes = traces,
            ComputedQuantities = computed.ToImmutable(),
            SurfaceResults = surfaces,
            VelocityPressureProfile = profile,
        };
    }
}
