using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class StandardsPreflightAndFailClosedTests
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
    public void Validate_Returns_Structured_FatalBlocker_Without_Throwing_When_Hash_Version_Unsupported()
    {
        var manifest = new StandardsDataBundleManifest(
            "TEST_UNSUPPORTED_VER", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty.Add("comp.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "99.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("comp.json", ImmutableArray<byte>.Empty);

        var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

        // Public Validate boundary MUST NOT throw an unhandled NotSupportedException!
        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_HASH_VERSION_UNSUPPORTED" && i.Severity == StandardsValidationSeverity.FatalBlocker);
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validate_Returns_Structured_FatalBlocker_Without_Throwing_When_Record_Value_Is_NonFinite()
    {
        var record = new CoefficientRecord(
            "REC_NAN", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            double.NaN, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_NAN_VAL", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty.Add("comp.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("comp.json", ImmutableArray<byte>.Empty);

        var bundle = new StandardsDataBundle(manifest, [record], rawComponentBytes: rawBytes, isBytesVerified: true);

        // Public Validate boundary MUST NOT throw an unhandled InvalidOperationException!
        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_NONFINITE_VALUE" && i.Severity == StandardsValidationSeverity.FatalBlocker);
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validate_Returns_Structured_FatalBlocker_Without_Throwing_When_Numerical_Condition_Is_NonFinite()
    {
        var record = new CoefficientRecord(
            "REC_INF_COND", "Table 1", "Clause 1", "Roof",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("Pitch", double.PositiveInfinity),
            1.0, "m", "+", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_INF_COND", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty.Add("comp.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("comp.json", ImmutableArray<byte>.Empty);

        var bundle = new StandardsDataBundle(manifest, [record], rawComponentBytes: rawBytes, isBytesVerified: true);

        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_NONFINITE_NUMERICAL_CONDITION" && i.Severity == StandardsValidationSeverity.FatalBlocker);
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validate_Returns_Structured_FatalBlocker_Without_Throwing_When_Component_Bytes_Are_Malformed_Json()
    {
        var malformedJsonBytes = System.Text.Encoding.UTF8.GetBytes("{ this is not valid json at all }");
        var sha = Convert.ToHexString(SHA256.HashData(malformedJsonBytes));

        var manifest = new StandardsDataBundleManifest(
            "TEST_MALFORMED_JSON", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty.Add("malformed.json", sha),
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("malformed.json", malformedJsonBytes.ToImmutableArray());

        var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

        // Must return structured issue STD_COMPONENT_JSON_MALFORMED without throwing JsonException
        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_COMPONENT_JSON_MALFORMED" && i.Severity == StandardsValidationSeverity.FatalBlocker);
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void Validate_Returns_Structured_FatalBlocker_When_Manifest_Component_Files_Is_Empty()
    {
        var manifest = new StandardsDataBundleManifest(
            "TEST_EMPTY_COMPONENTS", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty, // Zero files!
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, []);

        var report = StandardsDataBundleValidator.Validate(bundle);

        Assert.NotNull(report);
        Assert.Contains(report.Issues, i => i.Code == "STD_COMPONENT_FILES_EMPTY" && i.Severity == StandardsValidationSeverity.FatalBlocker);
        Assert.False(report.CanCalculateNormativePressures);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_When_Procedure_Method_Mismatches()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_DIR", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_PROC_METH", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Query procedure with Method "Envelope" when record specifies "Directional" -> must be rejected!
        var proc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "MWFRS", "Envelope");
        var match = bundle.FindRecordForProcedure(proc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_When_Procedure_Id_Mismatches()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_CH27", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_PROC_ID", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Query procedure with ProcedureId "Ch28" when record specifies "Ch27" -> must be rejected!
        var proc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch28", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(proc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_When_Edition_Is_Empty_Or_Mismatched()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_ED", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_EDITION", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Empty edition -> must be rejected
        var emptyEditionProc = new ProcedureSelection(StandardFamily.Asce7, "", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        Assert.Null(bundle.FindRecordForProcedure(emptyEditionProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } }));

        // Mismatched edition (7-16 vs 7-22) -> must be rejected
        var wrongEditionProc = new ProcedureSelection(StandardFamily.Asce7, "7-16", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        Assert.Null(bundle.FindRecordForProcedure(wrongEditionProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } }));
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_When_LimitState_Mismatches()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_ULS", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_LIMIT_STATE", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Querying for SLS on a ULS-specific record -> must be rejected!
        var slsProc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Sls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(slsProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_When_System_Is_Unknown()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_SYS", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_SYS_UNK", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        var unknownSysProc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "UnknownArbitrarySystem", "Directional");
        var match = bundle.FindRecordForProcedure(unknownSysProc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);
    }

    [Fact]
    public void FindExactRecord_Rejects_When_System_Omitted_In_Direct_Unscoped_Lookup()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_EXACT_SYS", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_UNSCOPED_SYS", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        // Direct unscoped lookup WITHOUT passing systemType and WITHOUT passing System in categoricalConditions
        // MUST NOT match a record that explicitly declared System="MWFRS"!
        var match = bundle.FindExactRecord("ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } }, systemType: null);

        Assert.Null(match);

        // Supplying explicit System in categoricalConditions matches
        var exactMatch = bundle.FindExactRecord("ExternalPressure", new Dictionary<string, string> { { "System", "MWFRS" }, { "Zone", "1" } }, systemType: null);
        Assert.NotNull(exactMatch);
        Assert.Equal("REC_EXACT_SYS", exactMatch.RecordKey);
    }

    [Fact]
    public void FindRecordForProcedure_Succeeds_For_Valid_Documented_Procedure_Match()
    {
        var mwfrsRecord = new CoefficientRecord(
            "REC_VALID_MATCH", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("LimitState", "Uls")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_VALID_PROC", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [mwfrsRecord]);

        var proc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(proc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.NotNull(match);
        Assert.Equal("REC_VALID_MATCH", match.RecordKey);
    }

    [Fact]
    public void FindRecordForProcedure_Rejects_Record_Lacking_Procedure_Applicability_Metadata()
    {
        // Record has System="MWFRS" and Zone="1", but NO ProcedureId or Method metadata!
        var bareRecord = new CoefficientRecord(
            "REC_BARE", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_BARE_PROC", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [bareRecord]);

        // Strict procedure lookup must return null for records lacking procedure metadata!
        var proc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(proc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);

        // But low-level FindExactRecord with explicit System STILL works for backward-compatible synthetic inspection!
        var lowLevelMatch = bundle.FindExactRecord("ExternalPressure", new Dictionary<string, string> { { "System", "MWFRS" }, { "Zone", "1" } });
        Assert.NotNull(lowLevelMatch);
        Assert.Equal("REC_BARE", lowLevelMatch.RecordKey);
    }


    [Fact]
    public void GenerateIntakePreflight_Reports_NotPerformed_When_Evidence_File_Path_Omitted()
    {
        var trace = new EngineeringReviewTrace(
            "Alice PE", "OrgA", DateTimeOffset.UtcNow,
            "Bob PE", "OrgB", DateTimeOffset.UtcNow,
            "Scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true);

        var manifest = new StandardsDataBundleManifest(
            "PREFLIGHT_TEST_1", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.LocalLicensedOnly, "Lic", "hash",
            ImmutableDictionary<string, string>.Empty.Add("c.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            trace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("c.json", ImmutableArray<byte>.Empty);

        var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

        var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: null);

        Assert.Equal(EvidenceVerificationStatus.NotPerformed, preflight.EvidenceFileStatus);
        Assert.Equal(HumanReviewStatus.SelfDeclaredMetadata, preflight.HumanReviewStatus);
        Assert.False(preflight.CanCalculateNormativePressures);
        Assert.Contains(preflight.Blockers, b => b.Contains("NotPerformed"));
    }

    [Fact]
    public void GenerateIntakePreflight_Blocks_Synthetic_Bundle_From_Production_Qualification()
    {
        var dir = GetFixtureDir("asce7-22-synthetic");
        var bundle = StandardsDataBundle.LoadFromDirectory(dir);

        var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, rawComponentFolder: dir);

        Assert.Equal(IntakeQualificationStatus.Blocked, preflight.OverallQualificationStatus);
        Assert.False(preflight.CanCalculateNormativePressures);
        Assert.Contains(preflight.Blockers, b => b.Contains("Synthetic"));
    }

    [Fact]
    public void GenerateIntakePreflight_Reports_HashMismatch_When_Evidence_File_Bytes_Differ()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "TAMPERED_OR_DIFFERING_EVIDENCE_BYTES");

            var trace = new EngineeringReviewTrace(
                "Alice PE", "OrgA", DateTimeOffset.UtcNow,
                "Bob PE", "OrgB", DateTimeOffset.UtcNow,
                "Scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true);

            var manifest = new StandardsDataBundleManifest(
                "PREFLIGHT_MISMATCH", "1.0", StandardFamily.Asce7, "7-22", [],
                DataRightsClassification.LocalLicensedOnly, "Lic", "hash",
                ImmutableDictionary<string, string>.Empty.Add("c.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
                trace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                CanonicalHashAlgorithmVersion: "3.0");

            var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
                .Add("c.json", ImmutableArray<byte>.Empty);

            var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: tempFile);

            Assert.Equal(EvidenceVerificationStatus.HashMismatch, preflight.EvidenceFileStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
            Assert.Contains(preflight.Blockers, b => b.Contains("mismatch"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateIntakePreflight_Reports_Verified_When_Evidence_File_Sha_Matches()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = "VALID_EVIDENCE_DOCUMENT_FOR_PE_CHECK";
            File.WriteAllText(tempFile, content);
            var sha = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

            var trace = new EngineeringReviewTrace(
                "Alice PE", "OrgA", DateTimeOffset.UtcNow,
                "Bob PE", "OrgB", DateTimeOffset.UtcNow,
                "Scope", "uri", sha, true);

            var manifest = new StandardsDataBundleManifest(
                "PREFLIGHT_VERIFIED", "1.0", StandardFamily.Asce7, "7-22", [],
                DataRightsClassification.LocalLicensedOnly, "Lic", "hash",
                ImmutableDictionary<string, string>.Empty.Add("c.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
                trace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                CanonicalHashAlgorithmVersion: "3.0");

            var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
                .Add("c.json", ImmutableArray<byte>.Empty);

            var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: tempFile);

            Assert.Equal(EvidenceVerificationStatus.Verified, preflight.EvidenceFileStatus);
            Assert.Equal(HumanReviewStatus.EvidenceHashVerified, preflight.HumanReviewStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateIntakePreflight_Requires_V3_Canonical_Hash_For_New_Candidates()
    {
        var trace = new EngineeringReviewTrace(
            "Alice PE", "OrgA", DateTimeOffset.UtcNow,
            "Bob PE", "OrgB", DateTimeOffset.UtcNow,
            "Scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true);

        // Legacy V2 algorithm version
        var manifest = new StandardsDataBundleManifest(
            "PREFLIGHT_V2", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.LocalLicensedOnly, "Lic", "hash",
            ImmutableDictionary<string, string>.Empty.Add("c.json", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            trace, [], "notice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "2.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("c.json", ImmutableArray<byte>.Empty);

        var bundle = new StandardsDataBundle(manifest, [], rawComponentBytes: rawBytes, isBytesVerified: true);

        var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle);

        Assert.Equal(IntakeQualificationStatus.Blocked, preflight.OverallQualificationStatus);
        Assert.Contains(preflight.Blockers, b => b.Contains("V3"));
    }

}
