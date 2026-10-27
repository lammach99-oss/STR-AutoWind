using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Domain;
public sealed record PressureZonePolygon(string Id,ImmutableArray<Vec3> Vertices,Vec3 OutwardNormal,string Reviewer,string Reason);
public sealed record TributarySupportReview(string PanelId,ImmutableArray<string> FirstSupport,ImmutableArray<string> SecondSupport,string Reviewer,string Reason,bool OneWaySimplySupported);
public sealed record FrameDistributedLoadSegment(string FrameId,double StartFraction,double EndFraction,Vec3 StartNPerM,Vec3 EndNPerM,string ZoneId);
public sealed record EquilibriumCheck(Vec3 SurfaceForceN,Vec3 SurfaceMomentNm,Vec3 FrameForceN,Vec3 FrameMomentNm,double ForceResidualN,double MomentResidualNm,bool Passed);
public sealed record TributaryMapping(ImmutableArray<FrameDistributedLoadSegment> Loads,EquilibriumCheck Check,double AreaM2,Vec3 Centroid);
public sealed record FrameLoadPlan(string ModelSnapshotHash,string CalculationHash,string Pattern,string ScenarioId,ImmutableArray<FrameDistributedLoadSegment> Segments,ImmutableArray<EquilibriumCheck> Checks)
{
    public ImmutableArray<TributarySupportReview> SupportReviews {get;init;}=[];
    public string ContentHash=>SnapshotJson.Hash(new{ModelSnapshotHash,CalculationHash,Pattern,ScenarioId,Segments,Checks,SupportReviews});
    public bool CanMutateCsi=>false;
    public string Status=>"EXTERNAL_ONLY_REVIEW_NOT_FOR_DESIGN / REAL_CSI_PREVIEW_ONLY";
}
