 using System.Collections.Immutable;
 using StructuralLoadAssistant.Domain;
 
 namespace StructuralLoadAssistant.RoofMapping;
 
 public enum OutwardNormalStatus { Unknown, ReadFromCsi, EngineerVerified, InconsistentWithLocal3 }
 
 public enum GeometryReviewStatus { GeometryDraft, GeometryValidated, GeometryReviewed, StaleNeedsRescan, PressureMappingUnavailable, ScopeReviewRequired }
 
 public sealed record RoofShell3D(
     string ShellId,
     string ModelId,
     string RoofPlaneId,
     ImmutableArray<Vec3> Vertices,
     Vec3 Local3,
     double SlopeDegrees,
     double? AzimuthDegrees,
     double SurfaceAreaM2,
     double ProjectedAreaM2,
     string GeometryFingerprint,
     OutwardNormalStatus OutwardStatus,
     Vec3? VerifiedOutwardNormal,
     ImmutableArray<int> TriangleIndices,
     bool IsPlanar,
    bool IsDegenerate,
    bool IsSelfIntersecting = false,
    double TriangulatedAreaM2 = 0.0,
    bool IsValid = true,
    string? GeometryError = null
 );
 
 public sealed record RoofMappingDraftItem(
     string ShellId,
     string? ZoneId,
     ShellZoneClassification Classification,
     ManualLocal3Direction ManualDirection = ManualLocal3Direction.Unacknowledged,
     bool MismatchAcknowledged = false,
     string? Reviewer = null,
     string? ReviewNotes = null,
     DateTimeOffset? ReviewedAt = null,
     int MappingContractVersion = 1,
     ImmutableArray<ZoneCoverage> ZoneCoverages = default,
     double? ShellAreaM2 = null
 )
 {
     public ImmutableArray<ZoneCoverage> ZoneCoverages { get; init; }=ZoneCoverages.IsDefault?[]:ZoneCoverages;
     [System.Text.Json.Serialization.JsonIgnore]
     public bool BlocksPressureMapping => Classification!=ShellZoneClassification.FullyInsideZone || MappingContractVersion!=2 ||
         ZoneCoverages.Length!=1 || ShellAreaM2 is not >0 || !double.IsFinite(ShellAreaM2.Value) ||
         string.IsNullOrWhiteSpace(ZoneCoverages[0].ZoneId) || !double.IsFinite(ZoneCoverages[0].IntersectionAreaM2) ||
         Math.Abs(ZoneCoverages[0].IntersectionAreaM2-ShellAreaM2.Value)>Math.Max(1e-6,1e-7*ShellAreaM2.Value) ||
         !double.IsFinite(ZoneCoverages[0].CoveragePercent) || Math.Abs(ZoneCoverages[0].CoveragePercent-100)>1e-5;
 }
 
 public sealed record SourceShellFingerprint(string ShellId, string Fingerprint);
 public sealed record RoofMappingDraft(
     string Id,
     int Revision,
     string ProjectId,
     string ModelId,
     string GeometryFingerprint,
     WindState WindState,
     ImmutableArray<RoofMappingDraftItem> Items,
     ImmutableArray<RoofScopeExclusion> Exclusions,
     ImmutableArray<EngineeringOverride> Overrides,
     DateTimeOffset CreatedAt,
     DateTimeOffset UpdatedAt,
     GeometryReviewStatus ReviewStatus = GeometryReviewStatus.GeometryDraft,
     ImmutableArray<SourceShellFingerprint> SourceShells = default,
     ImmutableArray<string> SelectedScope = default,
     ImmutableArray<EditableRoofZone> ZonePolygons = default
 )
 {
     public ImmutableArray<SourceShellFingerprint> SourceShells { get; init; }=SourceShells.IsDefault?[]:SourceShells;
     public ImmutableArray<string> SelectedScope { get; init; }=SelectedScope.IsDefault?[]:SelectedScope;
     public ImmutableArray<EditableRoofZone> ZonePolygons { get; init; }=ZonePolygons.IsDefault?[]:ZonePolygons;
 }
 
