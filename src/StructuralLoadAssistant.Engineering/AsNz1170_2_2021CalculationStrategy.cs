using System.Collections.Immutable;
using System.Globalization;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public sealed class AsNz1170_2_2021CalculationStrategy : IWindCalculationStrategy
{
    public string StandardFamily => "AS/NZS";
    public string StandardEdition => "AS/NZS 1170.2:2021";
    public ImmutableArray<string> SupportedProcedures => [
        "Section 2: Site Wind Speed",
        "Section 3: Design Wind Speed and Dynamic Pressure",
        "Section 4: Aerodynamic Shape Factor",
        "Section 5: Internal and External Pressures"
    ];

    public EngineeringCalculationRun Execute(EngineeringCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProcedureName == AsNz2021ReviewCalculation.Procedure) return AsNz2021ReviewCalculation.Execute(request);
        if (!WorkbookRequestDerivation.IsWorkbookMode(request, "ASNZS"))
            return ExecuteCore(request);

        // Workbook method (owner's AS.NZS_Wind_load.xlsm): V_R and Mz,cat
        // are derived from the workbook's Table 3.1 equations / Table 4.1
        // grids and Cpe values arrive pre-folded with the Cfig factor
        // chain; the standard pipeline then runs unchanged. Cdyn and the
        // alternate-limit-state ratio are applied as post-scales.
        var (derived, altRatio, cdyn, extraTraces) = WorkbookRequestDerivation.DeriveAsNz(request);
        var run = ExecuteCore(derived);
        return WorkbookRunPostProcess.Apply(run, altRatio, cdyn, extraTraces, "q_des", "q_des_alt");
    }

    private EngineeringCalculationRun ExecuteCore(EngineeringCalculationRequest request)
    {
        var issues = ImmutableArray.CreateBuilder<CalculationIssue>();
        var traces = ImmutableArray.CreateBuilder<CalculationTraceNode>();
        var computed = new Dictionary<string, double>();
        var profile = ImmutableArray.CreateBuilder<PressureSample>();
        var surfaces = ImmutableArray.CreateBuilder<SurfacePressureResult>();

        bool hasAmd1 = request.SelectedAmendments.Any(a => a.Contains("Amendment 1:2023", StringComparison.OrdinalIgnoreCase));
        bool hasAmd2 = request.SelectedAmendments.Any(a => a.Contains("Amendment 2:2024", StringComparison.OrdinalIgnoreCase));
        if (!hasAmd1 || !hasAmd2)
        {
            issues.Add(new CalculationIssue(
                "ASNZS_AMENDMENT_NOTICE",
                CalculationIssueSeverity.Information,
                "Standard Amendments",
                "AS/NZS 1170.2:2021 Amendment 1:2023 and Amendment 2:2024 should be evaluated for local jurisdictional applicability."
            ));
        }

        double vr = request.BasicWindSpeed;
        if (vr <= 0)
        {
            issues.Add(new CalculationIssue("INVALID_VR", CalculationIssueSeverity.Error, "Regional Wind Speed", "Regional wind speed V_R must be positive."));
        }
        computed["V_R"] = vr;
        traces.Add(new CalculationTraceNode(
            "TR-ASNZ-01",
            "Regional Wind Speed",
            "V_R",
            "V_R",
            $"V_R = {vr:F2} m/s",
            $"{vr:F2} m/s",
            vr,
            "m/s",
            "AS/NZS 1170.2:2021 Section 3.2, Table 3.1",
            "Regional 3-second gust wind speed for design average recurrence interval.",
            [],
            []
        ));

        bool hasMd = TryGetRequiredCoeff(request, "Md", "Wind Direction Multiplier", "AS/NZS 1170.2:2021 Section 3.3", traces, issues, out double md,
            minExclusive: 0, rangeRule: "Md > 0");
        bool hasMzCat = TryGetRequiredCoeff(request, "Mz_cat", "Terrain/Height Multiplier at reference height", "AS/NZS 1170.2:2021 Section 4.2, Table 4.1", traces, issues, out double mz_cat,
            minExclusive: 0, rangeRule: "Mz_cat > 0");
        bool hasMs = TryGetRequiredCoeff(request, "Ms", "Shielding Multiplier", "AS/NZS 1170.2:2021 Section 4.3", traces, issues, out double ms,
            minExclusive: 0, rangeRule: "Ms > 0");
        bool hasMt = TryGetRequiredCoeff(request, "Mt", "Topographic Multiplier", "AS/NZS 1170.2:2021 Section 4.4", traces, issues, out double mt,
            minExclusive: 0, rangeRule: "Mt > 0");
        bool hasMc = TryGetRequiredCoeff(request, "Mc", "Climate Multiplier", "AS/NZS 1170.2:2021 Section 3.4", traces, issues, out double mc,
            minExclusive: 0, rangeRule: "Mc > 0");

        bool hasAllMultipliers = hasMd && hasMzCat && hasMs && hasMt && hasMc && vr > 0;
        if (!hasAllMultipliers)
        {
            issues.Add(new CalculationIssue(
                "BLOCKED_REQUIRED_SITE_MULTIPLIERS",
                CalculationIssueSeverity.Error,
                "Site Wind Speed Multipliers",
                "Calculation blocked: Required engineer-provided multipliers (Md, Mz_cat, Ms, Mt, Mc) are missing."
            ));

            return new EngineeringCalculationRun(
                "calc-asnz-blocked-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow,
                request,
                DataProvenance.Unverified,
                false, false, false, false, false,
                "BLOCKED / REQUIRED MULTIPLIER EVIDENCE MISSING",
                profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }

        // Phase 1: roof slope drives the roof surface normals; reject
        // unusable geometry up front.
        double slopeDeg = request.RoofSlopeDegrees;
        if (!double.IsFinite(slopeDeg) || slopeDeg < 0 || slopeDeg >= 90)
        {
            issues.Add(new CalculationIssue("INVALID_ROOF_SLOPE", CalculationIssueSeverity.Error, "Geometry", "Roof slope must be finite and in [0, 90) degrees."));
            return new EngineeringCalculationRun(
                "calc-asnz-blocked-geom-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow, request, DataProvenance.Unverified, false, false, false, false, false,
                "BLOCKED / INVALID GEOMETRY", profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }

        computed["Md"] = md;
        computed["Mz_cat"] = mz_cat;
        computed["Ms"] = ms;
        computed["Mt"] = mt;
        computed["Mc"] = mc;

        double vsit = vr * md * (mz_cat * ms * mt * mc);
        computed["V_sit"] = vsit;
        traces.Add(new CalculationTraceNode(
            "TR-ASNZ-02",
            "Site Wind Speed",
            "V_sit,beta",
            "V_R * Md * (Mz_cat * Ms * Mt * Mc)",
            $"{vr:F2} * {md:F3} * ({mz_cat:F3} * {ms:F3} * {mt:F3} * {mc:F3})",
            $"{vsit:F2} m/s",
            vsit,
            "m/s",
            "AS/NZS 1170.2:2021 Section 2.2 (Eq. 2.2)",
            "Site wind speed for the building orientation and location.",
            ["TR-ASNZ-01"],
            []
        ));

        double qdes = 0.6 * (vsit * vsit);
        computed["q_des"] = qdes;
        traces.Add(new CalculationTraceNode(
            "TR-ASNZ-03",
            "Design Dynamic Pressure",
            "q_des",
            "0.5 * rho_air * (V_sit)^2 = 0.6 * (V_sit)^2",
            $"0.6 * ({vsit:F2})^2",
            $"{qdes:F2} Pa",
            qdes,
            "Pa",
            "AS/NZS 1170.2:2021 Section 2.4",
            "Dynamic wind pressure at reference height.",
            ["TR-ASNZ-02"],
            []
        ));

        double zRef = request.ReferenceHeightM > 0 ? request.ReferenceHeightM : 10.0;
        double[] elevations = [3.0, 6.0, 9.0, zRef];
        foreach (var z in elevations.Distinct().OrderBy(x => x))
        {
            double mz_z = z >= zRef ? mz_cat : mz_cat * Math.Pow(Math.Max(z, 3.0) / zRef, 0.15);
            double vz = vr * md * (mz_z * ms * mt * mc);
            double qz = 0.6 * (vz * vz);
            profile.Add(new PressureSample(z, qz));
        }

        bool hasCpeWw = TryGetRequiredCoeff(request, "Cpe_Windward", "Windward Wall External Coefficient", "AS/NZS 1170.2:2021 Section 5.4.1", traces, issues, out double cpe_ww);
        bool hasCpeLw = TryGetRequiredCoeff(request, "Cpe_Leeward", "Leeward Wall External Coefficient", "AS/NZS 1170.2:2021 Section 5.4.1", traces, issues, out double cpe_lw);
        bool hasCpeSide = TryGetRequiredCoeff(request, "Cpe_Side", "Side Wall External Coefficient", "AS/NZS 1170.2:2021 Section 5.4.1", traces, issues, out double cpe_side);
        bool hasCpeRw = TryGetRequiredCoeff(request, "Cpe_Roof_Windward", "Windward Roof External Coefficient", "AS/NZS 1170.2:2021 Section 5.4.2", traces, issues, out double cpe_rw);
        bool hasCpeRl = TryGetRequiredCoeff(request, "Cpe_Roof_Leeward", "Leeward Roof External Coefficient", "AS/NZS 1170.2:2021 Section 5.4.2", traces, issues, out double cpe_rl);
        bool hasCpi = TryGetRequiredCoeff(request, "Cpi", "Internal Pressure Coefficient", "AS/NZS 1170.2:2021 Section 5.3", traces, issues, out double cpi);

        if (!hasCpeWw || !hasCpeLw || !hasCpeSide || !hasCpeRw || !hasCpeRl || !hasCpi)
        {
            issues.Add(new CalculationIssue(
                "BLOCKED_ASNZS_AERODYNAMIC_FACTORS",
                CalculationIssueSeverity.Error,
                "Aerodynamic Factors",
                "Calculation blocked: Required external/internal aerodynamic shape factors missing."
            ));
            return new EngineeringCalculationRun(
                "calc-asnz-partial-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow, request, DataProvenance.Unverified, false, false, false, false, false,
                "BLOCKED / INCOMPLETE AERODYNAMIC SHAPE FACTORS",
                profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }

        // Phase 1 envelope: BOTH internal-pressure signs are computed for
        // every surface (the supplied |Cpi| is enveloped over +/-; AS/NZS
        // Table 5.1 case selection by openings remains engineer input). The
        // legacy Options["InternalPressure"] text flag no longer selects a
        // single case.
        double cpiAbs = Math.Abs(cpi);
        double piPos = qdes * cpiAbs;
        double piNeg = -piPos;
        computed["p_i"] = piPos;
        computed["p_i_pos"] = piPos;
        computed["p_i_neg"] = piNeg;

        traces.Add(new CalculationTraceNode(
            "TR-ASNZ-04",
            "Internal Pressure",
            "p_i",
            "q_des * (+/-C_p,i)",
            $"{qdes:F2} * (+/-{cpiAbs:F2})",
            $"+{piPos:F2} Pa / {piNeg:F2} Pa",
            piPos,
            "Pa",
            "AS/NZS 1170.2:2021 Section 5.3",
            "Internal pressure acting normal to interior surfaces. Both signs are evaluated per surface; each surface reports its governing (larger absolute) net pressure and carries both case values.",
            ["TR-ASNZ-03"],
            []
        ));

        void AddSurface(string id, string desc, double cpe, Vec3 normal)
        {
            double p_ext = qdes * cpe;
            double netPos = p_ext - piPos;
            double netNeg = p_ext - piNeg;
            bool negGoverns = Math.Abs(netNeg) > Math.Abs(netPos);
            double p_net = negGoverns ? netNeg : netPos;
            double activeCpi = negGoverns ? -cpiAbs : cpiAbs;
            double pi = negGoverns ? piNeg : piPos;
            Vec3 forceVec = EngineeringMath.GlobalForce(p_net, normal);

            surfaces.Add(new SurfacePressureResult(
                id, desc, cpe, p_ext, activeCpi, pi, p_net, normal, forceVec,
                netPos, netNeg, negGoverns ? "NEGATIVE" : "POSITIVE"
            ));

            traces.Add(new CalculationTraceNode(
                $"TR-ASNZ-SURF-{id}",
                "Surface Net Pressure",
                $"p_net ({id})",
                "q_des * C_pe - p_i",
                $"{qdes:F2} * ({cpe:F2}) - ({pi:F2})",
                $"{p_net:F2} Pa",
                p_net,
                "Pa",
                "AS/NZS 1170.2:2021 Section 5.2",
                $"{desc}: positive indicates inward acting pressure. Envelope: +Cpi net {netPos:F2} Pa; -Cpi net {netNeg:F2} Pa; governing case {(negGoverns ? "NEGATIVE" : "POSITIVE")}.",
                ["TR-ASNZ-03", "TR-ASNZ-04"],
                []
            ));
        }

        // Phase 1: roof normals derive from the actual roof slope, not a
        // hard-coded 36.9 deg vector.
        double slopeRad = slopeDeg * Math.PI / 180.0;
        Vec3 roofWindwardNormal = new Vec3(0, -Math.Sin(slopeRad), Math.Cos(slopeRad)).Unit();
        Vec3 roofLeewardNormal = new Vec3(0, Math.Sin(slopeRad), Math.Cos(slopeRad)).Unit();

        AddSurface("WindwardWall", "Windward Wall", cpe_ww, new Vec3(-1, 0, 0));
        AddSurface("LeewardWall", "Leeward Wall", cpe_lw, new Vec3(1, 0, 0));
        AddSurface("SideWall", "Side Wall", cpe_side, new Vec3(0, 1, 0));
        AddSurface("RoofWindward", "Windward Roof", cpe_rw, roofWindwardNormal);
        AddSurface("RoofLeeward", "Leeward Roof", cpe_rl, roofLeewardNormal);

        DataProvenance prov = request.ExplicitCoefficients.Values.Any(c => c.Provenance == DataProvenance.SyntheticDemoFixture)
            ? DataProvenance.SyntheticDemoFixture
            : DataProvenance.Unverified;

        string qualStatus = prov == DataProvenance.SyntheticDemoFixture
            ? "SYNTHETIC EXAMPLE - NOT FOR DESIGN"
            : "ENGINEER REVIEW - INPUTS / STANDARD APPLICABILITY NOT INDEPENDENTLY VERIFIED";

        bool ownerReviewed = request.Options.TryGetValue("OwnerReviewed", out var orVal) && bool.TryParse(orVal, out bool orBool) && orBool;

        return new EngineeringCalculationRun(
            "calc-asnz-" + Guid.NewGuid().ToString("N")[..8],
            DateTimeOffset.UtcNow,
            request,
            prov,
            true, false, false, ownerReviewed, false,
            qualStatus,
            profile.ToImmutable(),
            surfaces.ToImmutable(),
            traces.ToImmutable(),
            issues.ToImmutable(),
            computed.ToImmutableDictionary()
        );
    }

    private static bool TryGetRequiredCoeff(
        EngineeringCalculationRequest req,
        string name,
        string description,
        string reference,
        ImmutableArray<CalculationTraceNode>.Builder traces,
        ImmutableArray<CalculationIssue>.Builder issues,
        out double value,
        double? minExclusive = null,
        double? minInclusive = null,
        double? maxInclusive = null,
        string? rangeRule = null)
    {
        if (req.ExplicitCoefficients.TryGetValue(name, out var coeff))
        {
            value = coeff.Value;
            // Phase 1: a present-but-invalid multiplier/factor blocks exactly
            // like a missing one; NaN/Infinity or out-of-range values must
            // never flow into computed pressures.
            if (!double.IsFinite(value)
                || (minExclusive.HasValue && !(value > minExclusive.Value))
                || (minInclusive.HasValue && !(value >= minInclusive.Value))
                || (maxInclusive.HasValue && !(value <= maxInclusive.Value)))
            {
                issues.Add(new CalculationIssue(
                    $"INVALID_COEFF_{name}",
                    CalculationIssueSeverity.Error,
                    "Explicit Multiplier Input",
                    $"Parameter '{name}' ({description}) has invalid value {value}. Required: {(rangeRule ?? "a finite value")}. Reference: {reference}."
                ));
                return false;
            }
            traces.Add(new CalculationTraceNode(
                $"TR-ASNZ-COEFF-{name}",
                "Explicit Input Multiplier",
                name,
                "User Provided Value",
                $"{name} = {coeff.Value:F3} {coeff.Unit}",
                $"{coeff.Value:F3} {coeff.Unit}",
                coeff.Value,
                coeff.Unit,
                $"{coeff.Document} {coeff.ClauseOrTable}",
                $"Source: {coeff.Provenance}. {coeff.SourceNotes}",
                [],
                []
            ));
            return true;
        }

        value = 0;
        issues.Add(new CalculationIssue(
            $"MISSING_MULTIPLIER_{name}",
            CalculationIssueSeverity.Error,
            "Explicit Multiplier Input",
            $"Required parameter '{name}' ({description}) is missing. Reference: {reference}."
        ));
        return false;
    }
}
