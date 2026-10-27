using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

/// <summary>Invented operands verify provenance retention, not normative shielding coefficients.</summary>
public sealed class AsNzShieldingProvenanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceShieldingRetainsLookupEvidenceThroughRequestAndCalculationTrace(bool interpolate)
    {
        var source=AsNzAutoLookupFixture.Source();
        var rows=source.Bundle.Records.ToBuilder();
        foreach(string state in new[]{"Uls","Sls"})
        {
            var template=source.Bundle.Records.First(r=>r.ParameterCategory=="Mc"&&r.CategoricalConditions["LimitState"]==state);
            foreach(double parameter in interpolate?new[]{0.25,0.75}:new[]{0.5})
                rows.Add(template with
                {
                    RecordKey=$"SYNTHETIC-SHIELD-{state}-{parameter}",ParameterCategory="Ms",
                    TableOrFigure="Synthetic shielding grid",ClauseReference="Invented provenance regression only",
                    Value=(state=="Uls"?0.8:0.9)+(parameter-0.5)*0.4,
                    NumericalConditions=ImmutableDictionary<string,double>.Empty.Add("Parameter",parameter).Add("Height",4),
                    Interpolation=interpolate?InterpolationPolicy.Bilinear2D:InterpolationPolicy.None
                });
        }
        source=AsNzSlopedAutoLookupFixture.Repackage(source,rows.ToImmutable());
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),
            AsNzAutoLookupFixture.Site() with {NoShieldingBenefit=false,ShieldingParameter=0.5,ShieldingEligibilityReviewed=true},source);
        Assert.True(result.NumericalReviewPossible);
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.True(run.NumericalComputationPossible);
        foreach(string state in new[]{"Uls","Sls"})
        {
            string key=state+"_Ms";
            var lookup=Assert.Single(result.Values.Where(v=>v.Key==key&&v.RecordIds.Length>0));
            var evidence=result.Request.ExplicitCoefficients[key];
            Assert.Equal(lookup.Value!.Value,evidence.Value);
            Assert.Equal(source.Bundle.Manifest.BundleId+" / "+source.Bundle.Manifest.ContentHash,evidence.Document);
            Assert.Equal(lookup.Source,evidence.ClauseOrTable);
            Assert.Contains("AUTO LOOKUP",evidence.SourceNotes);
            Assert.All(lookup.RecordIds,id=>Assert.Contains(id,evidence.SourceNotes));
            Assert.Contains(lookup.Selection,evidence.SourceNotes);
            var trace=Assert.Single(run.TraceNodes.Where(t=>t.NodeId=="INPUT-"+key));
            Assert.Contains(evidence.Document,trace.SourceReference);
            Assert.Contains(evidence.ClauseOrTable,trace.SourceReference);
            Assert.Contains(evidence.SourceNotes,trace.ExplanatoryNote);
            Assert.Single(result.Values.Where(v=>v.Key==key));
        }
        Assert.Equal(36,run.ComputedQuantities["Vdes_Uls_PositiveX"],9);
        Assert.Equal(20.25,run.ComputedQuantities["Vdes_Sls_PositiveX"],9);
    }

    [Fact]
    public void NoShieldingBenefitRemainsAnExplicitAssumption()
    {
        var result=AsNzAutoLookupFixture.Resolve();
        foreach(string key in new[]{"Uls_Ms","Sls_Ms"})
        {
            var evidence=result.Request.ExplicitCoefficients[key];
            Assert.Equal(1,evidence.Value);
            Assert.Equal("Engineer selected input / assumption",evidence.Document);
            Assert.Contains("ASSUMPTION: no shielding benefit claimed",evidence.SourceNotes);
        }
    }
}
