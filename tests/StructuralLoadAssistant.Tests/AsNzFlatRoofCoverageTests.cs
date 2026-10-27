using System.Text.Json;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;

public sealed class AsNzFlatRoofCoverageTests
{
    static AsNzFlatRoofScenario Scenario()=>new("SYNTHETIC-FLAT-REVIEW",AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site());
    [Fact]
    public void CompleteSyntheticScenarioProvesOnlyItsRequestedInputs()
    {
        var report=AsNzFlatRoofCoverage.Evaluate(Scenario(),AsNzAutoLookupFixture.Source());
        Assert.True(report.ScenarioInputsResolved);Assert.Equal("SCENARIO_INPUTS_RESOLVED_FOR_REVIEW",report.Status);
        Assert.True(report.Synthetic);Assert.NotEmpty(report.DatasetHash);Assert.NotEmpty(report.Lookups);
        Assert.All(report.Lookups,v=>Assert.Equal(AutoLookupState.Resolved,v.State));
        Assert.False(report.FullTableCoverageEstablished);Assert.False(report.CanCalculateNormativePressures);Assert.False(report.CanMutateCsi);
        Assert.False(report.IndependentEngineeringQualified);
    }
    [Theory]
    [InlineData("source")]
    [InlineData("region")]
    [InlineData("terrain")]
    [InlineData("enclosure")]
    [InlineData("roof")]
    [InlineData("identity")]
    public void MissingOrUnsupportedScenarioEvidenceCannotClaimCompleteCoverage(string missing)
    {
        var s=Scenario();AsNzAutoSource? source=AsNzAutoLookupFixture.Source();
        if(missing=="source")source=null;
        if(missing=="region")s=s with {Site=s.Site with {Region=""}};
        if(missing=="terrain")s=s with {Site=s.Site with {UniformTerrain=false}};
        if(missing=="enclosure")s=s with {Site=s.Site with {Enclosure=""}};
        if(missing=="roof")s=s with {Geometry=s.Geometry with {RoofForm="Monoslope",RoofSlopeDegrees=14}};
        if(missing=="identity")s=s with {ScenarioId=""};
        var report=AsNzFlatRoofCoverage.Evaluate(s,source);
        Assert.False(report.ScenarioInputsResolved);Assert.NotEmpty(report.Blockers);
        Assert.False(report.FullTableCoverageEstablished);Assert.False(report.CanCalculateNormativePressures);Assert.False(report.CanMutateCsi);
    }
    [Fact]
    public void CoverageCliReturnsStructuredBlockedReportWhenPrivatePackageIsUnavailable()
    {
        string file=Path.Combine(Path.GetTempPath(),"asnz-scenario-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            File.WriteAllText(file,JsonSerializer.Serialize(Scenario(),AsNzPrivateStagingJson.Options));
            var output=new StringWriter();
            Assert.Equal(2,AsNzPrivateIntakeCommand.Run(["coverage",file+".missing-package",file],output));
            using var report=JsonDocument.Parse(output.ToString());
            Assert.Equal("SCENARIO_BLOCKED",report.RootElement.GetProperty("Status").GetString());
            Assert.False(report.RootElement.GetProperty("ScenarioInputsResolved").GetBoolean());
        }
        finally{File.Delete(file);}
    }
}
