using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class StandardsDataFoundationTests
{
    private static readonly string FixturesRoot = Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/standards");

    private static string GetFixtureDir(string subPath)
    {
        var path = Path.Combine(FixturesRoot, subPath);
        if (!Directory.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "fixtures/standards", subPath);
        }
        return path;
    }

    private static string GetFixtureFile(string relPath)
    {
        var path = Path.Combine(FixturesRoot, relPath);
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "fixtures/standards", relPath);
        }
        return path;
    }

    [Fact]
    public void LoadFromDirectory_Asce7_22_SyntheticBundle_ParsesSuccessfully()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        Assert.True(Directory.Exists(dir), $"Directory must exist: {dir}");

        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        Assert.NotNull(bundle);
        Assert.Equal("ASCE7-22-SYNTHETIC-BUNDLE-01", bundle.Manifest.BundleId);
        Assert.Equal(StandardFamily.Asce7, bundle.Manifest.Family);
        Assert.Equal("7-22", bundle.Manifest.Edition);
        Assert.Equal(DataRightsClassification.SyntheticTestData, bundle.Manifest.Rights);
        Assert.NotEmpty(bundle.Records);
        Assert.Equal(6, bundle.Records.Length); // 2 MWFRS + 2 CC + 2 IP
    }

    [Fact]
    public void LoadFromDirectory_AsNz1170_2_2021_SyntheticBundle_ParsesSuccessfully()
    {
        var dir = GetFixtureDir("asnz1170-2-2021-synthetic");
        Assert.True(Directory.Exists(dir), $"Directory must exist: {dir}");

        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        Assert.NotNull(bundle);
        Assert.Equal("ASNZ1170-2-2021-SYNTHETIC-BUNDLE-01", bundle.Manifest.BundleId);
        Assert.Equal(StandardFamily.AsNz1170_2, bundle.Manifest.Family);
        Assert.Equal("2021", bundle.Manifest.Edition);
        Assert.Contains("Amendment 1:2023", bundle.Manifest.Amendments);
        Assert.Contains("Amendment 2:2024", bundle.Manifest.Amendments);
        Assert.Equal(8, bundle.Records.Length); // 2 reg + 2 terrain + 2 shape + 2 ip
    }

    [Fact]
    public void Validator_Validates_ComponentChecksums_DetectsTampering()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Intentionally tamper manifest expected hash
        var tamperedManifest = bundle.Manifest with
        {
            ComponentFiles = bundle.Manifest.ComponentFiles.SetItem("coefficients_mwfrs.json", "0000000000000000000000000000000000000000000000000000000000000000")
        };
        var tamperedBundle = new StandardsDataBundle(tamperedManifest, bundle.Records, dir);

        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_HASH_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_SyntheticData_FromProductionCalculation()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var report = StandardsDataBundleValidator.Validate(bundle, provenance: DataProvenance.SyntheticDemoFixture);

        Assert.Equal(StandardsGateOutcome.UnqualifiedData, report.Outcome);
        Assert.False(report.CanCalculateNormativePressures);
        Assert.Contains(report.Issues, i => i.Code == "STD_SYNTHETIC_PROMOTION_BLOCKED");
    }

    [Fact]
    public void Validator_Rejects_UnknownRights()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);
        var unknownRightsManifest = bundle.Manifest with { Rights = DataRightsClassification.Unknown };
        var testBundle = new StandardsDataBundle(unknownRightsManifest, bundle.Records);

        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_RIGHTS_UNKNOWN");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_MissingMandatoryCoverageCategory()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Remove MWFRS records
        var filteredRecords = bundle.Records
            .Where(r => r.ParameterCategory != "ExternalPressure_MWFRS")
            .ToImmutableArray();

        var testBundle = new StandardsDataBundle(bundle.Manifest, filteredRecords);
        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_MANDATORY_CATEGORY_EMPTY" && i.RecordKey == "ExternalPressure_MWFRS");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_DuplicateRecordKeys()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Duplicate the first record
        var duplicateRecords = bundle.Records.Add(bundle.Records[0]);
        var testBundle = new StandardsDataBundle(bundle.Manifest, duplicateRecords);

        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_DUPLICATE_KEY");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_NonFiniteValues()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var badRecord = bundle.Records[0] with { Value = double.NaN };
        var badRecords = bundle.Records.SetItem(0, badRecord);
        var testBundle = new StandardsDataBundle(bundle.Manifest, badRecords);

        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_NONFINITE_VALUE");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_AsNzWithoutRequiredAmendments()
    {
        var dir = GetFixtureDir("asnz1170-2-2021-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Remove Amendment 2:2024
        var unamendedManifest = bundle.Manifest with
        {
            Amendments = ["Amendment 1:2023"]
        };
        var testBundle = new StandardsDataBundle(unamendedManifest, bundle.Records);

        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_AMENDMENT_UNRESOLVED");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_SelfApproval_InReviewTrace()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var selfApprovedTrace = bundle.Manifest.ReviewTrace with { IsIndependentReview = false };
        var testBundle = new StandardsDataBundle(bundle.Manifest with { ReviewTrace = selfApprovedTrace }, bundle.Records);

        var report = StandardsDataBundleValidator.Validate(testBundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_SELF_APPROVAL_REJECTED");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void ValidateDomain_Rejects_Extrapolation_WhenDisallowed()
    {
        var domain = new ParameterDomain("MeanRoofHeightM", 5.0, 30.0, "m", AllowsExtrapolation: false);

        // In range
        StandardsDataBundleValidator.ValidateDomain(domain, 10.0);
        StandardsDataBundleValidator.ValidateDomain(domain, 5.0);
        StandardsDataBundleValidator.ValidateDomain(domain, 30.0);

        // Out of range below
        var exLow = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardsDataBundleValidator.ValidateDomain(domain, 4.9));
        Assert.Contains("outside the valid applicability domain", exLow.Message);

        // Out of range above
        var exHigh = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardsDataBundleValidator.ValidateDomain(domain, 35.0));
        Assert.Contains("Extrapolation is strictly rejected", exHigh.Message);
    }

    [Fact]
    public void Interpolate1D_Rejects_Interpolation_Across_Categorical_Boundaries()
    {
        var record1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.Categorical);

        var record2 = new CoefficientRecord(
            "REC2", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "2"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 20.0),
            -0.8, "dimensionless", "+=P", InterpolationPolicy.Categorical);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardsDataBundleValidator.Interpolate1D(record1, record2, "Pitch", 15.0));

        Assert.Contains("strictly forbidden across categorical boundaries", ex.Message);
    }

    [Fact]
    public void ReferenceCalculationValidator_Evaluates_SyntheticFixture_Correctly()
    {
        var path = GetFixtureFile("reference-calculations/asce7_22_synthetic_gable_reference.json");
        Assert.True(File.Exists(path), $"File exists: {path}");

        var fixture = ReferenceCalculationValidator.LoadFixture(path);
        Assert.NotNull(fixture);
        Assert.Equal("ASCE7-22-SYNTHETIC-GABLE-REF-01", fixture.FixtureId);
        Assert.True(fixture.IsSynthetic);

        // Simulated matching factors
        var actualFactors = new System.Collections.Generic.Dictionary<string, double>
        {
            { "Kz", 0.98 },
            { "Kd", 0.85 },
            { "G", 0.85 },
            { "qz", 815.7 }
        };

        // Simulated matching pressures
        var actualPressures = new System.Collections.Generic.Dictionary<string, double>
        {
            { "Zone-A1-Full:WX+/IP+", -436.4 },
            { "Zone-A2-Half:WX+/IP+", -540.4 }
        };

        var eval = ReferenceCalculationValidator.Evaluate(fixture, actualFactors, actualPressures);

        Assert.True(eval.Passed);
        Assert.Empty(eval.Issues);
        Assert.Equal(6, eval.Comparisons.Length); // 4 factors + 2 pressures
        Assert.All(eval.Comparisons, c => Assert.True(c.Passed));
    }

    [Fact]
    public void ReferenceCalculationValidator_Fails_When_Tolerance_Exceeded()
    {
        var path = GetFixtureFile("reference-calculations/asce7_22_synthetic_gable_reference.json");
        var fixture = ReferenceCalculationValidator.LoadFixture(path);

        var actualFactors = new System.Collections.Generic.Dictionary<string, double>
        {
            { "Kz", 0.98 },
            { "Kd", 0.85 },
            { "G", 0.85 },
            { "qz", 850.0 } // Expected 815.7 +- 5.0 -> 850 exceeds tolerance!
        };

        var actualPressures = new System.Collections.Generic.Dictionary<string, double>
        {
            { "Zone-A1-Full:WX+/IP+", -436.4 },
            { "Zone-A2-Half:WX+/IP+", -540.4 }
        };

        var eval = ReferenceCalculationValidator.Evaluate(fixture, actualFactors, actualPressures);

        Assert.False(eval.Passed);
        Assert.NotEmpty(eval.Issues);
        Assert.Contains(eval.Comparisons, c => !c.Passed && c.ItemName == "qz");
    }

    [Fact]
    public void ReferenceCalculationValidator_AsNz1170_2_2021_Fixture_ParsesAndEvaluates()
    {
        var path = GetFixtureFile("reference-calculations/asnz1170_2_2021_synthetic_gable_reference.json");
        Assert.True(File.Exists(path), $"File exists: {path}");

        var fixture = ReferenceCalculationValidator.LoadFixture(path);
        Assert.NotNull(fixture);
        Assert.Equal("ASNZ1170-2-2021-SYNTHETIC-GABLE-REF-01", fixture.FixtureId);

        var actualFactors = new System.Collections.Generic.Dictionary<string, double>
        {
            { "VR", 45.0 },
            { "Mzcat", 1.00 },
            { "Vsitbeta", 45.0 },
            { "qdes_theta", 1215.0 }
        };

        var actualPressures = new System.Collections.Generic.Dictionary<string, double>
        {
            { "Zone-A1-Full:WX+/IP+", -607.5 },
            { "Zone-A2-Half:WX+/IP+", -850.5 }
        };

        var eval = ReferenceCalculationValidator.Evaluate(fixture, actualFactors, actualPressures);
        Assert.True(eval.Passed);
        Assert.Empty(eval.Issues);
    }

    [Fact]
    public void ComputeCanonicalHash_IsDeterministic_Across_Different_Orderings()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        // Reverse the records in bundle2
        var reversedRecords = bundle1.Records.Reverse().ToImmutableArray();
        var bundle2 = new StandardsDataBundle(bundle1.Manifest, reversedRecords);

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.Equal(hash1, hash2); // Deterministic ordering ensures hashes match
    }

    [Fact]
    public void Legacy_StandardCalculators_Signatures_Remain_Backward_Compatible()
    {
        var asceInput = new AsceWindInput(40.0, AsceRiskCategory.II, AsceExposure.C, 0.0, 10.0, LimitState.Uls, "Source");
        var asceManifest = new StandardManifest("ASCE", "7-22", [], StandardDataVerificationStatus.Missing, []);
        var asceResult = StandardCalculators.Asce7_22(asceInput, asceManifest);

        Assert.NotNull(asceResult);
        Assert.Null(asceResult.Run);
        Assert.False(asceResult.Gate.CanAssignRealModel);

        var asnzInput = new AsNzWindInput("A", 500, AsNzTerrainCategory.TC2, 10.0, LimitState.Uls);
        var asnzManifest = new StandardManifest("ASNZ", "2021", ["Amendment 1:2023", "Amendment 2:2024"], StandardDataVerificationStatus.Missing, []);
        var asnzResult = StandardCalculators.AsNz1170_2_2021(asnzInput, asnzManifest);

        Assert.NotNull(asnzResult);
        Assert.Null(asnzResult.Run);
        Assert.False(asnzResult.Gate.CanAssignRealModel);
    }
}

