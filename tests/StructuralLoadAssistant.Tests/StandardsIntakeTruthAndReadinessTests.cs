using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class StandardsIntakeTruthAndReadinessTests
{
    private static (StandardsDataBundle Bundle, string EvidencePath) CreateValidLicensedBundle(
        StandardFamily family = StandardFamily.Asce7,
        string edition = "7-22",
        string? procedureId = "Ch27",
        string? method = "Directional",
        string? limitState = "Uls")
    {
        var tempFile = Path.GetTempFileName();
        var content = "VERIFIED_INDEPENDENT_ENGINEERING_MEMO";
        File.WriteAllText(tempFile, content);
        var sha = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

        var trace = new EngineeringReviewTrace(
            "Alice PE", "OrgA", DateTimeOffset.UtcNow,
            "Bob PE", "OrgB", DateTimeOffset.UtcNow,
            "Full verification scope", "docs/memo.pdf", sha, true);

        var compJson = "[{\"RecordKey\":\"REC1\",\"TableOrFigure\":\"Table 26.5-1\",\"ClauseReference\":\"26.5\",\"ParameterCategory\":\"ExternalPressure\",\"Value\":-0.5,\"Unit\":\"dimensionless\",\"CategoricalConditions\":{\"System\":\"MWFRS\",\"ProcedureId\":\"" + (procedureId ?? "Ch27") + "\",\"Method\":\"" + (method ?? "Directional") + "\",\"LimitState\":\"" + (limitState ?? "Uls") + "\",\"Zone\":\"1\"},\"NumericalConditions\":{},\"SignConvention\":\"+=P\",\"Interpolation\":\"None\",\"SourceTableLocator\":\"Table 26.5-1\"}]";
        var compBytes = System.Text.Encoding.UTF8.GetBytes(compJson);
        var compSha = Convert.ToHexString(SHA256.HashData(compBytes));

        var coverage = ImmutableArray.Create(new MandatoryCoverageItem("ExternalPressure", "External Pressure", true, 1));

        var dummyManifest = new StandardsDataBundleManifest(
            "LICENSED_BUNDLE", "1.0", family, edition,
            family == StandardFamily.AsNz1170_2 ? ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024") : ImmutableArray<string>.Empty,
            DataRightsClassification.LocalLicensedOnly, "Licensed internal use", "TEMPORARY_HASH",
            ImmutableDictionary<string, string>.Empty.Add("coefficients.json", compSha),
            trace, coverage, "Proprietary", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            CanonicalHashAlgorithmVersion: "3.0");

        var rawBytes = ImmutableDictionary<string, ImmutableArray<byte>>.Empty
            .Add("coefficients.json", compBytes.ToImmutableArray());

        var recordConditions = ImmutableDictionary<string, string>.Empty
            .Add("System", "MWFRS")
            .Add("ProcedureId", procedureId ?? "Ch27")
            .Add("Method", method ?? "Directional")
            .Add("LimitState", limitState ?? "Uls")
            .Add("Zone", "1");

        var rec = new CoefficientRecord(
            "REC1", "Table 26.5-1", "26.5", "ExternalPressure",
            recordConditions,
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var preBundle = new StandardsDataBundle(dummyManifest, [rec], rawComponentBytes: rawBytes, isBytesVerified: true);
        var canonicalHash = preBundle.ComputeCanonicalHash();

        var finalManifest = dummyManifest with { ContentHash = canonicalHash };
        var finalBundle = new StandardsDataBundle(finalManifest, [rec], rawComponentBytes: rawBytes, isBytesVerified: true);

        return (finalBundle, tempFile);
    }

    private static StandardReferenceCalculationFixture CreateValidReferenceFixture(
        StandardFamily family = StandardFamily.Asce7,
        string edition = "7-22",
        string procedureId = "Ch27",
        bool isSynthetic = false)
    {
        var trace = new EngineeringReviewTrace(
            "Calc Preparer PE", "OrgA", DateTimeOffset.UtcNow,
            "Calc Reviewer PE", "OrgB", DateTimeOffset.UtcNow,
            "Benchmark check", "ref.pdf", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true);

        return new StandardReferenceCalculationFixture(
            "REF_BENCHMARK_1", "Independent Benchmark", family, edition,
            family == StandardFamily.AsNz1170_2 ? ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024") : ImmutableArray<string>.Empty,
            procedureId, "Clause 27",
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, double>.Empty,
            ImmutableArray.Create(new ExpectedIntermediateFactor("Kz", "26.10", 0.98, 0.01, "dimensionless")),
            ImmutableArray.Create(new ExpectedZonePressure("Zone-1", "WX+/IP+", -500.0, 5.0, 0.02, "Local3")),
            trace,
            isSynthetic ? DataRightsClassification.SyntheticTestData : DataRightsClassification.LocalLicensedOnly,
            IsSynthetic: isSynthetic);
    }

    [Fact]
    public void Reg1_Bundle_With_Error_Severity_Must_Not_Be_ReadyForIndependentReview()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var badRecord = bundle.Records[0] with { Unit = "" };
            var badBundle = new StandardsDataBundle(bundle.Manifest, [badRecord], rawComponentBytes: bundle.RawComponentBytes, isBytesVerified: true);
            var refFixture = CreateValidReferenceFixture();

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(badBundle, evidenceFilePath: evPath, referenceFixture: refFixture);

            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
            Assert.Contains(preflight.Blockers, b => b.Contains("Error") || b.Contains("validation") || b.Contains("STD_UNIT_MISSING"));
            Assert.False(preflight.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg2_Missing_Or_Empty_Manifest_ContentHash_Results_In_SemanticHashValid_False()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var manifestNoHash = bundle.Manifest with { ContentHash = "" };
            var bundleNoHash = new StandardsDataBundle(manifestNoHash, bundle.Records, rawComponentBytes: bundle.RawComponentBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundleNoHash, evidenceFilePath: evPath);

            Assert.False(preflight.SemanticHashValid);
            Assert.True(preflight.OverallQualificationStatus == IntakeQualificationStatus.Invalid ||
                        preflight.OverallQualificationStatus == IntakeQualificationStatus.Blocked);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg3_NonFinite_Value_Prevents_SemanticHashValid_True_Without_Exception()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var nonFiniteRec = bundle.Records[0] with { Value = double.NaN };
            var badBundle = new StandardsDataBundle(bundle.Manifest, [nonFiniteRec], rawComponentBytes: bundle.RawComponentBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(badBundle, evidenceFilePath: evPath);

            Assert.False(preflight.SemanticHashValid);
            Assert.Equal(IntakeQualificationStatus.Invalid, preflight.OverallQualificationStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg4_Invalid_Component_Json_Or_Extra_Component_Results_In_ComponentBytesCoherent_False()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var extraBytes = bundle.RawComponentBytes.Add("extra.json", ImmutableArray.Create<byte>(1, 2, 3));
            var badBundle = new StandardsDataBundle(bundle.Manifest, bundle.Records, rawComponentBytes: extraBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(badBundle, evidenceFilePath: evPath);

            Assert.False(preflight.ComponentBytesCoherent);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg5_Mandatory_Coverage_Empty_Results_In_ProcedureCoverageValid_False()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var emptyCategory = new MandatoryCoverageItem("NonExistentCategory", "Missing", true, 1);
            var manifestBadCov = bundle.Manifest with { CoverageMatrix = ImmutableArray.Create(emptyCategory) };
            var badBundle = new StandardsDataBundle(manifestBadCov, bundle.Records, rawComponentBytes: bundle.RawComponentBytes, isBytesVerified: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(badBundle, evidenceFilePath: evPath);

            Assert.False(preflight.ProcedureCoverageValid);
            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg6_Reference_Fixture_With_No_Assertions_Or_Self_Review_Does_Not_Qualify_Readiness()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var emptyFixture = new StandardReferenceCalculationFixture(
                "EMPTY_REF", "Empty", bundle.Manifest.Family, bundle.Manifest.Edition,
                [], "Ch27", "Clause 27",
                ImmutableDictionary<string, string>.Empty,
                ImmutableDictionary<string, double>.Empty,
                [], [],
                new EngineeringReviewTrace("Self PE", "Org", DateTimeOffset.UtcNow, "Self PE", "Org", DateTimeOffset.UtcNow, "scope", "uri", "hash", false),
                DataRightsClassification.LocalLicensedOnly,
                IsSynthetic: false);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: evPath, referenceFixture: emptyFixture);

            Assert.False(preflight.ReferenceCalculationAvailable);
            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg7_Valid_Synthetic_Reference_Fixture_Stays_Non_Production_Even_If_Matching()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var syntheticFixture = CreateValidReferenceFixture(isSynthetic: true);

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: evPath, referenceFixture: syntheticFixture);

            Assert.False(preflight.ReferenceCalculationAvailable);
            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg8_FindRecordForProcedure_Rejects_Coefficient_Missing_LimitState()
    {
        var recordWithoutLimitState = new CoefficientRecord(
            "REC_NO_LS", "Table 26.5-1", "26.5", "ExternalPressure",
            ImmutableDictionary<string, string>.Empty
                .Add("System", "MWFRS")
                .Add("ProcedureId", "Ch27")
                .Add("Method", "Directional")
                .Add("Zone", "1"),
            ImmutableDictionary<string, double>.Empty,
            -0.5, "dimensionless", "+=P", InterpolationPolicy.None);

        var manifest = new StandardsDataBundleManifest(
            "TEST_NO_LS", "1.0", StandardFamily.Asce7, "7-22", [],
            DataRightsClassification.SyntheticTestData, "test", "hash",
            ImmutableDictionary<string, string>.Empty,
            new EngineeringReviewTrace("A", "OrgA", DateTimeOffset.UtcNow, "B", "OrgB", DateTimeOffset.UtcNow, "scope", "uri", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true),
            [], "test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var bundle = new StandardsDataBundle(manifest, [recordWithoutLimitState]);

        var proc = new ProcedureSelection(StandardFamily.Asce7, "7-22", "Ch27", LimitState.Uls, "MWFRS", "Directional");
        var match = bundle.FindRecordForProcedure(proc, "ExternalPressure", new Dictionary<string, string> { { "Zone", "1" } });

        Assert.Null(match);
    }

    [Fact]
    public void Reg9_Valid_V3_NonSynthetic_Without_Review_Memo_Reports_Unverified_Not_Ready()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var refFixture = CreateValidReferenceFixture();

            var preflight = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: null, referenceFixture: refFixture);

            Assert.Equal(EvidenceVerificationStatus.NotPerformed, preflight.EvidenceFileStatus);
            Assert.Equal(IntakeQualificationStatus.Unverified, preflight.OverallQualificationStatus);
            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
            Assert.NotEqual(IntakeQualificationStatus.ReadyForIndependentReview, preflight.OverallQualificationStatus);
            Assert.False(preflight.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }

    [Fact]
    public void Reg10_CanCalculateNormativePressures_Remains_False_In_Every_Intake_Scenario()
    {
        var (bundle, evPath) = CreateValidLicensedBundle();
        try
        {
            var refFixture = CreateValidReferenceFixture();

            var preflightFull = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: evPath, referenceFixture: refFixture);
            Assert.False(preflightFull.CanCalculateNormativePressures);

            var preflightNoEv = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: null, referenceFixture: refFixture);
            Assert.False(preflightNoEv.CanCalculateNormativePressures);

            var preflightNoRef = StandardsDataBundleValidator.GenerateIntakePreflight(bundle, evidenceFilePath: evPath, referenceFixture: null);
            Assert.False(preflightNoRef.CanCalculateNormativePressures);
        }
        finally
        {
            if (File.Exists(evPath)) File.Delete(evPath);
        }
    }
}

