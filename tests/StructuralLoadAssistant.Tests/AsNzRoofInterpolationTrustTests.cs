using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

/// <summary>Synthetic sign-domain tests, not normative coefficient examples.</summary>
public sealed class AsNzRoofInterpolationTrustTests
{
    static AutoLookupValue Select(string table,double low,double high)
    {
        var source=AsNzSlopedAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("Surface")=="Roof");
        CoefficientRecord At(string key,double ratio,double value)=>row with {RecordKey=key,Value=value,TableOrFigure=table,
            NumericalConditions=row.NumericalConditions.SetItem("hd",ratio),Interpolation=InterpolationPolicy.Bilinear2D};
        source=AsNzSlopedAutoLookupFixture.Repackage(source,[At("SYNTHETIC-LOW",0.1,low),At("SYNTHETIC-HIGH",0.3,high)]);
        return new AsNz2021LocalAutoLookupService().Select(source,"roof","Cpe",row.CategoricalConditions,row.NumericalConditions.SetItem("hd",0.2));
    }

    [Theory]
    [InlineData("Table 5.3(A)")]
    [InlineData("Table 5.3(B)")]
    [InlineData("Table 5.3(C)")]
    public void RoofHeightRatioCannotInterpolateBetweenOppositeSigns(string table)
    {
        var value=Select(table,-0.3,0.2);
        Assert.Equal(AutoLookupState.OutsideDomain,value.State);
        Assert.Null(value.Value);
    }

    [Theory]
    [InlineData(-0.4,-0.2,-0.3)]
    [InlineData(0.2,0.4,0.3)]
    public void SameSignRoofInterpolationRetainsPermittedArithmetic(double low,double high,double expected)
    {
        var value=Select("Table 5.3(B)",low,high);
        Assert.Equal(AutoLookupState.Resolved,value.State);
        Assert.Equal(expected,value.Value!.Value,10);
    }
}
