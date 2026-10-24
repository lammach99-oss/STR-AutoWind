using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class SampleArtifactGeneratorTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "StructuralLoadAssistant.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void Generate_Flat_And_Gable_Roof_Artifacts()
    {
        string repoRoot = FindRepoRoot();
        string rootOutDir = Path.Combine(repoRoot, "artifacts", "sample_reports");
        string localOutDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "sample_reports");
        Directory.CreateDirectory(rootOutDir);
        Directory.CreateDirectory(localOutDir);

        var targetDirs = new[] { rootOutDir, localOutDir }.Distinct();

        // 1. ASCE 7-22 Gable Roof Run
        var asceStrategy = new Asce7_22CalculationStrategy();
        var bAsce = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void AddAsce(string k, double v) => bAsce.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        AddAsce("Kd", 0.85); AddAsce("Kzt", 1.0); AddAsce("Ke", 1.0); AddAsce("Kh", 1.0); AddAsce("G", 0.85); AddAsce("GCpi", 0.18);
        AddAsce("Cp_Windward", 0.8); AddAsce("Cp_Leeward", -0.5); AddAsce("Cp_Side", -0.7); AddAsce("Cp_Roof_Windward", -0.7); AddAsce("Cp_Roof_Leeward", -0.6);

        var reqGable = new EngineeringCalculationRequest(
            "PRJ-SYNTH-GABLE", "ASCE/SEI 7-22", ImmutableArray.Create("Supplement 1", "Supplement 2", "Supplement 3"),
            "Chapter 27: MWFRS Directional Procedure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Gable", "Enclosed",
            40.0, 10.0, 30.0, 20.0, 6.0, 9.0, 15.0, 0.0, "C", bAsce.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        var gableRun = asceStrategy.Execute(reqGable);
        var asceModel = new ReportModel(
            new Project("PRJ-SYNTH-GABLE", "Synthetic ASCE Gable Roof Building", "SI"),
            DemoFixtures.Calculation("ASCE/SEI 7-22"),
            DemoFixtures.Manifest("ASCE/SEI 7-22"),
            DemoFixtures.Model(),
            [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
            [new("B1", "WALL", "Illustrative", 3, -750)],
            null, null, null,
            new ReportDefinition("report-asce-gable")
        );
        var builder = new DetailedReportBuilder();

        byte[] asceXlsx = builder.BuildExcel(asceModel, gableRun);
        byte[] ascePdf = builder.BuildPdf(asceModel, gableRun);
        string asceHtml = builder.BuildHtml(asceModel, gableRun);
        string asceMd = builder.BuildMarkdown(asceModel, gableRun);
        string asceJson = builder.BuildJson(asceModel, gableRun);

        foreach (var dir in targetDirs)
        {
            File.WriteAllBytes(Path.Combine(dir, "asce7_22_gable_roof_audit.xlsx"), asceXlsx);
            File.WriteAllBytes(Path.Combine(dir, "asce7_22_gable_roof_audit.pdf"), ascePdf);
            File.WriteAllText(Path.Combine(dir, "asce7_22_gable_roof_audit.html"), asceHtml);
            File.WriteAllText(Path.Combine(dir, "asce7_22_gable_roof_audit.md"), asceMd);
            File.WriteAllText(Path.Combine(dir, "asce7_22_gable_roof_audit.json"), asceJson);
        }

        // 2. AS/NZS 1170.2:2021 Flat Roof Run
        var asnzStrategy = new AsNz1170_2_2021CalculationStrategy();
        var bAsnz = ImmutableDictionary.CreateBuilder<string, CoefficientInputEvidence>();
        void AddAsnz(string k, double v) => bAsnz.Add(k, new CoefficientInputEvidence(k, v, "dimensionless", DataProvenance.SyntheticDemoFixture, "Synthetic", "Table 1", "Note", DateTimeOffset.UtcNow));
        AddAsnz("Md", 1.0); AddAsnz("Mz_cat", 0.95); AddAsnz("Ms", 1.0); AddAsnz("Mt", 1.0); AddAsnz("Mc", 1.0);
        AddAsnz("Cpe_Windward", 0.7); AddAsnz("Cpe_Leeward", -0.5); AddAsnz("Cpe_Side", -0.65); AddAsnz("Cpe_Roof_Windward", -0.9); AddAsnz("Cpe_Roof_Leeward", -0.5); AddAsnz("Cpi", 0.2);

        var reqFlat = new EngineeringCalculationRequest(
            "PRJ-SYNTH-FLAT", "AS/NZS 1170.2:2021", ImmutableArray.Create("Amendment 1:2023", "Amendment 2:2024"),
            "Section 3: Design Wind Speed and Dynamic Pressure", LimitState.Uls, WindDirection.PositiveX, 0.0, "Flat", "Enclosed",
            45.0, 10.0, 30.0, 20.0, 10.0, 10.0, 0.0, 0.0, "TC2", bAsnz.ToImmutable(),
            new Dictionary<string, string> { ["InternalPressure"] = "IP_POS", ["OwnerReviewed"] = "true" }.ToImmutableDictionary());

        var flatRun = asnzStrategy.Execute(reqFlat);
        var asnzModel = new ReportModel(
            new Project("PRJ-SYNTH-FLAT", "Synthetic AS/NZS Flat Roof Building", "SI"),
            DemoFixtures.Calculation("AS/NZS 1170.2:2021"),
            DemoFixtures.Manifest("AS/NZS 1170.2:2021"),
            DemoFixtures.Model(),
            [new("B1-B2", "B1", "EqualSplit", 0, 10, 2100)],
            [new("B1", "WALL", "Illustrative", 3, -750)],
            null, null, null,
            new ReportDefinition("report-asnz-flat")
        );

        byte[] asnzXlsx = builder.BuildExcel(asnzModel, flatRun);
        byte[] asnzPdf = builder.BuildPdf(asnzModel, flatRun);
        string asnzHtml = builder.BuildHtml(asnzModel, flatRun);
        string asnzMd = builder.BuildMarkdown(asnzModel, flatRun);
        string asnzJson = builder.BuildJson(asnzModel, flatRun);

        foreach (var dir in targetDirs)
        {
            File.WriteAllBytes(Path.Combine(dir, "asnz1170_2_flat_roof_audit.xlsx"), asnzXlsx);
            File.WriteAllBytes(Path.Combine(dir, "asnz1170_2_flat_roof_audit.pdf"), asnzPdf);
            File.WriteAllText(Path.Combine(dir, "asnz1170_2_flat_roof_audit.html"), asnzHtml);
            File.WriteAllText(Path.Combine(dir, "asnz1170_2_flat_roof_audit.md"), asnzMd);
            File.WriteAllText(Path.Combine(dir, "asnz1170_2_flat_roof_audit.json"), asnzJson);
        }

        Assert.True(File.Exists(Path.Combine(rootOutDir, "asce7_22_gable_roof_audit.xlsx")));
        Assert.True(File.Exists(Path.Combine(rootOutDir, "asce7_22_gable_roof_audit.pdf")));
        Assert.True(File.Exists(Path.Combine(rootOutDir, "asnz1170_2_flat_roof_audit.xlsx")));
        Assert.True(File.Exists(Path.Combine(rootOutDir, "asnz1170_2_flat_roof_audit.pdf")));
    }
}
