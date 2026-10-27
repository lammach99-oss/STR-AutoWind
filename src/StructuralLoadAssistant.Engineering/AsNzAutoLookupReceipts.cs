using System.Runtime.CompilerServices;

namespace StructuralLoadAssistant.Engineering;

/// <summary>
/// Ephemeral evidence of an actual local selector run. JSON, record copies and request options
/// cannot create a receipt. Reopened requests must rerun lookup rather than inherit trust.
/// </summary>
internal static class AsNzAutoLookupReceipts
{
    internal sealed record Receipt(bool Complete,bool Synthetic,bool ContextValid)
    {
        internal string Status => Synthetic ? "SYNTHETIC AUTO LOOKUP / NOT FOR DESIGN" : "PRIVATE LOCAL REVIEW / NOT QUALIFIED FOR DESIGN";
    }
    static readonly ConditionalWeakTable<EngineeringCalculationRequest,Receipt> Requests=new();
    static readonly ConditionalWeakTable<AsNzAutoSource,object> Sources=new();
    static readonly ConditionalWeakTable<StandardsDataBundle,Integrity> Bundles=new();
    sealed record Integrity(bool Valid);

    internal static void Bind(EngineeringCalculationRequest request,bool complete,bool synthetic,bool contextValid=false)=>Requests.Add(request,new(complete,synthetic,contextValid));
    internal static bool TryGet(EngineeringCalculationRequest request,out Receipt? receipt)=>Requests.TryGetValue(request,out receipt);
    internal static AsNzAutoSource BindPrivateSource(AsNzAutoSource source){Sources.Add(source,new());return source;}
    internal static bool IsPrivateSourceVerified(AsNzAutoSource source)=>Sources.TryGetValue(source,out _);
    internal static bool HasCoherentBytes(StandardsDataBundle bundle)=>Bundles.GetValue(bundle,b=>
    {
        var check=StandardsDataBundleValidator.GenerateIntakePreflight(b);
        return new(b.Manifest.CanonicalHashAlgorithmVersion=="3.0" && check.SemanticHashValid && check.ComponentBytesCoherent);
    }).Valid;
}
