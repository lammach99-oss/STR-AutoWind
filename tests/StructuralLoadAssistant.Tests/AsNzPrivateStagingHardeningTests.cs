using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

/// <summary>Adversarial source staging with invented numerical records and evidence only.</summary>
public sealed class AsNzPrivateStagingHardeningTests : IDisposable
{
    readonly string tempRoot=Path.Combine(Path.GetTempPath(),"asnz-hardening-"+Guid.NewGuid().ToString("N"));
    string Root=>Path.Combine(tempRoot,"fixtures","standards","private","synthetic");
    public AsNzPrivateStagingHardeningTests()=>Directory.CreateDirectory(Root);
    public void Dispose()
    {
        var resolved=Path.GetFullPath(tempRoot);
        if(!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("asnz-hardening-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected test workspace");
        Directory.Delete(resolved,true);
    }
    PrivateEvidenceFile Evidence(string name,string text)
    {
        File.WriteAllText(Path.Combine(Root,name),text);
        return new(name,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }
    AsNzSourceStaging Valid(CoefficientRecord? record=null)
    {
        var src=Evidence("source.txt","SYNTHETIC PRIMARY CELL; NOT A STANDARD");
        var rights=Evidence("rights.txt","SYNTHETIC DOCUMENTARY PERMISSION TEST ONLY");
        var adoption=Evidence("adoption.txt","SYNTHETIC BASE REVIEW DECISION TEST ONLY");
        var check=Evidence("second-read.txt","SYNTHETIC SECOND READING TEST ONLY");
        var r=record??AsNzAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="VR");
        r=r with {CategoricalConditions=r.CategoricalConditions.SetItem("ProcedureId",AsNz2021LocalAutoLookupService.RoofMethod).SetItem("Method","RoofFormScoped")};
        var cell=new AsNzSourceCell(r,"source",r.TableOrFigure,1,"TEST","row","column","TestCase","NotApplicable",SourceCellStatus.SOURCE_CELL_CHECKED,"First reader");
        cell=cell with {Reconciliation=new("Second reader",AsNzPrivateDatasetPipeline.CellHash(cell),src.Sha256,r.Value,check)};
        return new("1.0","SYNTHETIC-HARDENING",AsNzPrivateDatasetPipeline.Edition,[],"AU","SYNTHETIC JURISDICTION",
            new("PERMITTED_LOCAL_REVIEW","Test reviewer","Synthetic decision",rights,true,true,true,[src.Sha256]),
            new("AU","SYNTHETIC JURISDICTION",AsNzPrivateDatasetPipeline.Edition,adoption,[new(r.TableOrFigure,"BASE_TEXT_REVIEW_ONLY","NOT_APPLICABLE","NOT_APPLICABLE","Synthetic decision locator")]),
            [new("source",src.File,src.Sha256,AsNzPrivateDatasetPipeline.Edition)],[cell]);
    }
    [Fact]
    public void MissingRequiredNumericalRegionalReturnPeriodCannotBecomeCheckedSource()
    {
        var row=AsNzAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="VR");
        var stage=Valid(row with {NumericalConditions=ImmutableDictionary<string,double>.Empty});
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
    [Theory]
    [InlineData("InventedUnsupportedCategory")]
    [InlineData("Md")]
    [InlineData("Mz")]
    [InlineData("Ms")]
    public void UnknownCategoryOrMissingCategoryDomainCannotBecomeCheckedSource(string category)
    {
        var row=AsNzAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="VR");
        var stage=Valid(row with {ParameterCategory=category,Unit="dimensionless",NumericalConditions=ImmutableDictionary<string,double>.Empty});
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
    [Fact]
    public void UnknownRoofFormCannotBecomeCheckedSource()
    {
        var row=AsNzAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="VR");
        var stage=Valid(row with {CategoricalConditions=row.CategoricalConditions.SetItem("RoofForm","ArbitraryUnsupportedForm")});
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
    [Fact]
    public void CoherentRoofCellCannotClaimWrongTenDegreeTable()
    {
        var row=AsNzSlopedAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("RoofFace")=="Upwind");
        var stage=Valid(row with {TableOrFigure="Table 5.3(A)"});
        // Stage has a valid second read over this invented row. The applicability is independently invalid.
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
    [Fact]
    public void DuplicateJsonPropertiesCannotSilentlyChangeDeclaredEdition()
    {
        var stage=Valid();
        string json=JsonSerializer.Serialize(stage,AsNzPrivateStagingJson.Options);
        json=json.Insert(json.IndexOf('{')+1,"\n\"Edition\":\"AS/NZS 1170.2:2011\",");
        var path=Path.Combine(Root,"source-staging.json");File.WriteAllText(path,json);
        Assert.False(AsNzPrivateDatasetPipeline.ValidateFile(path).ReadyForPrivateReview);
    }
    [Fact]
    public void RepackagedRecordsCannotReplaceReconciledSourceStage()
    {
        var stage=Valid();var path=Path.Combine(Root,"source-staging.json");
        File.WriteAllText(path,JsonSerializer.Serialize(stage,AsNzPrivateStagingJson.Options));
        var output=Path.Combine(Path.GetDirectoryName(Root)!,"package");
        Assert.True(AsNzPrivateDatasetPipeline.Package(path,output).ReadyForPrivateReview);
        var bundle=StandardsDataBundle.LoadFromDirectory(output);
        var changed=bundle.Records[0] with {Value=bundle.Records[0].Value+1};
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new[]{changed},AsNzPrivateStagingJson.Options);
        File.WriteAllBytes(Path.Combine(output,"records.json"),bytes);
        var manifest=bundle.Manifest with {ComponentFiles=ImmutableDictionary<string,string>.Empty.Add("records.json",Convert.ToHexString(SHA256.HashData(bytes)))};
        var repackaged=new StandardsDataBundle(manifest,[changed],rawComponentBytes:ImmutableDictionary<string,ImmutableArray<byte>>.Empty.Add("records.json",bytes.ToImmutableArray()),isBytesVerified:true);
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(manifest with {ContentHash=repackaged.ComputeCanonicalHash()},AsNzPrivateStagingJson.Options));
        Assert.ThrowsAny<Exception>(()=>AsNzPrivateAutoSourceLoader.Load(output));
    }
    [Fact]
    public void SelectedValueCarriesPrimarySourceByteHashIntoCalculationEvidence()
    {
        var stage=Valid();var path=Path.Combine(Root,"source-staging.json");
        File.WriteAllText(path,JsonSerializer.Serialize(stage,AsNzPrivateStagingJson.Options));
        var output=Path.Combine(Path.GetDirectoryName(Root)!,"trace-package");
        Assert.True(AsNzPrivateDatasetPipeline.Package(path,output).ReadyForPrivateReview);
        var source=AsNzPrivateAutoSourceLoader.Load(output);var row=source.Bundle.Records[0];
        var selected=new AsNz2021LocalAutoLookupService().Select(source,"VR","VR",row.CategoricalConditions,row.NumericalConditions);
        Assert.Equal(AutoLookupState.Resolved,selected.State);
        Assert.Contains(stage.Sources[0].Sha256,selected.Source);
        Assert.Contains(SourceCellStatus.SOURCE_CELL_CHECKED.ToString(),selected.Source);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownOrDuplicateAmendmentIdentityCannotBeReviewReady(bool duplicate)
    {
        var stage=Valid();
        if(!duplicate)stage=stage with {Amendments=["Invented Amendment"]};
        else
        {
            var amendment=Evidence("amendment.txt","SYNTHETIC AMENDMENT SOURCE");
            stage=stage with
            {
                Amendments=["Amendment 1:2023","Amendment 1:2023"],
                Sources=stage.Sources.Add(new("amendment",amendment.File,amendment.Sha256,"Amendment 1:2023")),
                Rights=stage.Rights! with {SourceHashes=stage.Rights.SourceHashes.Add(amendment.Sha256)},
                Adoption=stage.Adoption! with {Tables=stage.Adoption.Tables.Select(t=>t with {Amendment1Decision="APPLIED"}).ToImmutableArray()}
            };
        }
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
    [Theory]
    [InlineData("face")]
    [InlineData("case")]
    public void ReconciledCellLocatorCannotContradictLookupFaceOrCase(string contradiction)
    {
        var row=AsNzSlopedAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="Cpe"&&r.CategoricalConditions.GetValueOrDefault("RoofFace")=="Upwind"&&r.CategoricalConditions.GetValueOrDefault("Zone")=="0");
        var stage=Valid(row);var cell=stage.Cells[0];
        cell=cell with {Alternative=row.CategoricalConditions["Case"],WindIncidence=row.CategoricalConditions["RoofFace"]};
        cell=contradiction=="face"?cell with {WindIncidence="Downwind"}:cell with {Alternative=cell.Alternative=="1"?"2":"1"};
        cell=cell with {Reconciliation=cell.Reconciliation! with {CellHash=AsNzPrivateDatasetPipeline.CellHash(cell)}};
        stage=stage with {Cells=[cell]};
        Assert.False(AsNzPrivateDatasetPipeline.Validate(stage,Root).ReadyForPrivateReview);
    }
}
