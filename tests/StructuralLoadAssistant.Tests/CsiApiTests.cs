using System.Collections.Immutable;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.ApiV1;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;
public sealed class CsiApiTests
{
    [Fact] public void Units_are_SI_and_unknown_units_rejected() { Assert.Equal(.3048, CsiUnitConverter.FromId(4).LengthToM); Assert.Equal(4448.2216152605, CsiUnitConverter.FromId(4).ForceToN); Assert.Throws<ArgumentOutOfRangeException>(()=>CsiUnitConverter.FromId(0)); }
    [Fact] public async Task Unsupported_version_cannot_connect() { await using var a=new InstalledCsiReadAdapter(_=>new Session("22.0.0")); Assert.False((await a.ConnectAsync(new(CsiProduct.Etabs,"unused"))).Connected); }
    [Fact] public async Task Selection_preserves_XYZ_axes_and_unknown_outward_normal() { var s=new Session(); await using var a=new InstalledCsiReadAdapter(_=>s); Assert.True((await a.ConnectAsync(new(CsiProduct.Etabs,"unused"))).Connected); var r=await a.RefreshSelectionAsync(); Assert.Equal(new Vec3(1,2,3),r.Model.Frames.Single().Start); Assert.Equal(new Vec3(0,0,1),r.Model.Shells.Single().Local3); Assert.Equal(default,r.Model.Shells.Single().OutwardNormal); Assert.Null(r.AreaMetadata.Single().VerifiedOutwardNormal); Assert.Single(s.Threads); Assert.DoesNotContain(s.Calls,x=>!x.StartsWith("Get")); }
    [Fact] public async Task Return_codes_and_invalid_coordinates_clear_stale_selection() { var s=new Session(); await using var a=new InstalledCsiReadAdapter(_=>s); await a.ConnectAsync(new(CsiProduct.Etabs,"unused")); Assert.Single((await a.RefreshSelectionAsync()).Model.Frames); s.BadSelection=true; var scan=await a.RefreshSelectionAsync(); Assert.Empty(scan.Model.Frames); Assert.Contains(scan.Issues,x=>x.ReturnCode==7); }
    [Fact] public async Task NaN_coordinates_are_rejected() { var s=new Session { BadCoordinate=true }; await using var a=new InstalledCsiReadAdapter(_=>s); await a.ConnectAsync(new(CsiProduct.Etabs,"unused")); var scan=await a.RefreshSelectionAsync(); Assert.Empty(scan.Model.Frames); Assert.Contains(scan.Issues,x=>x.Code=="INVALID_DATA"); }
    [Fact] public async Task Canceled_refresh_never_invokes_session() { var s=new Session(); await using var a=new InstalledCsiReadAdapter(_=>s); await a.ConnectAsync(new(CsiProduct.Etabs,"unused")); var count=s.Calls.Count; await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>a.RefreshSelectionAsync(new CancellationToken(true))); Assert.Equal(count,s.Calls.Count); }
    [Fact] public async Task Disconnected_refresh_is_typed_and_empty() { await using var a=new InstalledCsiReadAdapter(_=>new Session()); var r=await a.RefreshSelectionAsync(); Assert.Empty(r.Model.Frames); Assert.Contains(r.Issues,x=>x.Code=="DISCONNECTED"); }
    [Fact] public async Task Rotated_matrix_uses_local3_column() {var s=new Session {Rotated=true};await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));var r=await a.RefreshSelectionAsync();Assert.Equal(new Vec3(1,0,0),r.Model.Shells.Single().Local3);}
    [Fact] public async Task Distributed_and_uniform_loads_convert_dimensionally() {var s=new Session {UnitId=5,WithLoads=true};await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));var r=await a.RefreshSelectionAsync();Assert.Equal(2e6,r.Model.ExistingLoads.Single(x=>x.Kind.EndsWith("endpoint=1")).ValueSI);Assert.Equal(3e9,r.Model.ExistingLoads.Single(x=>x.Kind.StartsWith("AreaUniform")).ValueSI);Assert.Contains("startM=0.001",r.Model.ExistingLoads.Single(x=>x.Kind.EndsWith("endpoint=1")).Kind);}
    [Fact] public async Task Unknown_units_and_changed_units_invalidate_selection() {var s=new Session();await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));s.UnitId=99;var r=await a.RefreshSelectionAsync();Assert.Empty(r.Model.Frames);Assert.Contains(r.Issues,x=>x.Code=="UNSUPPORTED_UNITS");}
    [Fact] public async Task Missing_optional_getter_downgrades_capability() {var s=new Session {MissingStories=true};await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));var r=await a.RefreshSelectionAsync();Assert.False(r.Capabilities.Stories);Assert.Contains(r.Issues,x=>x.Code=="UNSUPPORTED_GETTER");}
    [Fact] public async Task Missing_optional_axes_preserve_geometry_as_unknown() {var s=new Session {MissingAxes=true};await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));var r=await a.RefreshSelectionAsync();Assert.Single(r.Model.Frames);var shell=Assert.Single(r.Model.Shells);Assert.Equal(default,shell.Local3);Assert.StartsWith("unknown-plane:",shell.RoofPlaneId);Assert.False(r.Capabilities.LocalAxes);Assert.Contains(r.Issues,x=>x.Code=="UNSUPPORTED_GETTER");}
    [Fact] public async Task Model_change_during_scan_invalidates_all_scope() {var s=new Session {ModelChanges=true};await using var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));var r=await a.RefreshSelectionAsync();Assert.Empty(r.Model.Frames);Assert.Empty(r.Model.Shells);Assert.Empty(r.Model.Patterns);Assert.Empty(r.Stories);Assert.Equal("",r.Model.ModelId);Assert.Equal("unknown",r.Units.NativeUnitId);Assert.Contains(r.Issues,x=>x.Code=="STALE_SCAN");}
    [Fact] public async Task Connect_read_and_release_share_owned_apartment() {var s=new Session();var a=new InstalledCsiReadAdapter(_=>s);await a.ConnectAsync(new(CsiProduct.Etabs,"unused"));await a.RefreshSelectionAsync();await a.DisposeAsync();Assert.Single(s.Threads);Assert.True(s.Disposed);if(OperatingSystem.IsWindows())Assert.Equal(ApartmentState.STA,s.Apartment);}
    private sealed class Session(string version="23.0.0") : ICsiApiSession
    {
        public bool BadSelection, BadCoordinate, Rotated, WithLoads, MissingStories, MissingAxes, ModelChanges; public int UnitId=10; private int filenameReads; public bool Disposed; public ApartmentState Apartment; public List<string> Calls=new(); public HashSet<int> Threads=new();
        public object? Read(string component,string getter,object?[] args) { Calls.Add(getter); Threads.Add(Environment.CurrentManagedThreadId); Apartment=Thread.CurrentThread.GetApartmentState();
            switch(getter) {
            case "GetVersion": args[0]=version; args[1]=23d; return 0;
            case "GetModelFilename": return ModelChanges && ++filenameReads>=3?"changed.edb":"mock.edb";
            case "GetPresentUnits": return UnitId;
            case "GetModelIsLocked": return true;
            case "GetNameList": args[0]=0; args[1]=Array.Empty<string>(); return 0;
            case "GetStories": if(MissingStories)throw new NotSupportedException(); args[0]=0; args[1]=new[]{"Base"}; args[2]=new[]{0d}; args[3]=new[]{0d}; return 0;
            case "GetSelected": if(BadSelection)return 7; args[0]=2; args[1]=new[]{2,5};args[2]=new[]{"F1","A1"};return 0;
            case "GetPoints": if(component=="FrameObj"){args[1]="P1";args[2]="P2";}else{args[1]=3;args[2]=new[]{"P1","P2","P3"};}return 0;
            case "GetCoordCartesian": args[1]=BadCoordinate?double.NaN:((string)args[0]! =="P2"?2d:1d);args[2]=(string)args[0]! =="P3"?3d:2d;args[3]=(string)args[0]! =="P1"?3d:4d;return 0;
            case "GetTransformationMatrix": if(MissingAxes)throw new NotSupportedException();args[1]=Rotated?new[]{0d,0,1,0,1,0,-1,0,0}:new[]{1d,0,0,0,1,0,0,0,1};return 0;
                        case "GetLoadDistributed": if(!WithLoads){args[1]=0;return 0;}args[1]=1;args[2]=new[]{"F1"};args[3]=new[]{"W"};args[4]=new[]{1};args[5]=new[]{"Global"};args[6]=new[]{6};for(int i=7;i<=12;i++)args[i]=new[]{i==11?2d:1d};return 0;
            case "GetLoadUniform": if(!WithLoads){args[1]=0;return 0;}args[1]=1;args[2]=new[]{"A1"};args[3]=new[]{"W"};args[4]=new[]{"Global"};args[5]=new[]{6};args[6]=new[]{3d};return 0;
            default: throw new InvalidOperationException(getter); } }
        public void Dispose(){Disposed=true;Threads.Add(Environment.CurrentManagedThreadId);Apartment=Thread.CurrentThread.GetApartmentState();}
    }
}
