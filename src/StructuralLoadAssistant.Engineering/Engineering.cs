using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public readonly record struct Pressure
{
    public double Pa { get; }
    public Pressure(double pa) { Numeric.Finite(pa); Pa = pa; }
    public double KPa => Pa / 1000;
    public static Pressure FromKPa(double value) => new(value * 1000);
    public LineLoad Over(Length height) => new(Pa * Numeric.Positive(height.Metres));
}
public readonly record struct Length
{
    public double Metres { get; }
    public Length(double metres) { Metres = Numeric.Positive(metres); }
}
public readonly record struct LineLoad
{
    public double NPerM { get; }
    public LineLoad(double nPerM) { Numeric.Finite(nPerM); NPerM = nPerM; }
    public double KNPerM => NPerM / 1000;
    public static LineLoad FromKNPerM(double value) => new(value * 1000);
}
internal static class Numeric
{
    internal static double Finite(double value) => double.IsFinite(value) ? value : throw new ArgumentException("A finite SI value is required.");
    internal static double Positive(double value) => Finite(value) > 0 ? value : throw new ArgumentException("A positive dimension is required.");
    internal static void Vector(Vec3 v) { Finite(v.X); Finite(v.Y); Finite(v.Z); }
    internal static void Required(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Identifier is required."); }
}
public static class EngineeringMath
{
    public static double Linear(double x, double x1, double y1, double x2, double y2)
    {
        Numeric.Finite(x); Numeric.Finite(x1); Numeric.Finite(x2); Numeric.Finite(y1); Numeric.Finite(y2);
        Numeric.Positive(x2-x1);
        if (x<x1 || x>x2) throw new ArgumentException("Interpolation outside the supplied range is unsupported.");
        return Numeric.Finite(y1 + (y2-y1) * ((x-x1)/(x2-x1)));
    }
    /// <summary>Composite Simpson integration; callers must split at pressure discontinuities.</summary>
    public static double Integrate(Func<double,double> f, double lower, double upper, int intervals = 256)
    {
        ArgumentNullException.ThrowIfNull(f); Numeric.Finite(lower); Numeric.Finite(upper); Numeric.Positive(upper-lower);
        if (intervals < 2 || intervals > 1_000_000 || intervals%2 != 0) throw new ArgumentException("An even interval count from 2 to 1000000 is required.");
        double h=(upper-lower)/intervals, sum=Numeric.Finite(f(lower))+Numeric.Finite(f(upper));
        for (int i=1; i<intervals; i++) sum+=(i%2==0?2:4)*Numeric.Finite(f(lower+i*h));
        return Numeric.Finite(sum*h/3);
    }
    public static double WindHeight(double globalZ, double groundZ)
    {
        var height=Numeric.Finite(globalZ)-Numeric.Finite(groundZ);
        if (height<0) throw new ArgumentException("Elevation lies below configured wind datum.");
        return Numeric.Finite(height);
    }
    public static Vec3 GlobalForce(double pressurePa, Vec3 outwardNormal)
    {
        Numeric.Finite(pressurePa); Numeric.Vector(outwardNormal);
        return outwardNormal.Unit()*-pressurePa;
    }
}
public enum FacadeMethod { EqualSplit, StaticallyEquivalent }
public sealed record PanelReactions(double LowerNPerM, double UpperNPerM, double ResultantNPerM, double FirstMomentN, ImmutableArray<string> Warnings);
public static class FacadeDistribution
{
    public static PanelReactions Calculate(double p1Pa, double p2Pa, double heightM, FacadeMethod method)
    {
        Numeric.Positive(heightM); Numeric.Finite(p1Pa); Numeric.Finite(p2Pa);
        return Integrate(z=>EngineeringMath.Linear(z,0,p1Pa,heightM,p2Pa),0,heightM,method);
    }
    public static PanelReactions Integrate(Func<double,double> pressure, double lowerM, double upperM, FacadeMethod method)
    {
        if (!Enum.IsDefined(method)) throw new ArgumentException("Unknown facade method.");
        var h=Numeric.Positive(upperM-lowerM);
        var resultant=EngineeringMath.Integrate(pressure,lowerM,upperM);
        var moment=EngineeringMath.Integrate(z=>pressure(z)*(z-lowerM),lowerM,upperM);
        var upper=method==FacadeMethod.EqualSplit?resultant/2:moment/h;
        var lower=resultant-upper;
        var warning=method==FacadeMethod.EqualSplit && Math.Abs(moment-upper*h)>1e-8*Math.Max(1,Math.Abs(moment)) && Math.Abs(resultant)<1e-8*Math.Max(1,Math.Abs(moment)/h);
        return new(lower,upper,resultant,moment,warning?["Opposing pressures cancel the resultant; Equal Split does not preserve the nonzero first moment."]:[]);
    }
}
public sealed record FrameSegmentLoad(string FrameId, WindState State, string Pattern, double StartFraction, double EndFraction, double LoadNPerM);
public static class FacadeMapper
{
    public static ImmutableArray<FrameSegmentLoad> MapPair(FrameGeometry lower, FrameGeometry upper, Func<double,double> pressurePa, double groundDatumM, WindState state, string pattern, FacadeMethod method)
    {
        ValidateFrame(lower); ValidateFrame(upper); Numeric.Required(pattern); ArgumentNullException.ThrowIfNull(state);
        if (lower.Id==upper.Id || lower.FacadeId!=upper.FacadeId || upper.Start.Z-lower.Start.Z<1e-8) throw new ArgumentException("NeedsManualMapping: incompatible facade or elevations.");
        var direction=(lower.End-lower.Start).Unit();
        var upperDirection=(upper.End-upper.Start).Unit();
        var offset=new Vec3(upper.Start.X-lower.Start.X,upper.Start.Y-lower.Start.Y,0);
        var perpendicular=offset-direction*offset.Dot(direction);
        if (Math.Abs(direction.Dot(upperDirection))<1-1e-8 || perpendicular.Length>1e-7) throw new ArgumentException("NeedsManualMapping: projected spans are not collinear.");
        double length=(lower.End-lower.Start).Length, a=offset.Dot(direction), b=a+(upper.End-upper.Start).Dot(direction);
        double start=Math.Max(0,Math.Min(a,b)), end=Math.Min(length,Math.Max(a,b));
        if (end-start<=1e-8) throw new ArgumentException("NeedsManualMapping: no genuine projected overlap.");
        var reactions=FacadeDistribution.Integrate(pressurePa,EngineeringMath.WindHeight(lower.Start.Z,groundDatumM),EngineeringMath.WindHeight(upper.Start.Z,groundDatumM),method);
        var upperStart=(start-a)/(b-a); var upperEnd=(end-a)/(b-a);
        return [new(lower.Id,state,pattern,start/length,end/length,reactions.LowerNPerM),new(upper.Id,state,pattern,Math.Min(upperStart,upperEnd),Math.Max(upperStart,upperEnd),reactions.UpperNPerM)];
    }
    private static void ValidateFrame(FrameGeometry f)
    {
        ArgumentNullException.ThrowIfNull(f); Numeric.Required(f.Id); Numeric.Required(f.FacadeId); Numeric.Vector(f.Start); Numeric.Vector(f.End);
        if (Math.Abs(f.Start.Z-f.End.Z)>1e-8 || (f.End-f.Start).Length<=1e-8) throw new ArgumentException("NeedsManualMapping: horizontal nondegenerate frames required.");
    }
    public static ImmutableArray<FrameSegmentLoad> Accumulate(IEnumerable<FrameSegmentLoad> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        var result=ImmutableArray.CreateBuilder<FrameSegmentLoad>();
        foreach(var group in contributions.GroupBy(x=>(x.FrameId,x.State,x.Pattern)).OrderBy(x=>x.Key.FrameId,StringComparer.Ordinal).ThenBy(x=>x.Key.State.Id,StringComparer.Ordinal).ThenBy(x=>x.Key.Pattern,StringComparer.Ordinal))
        {
            var values=group.ToArray();
            foreach(var item in values)
            {
                Numeric.Required(item.FrameId); Numeric.Required(item.Pattern); Numeric.Finite(item.LoadNPerM); Numeric.Finite(item.StartFraction); Numeric.Finite(item.EndFraction);
                if(item.StartFraction<0 || item.EndFraction>1 || item.EndFraction<=item.StartFraction) throw new ArgumentException("Invalid frame segment fractions.");
            }
            var cuts=values.SelectMany(x=>new[]{x.StartFraction,x.EndFraction}).Distinct().Order().ToArray();
            for(int i=0;i<cuts.Length-1;i++)
            {
                var midpoint=(cuts[i]+cuts[i+1])/2;
                var active=values.Where(x=>x.StartFraction<midpoint && x.EndFraction>midpoint).ToArray();
                if(active.Length>0) result.Add(new(group.Key.FrameId,group.Key.State,group.Key.Pattern,cuts[i],cuts[i+1],Numeric.Finite(active.Sum(x=>x.LoadNPerM))));
            }
        }
        return result.ToImmutable();
    }
}
public sealed record WallType(string Id, double ThicknessM, double UnitWeightNPerM3, double InteriorFinishPa, double ExteriorFinishPa);
public sealed record WallInput(WallType Type, double BottomM, double TopM, double OpeningsFraction, string TargetPatternName, string SupportingFrameId, double? DirectLineLoadOverrideNPerM = null);
public sealed record WallResult(double TotalNPerM, Vec3 GlobalLoadNPerM, bool IsOverride);
public static class WallLoads
{
    /// <summary>Illustrative material values, unreviewed and not for engineering design.</summary>
    public static ImmutableArray<WallType> DemoLibrary => [new("DEMO_MASONRY",0.15,18000,100,100),new("DEMO_LIGHT_PANEL",0.1,6000,50,50)];
    public static WallResult Calculate(WallInput input)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Type);
        Numeric.Required(input.TargetPatternName); Numeric.Required(input.SupportingFrameId); Numeric.Required(input.Type.Id);
        Numeric.Finite(input.BottomM); Numeric.Finite(input.TopM);
        var h=Numeric.Positive(input.TopM-input.BottomM);
        Numeric.Positive(input.Type.ThicknessM); Numeric.Positive(input.Type.UnitWeightNPerM3);
        Numeric.Finite(input.OpeningsFraction); Numeric.Finite(input.Type.InteriorFinishPa); Numeric.Finite(input.Type.ExteriorFinishPa);
        if(input.OpeningsFraction<0 || input.OpeningsFraction>1 || input.Type.InteriorFinishPa<0 || input.Type.ExteriorFinishPa<0) throw new ArgumentException("Invalid wall opening or finish.");
        double total=input.DirectLineLoadOverrideNPerM ?? (input.Type.ThicknessM*input.Type.UnitWeightNPerM3+input.Type.InteriorFinishPa+input.Type.ExteriorFinishPa)*h*(1-input.OpeningsFraction);
        Numeric.Finite(total); if(total<0) throw new ArgumentException("Wall gravity line load must be nonnegative.");
        return new(total,new(0,0,-total),input.DirectLineLoadOverrideNPerM.HasValue);
    }
}
public enum StandardsGateStatus { NeedsVerifiedStandardsData, SyntheticDemoOnly }
public sealed record StandardsGateResult(StandardsGateStatus Status, bool CanAssignRealModel, ImmutableArray<string> MissingData);
public static class StandardsGate
{
    public static StandardsGateResult Check(StandardManifest manifest, DataProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new(provenance==DataProvenance.SyntheticDemoFixture?StandardsGateStatus.SyntheticDemoOnly:StandardsGateStatus.NeedsVerifiedStandardsData,false,
            ["Normative velocity/terrain tables, aerodynamic coefficients, reference-height rules and reviewed regression fixtures are unavailable. A metadata manifest cannot verify a dataset."]);
    }
}
