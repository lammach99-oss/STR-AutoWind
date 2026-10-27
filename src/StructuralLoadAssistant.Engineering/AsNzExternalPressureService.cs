using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;
public sealed record ExternalWindSpeedInput(LimitState State,CoefficientInputEvidence RegionalSpeed,CoefficientInputEvidence Climate,ImmutableArray<CoefficientInputEvidence> Directional,ImmutableArray<CoefficientInputEvidence> Terrain,ImmutableArray<CoefficientInputEvidence> Shielding,ImmutableArray<CoefficientInputEvidence> Topography,CoefficientInputEvidence Density,CoefficientInputEvidence Dynamics);
public sealed record ExternalSurfaceInput(string Id,string SurfaceClass,string RoofForm,double Pitch,AsNzRoofFace RoofFace,PressureZonePolygon Polygon,WindDirection WindFrom,LimitState State,string Alternative,string Applicability,CoefficientInputEvidence Cpe,CoefficientInputEvidence Ka,CoefficientInputEvidence Kce,CoefficientInputEvidence Kl,CoefficientInputEvidence Kp,double ParticipatingAreaM2,string StructuralClass);
public sealed record ExternalWindRequest(string ProjectId,string Country,string Region,string RevisionDecision,double ModelXWindFromBearingDegrees,ImmutableArray<ExternalWindSpeedInput> Speeds,ImmutableArray<ExternalSurfaceInput> Surfaces);
public sealed record ExternalSurfaceCase(string Id,string SurfaceId,WindDirection WindFrom,LimitState State,string Alternative,double? DesignSpeed,double? BasisPa,double? Shape,double? PressurePa,Vec3? TractionPa,string Table,string Status,ImmutableArray<CalculationTraceNode> Trace);
public sealed record ExternalWindRun(ExternalWindRequest Request,ImmutableArray<ExternalSurfaceCase> Cases,string Status)
{
    public string ContentHash=>SnapshotJson.Hash(new{Request,Cases,Status});
    public string InternalPressureMode=>"EngineerControlledSeparate";
    public bool NetPressureCalculated=>false;
    public bool CanCalculateNormativePressures=>false;
    public bool CanMutateCsi=>false;
}
/// <summary>Source-labeled explicit operands, independent of private normative table availability.</summary>
public static class AsNzExternalPressureService
{
    public static ExternalWindRun Execute(ExternalWindRequest request,CancellationToken ct=default)
    {
        if(request.Country is not ("AU" or "NZ")||string.IsNullOrWhiteSpace(request.Region)||string.IsNullOrWhiteSpace(request.RevisionDecision)||!double.IsFinite(request.ModelXWindFromBearingDegrees)||request.Surfaces.IsDefaultOrEmpty||request.Speeds.GroupBy(s=>s.State).Any(g=>g.Count()>1))throw new ArgumentException("Explicit country/region/revision, unique speed states and physical surfaces required");
        if(request.Surfaces.Select(s=>$"{s.State}_{s.WindFrom}_{s.Id}_{s.Alternative}").Distinct().Count()!=request.Surfaces.Length)throw new ArgumentException("Duplicate surface case identity");
        var cases=ImmutableArray.CreateBuilder<ExternalSurfaceCase>();
        foreach(var surface in request.Surfaces)
        {
            ct.ThrowIfCancellationRequested();var trace=ImmutableArray.CreateBuilder<CalculationTraceNode>();string table="";
            string id=$"{surface.State}_{surface.WindFrom}_{surface.Id}_{surface.Alternative}";
            try
            {
                if(!Enum.IsDefined(surface.State)||!Enum.IsDefined(surface.WindFrom)||string.IsNullOrWhiteSpace(surface.Alternative)||string.IsNullOrWhiteSpace(surface.Applicability)||surface.ParticipatingAreaM2<=0||!double.IsFinite(surface.ParticipatingAreaM2)||surface.StructuralClass is not ("MWFRS" or "ImmediateCladdingSupport"))throw new ArgumentException("Surface applicability, area and structural class required");
                if(surface.Polygon.Id!=surface.Id||surface.Polygon.Vertices.Length<3||surface.Polygon.Vertices.Any(p=>!double.IsFinite(p.Length))||Math.Abs(surface.Polygon.OutwardNormal.Length-1)>1e-8||!double.IsFinite(surface.Polygon.OutwardNormal.Length)||string.IsNullOrWhiteSpace(surface.Polygon.Reviewer)||string.IsNullOrWhiteSpace(surface.Polygon.Reason))throw new ArgumentException("Physical polygon and reviewed outward normal required");
                var origin=surface.Polygon.Vertices[0];
                if(surface.Polygon.Vertices.Any(p=>Math.Abs((p-origin).Dot(surface.Polygon.OutwardNormal))>1e-6))throw new ArgumentException("Physical surface must be planar and perpendicular to reviewed normal");
                Vec3 areaVector=default;for(int i=1;i<surface.Polygon.Vertices.Length-1;i++){var cross=(surface.Polygon.Vertices[i]-origin).Cross(surface.Polygon.Vertices[i+1]-origin);areaVector=new(areaVector.X+cross.X,areaVector.Y+cross.Y,areaVector.Z+cross.Z);}
                if(areaVector.Length<1e-8)throw new ArgumentException("Degenerate surface");
                table=surface.SurfaceClass switch {"Windward"=>"Table 5.2(A)","Leeward"=>"Table 5.2(B)","Side"=>"Table 5.2(C)","Roof"=>AsNzRoofApplicability.Evaluate(surface.RoofForm,surface.Pitch,surface.RoofFace) is var route&&route.Supported?route.Table:throw new ArgumentException("Unsupported roof form/face/pitch"),_=>throw new ArgumentException("Unknown surface class")};
                if(surface.SurfaceClass=="Leeward"&&surface.RoofForm is not ("Gable" or "Hip")&&!surface.Applicability.StartsWith("SOURCE_APPLICABILITY_REVIEW:",StringComparison.Ordinal))throw new ArgumentException("Flat/monoslope leeward needs explicit source applicability adjudication");
                double Value(string key,CoefficientInputEvidence e,bool signed=false)
                {
                    if(e==null||!double.IsFinite(e.Value)||(!signed&&e.Value<=0)||string.IsNullOrWhiteSpace(e.Document)||string.IsNullOrWhiteSpace(e.ClauseOrTable)||string.IsNullOrWhiteSpace(e.SourceNotes))throw new ArgumentException("Source-labeled finite input required: "+key);
                    string unit=key=="VR"?"m/s":key=="rho"?"kg/m3":"dimensionless";
                    if(e.Unit!=unit)throw new ArgumentException("Input units must be "+unit+": "+key);
                    trace.Add(new(id+"-"+key,"EngineerSourceLabeledInput",key,"Engineer supplied operand",$"{key}={e.Value:R}",$"{e.Value:R} {e.Unit}",e.Value,e.Unit,e.Document+" / "+e.ClauseOrTable,e.SourceNotes+" / NOT independently qualified",[],[]));return e.Value;
                }
                if(surface.Cpe==null||surface.Ka==null||surface.Kce==null||surface.Kl==null||surface.Kp==null||surface.Cpe.ClauseOrTable==null)throw new ArgumentException("Missing external operand / source locator");
                if(!surface.Cpe.ClauseOrTable.Contains(table,StringComparison.Ordinal))throw new ArgumentException("Cpe evidence table contradicts source route");
                var speed=request.Speeds.SingleOrDefault(s=>s.State==surface.State)??throw new ArgumentException("Limit-state speed inputs unavailable");
                if(new[]{speed.Directional.Length,speed.Terrain.Length,speed.Shielding.Length,speed.Topography.Length}.Any(n=>n!=8))throw new ArgumentException("Eight independent direction factors required");
                double vr=Value("VR",speed.RegionalSpeed),mc=Value("Mc",speed.Climate);var cardinal=new double[8];
                for(int b=0;b<8;b++)cardinal[b]=vr*mc*Value("Md"+b,speed.Directional[b])*Value("Mz"+b,speed.Terrain[b])*Value("Ms"+b,speed.Shielding[b])*Value("Mt"+b,speed.Topography[b]);
                double angle=surface.WindFrom switch {WindDirection.PositiveX=>0,WindDirection.NegativeX=>180,WindDirection.PositiveY=>90,_=>270};
                double bearing=((request.ModelXWindFromBearingDegrees-angle)%360+360)%360;
                double design=AsNz2021MatrixEngine.DesignSpeed(cardinal,bearing,surface.State),rho=Value("rho",speed.Density),dyn=Value("Cdyn",speed.Dynamics);
                double cp=Value("Cpe",surface.Cpe,true),ka=Value("Ka",surface.Ka),kce=Value("Kce",surface.Kce),kl=Value("Kl",surface.Kl),kp=Value("Kp",surface.Kp);
                if(kp!=1&&(cp>=0||surface.SurfaceClass is not ("Roof" or "Side")||!surface.Kp.SourceNotes.StartsWith("PERMEABILITY_APPLICABILITY_REVIEW:",StringComparison.Ordinal)))throw new ArgumentException("Permeability reduction needs eligible negative roof/side pressure and explicit source review");
                if(ka*kce<0.8-1e-12)throw new ArgumentException("Ka*Kce below source floor 0.8");
                if(surface.StructuralClass=="MWFRS"&&kl!=1)throw new ArgumentException("Local cladding amplification requires immediate-support classification");
                double q=0.5*rho*design*design*dyn,shape=cp*ka*kce*kl*kp,pe=AsNz2021MatrixEngine.ExternalPressure(design,rho,dyn,cp,ka,kce,kl,kp);
                trace.Add(new(id+"-pe","External pressure","pe","Vsit=VR Mc Md Mz Ms Mt; Vdes=max interpolated sector, ULS>=30; pe=0.5 rho Vdes^2 Cdyn Cpe Ka Kce Kl Kp",$"eight Vsit=[{string.Join(",",cardinal.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))}]; wind FROM bearing={bearing:R}; Vdes={design:R}; pe=0.5*{rho:R}*{design:R}^2*{dyn:R}*{cp:R}*{ka:R}*{kce:R}*{kl:R}*{kp:R}",$"{pe:R} Pa",pe,"Pa","AS/NZS 1170.2:2021 Cl 2.3, Eq 2.4(1), 5.2(2); "+table,"EXTERNAL ONLY / -pe*n_out / separate alternatives; internal controlled by engineer",[],[]));
                cases.Add(new(id,surface.Id,surface.WindFrom,surface.State,surface.Alternative,design,q,shape,pe,surface.Polygon.OutwardNormal*(-pe),table,"EXTERNAL_REVIEW_COMPUTED / NOT_QUALIFIED",trace.ToImmutable()));
            }
            catch(ArgumentException e){cases.Add(new(id,surface.Id,surface.WindFrom,surface.State,surface.Alternative,null,null,null,null,null,table,"EXTERNAL_BLOCKED: "+e.Message,trace.ToImmutable()));}
        }
        bool all=cases.All(c=>c.PressurePa.HasValue),any=cases.Any(c=>c.PressurePa.HasValue);
        return new(request,cases.ToImmutable(),all?"EXTERNAL_REVIEW_COMPUTED / NOT_QUALIFIED":any?"PARTIAL_EXTERNAL_REVIEW / NOT_QUALIFIED":"EXTERNAL_BLOCKED / NOT_QUALIFIED");
    }
}
