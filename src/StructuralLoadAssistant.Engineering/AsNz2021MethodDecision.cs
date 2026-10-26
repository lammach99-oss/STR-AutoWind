using System.Collections.Immutable;
namespace StructuralLoadAssistant.Engineering;
public sealed record AsNz2021MethodDecision(string GeometryClass,bool ReviewGeometrySupported,bool JurisdictionRecognized,bool NormativeLookupAvailable,
    double? WindRelativeSlopeBearing,double? HeightOverDepth,double? DepthOverBreadth,string Status,string SourceReference,ImmutableArray<string> Conditions)
{
    public static AsNz2021MethodDecision Evaluate(string roof,double pitch,double slopeBearing,double windBearing,double hd,double db,string jurisdiction,string region,double period,ImmutableArray<string> amendments)
    {
        bool finite=new[]{pitch,slopeBearing,windBearing,hd,db,period}.All(double.IsFinite)&&pitch>=0&&pitch<90&&hd>0&&db>0&&period>0;
        bool mono=roof.Equals("Monoslope",StringComparison.OrdinalIgnoreCase)||roof.Equals("Monopitch",StringComparison.OrdinalIgnoreCase);
        bool flat=roof.Equals("Flat",StringComparison.OrdinalIgnoreCase)&&pitch==0;
        bool supported=finite&&(mono||flat);
        bool country=jurisdiction is "AU" or "NZ";
        return new(pitch==0&&supported?"FLAT_OR_NEAR_FLAT":mono&&supported?"MONOSLOPE":"UNSUPPORTED",supported,country,false,
            double.IsFinite(windBearing-slopeBearing)?((windBearing-slopeBearing)%360+360)%360:null,double.IsFinite(hd)?hd:null,double.IsFinite(db)?db:null,
            supported?"LOOKUP_UNAVAILABLE / SOURCE RIGHTS REQUIRED / EXPLICIT REVIEW ONLY":"UNSUPPORTED / MODEL_GEOMETRY_REVIEW_REQUIRED",
            "AS/NZS 1170.2:2021 Cl 3.2–3.4, 4.2, 5.4.1; Tables 5.2(A–C), 5.3(A–C): exact condition/row selection requires qualified source data",
            [$"Jurisdiction {jurisdiction}; region {region}; R {period:R}; amendments [{string.Join(",",amendments)}] — adoption unverified",
             $"Wind-relative roof bearing {((windBearing-slopeBearing)%360+360)%360:R}°, pitch {pitch:R}°, h/d {hd:R}, d/b {db:R}",
             "AU Md Table 3.2(A) / NZ Table 3.2(B) selection requires jurisdiction and supplied evidenced operands",
             "A0 exception and complex upstream terrain require qualified applicability; supplied Mz,cat is explicit input, not a generic power law",
             "Monoslope/flat sheet identity does not select a normative roof table. Cases 1/2 and interpolation signs require engineer confirmation"]);
    }
}
