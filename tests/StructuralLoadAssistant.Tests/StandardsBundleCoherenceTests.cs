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

public sealed class StandardsBundleCoherenceTests
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
    public void Validator_Rejects_In_Memory_Records_Mismatching_Verified_Raw_Component_Bytes()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // Mutate one in-memory record (change its Value from -0.5 to -0.999)
        var mutatedRecord = bundle.Records[0] with { Value = -0.999 };
        var mutatedRecords = bundle.Records.SetItem(0, mutatedRecord);

        // Update ContentHash to match the mutated records so semantic hash check would pass
        var tempBundle = new StandardsDataBundle(bundle.Manifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);
        var newContentHash = tempBundle.ComputeCanonicalHash();

        var tamperedManifest = bundle.Manifest with { ContentHash = newContentHash };
        var tamperedBundle = new StandardsDataBundle(tamperedManifest, mutatedRecords, dir, bundle.RawComponentBytes, isBytesVerified: true);

        // The raw component bytes match the manifest ComponentFiles SHA-256.
        // The manifest ContentHash matches the in-memory records canonical hash.
        // BUT the in-memory records DO NOT match what was parsed from the raw component bytes!
        var report = StandardsDataBundleValidator.Validate(tamperedBundle, rawComponentFolder: dir);

        Assert.Contains(report.Issues, i => i.Code == "STD_RECORD_BYTES_MISMATCH");
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void FindExactRecord_Rejects_When_Record_Has_NumericalConditions_But_Query_Has_None()
    {
        var record = new CoefficientRecord(
            "REC_PITCH", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [record]);

        // Querying without numerical conditions must NOT match a record that requires Pitch=10.0!
        var match = bundle.FindExactRecord("Roof", new Dictionary<string, string> { { "Zone", "1" } }, numericalConditions: null);
        Assert.Null(match);

        var emptyNumMatch = bundle.FindExactRecord("Roof", new Dictionary<string, string> { { "Zone", "1" } }, numericalConditions: new Dictionary<string, double>());
        Assert.Null(emptyNumMatch);
    }

    [Fact]
    public void FindExactRecord_Rejects_Partial_Numerical_Conditions_Keyset()
    {
        var record = new CoefficientRecord(
            "REC_MULTI_NUM", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0).Add("HeightM", 15.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [record]);

        // Query provides only Pitch, missing HeightM -> must NOT match
        var partialMatch = bundle.FindExactRecord(
            "Roof",
            new Dictionary<string, string> { { "Zone", "1" } },
            new Dictionary<string, double> { { "Pitch", 10.0 } });

        Assert.Null(partialMatch);

        // Query provides exact complete set -> matches
        var exactMatch = bundle.FindExactRecord(
            "Roof",
            new Dictionary<string, string> { { "Zone", "1" } },
            new Dictionary<string, double> { { "Pitch", 10.0 }, { "HeightM", 15.0 } });

        Assert.NotNull(exactMatch);
        Assert.Equal("REC_MULTI_NUM", exactMatch.RecordKey);
    }

    [Fact]
    public void FindExactRecord_Rejects_Slightly_Off_Numerical_Value()
    {
        var record = new CoefficientRecord(
            "REC_EXACT_NUM", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty.Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty.Add("Pitch", 10.0),
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [record]);

        // Query with slightly off value (10.0000005) must not match exact 10.0
        var match = bundle.FindExactRecord(
            "Roof",
            new Dictionary<string, string> { { "Zone", "1" } },
            new Dictionary<string, double> { { "Pitch", 10.0000005 } });

        Assert.Null(match);
    }

    [Fact]
    public void Validator_Allows_Combined_MWFRS_And_CC_Bundle_For_MWFRS_Procedure()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        // ASCE 7-22 synthetic bundle legitimately contains both MWFRS and CC records.
        // Selecting an MWFRS procedure should NOT fail the entire bundle with STD_SYSTEM_MISMATCH!
        var procedure = new ProcedureSelection(
            StandardFamily.Asce7,
            "7-22",
            "Part1-Directional",
            LimitState.Uls,
            "MWFRS",
            "Directional");

        var report = StandardsDataBundleValidator.Validate(bundle, procedure: procedure, rawComponentFolder: dir);

        // STD_SYSTEM_MISMATCH must NOT be reported for a valid combined bundle!
        Assert.DoesNotContain(report.Issues, i => i.Code == "STD_SYSTEM_MISMATCH");
    }

    [Fact]
    public void Procedure_Lookup_Isolates_MWFRS_From_CC_Records()
    {
        var mwfrsRecord = new CoefficientRecord(
            "MWFRS_REC", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty.Add("System", "MWFRS").Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var ccRecord = new CoefficientRecord(
            "CC_REC", "Table 30.3-1", "30.3", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty.Add("System", "ComponentsAndCladding").Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.8, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_COMBINED", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord, ccRecord]);

        // Looking up MWFRS with systemType "MWFRS" must return only the MWFRS record
        var mwfrsMatch = bundle.FindExactRecord(
            "ExternalPressure",
            new Dictionary<string, string> { { "System", "MWFRS" }, { "Zone", "1" } },
            systemType: "MWFRS");

        Assert.NotNull(mwfrsMatch);
        Assert.Equal("MWFRS_REC", mwfrsMatch.RecordKey);

        // Looking up with systemType "ComponentsAndCladding" must return only the CC record
        var ccMatch = bundle.FindExactRecord(
            "ExternalPressure",
            new Dictionary<string, string> { { "System", "ComponentsAndCladding" }, { "Zone", "1" } },
            systemType: "ComponentsAndCladding");

        Assert.NotNull(ccMatch);
        Assert.Equal("CC_REC", ccMatch.RecordKey);

        // Querying for MWFRS must never match a CC record
        var crossMatch = bundle.FindExactRecord(
            "ExternalPressure",
            new Dictionary<string, string> { { "System", "ComponentsAndCladding" }, { "Zone", "1" } },
            systemType: "MWFRS");

        Assert.Null(crossMatch);
    }

    [Fact]
    public void CanonicalHash_V2_LengthPrefixed_Prevents_Delimiter_Injection_Collisions()
    {
        // Record A has RecordKey="A|B" and Category="C"
        var rA = new CoefficientRecord(
            "A|B", "Table", "Clause", "C",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            1.0, "m", "+", InterpolationPolicy.None);

        // Record B has RecordKey="A" and Category="B|C"
        var rB = new CoefficientRecord(
            "A", "Table", "Clause", "B|C",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            1.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_COLLISION", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "2.0");

        var bundleA = new StandardsDataBundle(manifest, [rA]);
        var bundleB = new StandardsDataBundle(manifest, [rB]);

        var hashA = bundleA.ComputeCanonicalHash();
        var hashB = bundleB.ComputeCanonicalHash();

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Validator_Rejects_Invalid_Evidence_Hash_Format_On_NonSynthetic_Bundle()
    {
        var reviewTrace = new EngineeringReviewTrace(
            "Alice", "OrgA", DateTimeOffset.UtcNow,
            "Bob", "OrgB", DateTimeOffset.UtcNow,
            "Scope", "https://evidence.example.com",
            "NOT_A_VALID_64_HEX_HASH", // Invalid hash format
            true);

        var manifest = new StandardsDataBundleManifest(
            "TEST_HASH_FMT", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.LocalLicensedOnly, "License", "hash",
            ImmutableDictionary<string, string>.Empty,
            reviewTrace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, []);
        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.Contains(report.Issues, i => i.Code == "STD_EVIDENCE_HASH_INVALID");
        Assert.False(report.CanCalculateNormativePressures);
    }
}
