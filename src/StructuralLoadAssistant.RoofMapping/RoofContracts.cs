using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.RoofMapping;

public enum RoofWorkflowState { Draft, Scanned, Mapped, Validated, Approved, Applied, Verified }
// Existing numeric values are immutable snapshot contracts. New semantics append values.
public enum ShellZoneClassification { FullyInsideZone, CrossesZoneBoundary, Unmapped, OverlapsAnotherShell, GeometryInvalid, NotGeometricallyValidated, OverlappingZones, InvalidGeometry, ExcludedWithReason }
public enum ManualLocal3Direction { Unacknowledged, Positive, Negative }
public sealed record RoofPressureZone(string Id, string RoofPlaneId, WindState State,
    ImmutableArray<Vec3> Vertices, double NetPressurePa, DataProvenance Provenance = DataProvenance.SyntheticDemoFixture);
public sealed record RoofValidationIssue(string Code, string ShellId, string Message, bool Blocking = true);
public sealed record RoofShellMapping(string ShellId, string StateId, string? ZoneId,
    ShellZoneClassification Classification, ManualLocal3Direction ManualDirection = ManualLocal3Direction.Unacknowledged,
    bool MismatchAcknowledged = false, int MappingContractVersion = 1,
    ImmutableArray<ZoneCoverage> ZoneCoverages = default, double? ShellAreaM2 = null)
{
    public ImmutableArray<ZoneCoverage> ZoneCoverages { get; init; }=ZoneCoverages.IsDefault?[]:ZoneCoverages;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool BlocksPressureMapping => Classification!=ShellZoneClassification.FullyInsideZone || MappingContractVersion!=2 ||
        ZoneCoverages.Length!=1 || ShellAreaM2 is not >0 || !double.IsFinite(ShellAreaM2.Value) ||
        string.IsNullOrWhiteSpace(ZoneCoverages[0].ZoneId) || !double.IsFinite(ZoneCoverages[0].IntersectionAreaM2) ||
        Math.Abs(ZoneCoverages[0].IntersectionAreaM2-ShellAreaM2.Value)>Math.Max(1e-6,1e-7*ShellAreaM2.Value) ||
        !double.IsFinite(ZoneCoverages[0].CoveragePercent) || Math.Abs(ZoneCoverages[0].CoveragePercent-100)>1e-5;
}
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
