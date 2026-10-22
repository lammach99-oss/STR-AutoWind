using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class StandardsDataIntegrityHardeningTests
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
    public void CanonicalHash_Is_Stable_Across_Json_Whitespace_Changes_While_Raw_Hash_Changes()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var mwfrsPath = Path.Combine(dir, "coefficients_mwfrs.json");
        var rawBytesOriginal = File.ReadAllBytes(mwfrsPath);
        var rawHashOriginal = Convert.ToHexString(SHA256.HashData(rawBytesOriginal));

        // Add whitespace/newlines to JSON text
        var text = File.ReadAllText(mwfrsPath);
        var whitespaceText = "\n\n  " + text + "  \n\n";
        var rawBytesModified = System.Text.Encoding.UTF8.GetBytes(whitespaceText);
        var rawHashModified = Convert.ToHexString(SHA256.HashData(rawBytesModified));

        // Raw file SHA256 must change when whitespace changes
        Assert.NotEqual(rawHashOriginal, rawHashModified);

        // But canonical semantic bundle hash must be invariant to whitespace/formatting!
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        var recordsOriginal = JsonSerializer.Deserialize<CoefficientRecord[]>(text, options)!;
        var recordsModified = JsonSerializer.Deserialize<CoefficientRecord[]>(whitespaceText, options)!;

        var b1 = new StandardsDataBundle(bundle.Manifest, recordsOriginal.ToImmutableArray());
        var b2 = new StandardsDataBundle(bundle.Manifest, recordsModified.ToImmutableArray());

        Assert.Equal(b1.ComputeCanonicalHash(), b2.ComputeCanonicalHash());
    }

    [Fact]
    public void CanonicalHash_Changes_When_NumericalConditions_Mutated_Without_Value_Change()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        var record0 = bundle1.Records[0];
        var mutatedRecord = record0 with
        {
            NumericalConditions = record0.NumericalConditions.SetItem("PitchDegrees", 35.0)
        };
        var bundle2 = new StandardsDataBundle(bundle1.Manifest, bundle1.Records.SetItem(0, mutatedRecord));

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Validator_Rejects_Extra_Undeclared_Component_Files()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var extraBytes = bundle.RawComponentBytes.Add("extra_untracked.json", ImmutableArray.Create<byte>(1, 2, 3));
        var extraBundle = new StandardsDataBundle(bundle.Manifest, bundle.Records, dir, extraBytes, isBytesVerified: false);

        var report = StandardsDataBundleValidator.Validate(extraBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_EXTRA_COMPONENT");
    }

    [Fact]
    public void Validator_Rejects_Coverage_Count_Mismatch()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Manifest expects 2 MWFRS records, but change CoverageMatrix to expect 5
        var tamperedCoverage = bundle.Manifest.CoverageMatrix.SetItem(0,
            bundle.Manifest.CoverageMatrix[0] with { AvailableRecordCount = 5 });

        var testBundle = new StandardsDataBundle(bundle.Manifest with { CoverageMatrix = tamperedCoverage }, bundle.Records, dir, bundle.RawComponentBytes);
        var report = StandardsDataBundleValidator.Validate(testBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_COVERAGE_COUNT_MISMATCH");
    }

    [Fact]
    public void Validator_Rejects_Bilinear2D_Interpolation_In_Interpolate1D()
    {
        var r1 = new CoefficientRecord("R1", "T", "C", "P", ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("X", 1.0), 1.0, "m", "+", InterpolationPolicy.Bilinear2D);
        var r2 = new CoefficientRecord("R2", "T", "C", "P", ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("X", 2.0), 2.0, "m", "+", InterpolationPolicy.Bilinear2D);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardsDataBundleValidator.Interpolate1D(r1, r2, "X", 1.5));
        Assert.Contains("Bilinear2D", ex.Message);
    }

    [Fact]
    public void ValidateDomain_Rejects_Inverted_Or_NonFinite_Bounds()
    {
        var badDomain = new ParameterDomain("BadDomain", 100.0, 10.0, "m"); // Min > Max!

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardsDataBundleValidator.ValidateDomain(badDomain, 50.0));
        Assert.Contains("invalid bounds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalHash_Changes_When_CategoricalConditions_Mutated_Without_Value_Change()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        // Mutate only a categorical condition on the first record (e.g. Zone 1 -> Zone 2), Value remains identical
        var record0 = bundle1.Records[0];
        var mutatedRecord = record0 with
        {
            CategoricalConditions = record0.CategoricalConditions.SetItem("Zone", "MUTATED_ZONE")
        };
        var bundle2 = new StandardsDataBundle(bundle1.Manifest, bundle1.Records.SetItem(0, mutatedRecord));

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CanonicalHash_Changes_When_Clause_Or_Table_Mutated_Without_Value_Change()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        var record0 = bundle1.Records[0];
        var mutatedRecord = record0 with
        {
            ClauseReference = "Clause 99.9.9",
            TableOrFigure = "Table 99-9"
        };
        var bundle2 = new StandardsDataBundle(bundle1.Manifest, bundle1.Records.SetItem(0, mutatedRecord));

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CanonicalHash_Changes_When_SignConvention_Or_Unit_Mutated()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        var record0 = bundle1.Records[0];
        var mutatedRecord = record0 with
        {
            SignConvention = "+=OutwardSuction"
        };
        var bundle2 = new StandardsDataBundle(bundle1.Manifest, bundle1.Records.SetItem(0, mutatedRecord));

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CanonicalHash_Changes_When_Amendments_Mutated()
    {
        var dir = GetFixtureDir("asnz1170-2-2021-synthetic");
        var bundle1 = StandardsDataBundle.LoadFromDirectory(dir);

        var mutatedManifest = bundle1.Manifest with
        {
            Amendments = ["Amendment 1:2023", "Amendment 2:2024", "Amendment 3:2026-Proposed"]
        };
        var bundle2 = new StandardsDataBundle(mutatedManifest, bundle1.Records);

        var hash1 = bundle1.ComputeCanonicalHash();
        var hash2 = bundle2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Validator_Rejects_Mismatched_Canonical_ContentHash()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Tamper ContentHash in manifest
        var tamperedManifest = bundle.Manifest with
        {
            ContentHash = "DEADBEEF00000000000000000000000000000000000000000000000000000000"
        };
        var tamperedBundle = new StandardsDataBundle(tamperedManifest, bundle.Records, dir);

        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_CANONICAL_HASH_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_Unverified_Component_Bytes_When_Bytes_Not_Supplied()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Calling Validate without rawComponentFolder and without verified bytes in bundle must fail-closed!
        var unverifiedBundle = new StandardsDataBundle(bundle.Manifest, bundle.Records, dir, rawComponentBytes: null, isBytesVerified: false);
        var report = StandardsDataBundleValidator.Validate(unverifiedBundle, rawComponentFolder: "");

        Assert.Contains(report.Issues, i => i.Code == "STD_INTEGRITY_UNVERIFIED");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_Unsafe_Relative_Path_Traversal_In_Component_Files()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var unsafeManifest = bundle.Manifest with
        {
            ComponentFiles = bundle.Manifest.ComponentFiles.Add("../../../secret.json", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")
        };
        var unsafeBundle = new StandardsDataBundle(unsafeManifest, bundle.Records, dir);

        var report = StandardsDataBundleValidator.Validate(unsafeBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_UNSAFE_PATH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Never_Allows_Production_Pressures_Even_For_NonSynthetic_Reviewed_Metadata()
    {
        // Construct a non-synthetic manifest with LocalLicensedOnly, IsIndependentReview=true, fake hash, plausible record counts
        var reviewTrace = new EngineeringReviewTrace(
            "Alice Engineer",
            "Consulting Inc",
            DateTimeOffset.UtcNow.AddDays(-10),
            "Bob Reviewer",
            "Peer Review LLC",
            DateTimeOffset.UtcNow.AddDays(-5),
            "Full Independent Review of Table 26.5-1",
            "https://evidence.example.com/review-doc.pdf",
            "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789",
            IsIndependentReview: true);

        var coverage = ImmutableArray.Create(
            new MandatoryCoverageItem("VelocityPressure", "Velocity Pressure Coefficients", true, 1));

        var manifest = new StandardsDataBundleManifest(
            "ASCE7-22-NONSYNTHETIC-MOCK",
            "1.0",
            StandardFamily.Asce7,
            "7-22",
            [],
            DataRightsClassification.LocalLicensedOnly,
            "Licensed for private design project",
            "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            ImmutableDictionary<string, string>.Empty.Add("mock.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            reviewTrace,
            coverage,
            "Private license",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var record = new CoefficientRecord(
            "REC-01", "Table 26.5-1", "26.5.1", "VelocityPressure",
            ImmutableDictionary<string, string>.Empty.Add("Exposure", "C"),
            ImmutableDictionary<string, double>.Empty.Add("HeightM", 10.0),
            1.0, "dimensionless", "None", InterpolationPolicy.None);

        var bundle = new StandardsDataBundle(manifest, [record]);

        // Even with non-synthetic rights and seemingly valid review trace, CanCalculateNormativePressures MUST be false in Sprint 4A.1!
        var report = StandardsDataBundleValidator.Validate(bundle, provenance: DataProvenance.Unverified);

        Assert.False(report.CanCalculateNormativePressures, "Sprint 4A.1 MUST NEVER set CanCalculateNormativePressures to true!");
    }

    [Fact]
    public void Validator_Rejects_Reviewer_When_Preparer_Equals_Reviewer()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Same person as preparer and reviewer
        var selfReview = bundle.Manifest.ReviewTrace with
        {
            PreparerName = "Dr. Jane Doe",
            ReviewerName = "dr. jane doe  ", // Case & whitespace variation
            IsIndependentReview = true
        };
        var testBundle = new StandardsDataBundle(bundle.Manifest with { ReviewTrace = selfReview }, bundle.Records, dir);

        var report = StandardsDataBundleValidator.Validate(testBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_SELF_APPROVAL_REJECTED");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_Incomplete_Internal_Pressure_With_Error_Severity()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Filter out negative internal pressure
        var positiveOnlyRecords = bundle.Records
            .Where(r => r.ParameterCategory != "InternalPressure" ||
                        (r.CategoricalConditions.TryGetValue("InternalPressureState", out var s) && s.Contains("+")))
            .ToImmutableArray();

        var testBundle = new StandardsDataBundle(bundle.Manifest, positiveOnlyRecords, dir);
        var report = StandardsDataBundleValidator.Validate(testBundle, rawComponentFolder: dir);

        var ipIssue = Assert.Single(report.Issues.Where(i => i.Code == "STD_INTERNAL_PRESSURE_INCOMPLETE"));
        Assert.Equal(StandardsValidationSeverity.Error, ipIssue.Severity); // Must be Error, not Warning!
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void FindExactRecord_Requires_Exact_Categorical_And_Numerical_Match()
    {
        var record1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1").Add("Surface", "Windward"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var record2 = new CoefficientRecord(
            "REC2", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1").Add("Surface", "Leeward"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.3, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "A", DateTimeOffset.UtcNow, "B", "B", DateTimeOffset.UtcNow, "scope", "uri", "hash", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [record1, record2]);

        // Querying with only Zone="1" is ambiguous (both match Zone="1", but one is Windward and one is Leeward) -> must return null!
        var ambiguous = bundle.FindExactRecord("Roof", new Dictionary<string, string> { { "Zone", "1" } });
        Assert.Null(ambiguous);

        // Querying with exact full categorical conditions matches uniquely
        var exact = bundle.FindExactRecord("Roof", new Dictionary<string, string> { { "Zone", "1" }, { "Surface", "Windward" } });
        Assert.NotNull(exact);
        Assert.Equal("REC1", exact.RecordKey);

        // Querying with mismatched numerical condition matches nothing
        var numMismatch = bundle.FindExactRecord(
            "Roof",
            new Dictionary<string, string> { { "Zone", "1" }, { "Surface", "Windward" } },
            new Dictionary<string, double> { { "Pitch", 25.0 } }); // record1 has Pitch=10.0
        Assert.Null(numMismatch);
    }

    [Fact]
    public void Interpolate1D_Rejects_Differing_Units_Or_Sign_Conventions()
    {
        var record1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "Pa", "+=P", InterpolationPolicy.Linear1D);

        var record2 = new CoefficientRecord(
            "REC2", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 20.0),
            -0.8, "kPa", "+=P", InterpolationPolicy.Linear1D); // Different unit!

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StandardsDataBundleValidator.Interpolate1D(record1, record2, "Pitch", 15.0));

        Assert.Contains("unit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpolate1D_Rejects_Extrapolation_Beyond_Bounds()
    {
        var record1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.Linear1D);

        var record2 = new CoefficientRecord(
            "REC2", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 20.0),
            -0.8, "dimensionless", "+=P", InterpolationPolicy.Linear1D);

        // Target value 25.0 is outside [10.0, 20.0] -> extrapolation rejected!
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardsDataBundleValidator.Interpolate1D(record1, record2, "Pitch", 25.0));

        Assert.Contains("extrapolation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceCalculationValidator_Rejects_Empty_Fixture()
    {
        var emptyFixture = new StandardReferenceCalculationFixture(
            "EMPTY-FIXTURE",
            "Empty Test",
            StandardFamily.Asce7,
            "7-22",
            [],
            "Procedure-01",
            "Clause 26",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            [], // Zero expected factors
            [], // Zero expected pressures
            new EngineeringReviewTrace("A", "A", DateTimeOffset.UtcNow, "B", "B", DateTimeOffset.UtcNow, "scope", "uri", "hash", true),
            DataRightsClassification.SyntheticTestData,
            IsSynthetic: true);

        var eval = ReferenceCalculationValidator.Evaluate(
            emptyFixture,
            new Dictionary<string, double>(),
            new Dictionary<string, double>());

        Assert.False(eval.Passed);
        Assert.Contains(eval.Issues, i => i.Contains("zero expected factors and zero expected pressures", StringComparison.OrdinalIgnoreCase) || i.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReferenceCalculationValidator_Rejects_Pressure_Missing_StateId_Fallback()
    {
        var fixture = new StandardReferenceCalculationFixture(
            "TEST-REF",
            "Test Ref",
            StandardFamily.Asce7,
            "7-22",
            [],
            "Procedure-01",
            "Clause 26",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            [new ExpectedIntermediateFactor("Kz", "26.10", 1.0, 0.01, "")],
            [new ExpectedZonePressure("Zone-1", "WX+/IP+", -500.0, 10.0, 0.05, "Outward")],
            new EngineeringReviewTrace("A", "A", DateTimeOffset.UtcNow, "B", "B", DateTimeOffset.UtcNow, "scope", "uri", "hash", true),
            DataRightsClassification.SyntheticTestData,
            IsSynthetic: true);

        var actualFactors = new Dictionary<string, double> { { "Kz", 1.0 } };

        // actualPressures has only "Zone-1", missing the StateId "WX+/IP+"!
        // Zone-only fallback is strictly prohibited!
        var actualPressuresZoneOnly = new Dictionary<string, double>
        {
            { "Zone-1", -500.0 }
        };

        var eval = ReferenceCalculationValidator.Evaluate(fixture, actualFactors, actualPressuresZoneOnly);

        Assert.False(eval.Passed, "Zone-only pressure match without StateId must fail closed!");
        Assert.Contains(eval.Issues, i => i.Contains("Zone-1", StringComparison.OrdinalIgnoreCase) && i.Contains("WX+/IP+", StringComparison.OrdinalIgnoreCase));
    }
}

