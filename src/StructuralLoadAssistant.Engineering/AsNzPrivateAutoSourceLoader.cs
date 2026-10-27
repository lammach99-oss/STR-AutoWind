using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace StructuralLoadAssistant.Engineering;
public sealed record PrivateAutoAuthorization(bool SoftwareUseAuthorized,string RightsFile,string RightsSha256,string AdoptionFile,string AdoptionSha256,ImmutableDictionary<string,string> TableDecisions,string StagingSha256="");
public static class AsNzPrivateAutoSourceLoader
{
    public static AsNzAutoSource Load(string directory)
    {
        var auth=JsonSerializer.Deserialize<PrivateAutoAuthorization>(File.ReadAllText(Path.Combine(directory,"auto-authorization.json")))??throw new InvalidDataException("Private authorization missing");
        // A boolean/evidence filename is not a substitute for source-bound permissions and row reconciliation.
        var stagingBytes=AsNzPrivateDatasetPipeline.ReadChecked(directory,new("source-staging.json",auth.StagingSha256));
        var staging=AsNzPrivateDatasetPipeline.ParseVerifiedStage(stagingBytes,auth.StagingSha256);
        var stagingReport=AsNzPrivateDatasetPipeline.Validate(staging,directory);
        if(!stagingReport.ReadyForPrivateReview)throw new InvalidDataException("Private source staging blocked: "+string.Join("; ",stagingReport.Issues.Select(i=>i.Code)));
        if(auth.RightsFile!=staging.Rights!.Evidence!.File||auth.RightsSha256!=staging.Rights.Evidence.Sha256||auth.AdoptionFile!=staging.Adoption!.Evidence!.File||auth.AdoptionSha256!=staging.Adoption.Evidence.Sha256||
            auth.TableDecisions.Count!=staging.Adoption.Tables.Length||staging.Adoption.Tables.Any(t=>auth.TableDecisions.GetValueOrDefault(t.Table)!=t.EvidenceLocator))
            throw new InvalidDataException("Authorization and staged rights/adoption scope differ");
        string Evidence(string file,string sha)
        {
            StandardsDataBundle.ValidateSafeRelativePath(file);
            var path=Path.Combine(directory,file);
            if(!File.Exists(path)||!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Private authorization evidence hash mismatch");
            return file+" SHA256="+sha;
        }
        var rights=Evidence(auth.RightsFile,auth.RightsSha256);var adoption=Evidence(auth.AdoptionFile,auth.AdoptionSha256);
        var bundle=StandardsDataBundle.LoadFromDirectory(directory);
        if(bundle.Manifest.ContentHash!=AsNzPrivateDatasetPipeline.BuildBundle(staging).Manifest.ContentHash)
            throw new InvalidDataException("Bundle records/manifest differ from reconciled source cells");
        var check=StandardsDataBundleValidator.GenerateIntakePreflight(bundle);
        if(!auth.SoftwareUseAuthorized||!check.SemanticHashValid||!check.ComponentBytesCoherent||bundle.Manifest.CanonicalHashAlgorithmVersion!="3.0"||bundle.Manifest.Family!=StandardFamily.AsNz1170_2||bundle.Manifest.Rights is not (DataRightsClassification.LocalLicensedOnly or DataRightsClassification.Restricted))throw new InvalidDataException("Private source integrity/rights/edition blocked");
        return AsNzAutoLookupReceipts.BindPrivateSource(new(bundle,rights,adoption,auth.TableDecisions,true));
    }
}
