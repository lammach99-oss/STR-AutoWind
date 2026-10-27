using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;

public sealed class AsNzScopedSiteLookupTests
{
    [Fact]
    public void InterpolationAuditIncludesEachEndpointAndItsWeight()
    {
        var source=AsNzAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mz");
        var value=new AsNz2021LocalAutoLookupService().Select(source,"Mz","Mz",row.CategoricalConditions,ImmutableDictionary<string,double>.Empty.Add("Height",4));
        Assert.Equal(AutoLookupState.Resolved,value.State);
        Assert.Contains("weight=0.5",value.Selection);
        Assert.All(value.RecordIds,id=>Assert.Contains(id,value.Selection));
    }
    [Theory]
    [InlineData("AU")]
    [InlineData("NZ")]
    public void SyntheticRegionalSpeedsRemainCountryAndLimitStateScoped(string country)
    {
        var source=AsNzAutoLookupFixture.Source();
        source=AsNzSlopedAutoLookupFixture.Repackage(source,source.Bundle.Records.Select(r=>r with {CategoricalConditions=r.CategoricalConditions.SetItem("Country",country)}).ToImmutableArray());
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {Country=country},source);
        Assert.True(result.NumericalReviewPossible);
        Assert.Equal(40,result.Values.Single(v=>v.Key=="VR_Uls").Value);
        Assert.Equal(20,result.Values.Single(v=>v.Key=="VR_Sls").Value);
        var wrong=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {Country=country=="AU"?"NZ":"AU"},source);
        Assert.False(wrong.NumericalReviewPossible);
    }
    [Fact]
    public void IntermediateTerrainUsesSourceDeclaredTwoDimensionalGrid()
    {
        var source=AsNzSlopedAutoLookupFixture.Source();
        var records=source.Bundle.Records.Where(r=>r.ParameterCategory!="Mz").ToImmutableArray().ToBuilder();
        foreach(var r in source.Bundle.Records.Where(r=>r.ParameterCategory=="Mz"))
        foreach(double terrain in new[]{2.0,3.0})records.Add(r with {RecordKey=r.RecordKey+"-"+terrain,CategoricalConditions=r.CategoricalConditions.Remove("Terrain"),NumericalConditions=r.NumericalConditions.SetItem("Terrain",terrain),Value=r.Value+(terrain-2)*0.4,Interpolation=InterpolationPolicy.Bilinear2D});
        source=AsNzSlopedAutoLookupFixture.Repackage(source,records.ToImmutable());
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzSlopedAutoLookupFixture.Geometry(),AsNzSlopedAutoLookupFixture.Site() with {Terrain=2.25},source);
        Assert.Equal(1.225,result.Values.Single(v=>v.Key=="Uls_Mz_cat").Value!.Value,9);
        Assert.Equal(4,result.Values.Single(v=>v.Key=="Uls_Mz_cat").RecordIds.Length);
    }
    [Fact]
    public void ShieldingRetainsIndependentStateFactorsInsteadOfLastStateOverwrite()
    {
        var source=AsNzAutoLookupFixture.Source();
        var rows=source.Bundle.Records.ToBuilder();
        foreach(string state in new[]{"Uls","Sls"})
        {
            var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mc"&&r.CategoricalConditions["LimitState"]==state);
            rows.Add(row with {RecordKey="SYNTHETIC-MS-"+state,ParameterCategory="Ms",Value=state=="Uls"?0.8:0.9,NumericalConditions=ImmutableDictionary<string,double>.Empty.Add("Parameter",0.5).Add("Height",4)});
        }
        source=AsNzSlopedAutoLookupFixture.Repackage(source,rows.ToImmutable());
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site() with {NoShieldingBenefit=false,ShieldingParameter=0.5,ShieldingEligibilityReviewed=true},source);
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.Equal(36,run.ComputedQuantities["Vdes_Uls_PositiveX"],9);
        Assert.Equal(20.25,run.ComputedQuantities["Vdes_Sls_PositiveX"],9);
    }
    [Fact]
    public void MissingOrientationEvidenceDefaultsToUnreviewed()=>Assert.False(new AsNzAutoSite("AU","SYNTHETIC").TrueNorthReviewed);
}
