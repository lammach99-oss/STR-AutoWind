using System.Collections.Immutable;
using System.Globalization;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public sealed class Asce7_22CalculationStrategy : IWindCalculationStrategy
{
    public string StandardFamily => "ASCE";
    public string StandardEdition => "ASCE/SEI 7-22";
    public ImmutableArray<string> SupportedProcedures => [
        "Chapter 27: MWFRS Directional Procedure",
        "Chapter 28: MWFRS Envelope Procedure",
        "Chapter 30: Components and Cladding"
    ];

    public EngineeringCalculationRun Execute(EngineeringCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = ImmutableArray.CreateBuilder<CalculationIssue>();
        var traces = ImmutableArray.CreateBuilder<CalculationTraceNode>();
        var computed = new Dictionary<string, double>();
        var profile = ImmutableArray.CreateBuilder<PressureSample>();
        var surfaces = ImmutableArray.CreateBuilder<SurfacePressureResult>();

        bool hasSupp3 = request.SelectedAmendments.Any(a => a.Contains("Supplement 3", StringComparison.OrdinalIgnoreCase));
        if (!hasSupp3)
        {
            issues.Add(new CalculationIssue(
                "ASCE_SUPPLEMENT_NOTICE",
                CalculationIssueSeverity.Information,
                "Standard Revisions",
                "ASCE 7-22 Supplement 3 published 2024. Confirm project jurisdiction adopted Supplement 3 provisions."
            ));
        }

        double v = request.BasicWindSpeed;
        if (v <= 0)
        {
            issues.Add(new CalculationIssue("INVALID_SPEED", CalculationIssueSeverity.Error, "Basic Wind Speed", "Basic wind speed must be positive."));
        }
        computed["V"] = v;
        traces.Add(new CalculationTraceNode(
            "TR-ASCE-01",
            "Wind Speed",
            "Basic Wind Speed V",
            "V",
            $"V = {v:F2} m/s",
            $"{v:F2} m/s",
            v,
            "m/s",
            "ASCE 7-22 Section 26.5.1",
            "Nominal 3-s gust wind speed at 10 m in Exposure C for the Risk Category.",
            [],
            []
        ));

        bool hasKd = TryGetRequiredCoeff(request, "Kd", "Wind Directionality Factor", "ASCE 7-22 Table 26.6-1", traces, issues, out double kd);
        bool hasKzt = TryGetRequiredCoeff(request, "Kzt", "Topographic Factor", "ASCE 7-22 Section 26.8.2", traces, issues, out double kzt);
        bool hasKe = TryGetRequiredCoeff(request, "Ke", "Ground Elevation Factor", "ASCE 7-22 Section 26.9", traces, issues, out double ke);
        bool hasKh = TryGetRequiredCoeff(request, "Kh", "Velocity Pressure Exposure Coefficient at h", "ASCE 7-22 Table 26.10-1", traces, issues, out double kh);
        bool hasG = TryGetRequiredCoeff(request, "G", "Gust-Effect Factor", "ASCE 7-22 Section 26.11.1", traces, issues, out double g);
        bool hasGcpi = TryGetRequiredCoeff(request, "GCpi", "Internal Pressure Coefficient", "ASCE 7-22 Table 26.13-1", traces, issues, out double gcpi);

        bool hasAllFactors = hasKd && hasKzt && hasKe && hasKh && hasG && hasGcpi && v > 0;

        if (!hasAllFactors)
        {
            issues.Add(new CalculationIssue(
                "BLOCKED_REQUIRED_NORMATIVE_DATA",
                CalculationIssueSeverity.Error,
                "Velocity Pressure & Aerodynamic Factors",
                "Calculation blocked: Required engineer-provided coefficients or normative tables are missing."
            ));

            return new EngineeringCalculationRun(
                "calc-asce-blocked-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow,
                request,
                DataProvenance.Unverified,
                false, false, false, false, false,
                "BLOCKED / REQUIRED COEFFICIENT EVIDENCE MISSING",
                profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }

        computed["Kd"] = kd;
        computed["Kzt"] = kzt;
        computed["Ke"] = ke;
        computed["Kh"] = kh;
        computed["G"] = g;
        computed["GCpi"] = gcpi;

        double densityFactor = 0.613 * ke;
        computed["DensityFactor"] = densityFactor;
        traces.Add(new CalculationTraceNode(
            "TR-ASCE-02",
            "Velocity Pressure Equation",
            "qz Constant",
            "0.613 * Ke",
            $"0.613 * {ke:F3}",
            $"{densityFactor:F4} N*s^2/m^4",
            densityFactor,
            "N*s^2/m^4",
            "ASCE 7-22 Section 26.10.1 (Eq. 26.10-1)",
            "Velocity pressure numerical constant in SI metric units.",
            ["TR-ASCE-01"],
            []
        ));

        double h = request.RidgeHeightM > 0 && request.EaveHeightM > 0 ? (request.EaveHeightM + request.RidgeHeightM) / 2.0 : request.ReferenceHeightM;
        if (h <= 0)
        {
            issues.Add(new CalculationIssue("INVALID_HEIGHT", CalculationIssueSeverity.Error, "Geometry", "Mean roof height must be strictly positive."));
            return new EngineeringCalculationRun(
                "calc-asce-blocked-geom-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow, request, DataProvenance.Unverified, false, false, false, false, false,
                "BLOCKED / INVALID GEOMETRY", profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }
        computed["h"] = h;

        double qh = densityFactor * kh * kzt * kd * (v * v);
        computed["qh"] = qh;
        traces.Add(new CalculationTraceNode(
            "TR-ASCE-03",
            "Velocity Pressure at Mean Roof Height",
            "qh",
            "0.613 * Ke * Kh * Kzt * Kd * V^2",
            $"0.613 * {ke:F3} * {kh:F3} * {kzt:F3} * {kd:F3} * ({v:F2})^2",
            $"{qh:F2} Pa",
            qh,
            "Pa",
            "ASCE 7-22 Section 26.10.1 (Eq. 26.10-1)",
            "Velocity pressure evaluated at mean roof height h.",
            ["TR-ASCE-02"],
            []
        ));

        double[] elevations = [3.0, 6.0, 9.0, h];
        foreach (var z in elevations.Distinct().OrderBy(x => x))
        {
            double kz_z = z >= h ? kh : kh * Math.Pow(Math.Max(z, 4.5) / h, 0.2);
            double qz = densityFactor * kz_z * kzt * kd * (v * v);
            profile.Add(new PressureSample(z, qz));
        }

        bool hasCpWw = TryGetRequiredCoeff(request, "Cp_Windward", "Windward Wall External Coefficient", "ASCE 7-22 Figure 27.3-1", traces, issues, out double cp_ww);
        bool hasCpLw = TryGetRequiredCoeff(request, "Cp_Leeward", "Leeward Wall External Coefficient", "ASCE 7-22 Figure 27.3-1", traces, issues, out double cp_lw);
        bool hasCpSide = TryGetRequiredCoeff(request, "Cp_Side", "Side Wall External Coefficient", "ASCE 7-22 Figure 27.3-1", traces, issues, out double cp_side);
        bool hasCpRw = TryGetRequiredCoeff(request, "Cp_Roof_Windward", "Windward Roof External Coefficient", "ASCE 7-22 Figure 27.3-1", traces, issues, out double cp_rw);
        bool hasCpRl = TryGetRequiredCoeff(request, "Cp_Roof_Leeward", "Leeward Roof External Coefficient", "ASCE 7-22 Figure 27.3-1", traces, issues, out double cp_rl);

        if (!hasCpWw || !hasCpLw || !hasCpSide || !hasCpRw || !hasCpRl)
        {
            issues.Add(new CalculationIssue(
                "BLOCKED_ROOF_WALL_COEFFICIENTS",
                CalculationIssueSeverity.Error,
                "External Coefficients",
                "Surface pressure calculation blocked: Explicit Cp coefficients not provided."
            ));
            return new EngineeringCalculationRun(
                "calc-asce-partial-" + Guid.NewGuid().ToString("N")[..8],
                DateTimeOffset.UtcNow, request, DataProvenance.Unverified, false, false, false, false, false,
                "BLOCKED / INCOMPLETE SURFACE COEFFICIENTS",
                profile.ToImmutable(), surfaces.ToImmutable(), traces.ToImmutable(), issues.ToImmutable(), computed.ToImmutableDictionary()
            );
        }

        bool isNegIp = request.Options.TryGetValue("InternalPressure", out var ipOpt) && ipOpt.Contains("NEG", StringComparison.OrdinalIgnoreCase);
        double activeGcpi = isNegIp ? -Math.Abs(gcpi) : Math.Abs(gcpi);
        double pi = qh * activeGcpi;
        computed["pi"] = pi;

        traces.Add(new CalculationTraceNode(
            "TR-ASCE-04",
            "Internal Pressure",
            "pi",
            "qh * (GCpi)",
            $"{qh:F2} * ({activeGcpi:F2})",
            $"{pi:F2} Pa",
            pi,
            "Pa",
            "ASCE 7-22 Section 26.13",
            "Internal pressure acting normal to interior building surfaces.",
            ["TR-ASCE-03"],
            []
        ));

        void AddSurface(string id, string desc, double q_surf, double cp, Vec3 normal)
        {
            double p_ext = q_surf * g * cp;
            double p_net = p_ext - pi;
            Vec3 forceVec = EngineeringMath.GlobalForce(p_net, normal);

            surfaces.Add(new SurfacePressureResult(
                id, desc, cp, p_ext, activeGcpi, pi, p_net, normal, forceVec
            ));

            traces.Add(new CalculationTraceNode(
                $"TR-ASCE-SURF-{id}",
                "Surface Net Pressure",
                $"p_net ({id})",
                "q * G * Cp - qh * (GCpi)",
                $"{q_surf:F2} * {g:F2} * ({cp:F2}) - ({pi:F2})",
                $"{p_net:F2} Pa",
                p_net,
                "Pa",
                "ASCE 7-22 Section 27.3.1 (Eq. 27.3-1)",
                $"{desc}: positive indicates inward acting pressure.",
                ["TR-ASCE-03", "TR-ASCE-04"],
                []
            ));
        }

        AddSurface("WindwardWall", "Windward Wall (MWFRS)", qh, cp_ww, new Vec3(-1, 0, 0));
        AddSurface("LeewardWall", "Leeward Wall (MWFRS)", qh, cp_lw, new Vec3(1, 0, 0));
        AddSurface("SideWall", "Side Wall (MWFRS)", qh, cp_side, new Vec3(0, 1, 0));
        AddSurface("RoofWindward", "Roof Windward Slope", qh, cp_rw, new Vec3(0, -0.6, 0.8).Unit());
        AddSurface("RoofLeeward", "Roof Leeward Slope", qh, cp_rl, new Vec3(0, 0.6, 0.8).Unit());

        DataProvenance prov = request.ExplicitCoefficients.Values.Any(c => c.Provenance == DataProvenance.SyntheticDemoFixture)
            ? DataProvenance.SyntheticDemoFixture
            : DataProvenance.Unverified;

        string qualStatus = prov == DataProvenance.SyntheticDemoFixture
            ? "SYNTHETIC EXAMPLE - NOT FOR DESIGN"
            : "ENGINEER REVIEW - INPUTS / STANDARD APPLICABILITY NOT INDEPENDENTLY VERIFIED";

        bool ownerReviewed = request.Options.TryGetValue("OwnerReviewed", out var orVal) && bool.TryParse(orVal, out bool orBool) && orBool;

        return new EngineeringCalculationRun(
            "calc-asce-" + Guid.NewGuid().ToString("N")[..8],
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
                $"TR-ASCE-COEFF-{name}",
                "Explicit Coefficient Input",
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
            $"MISSING_COEFF_{name}",
            CalculationIssueSeverity.Error,
            "Explicit Factor Input",
            $"Required coefficient '{name}' ({description}) is missing. Reference: {reference}."
        ));
        return false;
    }
}