using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public sealed class RoofZoneWorkspaceTests
{
    internal static ImmutableArray<RoofShell3D> Shells => new[] {
        new ShellGeometry("A","roof",[new(0,0,3),new(2,0,4),new(2,2,4),new(0,2,3)],new Vec3(-1,0,2).Unit(),default),
        new ShellGeometry("B","roof",[new(2,0,4),new(4,0,5),new(4,2,5),new(2,2,4)],new Vec3(-1,0,2).Unit(),default),
        new ShellGeometry("C","roof",[new(0,0,7),new(2,0,7),new(2,2,7),new(0,2,7)],new(0,0,1),default)
    }.Select(s => Roof3DGeometryProcessor.Process(s,"model")).ToImmutableArray();

    [Fact]
    public void Coplanar_shells_share_plane_but_overlapping_xy_does_not_merge_different_surfaces()
    {
        var editor = new RoofZoneWorkspace(); editor.LoadSource(Shells,"WX");
        Assert.Equal(2,editor.Planes.Count);
        Assert.Equal(editor.PlaneForShell("A").Id,editor.PlaneForShell("B").Id);
        Assert.NotEqual(editor.PlaneForShell("A").Id,editor.PlaneForShell("C").Id);
    }

    [Fact]
    public void Union_vertex_edit_and_undo_redo_retain_actual_polygons_and_audit()
    {
        var editor = new RoofZoneWorkspace(); editor.LoadSource(Shells,"WX");
        var zone = editor.CreateFromShells("Edge",["A","B"],"Engineer","Union footprint");
        Assert.Equal(Shells.Take(2).Sum(s=>s.SurfaceAreaM2),ZonePolygonEngine.Area(zone.Vertices),6);
        var original = zone.Vertices;
        editor.MoveVertex(zone.Id,0,new(original[0].U+.2,original[0].V),"Engineer","Boundary edit");
        Assert.NotEqual(original,editor.Zones[0].Vertices);
        editor.Undo("Engineer","Undo boundary");
        Assert.Equal(original,editor.Zones[0].Vertices);
        editor.Redo("Engineer","Redo boundary");
        Assert.NotEqual(original,editor.Zones[0].Vertices);
        Assert.Contains(editor.Zones[0].Audit,a=>a.Action=="Redo");
    }

    [Fact]
    public void Vertex_insert_delete_translate_metadata_and_exclusion_have_independent_geometry_semantics()
    {
        var editor=new RoofZoneWorkspace();editor.LoadSource(Shells,"WX");
        var zone=editor.CreateFromShells("Edge",["A"],"Engineer","Source footprint");
        editor.InsertVertex(zone.Id,0,new((zone.Vertices[0].U+zone.Vertices[1].U)/2,(zone.Vertices[0].V+zone.Vertices[1].V)/2),"Engineer","Insert edge vertex");
        Assert.Equal(zone.Vertices.Length+1,editor.Zones[0].Vertices.Length);
        editor.DeleteVertex(zone.Id,1,"Engineer","Remove inserted vertex");
        Assert.Equal(zone.Vertices,editor.Zones[0].Vertices);
        editor.Translate(zone.Id,new(.1,.2),"Engineer","Move zone");
        Assert.Equal(zone.Vertices[0].U+.1,editor.Zones[0].Vertices[0].U,8);
        editor.SetMetadata(zone.Id,"Corner","Engineer-defined only","Engineer","Rename");
        Assert.Equal("Corner",editor.Zones[0].Label);
        var results=editor.Validate([new("B","Engineer","Excluded from review",DateTimeOffset.UtcNow)]);
        Assert.Contains(results,r=>r.ShellId=="B"&&r.Classification==GeometryClassification.ExcludedWithReason);
        editor.Editable=false;
        Assert.Throws<InvalidOperationException>(()=>editor.Translate(zone.Id,new(1,0),"Engineer","Blocked"));
    }

    [Fact]
    public void Undo_redo_preserve_all_edit_audit_events_and_monotonic_zone_revisions()
    {
        var editor=new RoofZoneWorkspace();editor.LoadSource(Shells,"WX");
        var zone=editor.CreateFromShells("Edge",["A"],"Engineer","Create");
        editor.Translate(zone.Id,new(.1,0),"Engineer","Move");
        var editedRevision=editor.Zones[0].Revision;
        editor.Undo("Engineer","Undo");
        Assert.True(editor.Zones[0].Revision>editedRevision);
        Assert.Contains(editor.Zones[0].Audit,a=>a.Action=="MovePolygon");
        var undoRevision=editor.Zones[0].Revision;
        editor.Redo("Engineer","Redo");
        Assert.True(editor.Zones[0].Revision>undoRevision);
        Assert.Contains(editor.Zones[0].Audit,a=>a.Action=="Undo");
    }

    [Fact]
    public void Sqlite_restart_preserves_polygon_coordinates_audit_and_expected_base_revision()
    {
        var path=Path.Combine(Path.GetTempPath(),"zones-"+Guid.NewGuid().ToString("N")+".db");
        try
        {
            var state=new WindState(WindDirection.PositiveX,"IP_POS","RoofCase1",LimitState.Uls);
            var editor=new RoofZoneWorkspace();editor.LoadSource(Shells,state.Id);
            editor.CreateFromShells("Edge",["A","B"],"Engineer","Union");
            var draft=new RoofMappingDraft(RoofMappingDraftStore.DraftId("P","M",state.Id,1),1,"P","M","geometry",state,[],[],[],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,
                ZonePolygons:editor.Zones);
            new RoofMappingDraftStore(new StructuralLoadAssistant.Persistence.SqliteSnapshotStore(path)).SaveDraft("P",draft,0);
            var reopened=new RoofMappingDraftStore(new StructuralLoadAssistant.Persistence.SqliteSnapshotStore(path));
            var loaded=reopened.LoadLatestDraft("P","M",state.Id)!;
            Assert.Equal(editor.Zones[0].Vertices,loaded.ZonePolygons[0].Vertices);
            Assert.Equal(editor.Zones[0].Audit,loaded.ZonePolygons[0].Audit);
            Assert.Throws<InvalidOperationException>(()=>reopened.SaveDraft("P",loaded with{Revision=2,Id=RoofMappingDraftStore.DraftId("P","M",state.Id,2)},0));
            var legacyJson=SnapshotJson.Serialize(draft);
            var node=System.Text.Json.Nodes.JsonNode.Parse(legacyJson)!.AsObject();
            node.Remove("ZonePolygons");node.Remove("SourceShells");node.Remove("SelectedScope");
            var legacy=System.Text.Json.JsonSerializer.Deserialize<RoofMappingDraft>(node.ToJsonString());
            Assert.NotNull(legacy);Assert.Empty(legacy.ZonePolygons);
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void New_wind_state_starts_clean_and_restoring_polygons_does_not_share_mutable_arrays()
    {
        var editor = new RoofZoneWorkspace(); editor.LoadSource(Shells,"WX");
        editor.CreateFromShells("WX Edge",["A"],"Engineer","WX");
        var wx = editor.Zones;
        editor.LoadSource(Shells,"WY"); Assert.Empty(editor.Zones);
        editor.CreateFromShells("WY Corner",["B"],"Engineer","WY");
        editor.LoadSource(Shells,"WX"); editor.Restore(wx);
        Assert.Equal("WX Edge",Assert.Single(editor.Zones).Label);
        Assert.False(editor.CanUndo);
    }
}
