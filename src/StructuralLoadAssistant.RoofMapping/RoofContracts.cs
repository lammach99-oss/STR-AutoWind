using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.RoofMapping;

public enum RoofWorkflowState { Draft, Scanned, Mapped, Validated, Approved, Applied, Verified }
public enum ShellZoneClassification { FullyInsideZone, CrossesZoneBoundary, Unmapped, OverlapsAnotherShell, GeometryInvalid, NotGeometricallyValidated }
public enum ManualLocal3Direction { Unacknowledged, Positive, Negative }
public sealed record RoofPressureZone(string Id, string RoofPlaneId, WindState State,
    ImmutableArray<Vec3> Vertices, double NetPressurePa, DataProvenance Provenance = DataProvenance.SyntheticDemoFixture);
public sealed record RoofValidationIssue(string Code, string ShellId, string Message, bool Blocking = true);
public sealed record RoofShellMapping(string ShellId, string StateId, string? ZoneId,
    ShellZoneClassification Classification, ManualLocal3Direction ManualDirection = ManualLocal3Direction.Unacknowledged,
    bool MismatchAcknowledged = false);
public sealed record AreaLoadProposal(string ShellId, string StateId, string Pattern, string ZoneId,
    double NetPressurePa, ManualLocal3Direction ManualLocal3Direction, Vec3 Local3Vector,
    Vec3 OutwardNormalGlobal, double SignedCsiAreaLoadPa);
public sealed record RoofScopeExclusion(string ShellId, string User, string Reason, DateTimeOffset CreatedAt);
public sealed record RoofMappingApproval(string Id, int Revision, WindCalculationRun Calculation,
    StandardManifest? Manifest, CsiModelSnapshot Model, ImmutableArray<RoofPressureZone> Zones,
    ImmutableArray<RoofShellMapping> Mappings, ImmutableDictionary<string,string> Patterns,
    ImmutableArray<AreaLoadProposal> Proposals, ImmutableArray<EngineeringOverride> Overrides,
    ImmutableArray<RoofScopeExclusion> Exclusions, string User, DateTimeOffset ApprovedAt,
    bool Simulation, string ContentHash);
