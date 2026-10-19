using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public enum AsceRiskCategory { I, II, III, IV }
public enum AsceExposure { B, C, D }
public enum AsNzTerrainCategory { TC1, TC2, TC2Point5, TC3, TC4 }
public sealed record AsceWindInput(double BasicWindSpeedMPerS, AsceRiskCategory RiskCategory,
    AsceExposure Exposure, double GroundElevationM, double MeanRoofHeightM, LimitState DesignLevel, string WindSpeedSource);
public sealed record AsNzWindInput(string WindRegion, int ReturnPeriodYears, AsNzTerrainCategory Terrain,
    double ReferenceHeightM, LimitState DesignLevel);
public sealed record WindCalculationResult(StandardsGateResult Gate, WindCalculationRun? Run,
    ImmutableArray<string> RequiredStages);

/// <summary>Metadata-only standard boundaries. No normative coefficient dataset is bundled.</summary>
public static class StandardCalculators
{
    public static WindCalculationResult Asce7_22(AsceWindInput input, StandardManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(manifest);
        Numeric.Positive(input.BasicWindSpeedMPerS); Numeric.Finite(input.GroundElevationM); Numeric.Positive(input.MeanRoofHeightM); Numeric.Required(input.WindSpeedSource);
        if (!Enum.IsDefined(input.RiskCategory) || !Enum.IsDefined(input.Exposure) || !Enum.IsDefined(input.DesignLevel) || manifest.Edition is not ("7-22" or "ASCE/SEI 7-22")) throw new ArgumentException("ASCE/SEI 7-22 inputs and matching edition required.");
        return new(StandardsGate.Check(manifest,DataProvenance.Unverified),null,
            ["Risk category", "Wind speed source/design level", "Exposure", "Topography", "Ground elevation", "Reference height", "Velocity pressure", "Gust effect", "External coefficients", "Internal pressure states", "Wall pressure", "Roof pressure zones", "Normalized wind states"]);
    }
    public static WindCalculationResult AsNz1170_2_2021(AsNzWindInput input, StandardManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(manifest);
        Numeric.Required(input.WindRegion); Numeric.Positive(input.ReturnPeriodYears); Numeric.Positive(input.ReferenceHeightM);
        if (!Enum.IsDefined(input.Terrain) || !Enum.IsDefined(input.DesignLevel) || manifest.Edition is not ("2021" or "AS/NZS 1170.2:2021")) throw new ArgumentException("AS/NZS 1170.2:2021 inputs and matching edition required.");
        var gate=StandardsGate.Check(manifest,DataProvenance.Unverified);
        if(!manifest.Revisions.Contains("Amendment 1:2023") || !manifest.Revisions.Contains("Amendment 2:2024"))
            gate=gate with {MissingData=gate.MissingData.Add("Manifest must include Amendment 1:2023 and Amendment 2:2024.")};
        return new(gate,null,["Wind region", "Return period/design level", "Regional wind speed", "Terrain category", "Terrain/height multiplier", "Directional multiplier", "Shielding and topographic multipliers", "Climate multiplier", "Site wind speed", "Dynamic pressure", "Aerodynamic coefficients", "Internal pressure", "External wall regions", "Roof zones/cases", "Normalized wind states"]);
    }
}
