using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Csi.Fake;
public static class ExternalWindFixture
{
    public static CoefficientInputEvidence Input(string key,double value,string table="Synthetic operand",string unit="dimensionless")=>new(key,value,unit,DataProvenance.SyntheticDemoFixture,"Invented public arithmetic fixture",table,"Synthetic assumption for software test; NOT SOURCE QUALIFIED",DateTimeOffset.UnixEpoch);
    public static CsiModelSnapshot Model()=>new("external-synthetic","SYNTHETIC NOT ACTUAL CSI","Fake","1","fixture","fixture",[],[],[],[new("bottom","wall",new(0,0,0),new(2,0,0)),new("top","wall",new(2,0,3),new(0,0,3))],[],[]);
    public static StructuralLoadAssistant.Domain.TributarySupportReview Support()=>new("wall",["bottom"],["top"],"Synthetic reviewer","Vertically spanning wall / software fixture",true);
    public static ExternalWindRequest Request(double pitch=0)
    {
        ImmutableArray<CoefficientInputEvidence> Eight(string name)=>Enumerable.Range(0,8).Select(i=>Input(name+i,1)).ToImmutableArray();
        var speeds=new[]{LimitState.Uls,LimitState.Sls}.Select(state=>new ExternalWindSpeedInput(state,Input("VR",state==LimitState.Uls?40:20,unit:"m/s"),Input("Mc",1),Eight("Md"),Eight("Mz"),Eight("Ms"),Eight("Mt"),Input("rho",1.2,unit:"kg/m3"),Input("Cdyn",1))).ToImmutableArray();
        double a=pitch*Math.PI/180;var wall=new PressureZonePolygon("wall",[new(0,0,0),new(2,0,0),new(2,0,3),new(0,0,3)],new(0,-1,0),"Synthetic reviewer","Test physical wall");
        var roof=new PressureZonePolygon("roof",[new(0,0,3),new(2,0,3),new(2,3*Math.Cos(a),3+3*Math.Sin(a)),new(0,3*Math.Cos(a),3+3*Math.Sin(a))],new(0,-Math.Sin(a),Math.Cos(a)),"Synthetic reviewer","Test roof plane");
        var surfaces=ImmutableArray.CreateBuilder<ExternalSurfaceInput>();
        foreach(var state in new[]{LimitState.Uls,LimitState.Sls})foreach(var direction in Enum.GetValues<WindDirection>())
        {
            surfaces.Add(new("wall","Windward",pitch==0?"Flat":"Monoslope",pitch,AsNzRoofFace.Upwind,wall,direction,state,"COMMON","Synthetic ground-supported lowrise",Input("Cpe",0.7,"Table 5.2(A) / synthetic"),Input("Ka",1),Input("Kce",1),Input("Kl",1),Input("Kp",1),6,"MWFRS"));
            foreach(string alt in new[]{"A","B"})surfaces.Add(new("roof","Roof",pitch==0?"Flat":"Monoslope",pitch,AsNzRoofFace.Downwind,roof,direction,state,alt,"Single face explicitly adjudicated for arithmetic only",Input("Cpe",alt=="A"?-0.6:0.15,"Table 5.3(A) / synthetic"),Input("Ka",1),Input("Kce",1),Input("Kl",1),Input("Kp",1),6,"MWFRS"));
        }
        return new("external-test","AU","SYNTHETIC","Base 2021 / adoption unverified",0,speeds,surfaces.ToImmutable());
    }
}
