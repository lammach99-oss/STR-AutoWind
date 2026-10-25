using System.Globalization;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Display/report units for a calculation, selected by governing standard
/// (product decision, 2026-10-24, extended 2026-10-25): ASCE work is presented
/// in imperial units (lb/ft2, lb/ft, lb force, ft, mph); AS/NZS work in metric
/// kilo units (kN/m2, kN/m, kN, m, m/s). The calculation engines remain SI
/// internally (Pa, N/m, N, m, m/s); conversion happens only at this presentation
/// boundary, so no computed value changes - only how it is rendered in the UX and reports.
/// Within this product, "lb" in engineering force tables explicitly denotes pound-force.
/// </summary>
public sealed record ReportUnitProfile(
    string SystemName,
    string PressureUnit,
    double PressureFactorFromPa,
    string LineLoadUnit,
    double LineLoadFactorFromNPerM,
    string LengthUnit,
    double LengthFactorFromM,
    string SpeedUnit,
    double SpeedFactorFromMPerS,
    string ForceUnit = "N",
    double ForceFactorFromN = 1.0)
{
    // 1 Pa = 0.3048^2 / 4.4482216152605 lb/ft2
    // 1 N/m = 0.3048 / 4.4482216152605 lb/ft
    // 1 N = 0.22480894387096 lb (force)
    public static ReportUnitProfile ImperialAsce { get; } = new(
        "Imperial (US customary)",
        "lb/ft2", 0.0208854337883711,
        "lb/ft", 0.0685217658567918,
        "ft", 3.280839895013123,
        "mph", 2.2369362920544025,
        "lb", 0.22480894387096);

    // 1 Pa = 0.001 kN/m2
    // 1 N/m = 0.001 kN/m
    // 1 N = 0.001 kN
    public static ReportUnitProfile MetricKiloAsNz { get; } = new(
        "Metric (SI, kilo)",
        "kN/m2", 0.001,
        "kN/m", 0.001,
        "m", 1.0,
        "m/s", 1.0,
        "kN", 0.001);

    /// <summary>AS/NZS (or any edition mentioning 1170) is metric; everything else defaults to ASCE imperial.</summary>
    public static ReportUnitProfile ForStandard(string? standardEdition)
    {
        if (!string.IsNullOrEmpty(standardEdition) &&
            (standardEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) ||
             standardEdition.Contains("1170", StringComparison.OrdinalIgnoreCase)))
        {
            return MetricKiloAsNz;
        }
        return ImperialAsce;
    }

    private static string F(double value, int decimals) =>
        value.ToString("F" + decimals, CultureInfo.InvariantCulture);

    public double PressureValue(double pa) => pa * PressureFactorFromPa;
    public double LineLoadValue(double nPerM) => nPerM * LineLoadFactorFromNPerM;
    public double LengthValue(double m) => m * LengthFactorFromM;
    public double SpeedValue(double mPerS) => mPerS * SpeedFactorFromMPerS;
    public double ForceValue(double n) => n * ForceFactorFromN;

    public string Pressure(double pa, int decimals = 2) => $"{F(PressureValue(pa), decimals)} {PressureUnit}";
    public string LineLoad(double nPerM, int decimals = 2) => $"{F(LineLoadValue(nPerM), decimals)} {LineLoadUnit}";
    public string Length(double m, int decimals = 2) => $"{F(LengthValue(m), decimals)} {LengthUnit}";
    public string Speed(double mPerS, int decimals = 2) => $"{F(SpeedValue(mPerS), decimals)} {SpeedUnit}";
    public string Force(double n, int decimals = 2) => $"{F(ForceValue(n), decimals)} {ForceUnit}";

    /// <summary>
    /// Renders a trace-node result in this profile when the node's canonical
    /// unit is convertible (Pa, N, or m/s); otherwise returns the node's own
    /// result text unchanged (dimensionless and other units pass through).
    /// </summary>
    public string ConvertTraceResult(double numericValue, string? outputUnit, string fallbackResultWithUnit) =>
        outputUnit switch
        {
            "Pa" => Pressure(numericValue),
            "m/s" => Speed(numericValue),
            "N" => Force(numericValue),
            _ => fallbackResultWithUnit,
        };
}
