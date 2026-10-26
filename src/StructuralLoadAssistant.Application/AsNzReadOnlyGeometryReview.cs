using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Application;

public sealed record AsNzGeometryCandidates(string ModelFingerprint,double? LengthM,double? WidthM,double? PeakHeightM,double? AverageHeightM,
    double? PitchDegrees,double? SlopeBearingDegrees,bool AutomaticGeometryQualified,string Status,ImmutableArray<string> Evidence);
/// <summary>Consumes already SI-normalized read-only scan coordinates. Never infers natural ground or qualifies a selected subset as a complete envelope.</summary>
public static class AsNzReadOnlyGeometryReview
{
    public static AsNzGeometryCandidates FromSnapshot(CsiModelSnapshot snapshot,double? naturalGroundM,string datumEvidence,string selectionEvidence)
    {
        var evidence=ImmutableArray.CreateBuilder<string>();
        evidence.Add("READ ONLY / snapshot coordinates in metres; no further native-unit conversion");
        evidence.Add("Model fingerprint: "+snapshot.GeometryFingerprint);
        evidence.Add("Selected shell IDs: "+string.Join(",",snapshot.Shells.Select(s=>s.Id)));
        evidence.Add("Selection evidence: "+selectionEvidence);evidence.Add("Natural ground evidence: "+datumEvidence);
        var points=snapshot.Shells.SelectMany(s=>s.Vertices).Where(v=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z)).ToArray();
        if(points.Length<3)return new(snapshot.GeometryFingerprint,null,null,null,null,null,null,false,"MODEL_GEOMETRY_REVIEW_REQUIRED / NO SELECTED ROOF POLYGONS",evidence.ToImmutable());
        double xmin=points.Min(p=>p.X),xmax=points.Max(p=>p.X),ymin=points.Min(p=>p.Y),ymax=points.Max(p=>p.Y),zmin=points.Min(p=>p.Z),zmax=points.Max(p=>p.Z);
        evidence.Add($"Candidate bounds X [{xmin:R},{xmax:R}], Y [{ymin:R},{ymax:R}], Z [{zmin:R},{zmax:R}] m; frame endpoints excluded");
        bool datum=naturalGroundM.HasValue&&double.IsFinite(naturalGroundM.Value)&&!string.IsNullOrWhiteSpace(datumEvidence);
        double? pitch=null,bearing=null;
        if(!string.IsNullOrWhiteSpace(selectionEvidence))
        {
            Vec3? normal=null;bool valid=true;
            foreach(var shell in snapshot.Shells)
            {
                if(shell.Vertices.Length<3){valid=false;break;}
                var p0=shell.Vertices[0];var n=(shell.Vertices[1]-p0).Cross(shell.Vertices[2]-p0);
                if(n.Length<1e-8){valid=false;break;}n=n.Unit();if(n.Z<0)n=n*-1;
                if(Math.Abs(n.Z)<1e-6||shell.Vertices.Any(p=>Math.Abs((p-p0).Dot(n))>0.005)||normal.HasValue&&Math.Abs(normal.Value.Dot(n)-1)>1e-5){valid=false;break;}
                normal=n;
            }
            if(valid&&normal.HasValue)
            {
                var n=normal.Value;pitch=Math.Acos(Math.Clamp(n.Z,-1,1))*180/Math.PI;
                bearing=(Math.Atan2(-n.Y,-n.X)*180/Math.PI+360)%360;
                evidence.Add($"Planar selected-roof candidate: pitch {pitch:R}°, rise bearing from global X {bearing:R}°; tolerance 0.005m; NOT a full building envelope validation");
            }
        }
        return new(snapshot.GeometryFingerprint,xmax-xmin,ymax-ymin,datum?zmax-naturalGroundM:null,datum?(zmax+zmin)/2-naturalGroundM:null,pitch,bearing,false,
            "MODEL_GEOMETRY_REVIEW_REQUIRED / selected-envelope, roof-height definition and natural-ground datum need engineer review",evidence.ToImmutable());
    }
}
