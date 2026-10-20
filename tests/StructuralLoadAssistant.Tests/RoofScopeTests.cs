using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public sealed class RoofScopeTests
{
    private static readonly WindState State = new(WindDirection.PositiveX, "IP_POS", "RoofCase1", LimitState.Uls);
    [Fact]
    public async Task Smaller_selection_requires_scope_review_instead_of_claiming_modified_geometry()
    {
        var reader = new ScopeReader();
        await using var session = new CsiInspectionSession(reader);
        await session.ConnectAsync(new(CsiProduct.Etabs, "mock")); await session.RefreshAsync();
        session.SetGeometryZone("A", "Edge", "Engineer", "Label");
        var draft = session.CreateDraft("P", State);
        reader.OnlyA = true; await session.RefreshAsync();
        Assert.False(session.RestoreDraft(draft, "P", State.Id));
        Assert.Equal(GeometryReviewStatus.ScopeReviewRequired, session.ReviewStatus);
    }

    [Fact]
    public async Task Explicit_scope_merge_retains_unselected_mappings_and_exclusions()
    {
        var reader=new ScopeReader();
        await using var session=new CsiInspectionSession(reader);
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        session.SetGeometryZone("B","Corner","Engineer","Saved zone");
        session.ExcludeShell("B","Engineer","Omitted roof scope");
        var draft=session.CreateDraft("P",State);
        reader.OnlyA=true;await session.RefreshAsync();session.RestoreDraft(draft,"P",State.Id);
        Assert.Throws<InvalidOperationException>(()=>session.AcceptScopeMerge("",""));
        session.AcceptScopeMerge("Engineer","Retain omitted shell data");
        var saved=session.CreateDraft("P",State,2);
        Assert.Contains(saved.Items,i=>i.ShellId=="B"&&i.ZoneId=="Excluded");
        Assert.Contains(saved.Exclusions,e=>e.ShellId=="B");
        Assert.Equal(new[]{"A"},saved.SelectedScope);
        reader.OnlyA=false;await session.RefreshAsync();session.RestoreDraft(saved,"P",State.Id);
        Assert.Equal(GeometryReviewStatus.ScopeReviewRequired,session.ReviewStatus);
        session.AcceptScopeMerge("Engineer","Restore broader selection");
        Assert.Contains(session.RoofRows,r=>r.ShellId=="B"&&r.ZoneId=="Excluded");
    }

    [Fact]
    public async Task Polygon_edit_invalidates_omitted_derived_mappings_before_scope_restoration()
    {
        var reader=new ScopeReader();await using var session=new CsiInspectionSession(reader);
        await session.ConnectAsync(new(CsiProduct.Etabs,"mock"));await session.RefreshAsync();
        session.SetIntersectionMappings([new("B",GeometryClassification.FullyInsideZone,4,[new("deleted-zone",4,100)],false,"")],State.Id);
        var draft=session.CreateDraft("P",State);
        reader.OnlyA=true;await session.RefreshAsync();session.RestoreDraft(draft,"P",State.Id);
        session.AcceptScopeMerge("Engineer","Smaller selection");
        session.InvalidateGeometryReview();
        var saved=session.CreateDraft("P",State,2);
        Assert.DoesNotContain(saved.Items,i=>i.ShellId=="B"&&i.Classification==ShellZoneClassification.FullyInsideZone);
        Assert.Null(saved.Items.Single(i=>i.ShellId=="B").ZoneId);
    }

    [Fact]
    public async Task Existing_shell_vertex_change_is_stale_even_with_smaller_scope()
    {
        var reader = new ScopeReader();
        await using var session = new CsiInspectionSession(reader);
        await session.ConnectAsync(new(CsiProduct.Etabs, "mock")); await session.RefreshAsync();
        var draft = session.CreateDraft("P", State);
        reader.OnlyA = true; reader.MoveA = true; await session.RefreshAsync();
        Assert.False(session.RestoreDraft(draft, "P", State.Id));
        Assert.Equal(GeometryReviewStatus.StaleNeedsRescan, session.ReviewStatus);
    }

    internal sealed class ScopeReader : ICsiSessionReadAdapter
    {
        public bool OnlyA, MoveA;
        private static CsiCapabilities Capabilities => new(true,true,true,true);
        public Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options,CancellationToken cancellationToken=default) => Task.FromResult(new CsiConnectionResult(true,"MOCK","test",Capabilities,[]));
        public Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken=default)
        {
            double z = MoveA ? 4 : 3;
            ImmutableArray<ShellGeometry> shells = [new("A","roof",[new(0,0,z),new(2,0,z),new(2,2,z),new(0,2,z)],new(0,0,1),default)];
            if(!OnlyA) shells = shells.Add(new("B","roof",[new(2,0,3),new(4,0,3),new(4,2,3),new(2,2,3)],new(0,0,1),default));
            var model = new CsiModelSnapshot("scan","MOCK","MOCK","test",$"scope-{OnlyA}-{MoveA}","loads",[],[],[],[],shells,[]);
            return Task.FromResult(new CsiReadScan(model,new("SI",1,1,"SI"),true,Capabilities,[],[],[]));
        }
        public async Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken=default) => (await RefreshSelectionAsync(cancellationToken)).Model;
        public Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken=default) => Task.FromResult(ImmutableArray<ExistingLoad>.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
