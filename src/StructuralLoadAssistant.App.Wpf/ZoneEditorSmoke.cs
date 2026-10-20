using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.App.Wpf;

public sealed partial class MainWindow
{
    public async Task RunZoneEditorSmokeAsync(bool reopen,Action changeGeometry,string output)
    {
        void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException("Zone editor regression: "+message); }
        IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            yield return root;
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach(var descendant in Walk(child)) yield return descendant;
        }
        void Click(string label)
        {
            var button = Walk(this).OfType<Button>().Single(b=>Equals(b.Content,label));
            Check(button.IsEnabled,label+" disabled"); button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            // Existing workspace buttons use commands; editor toolbar buttons use Click handlers.
            if(button.Command?.CanExecute(null)==true) button.Command.Execute(null);
        }
        async Task Idle() => await Dispatcher.InvokeAsync(()=>UpdateLayout(),DispatcherPriority.ApplicationIdle);
        string GeometryHash()=>SnapshotJson.Hash(vm.ZoneEditor.Zones.OrderBy(z=>z.Id,StringComparer.Ordinal)
            .Select(z=>new{z.Id,z.Label,z.RoofPlaneId,z.WindStateId,z.Vertices}));
        var expectedPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(vm.DatabasePath))!,"zone-geometry-expected.json");
        async Task Switch(WindDirection direction)
        {
            vm.Direction=direction; vm.InternalPressure="IP_POS"; vm.RoofCase="RoofCase1";
            Click("Switch wind state & restore draft"); await Idle();
        }
        vm.Mode=MainViewModel.WorkspaceMode.RealReadOnly;
        Tabs.SelectedIndex=2; await vm.ConnectReal();
        Tabs.SelectedIndex=4; zoneEditor.ShowEditor(); await Idle();
        Check(vm.IsMockAdapter && vm.DisplayShells.Count==4,"labeled gable fixture");
        Check(vm.ZoneEditor.Planes.Count==2,"gable must reconstruct two distinct planes");
        zoneEditor.SelectPlaneForShell("MOCK-A1"); await Idle();
        if(!reopen)
        {
            Click("Create Zone Polygon");
            zoneEditor.HandleSurfaceClick(new Point(120,120));zoneEditor.HandleSurfaceClick(new Point(180,120));zoneEditor.HandleSurfaceClick(new Point(150,180));
            Click("Finish Polygon");Check(vm.ZoneEditor.Zones.Length==1,"drawn polygon uses real surface pointer handler");
            Click("Delete zone");Check(vm.ZoneEditor.Zones.IsEmpty,"delete drawn polygon");
            zoneEditor.ShellList.SelectedItems.Clear(); zoneEditor.ShellList.SelectedItems.Add("MOCK-A1");
            zoneEditor.ZoneLabelInput.Text="WX Edge";
            Click("Create Zone from Selected Shells");
            zoneEditor.ShellList.SelectedItems.Clear(); zoneEditor.ShellList.SelectedItems.Add("MOCK-A2");
            zoneEditor.ZoneLabelInput.Text="WX Interior";
            Click("Create Zone from Selected Shells");
            Check(vm.ZoneEditor.Zones.Length==2,"two actual zone polygons");
            zoneEditor.SelectZone(vm.ZoneEditor.Zones[0].Id); await Idle();
            var original=vm.ZoneEditor.Zones[0].Vertices;
            var first=zoneEditor.Surface.Children.OfType<Thumb>().Single(t=>t.Tag is int index&&index==0);
            var opposite=zoneEditor.Surface.Children.OfType<Thumb>().Single(t=>t.Tag is int index&&index==2);
            var invalidDx=Canvas.GetLeft(opposite)-Canvas.GetLeft(first)+30;
            var invalidDy=(Canvas.GetTop(opposite)-Canvas.GetTop(first))/2;
            first.RaiseEvent(new DragStartedEventArgs(0,0));first.RaiseEvent(new DragDeltaEventArgs(invalidDx,invalidDy));first.RaiseEvent(new DragCompletedEventArgs(invalidDx,invalidDy,false));
            Check(vm.ZoneEditor.Zones[0].ValidationStatus==ZoneGeometryStatus.Invalid,"invalid drag marked invalid");
            vm.Refresh();Click("Validate zone intersections");
            Check(vm.ZoneEditor.Classifications.Any(c=>c.Classification==GeometryClassification.InvalidGeometry),"invalid polygons remain inspectable");
            Click("Undo zone edit");
            Check(vm.ZoneEditor.Zones[0].Vertices.SequenceEqual(original),"repair invalid polygon with Undo");
            var handle=zoneEditor.Surface.Children.OfType<Thumb>().First(t=>t.Tag is int);
            handle.RaiseEvent(new DragStartedEventArgs(0,0));
            handle.RaiseEvent(new DragDeltaEventArgs(18,12));
            handle.RaiseEvent(new DragCompletedEventArgs(18,12,false));
            Check(!vm.ZoneEditor.Zones[0].Vertices.SequenceEqual(original),"vertex drag changes stored coordinates");
            Click("Validate zone intersections");
            Check(vm.ZoneEditor.Classifications.Any(c=>c.NeedsSplitInCSI),"crossing must require split in CSI");
            zoneEditor.Capture(Path.Combine(output,"editor-crossing.png"));
            Click("Undo zone edit"); Click("Validate zone intersections");
            Check(vm.ZoneEditor.Classifications.Where(c=>c.ShellId.StartsWith("MOCK-A",StringComparison.Ordinal))
                .All(c=>c.Classification==GeometryClassification.FullyInsideZone),"corrected containment");
            Click("Save Geometry Draft");
            var wx=GeometryHash();
            zoneEditor.Capture(Path.Combine(output,"editor-WX.png"));
            // Legacy shell-label edits cannot leave old intersection results visible.
            vm.SelectShell("MOCK-A1");vm.SelectedZone="Edge";vm.EditZone();
            Check(vm.ZoneEditor.Classifications.IsEmpty,"manual association clears intersection cache");
            Click("Validate zone intersections");
            vm.SelectShell("MOCK-B1");vm.Exclude();
            Check(vm.ZoneEditor.Classifications.IsEmpty,"exclusion clears intersection cache");
            Click("Validate zone intersections");Click("Save Geometry Draft");
            var viewTabs=Walk(this).OfType<TabControl>().Single(t=>t.Items.OfType<TabItem>().Any(i=>Equals(i.Header,"3D Roof Orbit")));
            viewTabs.SelectedIndex=1;roof3D.BringIntoView();await Idle();
            var bitmap=new RenderTargetBitmap((int)roof3D.ActualWidth,(int)roof3D.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(roof3D);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var imageFile=File.Create(Path.Combine(output,"editor-3D.png")))encoder.Save(imageFile);
            await Switch(WindDirection.NegativeY);
            Check(vm.ZoneEditor.Zones.IsEmpty,"WY starts without WX geometry");
            zoneEditor.SelectPlaneForShell("MOCK-A1");
            zoneEditor.ShellList.SelectedItems.Clear();
            zoneEditor.ShellList.SelectedItems.Add("MOCK-A1"); zoneEditor.ShellList.SelectedItems.Add("MOCK-A2");
            zoneEditor.ZoneLabelInput.Text="WY union";
            Click("Create Zone from Selected Shells");
            Check(vm.ZoneEditor.Zones.Length==1,"WY union polygon");
            Click("Save Geometry Draft");
            File.WriteAllText(expectedPath,JsonSerializer.Serialize(new Dictionary<string,string>{{"WX",wx},{"WY",GeometryHash()}}));
            await Switch(WindDirection.PositiveX); Check(vm.ZoneEditor.Zones.Length==2,"WX restoration");
            Check(GeometryHash()==wx,"switch restores exact coordinate and identity hash");
        }
        else
        {
            var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(expectedPath))!;
            Check(vm.ZoneEditor.Zones.Length==2 && GeometryHash()==expected["WX"],"new process restores exact WX polygons");
            await Switch(WindDirection.NegativeY); Check(vm.ZoneEditor.Zones.Length==1,"new process restores WY union");
            Check(GeometryHash()==expected["WY"],"new process restores exact WY polygon coordinates");
            await Switch(WindDirection.PositiveX);
            zoneEditor.Capture(Path.Combine(output,"editor-reopened.png"));
            changeGeometry(); await vm.RefreshReal();
            Check(vm.SelectedGeometryReviewStatus==GeometryReviewStatus.StaleNeedsRescan,"original shell modification is stale");
            Check(!vm.ZoneEditor.Editable,"stale editor is blocked");
        }
        Check(vm.Session.Assignment is null && vm.Session.Roof.Approval is null,"no engineering approvals or writes");
        foreach(var label in new[]{"Approve Mapping","Preview loads","Simulate Apply to Fake CSI"})
            Check(!Walk(this).OfType<Button>().Single(b=>Equals(b.Content,label)).IsEnabled,label+" safety gate");
    }
}
