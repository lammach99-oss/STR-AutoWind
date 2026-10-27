using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;

public sealed class AsNzRoofCoefficientTests
{
    static (AsNzAutoSource Source,CoefficientRecord Row) Fixture(string form,double pitch,AsNzRoofFace face,string table)
    {
        var source=AsNzSlopedAutoLookupFixture.Source();
        var template=source.Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("Surface")=="Roof");
        var cat=template.CategoricalConditions.SetItem("RoofForm",form).SetItem("RoofFace",face.ToString());
        var nums=ImmutableDictionary<string,double>.Empty.Add("hd",0.5).Add("Pitch",pitch);
        if(table=="Table 5.3(C)"&&pitch>=25)nums=nums.Add("bd",2);
        var row=template with {RecordKey="SYNTHETIC-FACE",TableOrFigure=table,CategoricalConditions=cat,NumericalConditions=nums,Value=-0.123};
        return(AsNzSlopedAutoLookupFixture.Repackage(source,[row]),row);
    }
    [Theory]
    [InlineData("Gable",9.99999,AsNzRoofFace.Upwind,"Table 5.3(A)")]
    [InlineData("Gable",10,AsNzRoofFace.Upwind,"Table 5.3(B)")]
    [InlineData("Gable",14,AsNzRoofFace.Downwind,"Table 5.3(C)")]
    [InlineData("Gable",25,AsNzRoofFace.Crosswind,"Table 5.3(A)")]
    [InlineData("Hip",25,AsNzRoofFace.Crosswind,"Table 5.3(C)")]
    [InlineData("Monoslope",14,AsNzRoofFace.Downwind,"Table 5.3(A)")]
    public void SingleFaceRequiresExactFormIncidencePitchAndTable(string form,double pitch,AsNzRoofFace face,string table)
    {
        var (source,row)=Fixture(form,pitch,face,table);
        var value=AsNzRoofCoefficientLookup.Select(source,"Cpe",face,row.CategoricalConditions,row.NumericalConditions);
        Assert.Equal(AutoLookupState.Resolved,value.State);Assert.Equal(-0.123,value.Value);
    }
    [Fact]
    public void TenDegreeUpwindCannotReuseTableA()
    {
        var(source,row)=Fixture("Gable",10,AsNzRoofFace.Upwind,"Table 5.3(A)");
        Assert.NotEqual(AutoLookupState.Resolved,AsNzRoofCoefficientLookup.Select(source,"Cpe",AsNzRoofFace.Upwind,row.CategoricalConditions,row.NumericalConditions).State);
    }
    [Fact]
    public void SteepDownwindCannotUseDepthBreadthInPlaceOfBreadthDepth()
    {
        var(source,row)=Fixture("Hip",25,AsNzRoofFace.Downwind,"Table 5.3(C)");
        var nums=row.NumericalConditions.Remove("bd").Add("db",0.5);
        row=row with {NumericalConditions=nums};source=AsNzSlopedAutoLookupFixture.Repackage(source,[row]);
        Assert.NotEqual(AutoLookupState.Resolved,AsNzRoofCoefficientLookup.Select(source,"Cpe",AsNzRoofFace.Downwind,row.CategoricalConditions,nums).State);
    }
}
