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

        bool hasMd = TryGetRequiredCoeff(request, "Md", "Wind Direction Multiplier", "AS/NZS 1170.2:2021 Section 3.3", traces, issues, out double md);
        bool hasMzCat = TryGetRequiredCoeff(request, "Mz_cat", "Terrain/Height Multiplier at reference height", "AS/NZS 1170.2:2021 Section 4.2, Table 4.1", traces, issues, out double mz_cat);
        bool hasMs = TryGetRequiredCoeff(request, "Ms", "Shielding Multiplier", "AS/NZS 1170.2:2021 Section 4.3", traces, issues, out double ms);
        bool hasMt = TryGetRequiredCoeff(request, "Mt", "Topographic Multiplier", "AS/NZS 1170.2:2021 Section 4.4", traces, issues, out double mt);
        bool hasMc = TryGetRequiredCoeff(request, "Mc", "Climate Multiplier", "AS/NZS 1170.2:2021 Section 3.4", traces, issues, out double mc);

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

        bool isNegIp = request.Options.TryGetValue("InternalPressure", out var ipOpt) && ipOpt.Contains("NEG", StringComparison.OrdinalIgnoreCase);
        double activeCpi = isNegIp ? -Math.Abs(cpi) : Math.Abs(cpi);
        double pi = qdes * activeCpi;
        computed["p_i"] = pi;

        traces.Add(new CalculationTraceNode(
            "TR-ASNZ-04",
            "Internal Pressure",
            "p_i",
            "q_des * C_p,i",
            $"{qdes:F2} * ({activeCpi:F2})",
            $"{pi:F2} Pa",
            pi,
            "Pa",
            "AS/NZS 1170.2:2021 Section 5.3",
            "Internal pressure acting normal to interior surfaces.",
            ["TR-ASNZ-03"],
            []
        ));

        void AddSurface(string id, string desc, double cpe, Vec3 normal)
        {
            double p_ext = qdes * cpe;
            double p_net = p_ext - pi;
            Vec3 forceVec = EngineeringMath.GlobalForce(p_net, normal);

            surfaces.Add(new SurfacePressureResult(
                id, desc, cpe, p_ext, activeCpi, pi, p_net, normal, forceVec
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
                $"{desc}: positive indicates inward acting pressure.",
                ["TR-ASNZ-03", "TR-ASNZ-04"],
                []
            ));
        }

        AddSurface("WindwardWall", "Windward Wall", cpe_ww, new Vec3(-1, 0, 0));
        AddSurface("LeewardWall", "Leeward Wall", cpe_lw, new Vec3(1, 0, 0));
        AddSurface("SideWall", "Side Wall", cpe_side, new Vec3(0, 1, 0));
        AddSurface("RoofWindward", "Windward Roof", cpe_rw, new Vec3(0, -0.6, 0.8).Unit());
        AddSurface("RoofLeeward", "Leeward Roof", cpe_rl, new Vec3(0, 0.6, 0.8).Unit());

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
        out double value)
    {
        if (req.ExplicitCoefficients.TryGetValue(name, out var coeff))
        {
            value = coeff.Value;
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