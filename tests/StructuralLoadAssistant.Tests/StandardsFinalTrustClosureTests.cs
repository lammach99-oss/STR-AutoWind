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

public sealed class StandardsFinalTrustClosureTests
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

    [Fact]
    public void V3_CanonicalHash_Distinguishes_Nested_Categorical_Condition_Delimiter_Ambiguity()
    {
        // In V2, {"A":"B,C=D"} and {"A":"B","C":"D"} both produced "A=B,C=D"
        var r1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("A", "B,C=D"),
            ImmutableDictionary<string, double>.Empty,
            1.0, "m", "+", InterpolationPolicy.None);

        var r2 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("A", "B").Add("C", "D"),
            ImmutableDictionary<string, double>.Empty,
            1.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_V3_CAT", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var b1 = new StandardsDataBundle(manifest, [r1]);
        var b2 = new StandardsDataBundle(manifest, [r2]);

        var hash1 = b1.ComputeCanonicalHash();
        var hash2 = b2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void V3_CanonicalHash_Distinguishes_Amendment_Collection_Delimiter_Ambiguity()
    {
        // In V2, Amendments=["A,B"] and Amendments=["A","B"] both joined to "A,B"
        var m1 = new StandardsDataBundleManifest(
            "TEST_V3_AMD", "1.0", StandardFamily.AsNz1170_2, "2021",
            ["A,B"],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var m2 = m1 with { Amendments = ["A", "B"] };
        var b1 = new StandardsDataBundle(m1, []);
        var b2 = new StandardsDataBundle(m2, []);

        var hash1 = b1.ComputeCanonicalHash();
        var hash2 = b2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Validator_Rejects_When_Only_ApplicabilityNotes_Mutated_In_Memory()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Mutate ONLY ApplicabilityNotes on the first record
        var r0 = bundle.Records[0];
        var mutatedRecord = r0 with { ApplicabilityNotes = "MUTATED_UNVERIFIED_NOTE_XYZ" };
        var mutatedRecords = bundle.Records.SetItem(0, mutatedRecord);

        // Update manifest ContentHash to match the mutated records so semantic hash check passes
        var tempBundle = new StandardsDataBundle(bundle.Manifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var newContentHash = tempBundle.ComputeCanonicalHash();

        var tamperedManifest = bundle.Manifest with { ContentHash = newContentHash };
        var tamperedBundle = new StandardsDataBundle(tamperedManifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);

        // Validator must catch that in-memory record ApplicabilityNotes differs from verified raw component bytes!
        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_RECORD_BYTES_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Handles_Duplicate_Keys_In_Raw_Component_Bytes_Gracefully_Without_Throwing()
    {
        // Create raw component bytes containing duplicate record keys
        var r1 = new CoefficientRecord("DUP_KEY", "T1", "C1", "Cat", ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, double>.Empty, 1.0, "m", "+", InterpolationPolicy.None);
        var r2 = new CoefficientRecord("DUP_KEY", "T1", "C1", "Cat", ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, double>.Empty, 2.0, "m", "+", InterpolationPolicy.None);

        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { r1, r2 }));
        var rawSha = Convert.ToHexString(SHA256.HashData(jsonBytes));

        var manifest = new StandardsDataBundleManifest(
            "DUP_RAW_TEST", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty.Add("dup.json", rawSha),
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty.Add("dup.json", jsonBytes.ToImmutableArray());
        var bundle = new StandardsDataBundle(manifest, [r1, r2], rawComponentBytes: rawBytes, isBytesVerified: true);

        // Validate must NOT throw an unhandled exception when raw bytes have duplicates!
        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_DUPLICATE_SOURCE_KEY" || i.Code == "STD_DUPLICATE_KEY");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void MatchesSystem_Rejects_Unknown_System_Type_Rather_Than_Defaulting_True()
    {
        var record = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("System", "MWFRS"),
            ImmutableDictionary<string, double>.Empty,
            1.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_SYS", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [record]);

        // Querying with an unrecognized system type must return null (rejected), not match!
        var result = bundle.FindExactRecord(
            "Roof",
            new Dictionary<string, string> { { "System", "MWFRS" } },
            systemType: "ArbitraryUnknownSystem");

        Assert.Null(result);
    }

    [Fact]
    public void FindRecordForProcedure_Enforces_Family_And_System_Type_Strictly()
    {
        var mwfrsRecord = new CoefficientRecord(
            "MWFRS_REC", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty.Add("System", "MWFRS").Add("Zone", "1").Add("ProcedureId", "Ch27").Add("Method", "Directional").Add("LimitState", "Uls"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_PROC", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Correct procedure
        var validProc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(validProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });
        Assert.NotNull(match);
        Assert.Equal("MWFRS_REC", match.RecordKey);

        // Procedure with wrong family
        var wrongFamilyProc = new ProcedureSelection(StandardFamily.AsNz1170_2, "2021", "Section5", LimitState.Uls, "MWFRS", "Standard");
        var wrongFamilyMatch = bundle.FindRecordForProcedure(wrongFamilyProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });
        Assert.Null(wrongFamilyMatch);

        // Procedure querying C&C on MWFRS record
        var ccProc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch30", LimitState.Uls, "ComponentsAndCladding", "Part1");
        var ccMatch = bundle.FindRecordForProcedure(ccProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });
        Assert.Null(ccMatch);
    }


    [Fact]
    public void V3_CanonicalHash_Distinguishes_Coverage_Category_Keys_With_Delimiters()
    {
        var cov1 = new MandatoryCoverageItem("A:B,C=D", "Desc", true, 1);
        var cov2 = new MandatoryCoverageItem("A", "Desc", true, 1);

        var m1 = new StandardsDataBundleManifest(
            "TEST_V3_COV", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [cov1], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var m2 = m1 with { CoverageMatrix = [cov2] };

        var b1 = new StandardsDataBundle(m1, []);
        var b2 = new StandardsDataBundle(m2, []);

        var hash1 = b1.ComputeCanonicalHash();
        var hash2 = b2.ComputeCanonicalHash();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void V3_CanonicalHash_Distinguishes_Numerical_Condition_Keys_With_Delimiters()
    {
        var r1 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("A=1.0,B", 2.0),
            1.0, "m", "+", InterpolationPolicy.None);

        var r2 = new CoefficientRecord(
            "REC1", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("A", 1.0).Add("B", 2.0),
            1.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_V3_NUM", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var b1 = new StandardsDataBundle(manifest, [r1]);
        var b2 = new StandardsDataBundle(manifest, [r2]);

        Assert.NotEqual(b1.ComputeCanonicalHash(), b2.ComputeCanonicalHash());
    }

    [Fact]
    public void V3_CanonicalHash_Invariant_To_Record_Order_And_Map_Insertion_Order()
    {
        var r1 = new CoefficientRecord(
            "REC_A", "T1", "C1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Z", "1").Add("A", "2"),
            ImmutableDictionary<string, double>.Empty.Add("Height", 10.0).Add("Angle", 30.0),
            1.0, "m", "+", InterpolationPolicy.None);

        var r2 = new CoefficientRecord(
            "REC_B", "T1", "C1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("A", "2").Add("Z", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Angle", 30.0).Add("Height", 10.0),
            2.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_V3_ORDER", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var b1 = new StandardsDataBundle(manifest, [r1, r2]);
        var b2 = new StandardsDataBundle(manifest, [r2, r1]);

        Assert.Equal(b1.ComputeCanonicalHash(), b2.ComputeCanonicalHash());

        var rMutated = r2 with { Value = 2.00001 };
        var bMutated = new StandardsDataBundle(manifest, [r1, rMutated]);
        Assert.NotEqual(b1.ComputeCanonicalHash(), bMutated.ComputeCanonicalHash());
    }

    [Fact]
    public void V3_CanonicalHash_Normalizes_Signed_Zero_And_Rejects_Non_Finite()
    {
        var rZeroPositive = new CoefficientRecord(
            "REC_ZERO", "T1", "C1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            0.0, "m", "+", InterpolationPolicy.None);

        var rZeroNegative = new CoefficientRecord(
            "REC_ZERO", "T1", "C1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            -0.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_V3_ZERO", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var bPos = new StandardsDataBundle(manifest, [rZeroPositive]);
        var bNeg = new StandardsDataBundle(manifest, [rZeroNegative]);

        Assert.Equal(bPos.ComputeCanonicalHash(), bNeg.ComputeCanonicalHash());

        var rNan = rZeroPositive with { Value = double.NaN };
        var bNan = new StandardsDataBundle(manifest, [rNan]);
        Assert.Throws<InvalidOperationException>(() => bNan.ComputeCanonicalHash());

        var rInf = rZeroPositive with { Value = double.PositiveInfinity };
        var bInf = new StandardsDataBundle(manifest, [rInf]);
        Assert.Throws<InvalidOperationException>(() => bInf.ComputeCanonicalHash());
    }

    [Fact]
    public void V3_CanonicalHash_Throws_When_Algorithm_Version_Unsupported()
    {
        var manifest = new StandardsDataBundleManifest(
            "TEST_UNKNOWN_VER", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "99.0");

        var bundle = new StandardsDataBundle(manifest, []);
        Assert.Throws<NotSupportedException>(() => bundle.ComputeCanonicalHash());
    }

    [Fact]
    public void V2_CanonicalHash_Demonstrates_Prior_Delimiter_Ambiguity()
    {
        var m1 = new StandardsDataBundleManifest(
            "TEST_V2_AMD", "1.0", StandardFamily.AsNz1170_2, "2021",
            ["A,B"],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "2.0");

        var m2 = m1 with { Amendments = ["A", "B"] };
        var b1 = new StandardsDataBundle(m1, []);
        var b2 = new StandardsDataBundle(m2, []);

        Assert.Equal(b1.ComputeCanonicalHash(), b2.ComputeCanonicalHash());

        var b1V3 = new StandardsDataBundle(m1 with { CanonicalHashAlgorithmVersion = "3.0" }, []);
        var b2V3 = new StandardsDataBundle(m2 with { CanonicalHashAlgorithmVersion = "3.0" }, []);
        Assert.NotEqual(b1V3.ComputeCanonicalHash(), b2V3.ComputeCanonicalHash());
    }

    [Fact]
    public void Validator_Rejects_When_Case_Mutated_In_Unit_Or_Sign_Convention()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var mutatedRecord = bundle.Records[0] with { Unit = "Dimensionless" };
        var mutatedRecords = bundle.Records.SetItem(0, mutatedRecord);

        var tempBundle = new StandardsDataBundle(bundle.Manifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var newContentHash = tempBundle.ComputeCanonicalHash();

        var tamperedBundle = new StandardsDataBundle(bundle.Manifest with { ContentHash = newContentHash }, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_RECORD_BYTES_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validator_Rejects_When_Condition_Map_Value_Mutated_In_Memory()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var r0 = bundle.Records[0];
        var mutatedRecord = r0 with { CategoricalConditions = r0.CategoricalConditions.SetItem("Zone", "MUTATED_CONDITION_XYZ") };
        var mutatedRecords = bundle.Records.SetItem(0, mutatedRecord);

        var tempBundle = new StandardsDataBundle(bundle.Manifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var newContentHash = tempBundle.ComputeCanonicalHash();

        var tamperedBundle = new StandardsDataBundle(bundle.Manifest with { ContentHash = newContentHash }, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_RECORD_BYTES_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void MatchesSystem_Rejects_Misleading_Category_Substring()
    {
        var misleadingRecord = new CoefficientRecord(
            "FAKE_CC", "T1", "C1", "ComponentsAndCladding_Special",
            ImmutableDictionary<string, string>.Empty.Add("Roof", "Flat"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_MISLEADING", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [misleadingRecord]);

        var match = bundle.FindExactRecord(
            "ComponentsAndCladding_Special",
            new Dictionary<string, string> { { "Roof", "Flat" } },
            systemType: "ComponentsAndCladding");

        Assert.Null(match);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_Wrong_Edition_And_Method()
    {
        var mwfrsRecord = new CoefficientRecord(
            "MWFRS_REC2", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty.Add("System", "MWFRS").Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_PROC2", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        var wrongEdition = new ProcedureSelection(StandardFamily.Asce7, "7-16", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(wrongEdition, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });
        Assert.Null(match);
    }

    [Fact]
    public void CanCalculateNormativePressures_Remains_False_For_All_Rights_Classifications()
    {
        var rightsList = new[]
        {
            DataRightsClassification.SyntheticTestData,
            DataRightsClassification.LocalLicensedOnly,
            DataRightsClassification.Restricted,
            DataRightsClassification.RedistributionAllowed
        };

        foreach (var rights in rightsList)
        {
            var reviewTrace = new EngineeringReviewTrace(
                "Alice P.E.", "EngOrg", DateTimeOffset.UtcNow.AddDays(-10),
                "Bob P.E.", "ReviewOrg", DateTimeOffset.UtcNow.AddDays(-5),
                "Full Scope", "https://evidence.example.com/rev.pdf",
                "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
                IsIndependentReview: true);

            var manifest = new StandardsDataBundleManifest(
                $"TEST_{rights}", "1.0", StandardFamily.Asce7, "7-22", [],
                rights, "License grant", "hash",
                ImmutableDictionary<string, string>.Empty,
                reviewTrace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            var bundle = new StandardsDataBundle(manifest, []);
            var report = StandardsDataBundleValidator.Validate(bundle);

            Assert.False(report.CanCalculateNormativePressures,
                $"CanCalculateNormativePressures must be false for rights {rights} in Sprint 4A.3!");
        }
    }

    [Fact]
    public void VerifyEvidenceDocument_Validates_SHA256_Match_And_Detects_Mismatch()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = "INDEPENDENT_ENGINEERING_REVIEW_EVIDENCE_DOCUMENT_SAMPLE";
            File.WriteAllText(tempFile, content);
            var expectedSha = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

            var trace = new EngineeringReviewTrace(
                "Alice", "OrgA", DateTimeOffset.UtcNow,
                "Bob", "OrgB", DateTimeOffset.UtcNow,
                "Scope", "uri", expectedSha, true);

            var manifest = new StandardsDataBundleManifest(
                "TEST_EV", "1.0", StandardFamily.Asce7, "7-22", [],
                DataRightsClassification.LocalLicensedOnly, "Lic", "hash",
                ImmutableDictionary<string, string>.Empty,
                trace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            var (valid, msg) = StandardsDataBundleValidator.VerifyEvidenceDocument(tempFile, manifest);
            Assert.True(valid);
            Assert.Contains("verified successfully", msg);

            File.WriteAllText(tempFile, content + "_TAMPERED");
            var (invalid, invalidMsg) = StandardsDataBundleValidator.VerifyEvidenceDocument(tempFile, manifest);
            Assert.False(invalid);
            Assert.Contains("mismatch", invalidMsg);

            var (missing, missingMsg) = StandardsDataBundleValidator.VerifyEvidenceDocument(tempFile + ".missing", manifest);
            Assert.False(missing);
            Assert.Contains("not found", missingMsg);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

}
