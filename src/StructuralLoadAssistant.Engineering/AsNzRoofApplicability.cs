namespace StructuralLoadAssistant.Engineering;

public enum AsNzRoofFace { Upwind, Downwind, Crosswind }
public sealed record AsNzRoofBranch(string RoofForm, double Pitch, AsNzRoofFace Face, string Table, string Status, string Reason)
{
    public bool Supported => Status == "SOURCE_ROWS_REQUIRED";
}

/// <summary>Base-2021 table routing only. A route supplies no coefficient or qualification.</summary>
public static class AsNzRoofApplicability
{
    public static AsNzRoofBranch Evaluate(string roofForm, double pitch, AsNzRoofFace face)
    {
        AsNzRoofBranch Block(string reason)=>new(roofForm,pitch,face,"","BLOCKED",reason);
        if(!double.IsFinite(pitch)||pitch<0||pitch>=90||!Enum.IsDefined(face))return Block("Finite physical pitch and explicit face required");
        if(roofForm is not ("Flat" or "LowSlope" or "Monoslope" or "Gable" or "Hip"))return Block("Unsupported roof form");
        if((roofForm=="Flat"&&pitch!=0)||(roofForm=="LowSlope"&&pitch>=10))return Block("Roof-form and pitch contradiction");
        if(face==AsNzRoofFace.Crosswind && roofForm is "Monoslope" or "LowSlope")
            return Block("Crosswind incidence requires separate source applicability; no gable substitution");
        string table=roofForm=="Flat" || (face==AsNzRoofFace.Crosswind&&roofForm=="Gable") ||
            (face==AsNzRoofFace.Downwind&&roofForm=="Monoslope") || pitch<10 ? "Table 5.3(A)" :
            face==AsNzRoofFace.Upwind ? "Table 5.3(B)" : "Table 5.3(C)";
        return new(roofForm,pitch,face,table,"SOURCE_ROWS_REQUIRED",
            "Base-2021 PDF p61 / printed51; route only, rights/adoption and exact source rows still required");
    }

    // Wind direction and uphill bearing are both in model XY; true-north rotation is separate.
    public static AsNzRoofFace? FaceForWind(double uphillBearing,double windBearing)
    {
        if(!double.IsFinite(uphillBearing)||!double.IsFinite(windBearing))return null;
        double delta=((windBearing-uphillBearing)%360+360)%360;
        return delta==0?AsNzRoofFace.Upwind:delta==180?AsNzRoofFace.Downwind:
            delta is 90 or 270?AsNzRoofFace.Crosswind:null;
    }
}
