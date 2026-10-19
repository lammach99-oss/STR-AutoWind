using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.RoofMapping;

/// <summary>Planar simple polygons with concave-aware triangulation. No shell mutation.</summary>
public static class RoofGeometry
{
    private const double Epsilon=1e-8;
    private readonly record struct Point(double X,double Y)
    {
        public static Point operator -(Point a,Point b)=>new(a.X-b.X,a.Y-b.Y);
        public static Point operator +(Point a,Point b)=>new(a.X+b.X,a.Y+b.Y);
        public static Point operator *(Point a,double b)=>new(a.X*b,a.Y*b);
    }
    public static double Local3Load(double pressurePa,Vec3 outwardNormal,Vec3 local3)
    {
        if(!double.IsFinite(pressurePa)) throw new ArgumentException("Pressure must be finite.");
        var dot=outwardNormal.Unit().Dot(local3.Unit());
        if(Math.Abs(Math.Abs(dot)-1)>1e-6) throw new ArgumentException("Local 3 and outward normal must be parallel; projection is unsupported.");
        return -pressurePa*dot;
    }
    public static ShellZoneClassification Classify(ShellGeometry shell,IEnumerable<RoofPressureZone> zones,IEnumerable<ShellGeometry>? peers=null)
    {
        if(!TryPolygon(shell.Vertices,shell.OutwardNormal,out _,out _) || !TryProjectedPolygon(shell.Vertices,out var polygon)) return ShellZoneClassification.GeometryInvalid;
        try { Local3Load(0,shell.OutwardNormal,shell.Local3); }
        catch(ArgumentException){return ShellZoneClassification.GeometryInvalid;}
        foreach(var peer in peers??[])
        {
            if(peer.Id==shell.Id||peer.RoofPlaneId!=shell.RoofPlaneId||!Coplanar(peer.Vertices,shell.Vertices[0],shell.OutwardNormal))continue;
            if(TryPolygon(peer.Vertices,shell.OutwardNormal,out _,out _)&&TryProjectedPolygon(peer.Vertices,out var other)&&IntersectionArea(polygon,other)>Epsilon)return ShellZoneClassification.OverlapsAnotherShell;
        }
        var intersections=new List<double>();
        foreach(var zone in zones.Where(z=>z.RoofPlaneId==shell.RoofPlaneId))
        {
            // Pressure zones are plan-view regions. Their drawing elevation need not be on this roof slope.
            if(!TryProjectedPolygon(zone.Vertices,out var area))return ShellZoneClassification.GeometryInvalid;
            intersections.Add(IntersectionArea(polygon,area));
        }
        var shellArea=Math.Abs(SignedArea(polygon));var positive=intersections.Where(a=>a>Epsilon).ToArray();
        if(positive.Length==0)return ShellZoneClassification.Unmapped;
        return positive.Length==1&&Math.Abs(positive[0]-shellArea)<=Epsilon*Math.Max(1,shellArea)?ShellZoneClassification.FullyInsideZone:ShellZoneClassification.CrossesZoneBoundary;
    }
    public static bool IsValidZone(RoofPressureZone zone)=>double.IsFinite(zone.NetPressurePa)&&zone.Vertices.Length>=3&&TryNormal(zone.Vertices,out var normal)&&TryPolygon(zone.Vertices,normal,out _,out _);
    private static bool TryProjectedPolygon(ImmutableArray<Vec3> vertices,out List<Point> polygon)=>
        TryPolygon(vertices.Select(v=>new Vec3(v.X,v.Y,0)).ToImmutableArray(),new(0,0,1),out polygon,out _);
    private static bool TryNormal(ImmutableArray<Vec3> vertices,out Vec3 normal)
    {
        normal=default;if(vertices.Length<3)return false;
        for(var i=0;i<vertices.Length;i++)
        {
            var a=vertices[i];var b=vertices[(i+1)%vertices.Length];
            normal=new(normal.X+(a.Y-b.Y)*(a.Z+b.Z),normal.Y+(a.Z-b.Z)*(a.X+b.X),normal.Z+(a.X-b.X)*(a.Y+b.Y));
        }
        return double.IsFinite(normal.Length)&&normal.Length>Epsilon;
    }
    private static bool Coplanar(ImmutableArray<Vec3> vertices,Vec3 origin,Vec3 normal)
    {
        if(vertices.IsDefaultOrEmpty||!double.IsFinite(normal.Length)||normal.Length<Epsilon)return false;
        var unit=normal.Unit();return vertices.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z)&&Math.Abs((p-origin).Dot(unit))<=1e-6);
    }
    private static bool TryPolygon(ImmutableArray<Vec3> vertices,Vec3 normal,out List<Point> polygon,out int drop)
    {
        polygon=[];drop=2;if(vertices.Length<3||!Coplanar(vertices,vertices[0],normal))return false;
        var ax=Math.Abs(normal.X);var ay=Math.Abs(normal.Y);var az=Math.Abs(normal.Z);drop=ax>=ay&&ax>=az?0:ay>=az?1:2;
        foreach(var p in vertices)polygon.Add(drop==0?new(p.Y,p.Z):drop==1?new(p.X,p.Z):new(p.X,p.Y));
        if(Math.Abs(SignedArea(polygon))<=Epsilon)return false;
        for(var i=0;i<polygon.Count;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];if(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<Epsilon)return false;
            for(var j=i+1;j<polygon.Count;j++)
            {
                if(j==i+1||i==0&&j==polygon.Count-1)continue;
                if(SegmentsIntersect(a,b,polygon[j],polygon[(j+1)%polygon.Count]))return false;
            }
        }
        if(SignedArea(polygon)<0)polygon.Reverse();return true;
    }
    private static double Cross(Point a,Point b)=>a.X*b.Y-a.Y*b.X;
    private static double Side(Point a,Point b,Point p)=>Cross(b-a,p-a);
    private static double SignedArea(List<Point> p){var sum=0d;for(var i=0;i<p.Count;i++)sum+=Cross(p[i],p[(i+1)%p.Count]);return sum/2;}
    private static bool OnSegment(Point a,Point b,Point p)=>Math.Abs(Side(a,b,p))<Epsilon&&p.X>=Math.Min(a.X,b.X)-Epsilon&&p.X<=Math.Max(a.X,b.X)+Epsilon&&p.Y>=Math.Min(a.Y,b.Y)-Epsilon&&p.Y<=Math.Max(a.Y,b.Y)+Epsilon;
    private static bool SegmentsIntersect(Point a,Point b,Point c,Point d)=>Side(a,b,c)*Side(a,b,d)<-Epsilon&&Side(c,d,a)*Side(c,d,b)<-Epsilon||OnSegment(a,b,c)||OnSegment(a,b,d)||OnSegment(c,d,a)||OnSegment(c,d,b);
    private static List<List<Point>> Triangles(List<Point> polygon)
    {
        var remaining=new List<Point>(polygon);var triangles=new List<List<Point>>();
        while(remaining.Count>3)
        {
            var found=false;
            for(var i=0;i<remaining.Count;i++)
            {
                var prev=(i+remaining.Count-1)%remaining.Count;var next=(i+1)%remaining.Count;var a=remaining[prev];var b=remaining[i];var c=remaining[next];
                if(Side(a,b,c)<=Epsilon)continue;
                if(remaining.Where((_,j)=>j!=prev&&j!=i&&j!=next).Any(p=>Side(a,b,p)>=-Epsilon&&Side(b,c,p)>=-Epsilon&&Side(c,a,p)>=-Epsilon))continue;
                triangles.Add([a,b,c]);remaining.RemoveAt(i);found=true;break;
            }
            if(!found)
            {
                var index=Enumerable.Range(0,remaining.Count).FirstOrDefault(i=>Math.Abs(Side(remaining[(i+remaining.Count-1)%remaining.Count],remaining[i],remaining[(i+1)%remaining.Count]))<Epsilon,-1);
                if(index<0)throw new ArgumentException("Polygon cannot be triangulated.");remaining.RemoveAt(index);
            }
        }
        if(remaining.Count==3)triangles.Add(remaining);return triangles;
    }
    private static double IntersectionArea(List<Point> a,List<Point> b)
    {
        var total=0d;
        foreach(var ta in Triangles(a))foreach(var tb in Triangles(b))
        {
            var clipped=new List<Point>(ta);
            for(var i=0;i<3&&clipped.Count>0;i++)
            {
                var start=tb[i];var end=tb[(i+1)%3];var input=clipped;clipped=[];var previous=input[^1];var prevSide=Side(start,end,previous);
                foreach(var current in input)
                {
                    var currentSide=Side(start,end,current);
                    if((currentSide>=-Epsilon)!=(prevSide>=-Epsilon)){var t=prevSide/(prevSide-currentSide);clipped.Add(previous+(current-previous)*t);}
                    if(currentSide>=-Epsilon)clipped.Add(current);previous=current;prevSide=currentSide;
                }
            }
            if(clipped.Count>=3)total+=Math.Abs(SignedArea(clipped));
        }
        return total;
    }
}
