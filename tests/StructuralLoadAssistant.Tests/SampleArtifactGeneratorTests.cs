using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public class SampleArtifactGeneratorTests
{
    [Fact]
    public void Generate_Flat_And_Gable_Roof_Artifacts()
    {
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "sample_reports");
        Directory.CreateDirectory(outDir);

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
        var model = ReportTests.Model();
        var builder = new DetailedReportBuilder();

        File.WriteAllBytes(Path.Combine(outDir, "asce7_22_gable_roof_audit.xlsx"), builder.BuildExcel(model, gableRun));
        File.WriteAllBytes(Path.Combine(outDir, "asce7_22_gable_roof_audit.pdf"), builder.BuildPdf(model, gableRun));
        File.WriteAllText(Path.Combine(outDir, "asce7_22_gable_roof_audit.html"), builder.BuildHtml(model, gableRun));
        File.WriteAllText(Path.Combine(outDir, "asce7_22_gable_roof_audit.md"), builder.BuildMarkdown(model, gableRun));
        File.WriteAllText(Path.Combine(outDir, "asce7_22_gable_roof_audit.json"), builder.BuildJson(model, gableRun));

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

        File.WriteAllBytes(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.xlsx"), builder.BuildExcel(model, flatRun));
        File.WriteAllBytes(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.pdf"), builder.BuildPdf(model, flatRun));
        File.WriteAllText(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.html"), builder.BuildHtml(model, flatRun));
        File.WriteAllText(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.md"), builder.BuildMarkdown(model, flatRun));
        File.WriteAllText(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.json"), builder.BuildJson(model, flatRun));

        Assert.True(File.Exists(Path.Combine(outDir, "asce7_22_gable_roof_audit.xlsx")));
        Assert.True(File.Exists(Path.Combine(outDir, "asce7_22_gable_roof_audit.pdf")));
        Assert.True(File.Exists(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.xlsx")));
        Assert.True(File.Exists(Path.Combine(outDir, "asnz1170_2_flat_roof_audit.pdf")));
    }
}

