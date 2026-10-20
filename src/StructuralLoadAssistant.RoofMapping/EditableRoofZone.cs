using System.Collections.Immutable;

namespace StructuralLoadAssistant.RoofMapping;

public readonly record struct UvPoint(double U, double V);
public enum ZoneGeometryStatus { Draft, Valid, Invalid }
public sealed record ZoneAudit(string Action, string User, string Reason, DateTimeOffset At);
public sealed record EditableRoofZone(string Id, string Label, string RoofPlaneId, string WindStateId,
    ImmutableArray<UvPoint> Vertices, string? Notes, int Revision, string SourceFingerprint,
    DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt, ZoneGeometryStatus ValidationStatus,
    ImmutableArray<ZoneAudit> Audit);
public enum GeometryClassification { Unmapped, FullyInsideZone, CrossesZoneBoundary, OverlappingZones, InvalidGeometry, ExcludedWithReason }
public sealed record ZoneCoverage(string ZoneId, double IntersectionAreaM2, double CoveragePercent);
public sealed record ShellZoneIntersection(string ShellId, GeometryClassification Classification,
    double ShellAreaM2, ImmutableArray<ZoneCoverage> Coverages, bool NeedsSplitInCSI, string Diagnostic);
