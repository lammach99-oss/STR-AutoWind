using System.Collections.Immutable;
using System.Text.Json;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;

namespace StructuralLoadAssistant.Tests;

/// <summary>Synthetic software integrity assertions; these are not standards benchmarks.</summary>
public sealed class AsNzLookupTrustTests
{
    static AutoLookupValue Select(AsNzAutoSource source)
    {
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mz");
        return new AsNz2021LocalAutoLookupService().Select(source,"Mz","Mz",row.CategoricalConditions,row.NumericalConditions);
    }

    [Fact]
    public void DirectSelectorRejectsTamperedManifestIdentity()
    {
        var source=AsNzAutoLookupFixture.Source();
        var changed=new StandardsDataBundle(source.Bundle.Manifest with {BundleId="WRONG-PACKAGE"},source.Bundle.Records,
            rawComponentBytes:source.Bundle.RawComponentBytes,isBytesVerified:true);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,Select(source with {Bundle=changed}).State);
    }

    [Fact]
    public void DirectSelectorRejectsChangedCoefficientWithOriginalComponentBytes()
    {
        var source=AsNzAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="Mz");
        var records=source.Bundle.Records.Replace(row,row with {Value=9});
        var altered=new StandardsDataBundle(source.Bundle.Manifest,records,rawComponentBytes:source.Bundle.RawComponentBytes,isBytesVerified:true);
        altered=new(altered.Manifest with {ContentHash=altered.ComputeCanonicalHash()},records,rawComponentBytes:altered.RawComponentBytes,isBytesVerified:true);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,Select(source with {Bundle=altered}).State);
    }

    [Fact]
    public void DirectSelectorRejectsMissingAndTamperedComponentBytes()
    {
        var source=AsNzAutoLookupFixture.Source();
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,Select(source with {Bundle=new(source.Bundle.Manifest,source.Bundle.Records)}).State);
        var raw=source.Bundle.RawComponentBytes.SetItem("records.json",new byte[]{123,125}.ToImmutableArray());
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,Select(source with {Bundle=new(source.Bundle.Manifest,source.Bundle.Records,rawComponentBytes:raw,isBytesVerified:true)}).State);
    }

    [Fact]
    public void ConstructedPrivateRightsStringsCannotReplaceVerifiedLocalEvidence()
    {
        var source=AsNzAutoLookupFixture.Source();
        var bundle=new StandardsDataBundle(source.Bundle.Manifest with {Rights=DataRightsClassification.LocalLicensedOnly},source.Bundle.Records,
            rawComponentBytes:source.Bundle.RawComponentBytes,isBytesVerified:true);
        bundle=new(bundle.Manifest with {ContentHash=bundle.ComputeCanonicalHash()},bundle.Records,rawComponentBytes:bundle.RawComponentBytes,isBytesVerified:true);
        Assert.Equal(AutoLookupState.RightsOrEditionBlocked,Select(source with {Bundle=bundle,Synthetic=false,RightsReference="CLAIMED",AdoptionReference="CLAIMED"}).State);
    }

    [Fact]
    public void ImportedAutomaticFlagsCannotAuthenticateLookup()
    {
        var genuine=AsNzAutoLookupFixture.Resolve();
        var imported=JsonSerializer.Deserialize<EngineeringCalculationRequest>(JsonSerializer.Serialize(genuine.Request))!;
        var run=AsNz2021ReviewCalculation.Execute(imported);
        Assert.False(run.NumericalComputationPossible);
        Assert.Contains(run.Issues,i=>i.Code=="AUTO_LOOKUP_PROVENANCE_UNVERIFIED");
        Assert.DoesNotContain("SYNTHETIC AUTO LOOKUP",run.Request.Options.GetValueOrDefault("AutoLookupStatus", ""));
        Assert.All(run.AsNz2021Cases,c=>Assert.Null(c.NetPa));
    }

    [Fact]
    public void CopyingReceiptOptionsCannotAuthenticateChangedGeometryOrFactors()
    {
        var request=AsNzAutoLookupFixture.Resolve().Request;
        var altered=request with {WindAngleDegrees=request.WindAngleDegrees+17};
        Assert.False(AsNz2021ReviewCalculation.Execute(altered).NumericalComputationPossible);
        var key=request.ExplicitCoefficients.Keys.First(k=>k.EndsWith("_Cpe",StringComparison.Ordinal));
        altered=request with {ExplicitCoefficients=request.ExplicitCoefficients.SetItem(key,request.ExplicitCoefficients[key] with {Value=2})};
        Assert.False(AsNz2021ReviewCalculation.Execute(altered).NumericalComputationPossible);
        Assert.True(AsNz2021ReviewCalculation.Execute(request).NumericalComputationPossible);
    }

    [Fact]
    public void UntrustedDisplayStatusCannotMasqueradeAsAutomaticLookup()
    {
        var request=AsNzMatrixFixture.Request();
        request=request with {Options=request.Options.SetItem("AutoLookupStatus","TRUSTED AUTO LOOKUP COMPLETE")};
        var run=AsNz2021ReviewCalculation.Execute(request);
        Assert.DoesNotContain("TRUSTED AUTO",run.Request.Options.GetValueOrDefault("AutoLookupStatus",""));
    }

    [Fact]
    public void PlainExplicitReviewKeepsOriginalRequestIdentityForExportBinding()
    {
        var request=AsNzMatrixFixture.Request();
        Assert.Same(request,AsNz2021ReviewCalculation.Execute(request).Request);
    }
}
