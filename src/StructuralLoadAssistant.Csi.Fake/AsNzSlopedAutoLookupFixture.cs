using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Csi.Fake;

/// <summary>Invented software operands. Source-table labels exercise routing, not licensed table values.</summary>
public static class AsNzSlopedAutoLookupFixture
{
    public static EngineeringCalculationRequest Geometry()=>AsNzAutoLookupFixture.Geometry() with {RoofForm="Monoslope",RoofSlopeDegrees=14,EaveHeightM=3,RidgeHeightM=5};
    public static AsNzAutoSite Site()=>AsNzAutoLookupFixture.Site() with {SlopeBearingDegrees=0,TrueNorthReviewed=true};
    public static AsNzAutoResult Resolve()=>new AsNz2021LocalAutoLookupService().Resolve(Geometry(),Site(),Source());
    public static AsNzAutoSource Source()
    {
        var template=AsNzAutoLookupFixture.Source();
        var rows=ImmutableArray.CreateBuilder<CoefficientRecord>();
        foreach(var r in template.Bundle.Records)
        {
            var cat=r.CategoricalConditions.SetItem("ProcedureId",AsNz2021LocalAutoLookupService.RoofMethod).SetItem("Method","RoofFormScoped").SetItem("RoofForm","Monoslope");
            if(r.ParameterCategory=="Cpe"&&cat.GetValueOrDefault("Surface")=="Leeward")continue;
            if(r.ParameterCategory=="Cpe"&&cat.GetValueOrDefault("Surface")=="Windward")cat=cat.Add("Support","GroundSupported").Add("SpeedBasis","AtMeanRoofHeight");
            if(r.ParameterCategory=="Cpe"&&cat.GetValueOrDefault("Surface")=="Roof")
            {
                foreach(var face in new[]{AsNzRoofFace.Upwind,AsNzRoofFace.Downwind})
                {
                    var branch=AsNzRoofApplicability.Evaluate("Monoslope",14,face);
                    rows.Add(r with {RecordKey=r.RecordKey+"-"+face,TableOrFigure=branch.Table,ClauseReference="SYNTHETIC ROUTING OPERANDS / NOT STANDARD VALUES",CategoricalConditions=cat.Add("RoofFace",face.ToString()),NumericalConditions=r.NumericalConditions.Add("Pitch",14)});
                }
            }
            else rows.Add(r with {CategoricalConditions=r.ParameterCategory=="Mz"?cat.Remove("Terrain"):cat,NumericalConditions=r.ParameterCategory=="Mz"?r.NumericalConditions.Add("Terrain",2):r.NumericalConditions,Interpolation=r.ParameterCategory=="Mz"?InterpolationPolicy.Bilinear2D:r.Interpolation});
        }
        return Repackage(template,rows.ToImmutable());
    }
    public static AsNzAutoSource Repackage(AsNzAutoSource template,ImmutableArray<CoefficientRecord> rows)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(rows);
        var raw=ImmutableDictionary<string,ImmutableArray<byte>>.Empty.Add("records.json",bytes.ToImmutableArray());
        var manifest=template.Bundle.Manifest with {ComponentFiles=ImmutableDictionary<string,string>.Empty.Add("records.json",Convert.ToHexString(SHA256.HashData(bytes)))};
        var bundle=new StandardsDataBundle(manifest,rows,rawComponentBytes:raw,isBytesVerified:true);
        bundle=new(manifest with {ContentHash=bundle.ComputeCanonicalHash()},rows,rawComponentBytes:raw,isBytesVerified:true);
        return template with {Bundle=bundle};
    }
}
