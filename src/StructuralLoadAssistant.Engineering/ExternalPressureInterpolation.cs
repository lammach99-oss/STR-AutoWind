using System.Globalization;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;
/// <summary>Engineer-provided bracket arithmetic only; never authenticates a source row.</summary>
public static class ExternalPressureInterpolation
{
    public static CoefficientInputEvidence Interpolate(double x,double low,double high,CoefficientInputEvidence a,CoefficientInputEvidence b,string alternativeA,string alternativeB,bool sameSign)
    {
        if(string.IsNullOrWhiteSpace(alternativeA)||alternativeA!=alternativeB||a.Unit!=b.Unit||a.Document!=b.Document||a.ClauseOrTable!=b.ClauseOrTable||sameSign&&Math.Sign(a.Value)!=Math.Sign(b.Value))throw new ArgumentException("Source scope, units, alternative and permitted sign branch must match");
        double value=AsNz2021InputInterpolation.Linear(x,low,high,a.Value,b.Value),weight=(x-low)/(high-low);
        string N(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
        return a with{Value=value,Provenance=DataProvenance.Unverified,SourceNotes=$"ENGINEER SUPPLIED / NOT AUTHENTICATED; {alternativeA}; x={N(x)}; endpoints [{N(low)}:{N(a.Value)}, {N(high)}:{N(b.Value)}]; weight={N(weight)}; {a.SourceNotes}; {b.SourceNotes}"};
    }
}
