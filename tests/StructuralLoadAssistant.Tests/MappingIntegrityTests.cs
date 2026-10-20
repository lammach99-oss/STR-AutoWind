using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public sealed class MappingIntegrityTests
{
    private static readonly WindState State=new(WindDirection.PositiveX,"IP_POS","RoofCase1",LimitState.Uls);
    [Theory]
    [InlineData(GeometryClassification.Unmapped,ShellZoneClassification.Unmapped)]
    [InlineData(GeometryClassification.FullyInsideZone,ShellZoneClassification.FullyInsideZone)]
    [InlineData(GeometryClassification.CrossesZoneBoundary,ShellZoneClassification.CrossesZoneBoundary)]
    [InlineData(GeometryClassification.OverlappingZones,ShellZoneClassification.OverlappingZones)]
    [InlineData(GeometryClassification.InvalidGeometry,ShellZoneClassification.InvalidGeometry)]
    public async Task Intersection_status_and_structured_coverage_survive_draft_restore(GeometryClassification input,ShellZoneClassification expected)
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        ImmutableArray<ZoneCoverage> coverage=input==GeometryClassification.Unmapped?[]:input==GeometryClassification.FullyInsideZone?[new("zone,with,comma",4,100)]:[new("zone,with,comma",2,50),new("Z2",1,25)];
        session.SetIntersectionMappings([new("A",input,4,coverage,input is GeometryClassification.CrossesZoneBoundary or GeometryClassification.OverlappingZones,"diagnostic")],State.Id);
        var row=session.RoofRows.Single(r=>r.ShellId=="A");Assert.Equal(expected,row.Classification);
        Assert.Equal(2,row.MappingContractVersion);Assert.Equal(coverage,row.ZoneCoverages);
        Assert.Equal(input!=GeometryClassification.FullyInsideZone,row.BlocksPressureMapping);
        Assert.Equal(coverage.Length==1?coverage[0].ZoneId:null,row.ZoneId);
        var draft=session.CreateDraft("P",State);Assert.True(session.RestoreDraft(draft,"P",State.Id));
        var restored=session.RoofRows.Single(r=>r.ShellId=="A");Assert.Equal(coverage,restored.ZoneCoverages);Assert.Equal(expected,restored.Classification);
    }
    [Theory]
    [InlineData("Crossing Warning")][InlineData("FullyInsideZone")][InlineData("Excluded")][InlineData("OverlappingZones")]
    public async Task Labels_never_claim_geometric_validation(string label)
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        session.SetGeometryZone("A",label,"Engineer","Label only");
        var row=session.RoofRows.Single(r=>r.ShellId=="A");Assert.Equal(ShellZoneClassification.NotGeometricallyValidated,row.Classification);
        Assert.Empty(row.ZoneCoverages);Assert.True(row.BlocksPressureMapping);
    }
    [Fact]
    public void Legacy_json_without_version_or_coverage_is_readable()
    {
        var item=System.Text.Json.JsonSerializer.Deserialize<RoofMappingDraftItem>("{\"ShellId\":\"A\",\"ZoneId\":\"Z1,Z2\",\"Classification\":3}")!;
        Assert.Equal(1,item.MappingContractVersion);Assert.Empty(item.ZoneCoverages);
        Assert.Equal(ShellZoneClassification.OverlapsAnotherShell,item.Classification);
        Assert.Equal("Z1,Z2",item.ZoneId);
    }
    [Fact]
    public async Task Unsupported_mapping_version_does_not_clear_current_editor()
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        session.SetGeometryZone("A","Current label","Engineer","Current edit");var before=session.RoofRows;
        var draft=session.CreateDraft("P",State);draft=draft with{Items=draft.Items.Select(i=>i with{MappingContractVersion=99}).ToImmutableArray()};
        Assert.Throws<NotSupportedException>(()=>session.RestoreDraft(draft,"P",State.Id));Assert.Equal(before,session.RoofRows);
    }
    [Fact]
    public async Task Excluded_intersection_retains_distinct_status_after_restore()
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        session.ExcludeShell("A","Engineer","Explicit exclusion");
        session.SetIntersectionMappings([new("A",GeometryClassification.ExcludedWithReason,4,[],false,"")],State.Id);
        var draft=session.CreateDraft("P",State);session.RestoreDraft(draft,"P",State.Id);
        var row=session.RoofRows.Single(r=>r.ShellId=="A");Assert.Equal(ShellZoneClassification.ExcludedWithReason,row.Classification);Assert.Empty(row.ZoneCoverages);
    }
    [Fact]
    public void Incomplete_full_containment_evidence_remains_blocked()
    {
        var row=new RoofShellMapping("A","WX","Z",ShellZoneClassification.FullyInsideZone,MappingContractVersion:2,ZoneCoverages:[new("Z",2,50)],ShellAreaM2:4);
        Assert.True(row.BlocksPressureMapping);
        Assert.True((row with{MappingContractVersion=3}).BlocksPressureMapping);
    }
    [Fact]
    public async Task Failed_batch_does_not_publish_partial_intersection_results()
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();var before=session.RoofRows;
        Assert.Throws<InvalidOperationException>(()=>session.SetIntersectionMappings([
            new("A",GeometryClassification.FullyInsideZone,4,[new("Z",4,100)],false,""),
            new("B",GeometryClassification.ExcludedWithReason,4,[],false,"")],State.Id));
        Assert.Equal(before,session.RoofRows);
    }
    [Fact]
    public async Task Structured_coverage_survives_sqlite_without_splitting_opaque_ids()
    {
        var path=Path.Combine(Path.GetTempPath(),"mapping-v2-"+Guid.NewGuid().ToString("N")+".db");
        try
        {
            await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
            await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
            session.SetIntersectionMappings([new("A",GeometryClassification.OverlappingZones,4,[new("Z,1",3,75),new("Z,2",2,50)],true,"")],State.Id);
            var draft=session.CreateDraft("P",State);
            var store=new RoofMappingDraftStore(new StructuralLoadAssistant.Persistence.SqliteSnapshotStore(path));store.SaveDraft("P",draft,0);
            var loaded=new RoofMappingDraftStore(new StructuralLoadAssistant.Persistence.SqliteSnapshotStore(path)).LoadLatestDraft("P",draft.ModelId,State.Id)!;
            Assert.True(session.RestoreDraft(loaded,"P",State.Id));
            var row=session.RoofRows.Single(r=>r.ShellId=="A");Assert.Null(row.ZoneId);
            Assert.Equal(new[]{"Z,1","Z,2"},row.ZoneCoverages.Select(c=>c.ZoneId));Assert.True(row.BlocksPressureMapping);
        }
        finally{File.Delete(path);}
    }
    [Fact]
    public async Task Exclusion_is_distinct_and_requires_audited_reason()
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        Assert.Throws<ArgumentException>(()=>session.ExcludeShell("A","Engineer",""));
        session.ExcludeShell("A","Engineer","Outside review scope");
        Assert.Equal(ShellZoneClassification.ExcludedWithReason,session.RoofRows.Single(r=>r.ShellId=="A").Classification);
    }
    [Fact]
    public async Task Legacy_ambiguous_overlap_is_unverified_without_rewriting_snapshot()
    {
        await using var session=new CsiInspectionSession(new RoofScopeTests.ScopeReader());
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        var draft=session.CreateDraft("P",State) with{Items=[new("A","Z1,Z2",ShellZoneClassification.OverlapsAnotherShell)],ReviewStatus=GeometryReviewStatus.GeometryReviewed};
        var json=SnapshotJson.Serialize(draft);session.RestoreDraft(draft,"P",State.Id);
        Assert.Equal(ShellZoneClassification.NotGeometricallyValidated,session.RoofRows.Single(r=>r.ShellId=="A").Classification);
        Assert.Equal(GeometryReviewStatus.GeometryDraft,session.ReviewStatus);Assert.Equal(json,SnapshotJson.Serialize(draft));
    }
}
