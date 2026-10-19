 using System.Collections.Immutable;
 using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
 using StructuralLoadAssistant.Domain;
 using StructuralLoadAssistant.Persistence;
 using StructuralLoadAssistant.RoofMapping;
 using Xunit;
 
 namespace StructuralLoadAssistant.Tests;
 
 public sealed class Roof3DGeometryAndDraftTests : IDisposable
 {
     private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"roof3d-test-{Guid.NewGuid():N}.db");
 
     public void Dispose()
     {
         try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
     }
 
     [Fact]
     public void Flat_horizontal_slab_computes_zero_slope_and_plan_area()
     {
         var vertices = ImmutableArray.Create(
             new Vec3(0, 0, 5),
             new Vec3(10, 0, 5),
             new Vec3(10, 8, 5),
             new Vec3(0, 8, 5)
         );
         var shell = new ShellGeometry("FLAT-1", "Plane-1", vertices, new Vec3(0, 0, 1), new Vec3(0, 0, 1));
         var shell3D = Roof3DGeometryProcessor.Process(shell, "model-1");
 
         Assert.True(shell3D.IsPlanar);
         Assert.False(shell3D.IsDegenerate);
         Assert.True(Math.Abs(shell3D.SlopeDegrees - 0.0) < 1e-4, $"Expected 0 slope, got {shell3D.SlopeDegrees}");
         Assert.Null(shell3D.AzimuthDegrees);
         Assert.True(Math.Abs(shell3D.SurfaceAreaM2 - 80.0) < 1e-4);
         Assert.True(Math.Abs(shell3D.ProjectedAreaM2 - 80.0) < 1e-4);
         Assert.Equal(6, shell3D.TriangleIndices.Length);
     }
 
     [Fact]
     public void Monopitch_sloped_roof_computes_correct_slope_and_azimuth()
     {
         // 10m wide (X: 0->10), rising 3m over 4m run (Y: 0->4, Z: 5->8)
         // Hypotenuse = sqrt(4^2 + 3^2) = 5m. Surface area = 10 * 5 = 50 m^2. Plan area = 10 * 4 = 40 m^2.
         var vertices = ImmutableArray.Create(
             new Vec3(0, 0, 5),
             new Vec3(10, 0, 5),
             new Vec3(10, 4, 8),
             new Vec3(0, 4, 8)
         );
         var normal = new Vec3(0, -3.0 / 5.0, 4.0 / 5.0); // Outward normal tilted toward -Y
         var shell = new ShellGeometry("SLOPE-1", "Plane-Slope", vertices, normal, normal);
         var shell3D = Roof3DGeometryProcessor.Process(shell, "model-1");
 
         Assert.True(shell3D.IsPlanar);
         Assert.False(shell3D.IsDegenerate);
         double expectedSlope = Math.Acos(4.0 / 5.0) * 180.0 / Math.PI; // ~36.87 deg
         Assert.True(Math.Abs(shell3D.SlopeDegrees - expectedSlope) < 0.1, $"Expected {expectedSlope}, got {shell3D.SlopeDegrees}");
         Assert.NotNull(shell3D.AzimuthDegrees);
         Assert.True(Math.Abs(shell3D.SurfaceAreaM2 - 50.0) < 1e-2);
         Assert.True(Math.Abs(shell3D.ProjectedAreaM2 - 40.0) < 1e-2);
     }
 
     [Fact]
     public void Gable_roof_two_slopes_opposite_normals_and_azimuths()
     {
         // Gable side A (facing -X) and side B (facing +X), peak at X=5, Z=7, eaves at X=0 and X=10, Z=4
         var slopeA = ImmutableArray.Create(
             new Vec3(0, 0, 4), new Vec3(5, 0, 7), new Vec3(5, 6, 7), new Vec3(0, 6, 4));
         var slopeB = ImmutableArray.Create(
             new Vec3(5, 0, 7), new Vec3(10, 0, 4), new Vec3(10, 6, 4), new Vec3(5, 6, 7));
 
         var shellA = new ShellGeometry("GABLE-A", "Plane-A", slopeA, new Vec3(-3, 0, 5).Unit(), new Vec3(-3, 0, 5).Unit());
         var shellB = new ShellGeometry("GABLE-B", "Plane-B", slopeB, new Vec3(3, 0, 5).Unit(), new Vec3(3, 0, 5).Unit());
 
         var procA = Roof3DGeometryProcessor.Process(shellA, "gable-model");
         var procB = Roof3DGeometryProcessor.Process(shellB, "gable-model");
 
         Assert.True(procA.IsPlanar && procB.IsPlanar);
         Assert.True(Math.Abs(procA.SlopeDegrees - procB.SlopeDegrees) < 0.1);
         Assert.NotNull(procA.AzimuthDegrees);
         Assert.NotNull(procB.AzimuthDegrees);
         Assert.NotEqual(procA.AzimuthDegrees, procB.AzimuthDegrees);
     }
 
     [Fact]
     public void Hip_triangular_and_trapezoidal_polygons_triangulate_cleanly()
     {
         // Triangular hip face
         var triangle = ImmutableArray.Create(
             new Vec3(0, 0, 3), new Vec3(6, 0, 3), new Vec3(3, 3, 5));
         var shellTri = new ShellGeometry("HIP-TRI", "Plane-Hip", triangle, new Vec3(0, -2, 3).Unit(), new Vec3(0, -2, 3).Unit());
         var procTri = Roof3DGeometryProcessor.Process(shellTri, "hip-model");
 
         Assert.True(procTri.IsPlanar);
         Assert.Equal(3, procTri.TriangleIndices.Length);
 
         // Trapezoidal hip face
         var trap = ImmutableArray.Create(
             new Vec3(0, 0, 3), new Vec3(12, 0, 3), new Vec3(9, 3, 5), new Vec3(3, 3, 5));
         var shellTrap = new ShellGeometry("HIP-TRAP", "Plane-Hip2", trap, new Vec3(0, -2, 3).Unit(), new Vec3(0, -2, 3).Unit());
         var procTrap = Roof3DGeometryProcessor.Process(shellTrap, "hip-model");
 
         Assert.True(procTrap.IsPlanar);
         Assert.Equal(6, procTrap.TriangleIndices.Length);
     }
 
     [Fact]
     public void Opposite_Local3_axes_between_identical_slabs_are_preserved()
     {
         var slab = ImmutableArray.Create(new Vec3(0, 0, 3), new Vec3(5, 0, 3), new Vec3(5, 5, 3), new Vec3(0, 5, 3));
         var shellUp = new ShellGeometry("SLAB-UP", "Plane-H", slab, new Vec3(0, 0, 1), default);
         var shellDown = new ShellGeometry("SLAB-DOWN", "Plane-H", slab, new Vec3(0, 0, -1), default);
 
         var pUp = Roof3DGeometryProcessor.Process(shellUp, "m1");
         var pDown = Roof3DGeometryProcessor.Process(shellDown, "m1");
 
         Assert.Equal(new Vec3(0, 0, 1), pUp.Local3);
         Assert.Equal(new Vec3(0, 0, -1), pDown.Local3);
         Assert.Equal(pUp.SurfaceAreaM2, pDown.SurfaceAreaM2);
     }
 
     [Fact]
     public void Vertical_wall_shell_computes_90_degree_slope_and_zero_projected_area()
     {
         var wall = ImmutableArray.Create(
             new Vec3(0, 0, 0), new Vec3(5, 0, 0), new Vec3(5, 0, 3), new Vec3(0, 0, 3));
         var shell = new ShellGeometry("WALL-1", "Plane-V", wall, new Vec3(0, -1, 0), new Vec3(0, -1, 0));
         var pWall = Roof3DGeometryProcessor.Process(shell, "m1");
 
         Assert.True(pWall.IsPlanar);
         Assert.True(Math.Abs(pWall.SlopeDegrees - 90.0) < 1e-3);
         Assert.True(pWall.SurfaceAreaM2 > 14.9);
         Assert.True(pWall.ProjectedAreaM2 < 1e-4);
     }
 
     [Fact]
     public void Multiple_shells_with_overlapping_XY_projections_at_different_elevations_remain_distinct()
     {
         var poly = ImmutableArray.Create(new Vec3(0, 0, 3), new Vec3(4, 0, 3), new Vec3(4, 4, 3), new Vec3(0, 4, 3));
         var polyUpper = ImmutableArray.Create(new Vec3(0, 0, 6), new Vec3(4, 0, 6), new Vec3(4, 4, 6), new Vec3(0, 4, 6));
 
         var s1 = new ShellGeometry("LOWER", "P1", poly, new Vec3(0, 0, 1), new Vec3(0, 0, 1));
         var s2 = new ShellGeometry("UPPER", "P2", polyUpper, new Vec3(0, 0, 1), new Vec3(0, 0, 1));
 
         var p1 = Roof3DGeometryProcessor.Process(s1, "m1");
         var p2 = Roof3DGeometryProcessor.Process(s2, "m1");
 
         Assert.NotEqual(p1.GeometryFingerprint, p2.GeometryFingerprint);
         Assert.True(p1.Vertices[0].Z < p2.Vertices[0].Z);
         Assert.Equal(p1.ProjectedAreaM2, p2.ProjectedAreaM2);
     }
 
     [Fact]
     public void Draft_store_persists_and_restores_independent_wind_state_drafts()
     {
         var store = new SqliteSnapshotStore(dbPath);
         var draftStore = new RoofMappingDraftStore(store);
 
         var state1 = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
         var state2 = new WindState(WindDirection.NegativeY, "IP_NEG", "RoofCase2", LimitState.Uls);
 
         var items1 = ImmutableArray.Create(
             new RoofMappingDraftItem("S1", "Edge", ShellZoneClassification.FullyInsideZone, ManualLocal3Direction.Positive, false, "Engineer1", "Initial review", DateTimeOffset.UtcNow),
             new RoofMappingDraftItem("S2", "Interior", ShellZoneClassification.FullyInsideZone, ManualLocal3Direction.Negative, true, "Engineer1", "Mismatch acknowledged", DateTimeOffset.UtcNow)
         );
 
         var items2 = ImmutableArray.Create(
             new RoofMappingDraftItem("S1", "Corner", ShellZoneClassification.FullyInsideZone, ManualLocal3Direction.Negative, false, "Engineer2", "WY review", DateTimeOffset.UtcNow),
             new RoofMappingDraftItem("S2", "Field", ShellZoneClassification.FullyInsideZone, ManualLocal3Direction.Positive, false, "Engineer2", "WY field", DateTimeOffset.UtcNow)
         );
 
         var draft1 = new RoofMappingDraft(
             RoofMappingDraftStore.DraftId("proj1", "model1", state1.Id, 1),
             1, "proj1", "model1", "geom-hash-1", state1, items1, [], [],
             DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, GeometryReviewStatus.GeometryReviewed);
 
         var draft2 = new RoofMappingDraft(
             RoofMappingDraftStore.DraftId("proj1", "model1", state2.Id, 1),
             1, "proj1", "model1", "geom-hash-1", state2, items2, [], [],
             DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, GeometryReviewStatus.GeometryReviewed);
 
         draftStore.SaveDraft("proj1", draft1);
         draftStore.SaveDraft("proj1", draft2);
 
         // Reload and verify isolation
         var loaded1 = draftStore.LoadLatestDraft("proj1", "model1", state1.Id);
         var loaded2 = draftStore.LoadLatestDraft("proj1", "model1", state2.Id);
 
         Assert.NotNull(loaded1);
         Assert.NotNull(loaded2);
         Assert.Equal("Edge", loaded1.Items.First(i => i.ShellId == "S1").ZoneId);
         Assert.Equal("Corner", loaded2.Items.First(i => i.ShellId == "S1").ZoneId);
         Assert.Equal(ManualLocal3Direction.Positive, loaded1.Items.First(i => i.ShellId == "S1").ManualDirection);
         Assert.Equal(ManualLocal3Direction.Negative, loaded2.Items.First(i => i.ShellId == "S1").ManualDirection);
     }
 
     [Fact]
     public void Stale_geometry_detection_flags_reconciliation()
     {
         var state = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
         var draft = new RoofMappingDraft(
             "draft-1", 1, "proj1", "model1", "original-geom-hash", state,
             [], [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, GeometryReviewStatus.GeometryReviewed);
 
         // Matching fingerprint -> remains GeometryReviewed
         var matched = RoofMappingDraftStore.ReconcileGeometry(draft, "original-geom-hash");
         Assert.Equal(GeometryReviewStatus.GeometryReviewed, matched);
 
         // Changed fingerprint -> flags StaleNeedsRescan
         var stale = RoofMappingDraftStore.ReconcileGeometry(draft, "modified-geom-hash");
         Assert.Equal(GeometryReviewStatus.StaleNeedsRescan, stale);
     }
 
     [Fact]
    public async Task Inspection_session_tracks_geometry_zones_exclusions_and_review_status()
     {
         var model = new CsiModelSnapshot(
             "id", "model-test", "Etabs", "23.2.0", "geom-123", "loads",
             ["PAT"], ["CASE"], ["COMBO"],
             [],
             [
                 new("R1", "Plane-1", [new(0, 0, 5), new(6, 0, 5), new(6, 4, 7), new(0, 4, 7)], new(0, 0, 1), default),
                 new("R2", "Plane-1", [new(6, 0, 5), new(12, 0, 5), new(12, 4, 7), new(6, 4, 7)], new(0, 0, 1), default)
             ],
             []
         );
 
         var adapter = new FakeSessionAdapter(model);
        await using var session = new CsiInspectionSession(adapter);
        await session.ConnectAsync(new CsiConnectionOptions(CsiProduct.Etabs, "fake.dll"));
        await session.RefreshAsync();
 
         Assert.Equal(2, session.Shells3D.Length);
         Assert.Equal(GeometryReviewStatus.GeometryDraft, session.ReviewStatus);
 
         // Assign zone to R1
         session.SetGeometryZone("R1", "Edge", "Eng1", "Visual edge zone review");
         Assert.Equal(GeometryReviewStatus.GeometryReviewed, session.ReviewStatus);
         Assert.Equal("Edge", session.RoofRows.First(r => r.ShellId == "R1").ZoneId);
 
         // Exclude R2
         session.ExcludeShell("R2", "Eng1", "Equipment cutout exclusion");
         Assert.Equal("Excluded", session.RoofRows.First(r => r.ShellId == "R2").ZoneId);
         Assert.Single(session.Exclusions);
 
         // Create draft
         var state = new WindState(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
         var draft = session.CreateDraft("proj1", state);
         Assert.Equal(2, draft.Items.Length);
         Assert.Equal("Edge", draft.Items.First(i => i.ShellId == "R1").ZoneId);
 
         // Restore draft
         bool restored = session.RestoreDraft(draft);
         Assert.True(restored);
         Assert.Equal("Edge", session.RoofRows.First(r => r.ShellId == "R1").ZoneId);
     }
 
     private sealed class FakeSessionAdapter : ICsiSessionReadAdapter
     {
         private readonly CsiModelSnapshot model;
         public FakeSessionAdapter(CsiModelSnapshot model) => this.model = model;
 
         public Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options, CancellationToken cancellationToken = default) =>
             Task.FromResult(new CsiConnectionResult(true, "Etabs", "23.2.0", new(true, true, true, true, true), []));
 
         public Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken = default) =>
             Task.FromResult(new CsiReadScan(model, new("SI", 1, 1, "SI"), true, new(true, true, true, true, true), [], [], [], CsiScanOutcome.Succeeded));
 
         public Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default) => Task.FromResult(model);
         public Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default) => Task.FromResult(model.ExistingLoads);
         public ValueTask DisposeAsync() => ValueTask.CompletedTask;
     }
 }
 
