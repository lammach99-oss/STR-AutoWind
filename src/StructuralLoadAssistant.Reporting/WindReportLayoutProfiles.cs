namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Authoritative layout profile selecting between the project owner's
/// reference workbook presentation sheets:
/// - AsceImperial: Hawaii Housing / ASCE workbook 'Windload (Imperial)' (A1:P225)
/// - AsNzSlopeRoof: AS/NZS workbook 'Slope Roof' (A1:N103)
/// - AsNzFlatRoof: AS/NZS workbook 'Flat roof' (A1:N124)
/// </summary>
public enum WindReportLayoutProfile
{
    AsceImperial,
    AsNzSlopeRoof,
    AsNzFlatRoof
}

public static class WindReportLayoutProfileSelector
{
    public static WindReportLayoutProfile Select(string standardEdition, string roofForm, double roofSlopeDegrees)
    {
        if (!string.IsNullOrEmpty(standardEdition) &&
            (standardEdition.Contains("AS/NZS", StringComparison.OrdinalIgnoreCase) ||
             standardEdition.Contains("1170", StringComparison.OrdinalIgnoreCase)))
        {
            if (roofSlopeDegrees <= 1.0 || roofForm.Equals("Flat", StringComparison.OrdinalIgnoreCase))
            {
                return WindReportLayoutProfile.AsNzFlatRoof;
            }
            return WindReportLayoutProfile.AsNzSlopeRoof;
        }

        return WindReportLayoutProfile.AsceImperial;
    }
}
