using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

/// <summary>Expected intermediate factor within an independent standard calculation benchmark.</summary>
public sealed record ExpectedIntermediateFactor(
    string Name,
    string Clause,
    double ExpectedValue,
    double Tolerance,
    string Unit);

/// <summary>Expected net surface pressure for a specific zone and wind state.</summary>
public sealed record ExpectedZonePressure(
    string ZoneId,
    string StateId,
    double ExpectedNetPressurePa,
    double AbsoluteTolPa,
    double RelativeTol,
    string Direction);

/// <summary>
/// Machine-readable benchmark specification for independent standard calculation verification.
/// Used to qualify numerical calculation implementations against independently verified manual notes.
/// </summary>
public sealed record StandardReferenceCalculationFixture(
    string FixtureId,
    string Title,
    StandardFamily Family,
    string Edition,
    ImmutableArray<string> Amendments,
    string ProcedureId,
    string SourceClauseReference,
    ImmutableDictionary<string, string> CategoricalInputs,
    ImmutableDictionary<string, double> NumericalInputs,
    ImmutableArray<ExpectedIntermediateFactor> ExpectedFactors,
    ImmutableArray<ExpectedZonePressure> ExpectedPressures,
    EngineeringReviewTrace ReviewTrace,
    DataRightsClassification RightsClassification,
    bool IsSynthetic);

/// <summary>Comparison result for a single factor or pressure assertion.</summary>
public sealed record ReferenceComparison(
    string ItemName,
    string Category,
    double Expected,
    double Actual,
    double Tolerance,
    bool Passed,
    string Details);

/// <summary>Overall evaluation outcome of a reference calculation benchmark.</summary>
public sealed record ReferenceCalculationEvaluation(
    string FixtureId,
    bool Passed,
    bool IsNumericalMatch,
    bool CanAuthorizeProductionCalculation,
    ImmutableArray<ReferenceComparison> Comparisons,
    ImmutableArray<string> Issues,
    string Summary)
{
    public ReferenceCalculationEvaluation(
        string fixtureId,
        bool passed,
        ImmutableArray<ReferenceComparison> comparisons,
        ImmutableArray<string> issues,
        string summary)
        : this(fixtureId, passed, passed, false, comparisons, issues, summary)
    {
    }
}

/// <summary>Evaluates analytical calculation results against an independent reference fixture.</summary>
public static class ReferenceCalculationValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static StandardReferenceCalculationFixture LoadFixture(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!System.IO.File.Exists(filePath))
            throw new System.IO.FileNotFoundException($"Reference calculation fixture not found at '{filePath}'.", filePath);

        var json = System.IO.File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<StandardReferenceCalculationFixture>(json, JsonOptions)
            ?? throw new System.IO.InvalidDataException("Failed to deserialize StandardReferenceCalculationFixture.");
    }

    public static ReferenceCalculationEvaluation Evaluate(
        StandardReferenceCalculationFixture fixture,
        IDictionary<string, double> actualFactors,
        IDictionary<string, double> actualPressures)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(actualFactors);
        ArgumentNullException.ThrowIfNull(actualPressures);

        var comps = ImmutableArray.CreateBuilder<ReferenceComparison>();
        var issues = ImmutableArray.CreateBuilder<string>();

        // Detect empty fixture (must never pass)
        if (fixture.ExpectedFactors.IsEmpty && fixture.ExpectedPressures.IsEmpty)
        {
            issues.Add("Reference calculation fixture contains zero expected factors and zero expected pressures. Cannot evaluate empty fixture.");
            return new ReferenceCalculationEvaluation(
                fixture.FixtureId,
                false,
                false,
                false,
                ImmutableArray<ReferenceComparison>.Empty,
                issues.ToImmutable(),
                "Reference Calculation Benchmark FAILED • Fixture contains zero expected assertions.");
        }

        // Validate factors definitions
        var seenFactorNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ef in fixture.ExpectedFactors)
        {
            if (!seenFactorNames.Add(ef.Name))
            {
                issues.Add($"Duplicate expected intermediate factor '{ef.Name}' declared in reference fixture.");
            }
            if (!double.IsFinite(ef.ExpectedValue))
            {
                issues.Add($"Expected intermediate factor '{ef.Name}' has non-finite value '{ef.ExpectedValue}'.");
            }
            if (ef.Tolerance < 0 || !double.IsFinite(ef.Tolerance))
            {
                issues.Add($"Expected intermediate factor '{ef.Name}' has invalid negative or non-finite tolerance '{ef.Tolerance}'.");
            }
        }

        // 1. Evaluate Intermediate Factors
        foreach (var ef in fixture.ExpectedFactors)
        {
            if (!actualFactors.TryGetValue(ef.Name, out double actualVal))
            {
                issues.Add($"Missing intermediate factor '{ef.Name}' in actual calculation output.");
                comps.Add(new ReferenceComparison(ef.Name, "IntermediateFactor", ef.ExpectedValue, double.NaN, ef.Tolerance, false, "Factor missing from calculation output"));
                continue;
            }

            double diff = Math.Abs(actualVal - ef.ExpectedValue);
            bool pass = diff <= ef.Tolerance;
            if (!pass) issues.Add($"Factor '{ef.Name}' failed: expected {ef.ExpectedValue:F4}, got {actualVal:F4} (diff {diff:F4} > tol {ef.Tolerance:F4}).");
            comps.Add(new ReferenceComparison(ef.Name, "IntermediateFactor", ef.ExpectedValue, actualVal, ef.Tolerance, pass, $"Expected {ef.ExpectedValue:F4} {ef.Unit}, actual {actualVal:F4} {ef.Unit}"));
        }

        // 2. Evaluate Pressures
        var seenPressureKeys = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ep in fixture.ExpectedPressures)
        {
            string key = $"{ep.ZoneId}:{ep.StateId}";
            if (!seenPressureKeys.Add(key))
            {
                issues.Add($"Duplicate expected pressure key '{key}' declared in reference fixture.");
            }
            if (!double.IsFinite(ep.ExpectedNetPressurePa))
            {
                issues.Add($"Expected pressure '{key}' has non-finite value '{ep.ExpectedNetPressurePa}'.");
            }
            if (ep.AbsoluteTolPa < 0 || !double.IsFinite(ep.AbsoluteTolPa) ||
                ep.RelativeTol < 0 || !double.IsFinite(ep.RelativeTol))
            {
                issues.Add($"Expected pressure '{key}' has invalid negative or non-finite tolerance.");
            }
        }

        foreach (var ep in fixture.ExpectedPressures)
        {
            string key = $"{ep.ZoneId}:{ep.StateId}";
            // Strict (ZoneId, StateId) key matching only! Fallback to zone-only is strictly prohibited.
            if (!actualPressures.TryGetValue(key, out double actualPress))
            {
                issues.Add($"Missing pressure result for exact key '{key}'.");
                comps.Add(new ReferenceComparison(key, "ZonePressure", ep.ExpectedNetPressurePa, double.NaN, ep.AbsoluteTolPa, false, "Exact (ZoneId:StateId) pressure missing from calculation output"));
                continue;
            }
            if (!double.IsFinite(actualPress))
            {
                issues.Add($"Actual pressure for '{key}' is non-finite '{actualPress}'.");
                comps.Add(new ReferenceComparison(key, "ZonePressure", ep.ExpectedNetPressurePa, actualPress, ep.AbsoluteTolPa, false, "Actual pressure is non-finite"));
                continue;
            }

            double diff = Math.Abs(actualPress - ep.ExpectedNetPressurePa);
            bool pass = diff <= ep.AbsoluteTolPa || (ep.ExpectedNetPressurePa != 0 && diff / Math.Abs(ep.ExpectedNetPressurePa) <= ep.RelativeTol);
            if (!pass) issues.Add($"Pressure for '{key}' failed: expected {ep.ExpectedNetPressurePa:F2} Pa, got {actualPress:F2} Pa (diff {diff:F2} Pa > tol {ep.AbsoluteTolPa:F2} Pa).");
            comps.Add(new ReferenceComparison(key, "ZonePressure", ep.ExpectedNetPressurePa, actualPress, ep.AbsoluteTolPa, pass, $"Expected {ep.ExpectedNetPressurePa:F2} Pa, actual {actualPress:F2} Pa"));
        }

        bool isNumericalMatch = issues.Count == 0 && comps.Count > 0 && comps.All(c => c.Passed);
        bool canAuthorizeProduction = false; // Strictly false in Sprint 4A.1!
        bool passed = isNumericalMatch;
        string summary = passed
            ? $"Reference Calculation Benchmark NUMERICAL MATCH • All {comps.Count} comparisons satisfied. (Production authorization: strictly disabled in Sprint 4A.1)"
            : $"Reference Calculation Benchmark FAILED • {issues.Count} issues found.";

        return new ReferenceCalculationEvaluation(
            fixture.FixtureId,
            passed,
            isNumericalMatch,
            canAuthorizeProduction,
            comps.ToImmutable(),
            issues.ToImmutable(),
            summary);
    }
}

