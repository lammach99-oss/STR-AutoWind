using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.RoofMapping;
/// <summary>Reviewed one-way panel on two parallel support lines. Coordinates and area are true 3D SI.</summary>
public static class PressureSurfaceTributaryMapper
{
    public static ImmutableArray<PressureZonePolygon> Intersect(PressureZonePolygon surface,ImmutableArray<Vec3> boundary)
    {
        var plane=RoofPlaneCoordinateSystem.Create(surface.Id,surface.Vertices);
        if(boundary.Any(p=>Math.Abs((p-plane.Origin).Dot(plane.Normal))>1e-6))throw new ArgumentException("Zone boundary must be on physical surface plane");
        NetTopologySuite.Geometries.Polygon Polygon(ImmutableArray<Vec3> vertices)
        {
            var uv=vertices.Select(plane.Project).ToImmutableArray();var error=ZonePolygonEngine.Validate(uv);if(error!=null)throw new ArgumentException(error);
            var coords=uv.Select(p=>new NetTopologySuite.Geometries.Coordinate(p.U,p.V)).ToList();if(!coords[0].Equals2D(coords[^1]))coords.Add(coords[0].Copy());return new NetTopologySuite.Geometries.GeometryFactory().CreatePolygon(coords.ToArray());
        }
        var result=NetTopologySuite.Operation.OverlayNG.OverlayNGRobust.Overlay(Polygon(surface.Vertices),Polygon(boundary),NetTopologySuite.Operation.Overlay.SpatialFunction.Intersection);
        var output=ImmutableArray.CreateBuilder<PressureZonePolygon>();
        for(int i=0;i<result.NumGeometries;i++)if(result.GetGeometryN(i) is NetTopologySuite.Geometries.Polygon p&&p.Area>1e-10){if(p.NumInteriorRings!=0)throw new ArgumentException("Holes must be explicitly partitioned into physical panels");output.Add(surface with {Vertices=p.ExteriorRing.Coordinates.SkipLast(1).Select(c=>plane.Unproject(new(c.X,c.Y))).ToImmutableArray()});}
        return output.ToImmutable();
    }
    static Vec3 Add(Vec3 a,Vec3 b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static TributaryMapping Map(PressureZonePolygon panel,double pressurePa,ImmutableArray<FrameGeometry> frames,TributarySupportReview review,double forceToleranceN=0.01,double momentToleranceNm=0.01)
    {
        if(!double.IsFinite(pressurePa)||!double.IsFinite(forceToleranceN)||!double.IsFinite(momentToleranceNm)||forceToleranceN<=0||momentToleranceNm<=0||panel.Id!=review.PanelId||!review.OneWaySimplySupported||string.IsNullOrWhiteSpace(review.Reviewer)||string.IsNullOrWhiteSpace(review.Reason)||string.IsNullOrWhiteSpace(panel.Reviewer)||string.IsNullOrWhiteSpace(panel.Reason))throw new ArgumentException("Explicit physical normal and one-way support review required");
        if(Math.Abs(panel.OutwardNormal.Length-1)>1e-8||!double.IsFinite(panel.OutwardNormal.Length))throw new ArgumentException("Reviewed outward unit normal required; Local3 is not evidence");
        if(review.FirstSupport.IsDefaultOrEmpty||review.SecondSupport.IsDefaultOrEmpty||review.FirstSupport.Concat(review.SecondSupport).Distinct().Count()!=review.FirstSupport.Length+review.SecondSupport.Length)throw new ArgumentException("Two distinct support chains required");
        FrameGeometry Get(string id)=>frames.SingleOrDefault(f=>f.Id==id)??throw new ArgumentException("Support not in actual selected frames: "+id);
        var first=review.FirstSupport.Select(Get).ToArray();var second=review.SecondSupport.Select(Get).ToArray();
        var origin=first[0].Start;var u=(first[0].End-origin).Unit();var offset=second[0].Start-origin;var v=(offset-u*offset.Dot(u)).Unit();double height=offset.Dot(v);
        if(height<=1e-8||Math.Abs(panel.OutwardNormal.Dot(u))>1e-7||Math.Abs(panel.OutwardNormal.Dot(v))>1e-7)throw new ArgumentException("Supports must lie in the reviewed physical panel plane");
        var points=panel.Vertices.Select(p=>{var d=p-origin;if(Math.Abs(d.Dot(u.Cross(v)))>1e-6)throw new ArgumentException("Nonplanar panel");return new UvPoint(d.Dot(u),d.Dot(v));}).ToImmutableArray();
        var error=ZonePolygonEngine.Validate(points);if(error!=null)throw new ArgumentException(error);
        if(points.Any(p=>p.V< -1e-7||p.V>height+1e-7))throw new ArgumentException("Panel outside support lines");
        double crossSum=0,cx=0,cy=0;for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];double cross=a.U*b.V-b.U*a.V;crossSum+=cross;cx+=(a.U+b.U)*cross;cy+=(a.V+b.V)*cross;}
        double area=Math.Abs(crossSum)/2;var centroid=Add(origin,Add(u*(cx/(3*crossSum)),v*(cy/(3*crossSum))));var traction=panel.OutwardNormal*(-pressurePa);var surfaceForce=traction*area;var surfaceMoment=centroid.Cross(surfaceForce);
        var chains=new[]{first,second};
        var limits=new List<(FrameGeometry Frame,int Side,double A,double B)>();
        for(int side=0;side<2;side++)foreach(var frame in chains[side])
        {
            var a=frame.Start-origin;var b=frame.End-origin;
            if((frame.End-frame.Start).Length<1e-8||Math.Abs((frame.End-frame.Start).Unit().Dot(u))<1-1e-8||Math.Abs(a.Dot(v)-side*height)>1e-6||Math.Abs(b.Dot(v)-side*height)>1e-6||Math.Abs(a.Dot(u.Cross(v)))>1e-6)throw new ArgumentException("Nonparallel/disconnected support geometry");
            limits.Add((frame,side,Math.Min(a.Dot(u),b.Dot(u)),Math.Max(a.Dot(u),b.Dot(u))));
        }
        double lo=points.Min(p=>p.U),hi=points.Max(p=>p.U);
        foreach(int side in new[]{0,1}){double end=lo;foreach(var span in limits.Where(l=>l.Side==side&&l.B>lo&&l.A<hi).OrderBy(l=>l.A)){double a=Math.Max(lo,span.A);if(Math.Abs(a-end)>1e-6)throw new ArgumentException("Support chain gap or overlap");end=Math.Min(hi,span.B);}if(Math.Abs(end-hi)>1e-6)throw new ArgumentException("Incomplete support chain");}
        double Reaction(double x,int side)
        {
            var intersections=new List<double>();
            for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];if(a.U==b.U)continue;if(x>=Math.Min(a.U,b.U)&&x<Math.Max(a.U,b.U))intersections.Add(a.V+(b.V-a.V)*(x-a.U)/(b.U-a.U));}
            intersections.Sort();if(intersections.Count%2!=0)throw new ArgumentException("Ambiguous polygon section");double weight=0;for(int i=0;i<intersections.Count;i+=2){double a=intersections[i],b=intersections[i+1];double upper=(b*b-a*a)/(2*height);weight+=side==0?b-a-upper:upper;}return weight;
        }
        var knots=points.Select(p=>p.U).Concat(limits.SelectMany(l=>new[]{l.A,l.B})).Where(x=>x>=lo&&x<=hi).Distinct().Order().ToArray();
        for(int subdivisions=1;subdivisions<=1024;subdivisions*=2)
        {
            var loads=ImmutableArray.CreateBuilder<FrameDistributedLoadSegment>();Vec3 force=default,moment=default;
            foreach(var span in limits)for(int k=0;k<knots.Length-1;k++)
            {
                double from=Math.Max(span.A,knots[k]),to=Math.Min(span.B,knots[k+1]);if(to-from<1e-9)continue;
                for(int j=0;j<subdivisions;j++)
                {
                    double a=from+(to-from)*j/subdivisions,b=from+(to-from)*(j+1)/subdivisions,eps=Math.Min(1e-9,(b-a)*1e-6);
                    var wa=traction*Reaction(a+eps,span.Side);var wb=traction*Reaction(b-eps,span.Side);
                    var pa=Add(origin,Add(u*a,v*(span.Side*height)));var pb=Add(origin,Add(u*b,v*(span.Side*height)));
                    double length=b-a;var f=Add(wa,wb)*(length/2);var m=Add(pa.Cross(Add(wa*2,wb)),pb.Cross(Add(wa,wb*2)))*(length/6);force=Add(force,f);moment=Add(moment,m);
                    double Station(Vec3 p)=>(p-span.Frame.Start).Dot((span.Frame.End-span.Frame.Start).Unit())/(span.Frame.End-span.Frame.Start).Length;
                    double sa=Station(pa),sb=Station(pb);loads.Add(sa<sb?new(span.Frame.Id,sa,sb,wa,wb,panel.Id):new(span.Frame.Id,sb,sa,wb,wa,panel.Id));
                }
            }
            double fr=(force-surfaceForce).Length,mr=(moment-surfaceMoment).Length;var check=new EquilibriumCheck(surfaceForce,surfaceMoment,force,moment,fr,mr,fr<=forceToleranceN&&mr<=momentToleranceNm);
            if(check.Passed)return new(loads.ToImmutable(),check,area,centroid);
        }
        throw new ArgumentException("TRANSFER_UNSUPPORTED: force/moment tolerances not met; no compensation loads created");
    }
}
