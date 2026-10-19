using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Tests;

public class CsiInspectionTests
{
    private static CsiConnectionOptions Options => new(CsiProduct.Etabs,"local API");
    [Fact] public async Task ConnectedSelection_IsRealGeometryOnlyAndCannotApprove()
    {
        var adapter=new Reader();await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);await session.RefreshAsync();
        Assert.Equal(CsiScanOutcome.Succeeded,session.Outcome);
        Assert.Equal("Selected",session.Scan!.Model.ModelId);Assert.Empty(session.Facade!.Pairs);
        var row=Assert.Single(session.RoofRows);Assert.Equal(ShellZoneClassification.Unmapped,row.Classification);
        Assert.Equal(new Vec3(0,0,-1),session.Scan.Model.Shells[0].Local3);
        Assert.Equal(default,session.Scan.Model.Shells[0].OutwardNormal);
        Assert.Null(session.Scan.AreaMetadata[0].VerifiedOutwardNormal);
        Assert.Contains(session.Roof!.Validate(),i=>i.Code=="StandardsGate"&&i.Blocking);
        Assert.Throws<InvalidOperationException>(()=>session.Roof.Approve("Engineer",DateTimeOffset.UtcNow));
    }
    [Fact] public async Task RefreshAndClear_InvalidateSelectionAndRoofContext()
    {
        await using var session=new CsiInspectionSession(new Reader());await session.ConnectAsync(Options);await session.RefreshAsync();
        session.Facade!.SelectPair("L","U");var oldRoof=session.Roof;
        await session.RefreshAsync();Assert.Empty(session.Facade!.Pairs);Assert.NotSame(oldRoof,session.Roof);
        session.Clear();Assert.Null(session.Connection);Assert.Null(session.Scan);Assert.Null(session.Facade);Assert.Null(session.Roof);Assert.Empty(session.RoofRows);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>session.RefreshAsync());
    }
    [Fact] public async Task FailedRefresh_DropsStaleGeometryAndReportsIssue()
    {
        var adapter=new Reader();await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);await session.RefreshAsync();
        adapter.Fail=true;await session.RefreshAsync();Assert.Null(session.Scan);Assert.Null(session.Roof);Assert.Null(session.Facade);Assert.Contains(session.Issues,i=>i.Code=="RefreshFailed");
    }
    [Fact] public async Task FailedConnection_DoesNotReadSelection()
    {
        var adapter=new Reader{Connected=false};await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);
        Assert.False(session.Connection!.Connected);await Assert.ThrowsAsync<InvalidOperationException>(()=>session.RefreshAsync());Assert.Null(session.Scan);
    }
    [Fact] public async Task ManualLocal3Review_RequiresAuditAndNeverInfersOutwardNormal()
    {
        await using var session=new CsiInspectionSession(new Reader());await session.ConnectAsync(Options);await session.RefreshAsync();
        Assert.Throws<ArgumentException>(()=>session.SetLocal3Review("R",ManualLocal3Direction.Positive,"",""));
        Assert.Empty(session.Local3Reviews);Assert.Equal(ManualLocal3Direction.Unacknowledged,session.RoofRows[0].ManualDirection);
        session.SetLocal3Review("R",ManualLocal3Direction.Positive,"Engineer","Axis reviewed");
        Assert.Equal(ManualLocal3Direction.Positive,session.RoofRows[0].ManualDirection);Assert.Single(session.Local3Reviews);
        Assert.Null(session.Scan!.AreaMetadata[0].VerifiedOutwardNormal);Assert.Equal(default,session.Scan.Model.Shells[0].OutwardNormal);
        await session.RefreshAsync();Assert.Empty(session.Local3Reviews);Assert.Equal(ManualLocal3Direction.Unacknowledged,session.RoofRows[0].ManualDirection);
    }
    [Fact] public async Task ClearDuringRefresh_CannotRestoreGeometry()
    {
        var adapter=new Reader();await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);
        adapter.ScanGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var refresh=session.RefreshAsync();session.Clear();
        adapter.ScanGate.SetResult(true);await refresh;
        Assert.Null(session.Connection);Assert.Null(session.Scan);Assert.Null(session.Roof);Assert.Empty(session.RoofRows);
    }
    [Fact] public async Task ClearDuringConnect_CannotRestoreConnection()
    {
        var adapter=new Reader{ConnectGate=new(TaskCreationOptions.RunContinuationsAsynchronously)};await using var session=new CsiInspectionSession(adapter);
        var connect=session.ConnectAsync(Options);session.Clear();adapter.ConnectGate.SetResult(true);await connect;
        Assert.Null(session.Connection);Assert.Null(session.Scan);
    }
    [Fact] public async Task CanceledRefresh_IgnoringAdapterCancellationCannotRestoreGeometry()
    {
        var adapter=new Reader();await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);await session.RefreshAsync();
        adapter.ScanGate=new(TaskCreationOptions.RunContinuationsAsynchronously);using var cancellation=new CancellationTokenSource();
        var refresh=session.RefreshAsync(cancellation.Token);cancellation.Cancel();adapter.ScanGate.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>refresh);Assert.Null(session.Scan);Assert.Null(session.Roof);
    }
    [Fact] public async Task CanceledConnect_IgnoringAdapterCancellationCannotRestoreConnection()
    {
        var adapter=new Reader{ConnectGate=new(TaskCreationOptions.RunContinuationsAsynchronously)};await using var session=new CsiInspectionSession(adapter);
        using var cancellation=new CancellationTokenSource();var connect=session.ConnectAsync(Options,cancellation.Token);
        cancellation.Cancel();adapter.ConnectGate.SetResult(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>connect);Assert.Null(session.Connection);Assert.Null(session.Scan);
    }
    [Theory]
    [InlineData(0,0,0)]
    [InlineData(0,0,2)]
    [InlineData(double.NaN,0,1)]
    public async Task InvalidVerifiedNormal_RemainsUnknown(double x,double y,double z)
    {
        var adapter=new Reader{Normal=new(x,y,z)};await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);await session.RefreshAsync();
        Assert.Null(session.Scan!.AreaMetadata[0].VerifiedOutwardNormal);Assert.Equal(default,session.Scan.Model.Shells[0].OutwardNormal);
        Assert.Contains(session.Issues,i=>i.Code=="NormalMetadataInvalid");
    }
    [Fact] public async Task ExplicitVerifiedUnitNormal_IsRetainedWithoutChangingLocal3()
    {
        var adapter=new Reader{Normal=new(0,0,1)};await using var session=new CsiInspectionSession(adapter);await session.ConnectAsync(Options);await session.RefreshAsync();
        Assert.Equal(new Vec3(0,0,1),session.Scan!.Model.Shells[0].OutwardNormal);Assert.Equal(new Vec3(0,0,-1),session.Scan.Model.Shells[0].Local3);
    }
    [Fact] public async Task InspectionSession_TracksExplicitScanOutcomes()
    {
        var adapter=new Reader();await using var session=new CsiInspectionSession(adapter);
        Assert.Equal(CsiScanOutcome.Disconnected, session.Outcome);
        await session.ConnectAsync(Options);
        Assert.Equal(CsiScanOutcome.Disconnected, session.Outcome);
        await session.RefreshAsync();
        Assert.Equal(CsiScanOutcome.Succeeded, session.Outcome);
        adapter.OutcomeToReturn = CsiScanOutcome.EmptySelection;
        await session.RefreshAsync();
        Assert.Equal(CsiScanOutcome.EmptySelection, session.Outcome);
        adapter.OutcomeToReturn = CsiScanOutcome.PartialScan;
        await session.RefreshAsync();
        Assert.Equal(CsiScanOutcome.PartialScan, session.Outcome);
        adapter.Fail = true;
        await session.RefreshAsync();
        Assert.Equal(CsiScanOutcome.Failed, session.Outcome);
        session.Clear();
        Assert.Equal(CsiScanOutcome.Disconnected, session.Outcome);
    }
    private sealed class Reader : ICsiSessionReadAdapter
    {
        public bool Fail; public bool Connected=true;
        public Vec3? Normal;
        public CsiScanOutcome OutcomeToReturn = CsiScanOutcome.Succeeded;
        public TaskCompletionSource<bool>? ScanGate;
        public TaskCompletionSource<bool>? ConnectGate;
        private static CsiCapabilities Capabilities => new(true,true,true,true);
        public async Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options,CancellationToken cancellationToken=default)
        {
            if(ConnectGate is not null)await ConnectGate.Task;
            return new(Connected,"ETABS","test",Capabilities,[]);
        }
        public async Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken=default)
        {
            if(ScanGate is not null)await ScanGate.Task;
            if(Fail)throw new InvalidOperationException("Read failed");
            var model=new CsiModelSnapshot("scan","Selected","ETABS","test","geometry","loads",["W"],[],[],
                [new("L","A",new(0,0,0),new(10,0,0)),new("U","A",new(0,0,3),new(10,0,3))],
                [new("R","plane",[new(0,0,3),new(10,0,3),new(10,10,3),new(0,10,3)],new(0,0,-1),default)],[]);
             return new CsiReadScan(model,new("SI",1,1,"SI"),true,Capabilities,[],[new("R","plane",Normal,"Unknown")],[],OutcomeToReturn);
        }
        public async Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken=default)=>(await RefreshSelectionAsync(cancellationToken)).Model;
        public Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken=default)=>Task.FromResult(ImmutableArray<ExistingLoad>.Empty);
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
