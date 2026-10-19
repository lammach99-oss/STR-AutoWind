 using System.Collections.Immutable;
 using StructuralLoadAssistant.Domain;
 
 namespace StructuralLoadAssistant.RoofMapping;
 
 public enum OutwardNormalStatus { Unknown, ReadFromCsi, EngineerVerified, InconsistentWithLocal3 }
 
 public enum GeometryReviewStatus { GeometryDraft, GeometryValidated, GeometryReviewed, StaleNeedsRescan, PressureMappingUnavailable }
 
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
     DateTimeOffset? ReviewedAt = null
 );
 
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
     GeometryReviewStatus ReviewStatus = GeometryReviewStatus.GeometryDraft
 );
 
