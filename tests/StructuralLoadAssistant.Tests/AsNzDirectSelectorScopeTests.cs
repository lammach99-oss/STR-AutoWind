using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

/// <summary>Synthetic malformed-package assertions; no standard table operands.</summary>
public sealed class AsNzDirectSelectorScopeTests
{
    [Theory]
    [InlineData("VR","km/h")]
    [InlineData("MinimumSpeed","mph")]
    [InlineData("rho","g/cm3")]
    [InlineData("Md","m/s")]
    [InlineData("Cpe","Pa")]
    [InlineData("Mz","")]
    public void ExactSelectorRejectsWrongOrMissingSiUnit(string category,string unit)
    {
        var source=AsNzAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory==category);
        source=AsNzSlopedAutoLookupFixture.Repackage(source,source.Bundle.Records.Replace(row,row with {Unit=unit}));
        var result=new AsNz2021LocalAutoLookupService().Select(source,"test",category,row.CategoricalConditions,row.NumericalConditions);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,result.State);
        Assert.Null(result.Value);
    }

    [Fact]
    public void DirectGenericRoofSelectorRejectsTableContradictingFormIncidenceAndPitch()
    {
        var source=AsNzSlopedAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("RoofFace")=="Upwind");
        source=AsNzSlopedAutoLookupFixture.Repackage(source,source.Bundle.Records.Replace(row,row with {TableOrFigure="Table 5.3(A)"}));
        var result=new AsNz2021LocalAutoLookupService().Select(source,"roof","Cpe",row.CategoricalConditions,row.NumericalConditions);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,result.State);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("Pitch")]
    [InlineData("hd")]
    public void DirectRoofSelectorCannotTrustPackageWithOmittedRequiredDomain(string omitted)
    {
        var source=AsNzSlopedAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("RoofFace")=="Upwind");
        var changed=row with {NumericalConditions=row.NumericalConditions.Remove(omitted)};
        source=AsNzSlopedAutoLookupFixture.Repackage(source,source.Bundle.Records.Replace(row,changed));
        Assert.NotEqual(AutoLookupState.Resolved,new AsNz2021LocalAutoLookupService().Select(source,"roof","Cpe",changed.CategoricalConditions,changed.NumericalConditions).State);
    }
}
