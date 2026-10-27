using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace StructuralLoadAssistant.Engineering;
public sealed record PrivateAutoAuthorization(bool SoftwareUseAuthorized,string RightsFile,string RightsSha256,string AdoptionFile,string AdoptionSha256,ImmutableDictionary<string,string> TableDecisions);
public static class AsNzPrivateAutoSourceLoader
{
    public static AsNzAutoSource Load(string directory)
    {
        var auth=JsonSerializer.Deserialize<PrivateAutoAuthorization>(File.ReadAllText(Path.Combine(directory,"auto-authorization.json")))??throw new InvalidDataException("Private authorization missing");
        string Evidence(string file,string sha)
        {
            StandardsDataBundle.ValidateSafeRelativePath(file);
            var path=Path.Combine(directory,file);
            if(!File.Exists(path)||!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Private authorization evidence hash mismatch");
            return file+" SHA256="+sha;
        }
        var rights=Evidence(auth.RightsFile,auth.RightsSha256);var adoption=Evidence(auth.AdoptionFile,auth.AdoptionSha256);
        var bundle=StandardsDataBundle.LoadFromDirectory(directory);
        var check=StandardsDataBundleValidator.GenerateIntakePreflight(bundle);
        if(!auth.SoftwareUseAuthorized||!check.SemanticHashValid||!check.ComponentBytesCoherent||bundle.Manifest.CanonicalHashAlgorithmVersion!="3.0"||bundle.Manifest.Family!=StandardFamily.AsNz1170_2||bundle.Manifest.Rights is not (DataRightsClassification.LocalLicensedOnly or DataRightsClassification.Restricted))throw new InvalidDataException("Private source integrity/rights/edition blocked");
        return new(bundle,rights,adoption,auth.TableDecisions,true);
    }
}
