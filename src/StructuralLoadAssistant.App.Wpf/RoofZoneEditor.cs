using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.App.Wpf;

/// <summary>Surface UV editor. Coordinates and edits remain detached from CSI model objects.</summary>
public sealed class RoofZoneEditor : DockPanel
{
    private readonly MainViewModel vm;
    private readonly ComboBox planes=new(){MinWidth=180,DisplayMemberPath="Id"};
    private readonly ListBox zones=new(){Height=85,DisplayMemberPath="Label"};
    private readonly TextBox label=new(){Text="Engineer zone",Width=150},notes=new(){Width=220};
    private readonly TextBlock detail=new(){TextWrapping=TextWrapping.Wrap},cursor=new();
    private readonly DataGrid results=new(){IsReadOnly=true,AutoGenerateColumns=true,Height=140};
    private readonly List<UvPoint> drawing=[];
    private string? selectedZone;
    private bool synchronizing,creating;
    private int selectedVertex;
    private double scale=35,offsetX=35,offsetY=380;
    private Point? panStart;
    private UvPoint dragDelta;
    public Canvas Surface {get;}=new(){Width=1000,Height=430,Background=Brushes.WhiteSmoke,ClipToBounds=true,Focusable=true};
    public ListBox ShellList {get;}=new(){SelectionMode=SelectionMode.Extended,Height=85,MinWidth=160};
    public string? SelectedZoneId=>selectedZone;
    public TextBox ZoneLabelInput=>label;
    public IReadOnlyCollection<string> SelectedShellIds=>ShellList.SelectedItems.Cast<string>().ToArray();
    public event Action? SelectionChanged;
    public RoofZoneEditor(MainViewModel viewModel)
    {
        vm=viewModel;LastChildFill=true;Background=Brushes.White;Height=710;
        var toolbar=new WrapPanel();DockPanel.SetDock(toolbar,Dock.Top);Children.Add(toolbar);
        void Add(string text,Action action)=>toolbar.Children.Add(ActionButton(text,action));
        toolbar.Children.Add(planes);
        Add("Create Zone Polygon",()=>{RequireEditable();creating=true;drawing.Clear();Redraw();});
        Add("Finish Polygon",()=>{vm.ZoneEditor.Create(label.Text,Plane.Id,drawing.ToImmutableArray(),vm.User,vm.Reason);creating=false;drawing.Clear();SelectZone(vm.ZoneEditor.Zones[^1].Id);});
        Add("Cancel drawing",()=>{creating=false;drawing.Clear();Redraw();});
        Add("Create Zone from Selected Shells",()=>{var z=vm.ZoneEditor.CreateFromShells(label.Text,ShellList.SelectedItems.Cast<string>(),vm.User,vm.Reason);SelectZone(z.Id);});
        Add("Insert vertex",()=>{var z=Selected;var a=z.Vertices[selectedVertex];var b=z.Vertices[(selectedVertex+1)%z.Vertices.Length];vm.ZoneEditor.InsertVertex(z.Id,selectedVertex,new((a.U+b.U)/2,(a.V+b.V)/2),vm.User,vm.Reason);});
        Add("Delete vertex",()=>vm.ZoneEditor.DeleteVertex(Selected.Id,selectedVertex,vm.User,vm.Reason));
        Add("Snap zone to shell edges",()=>
        {
            var z=Selected;
            for(var i=0;i<z.Vertices.Length;i++)vm.ZoneEditor.MoveVertex(z.Id,i,vm.ZoneEditor.Snap(Plane.Id,z.Vertices[i],.1),vm.User,vm.Reason);
        });
        Add("Undo zone edit",()=>vm.ZoneEditor.Undo(vm.User,vm.Reason));
        Add("Redo zone edit",()=>vm.ZoneEditor.Redo(vm.User,vm.Reason));
        Add("Delete zone",()=>{vm.ZoneEditor.Delete(Selected.Id,vm.User,vm.Reason);selectedZone=null;});
        Add("Validate zone intersections",vm.ValidateZoneIntersections);
        Add("Review / merge selection scope",vm.AcceptSelectionScopeMerge);
        Add("Zoom +",()=>Zoom(1.25));Add("Zoom −",()=>Zoom(.8));Add("Reset UV view",ResetView);
        var side=new StackPanel{Width=255,Margin=new Thickness(8)};DockPanel.SetDock(side,Dock.Right);Children.Add(side);
        side.Children.Add(new TextBlock{Text="Zones • engineer labels, no pressures",TextWrapping=TextWrapping.Wrap});side.Children.Add(zones);
        side.Children.Add(label);side.Children.Add(notes);side.Children.Add(ActionButton("Save zone label / notes",()=>vm.ZoneEditor.SetMetadata(Selected.Id,label.Text,notes.Text,vm.User,vm.Reason)));
        side.Children.Add(new TextBlock{Text="Shells • Ctrl/Shift for multiple"});side.Children.Add(ShellList);
        side.Children.Add(new TextBlock{Text="Drag handles: vertices. Drag center: whole zone. Shift while dragging snaps within 0.10 m. Middle-drag pans; wheel zooms.",TextWrapping=TextWrapping.Wrap});
        side.Children.Add(detail);side.Children.Add(cursor);
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);Children.Add(bottom);
        bottom.Children.Add(new TextBlock{Text="Surface intersections • NeedsSplitInCSI is advisory only; edit CSI manually. Areas in m², coverage in %."});bottom.Children.Add(results);
        Children.Add(Surface);
        planes.SelectionChanged+=(_,_)=>{if(!synchronizing){selectedZone=null;creating=false;drawing.Clear();RefreshLists();ResetView();}};
        zones.SelectionChanged+=(_,_)=>{if(!synchronizing&&zones.SelectedItem is EditableRoofZone z){selectedZone=z.Id;selectedVertex=0;label.Text=z.Label;notes.Text=z.Notes??"";Redraw();SelectionChanged?.Invoke();}};
        ShellList.SelectionChanged+=(_,_)=>{if(!synchronizing&&ShellList.SelectedItem is string id)vm.SelectShell(id);Redraw();};
        Surface.MouseLeftButtonDown+=(_,e)=>{if(creating){HandleSurfaceClick(e.GetPosition(Surface));e.Handled=true;}};
        Surface.MouseDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Middle){panStart=e.GetPosition(Surface);Surface.CaptureMouse();}};
        Surface.MouseMove+=(_,e)=>
        {
            var p=e.GetPosition(Surface);var uv=FromScreen(p);cursor.Text=string.Create(CultureInfo.InvariantCulture,$"U={uv.U:F3} m  V={uv.V:F3} m");
            if(panStart is Point last&&e.MiddleButton==MouseButtonState.Pressed){offsetX+=p.X-last.X;offsetY+=p.Y-last.Y;panStart=p;Redraw();}
        };
        Surface.MouseUp+=(_,e)=>{if(e.ChangedButton==MouseButton.Middle){panStart=null;Surface.ReleaseMouseCapture();}};
        Surface.MouseWheel+=(_,e)=>{Zoom(e.Delta>0?1.15:1/1.15);e.Handled=true;};
        vm.Refreshed+=RefreshEditor;RefreshEditor();
    }
    private RoofPlaneCoordinateSystem Plane=>planes.SelectedItem as RoofPlaneCoordinateSystem??throw new InvalidOperationException("Select a roof plane.");
    internal void HandleSurfaceClick(Point point)
    {if(!creating)return;RequireEditable();drawing.Add(FromScreen(point));Redraw();}
    private EditableRoofZone Selected=>vm.ZoneEditor.Zones.FirstOrDefault(z=>z.Id==selectedZone)??throw new InvalidOperationException("Select a zone.");
    private Button ActionButton(string text,Action action)
    {
        var b=new Button{Content=text,Margin=new Thickness(3),Padding=new Thickness(7,5,7,5)};
        b.Click+=(_,_)=>vm.Run(()=>{action();RefreshEditor();});return b;
    }
    private void RequireEditable(){if(!vm.ZoneEditor.Editable)throw new InvalidOperationException("Connect current valid read-only geometry; resolve stale/scope review first.");}
    public void ShowEditor(){if(Parent is TabItem item&&item.Parent is TabControl tabs)tabs.SelectedItem=item;}
    public void SelectPlaneForShell(string id){planes.SelectedItem=vm.ZoneEditor.PlaneForShell(id);RefreshLists();ResetView();}
    public void SelectZone(string id)
    {
        var zone=vm.ZoneEditor.Zones.Single(z=>z.Id==id);
        if((planes.SelectedItem as RoofPlaneCoordinateSystem)?.Id!=zone.RoofPlaneId)
            planes.SelectedItem=vm.ZoneEditor.Planes.Single(p=>p.Id==zone.RoofPlaneId);
        selectedZone=id;selectedVertex=0;label.Text=zone.Label;notes.Text=zone.Notes??"";
        RefreshLists();Redraw();SelectionChanged?.Invoke();
    }
    private void RefreshEditor()
    {
        var id=(planes.SelectedItem as RoofPlaneCoordinateSystem)?.Id;
        synchronizing=true;planes.ItemsSource=vm.ZoneEditor.Planes;planes.SelectedItem=vm.ZoneEditor.Planes.FirstOrDefault(p=>p.Id==id)??vm.ZoneEditor.Planes.FirstOrDefault();synchronizing=false;
        RefreshLists();Redraw();
    }
    private void RefreshLists()
    {
        synchronizing=true;
        zones.ItemsSource=vm.ZoneEditor.Zones.Where(z=>z.RoofPlaneId==(planes.SelectedItem as RoofPlaneCoordinateSystem)?.Id).ToArray();
        zones.SelectedItem=vm.ZoneEditor.Zones.FirstOrDefault(z=>z.Id==selectedZone);
        var selected=ShellList.SelectedItems.Cast<string>().ToArray();
        ShellList.ItemsSource=planes.SelectedItem is RoofPlaneCoordinateSystem p?vm.ZoneEditor.ShellIds(p.Id):[];
        foreach(var id in selected.Where(id=>ShellList.Items.Contains(id)))ShellList.SelectedItems.Add(id);
        results.ItemsSource=vm.ZoneEditor.Classifications.Select(c=>new{c.ShellId,c.Classification,c.ShellAreaM2,c.NeedsSplitInCSI,
            Coverage=string.Join("; ",c.Coverages.Select(v=>$"{v.ZoneId}: {v.IntersectionAreaM2:F4} m² / {v.CoveragePercent:F2}%")),c.Diagnostic}).ToArray();
        synchronizing=false;
    }
    private Point ToScreen(UvPoint p)=>new(offsetX+p.U*scale,offsetY-p.V*scale);
    private UvPoint FromScreen(Point p)=>new((p.X-offsetX)/scale,(offsetY-p.Y)/scale);
    private void Zoom(double factor){scale=Math.Clamp(scale*factor,2,1500);Redraw();}
    private void ResetView()
    {
        if(planes.SelectedItem is not RoofPlaneCoordinateSystem plane)return;
        var points=vm.ZoneEditor.ShellIds(plane.Id).SelectMany(id=>vm.ZoneEditor.Footprint(id)).ToArray();if(points.Length==0)return;
        var minU=points.Min(p=>p.U);var maxU=points.Max(p=>p.U);var minV=points.Min(p=>p.V);var maxV=points.Max(p=>p.V);
        scale=Math.Min(900/Math.Max(.1,maxU-minU),350/Math.Max(.1,maxV-minV));offsetX=40-minU*scale;offsetY=390+minV*scale;Redraw();
    }
    private void Redraw()
    {
        Surface.Children.Clear();if(planes.SelectedItem is not RoofPlaneCoordinateSystem plane)return;
        foreach(var id in vm.ZoneEditor.ShellIds(plane.Id))
        {
            var points=vm.ZoneEditor.Footprint(id);var selected=ShellList.SelectedItems.Contains(id)||vm.SelectedShell==id;
            var polygon=Polygon(points,selected?Brushes.LightGoldenrodYellow:Brushes.White,Brushes.SlateGray,1.5);polygon.Tag=id;
            polygon.MouseLeftButtonDown+=(_,e)=>
            {
                if(creating)return;
                if((Keyboard.Modifiers&ModifierKeys.Control)==0)ShellList.SelectedItems.Clear();
                if(!ShellList.SelectedItems.Contains(id))ShellList.SelectedItems.Add(id);vm.SelectShell(id);e.Handled=true;
            };
            Surface.Children.Add(polygon);
            AddText(id,new(points.Average(p=>p.U),points.Average(p=>p.V)));
        }
        foreach(var zone in vm.ZoneEditor.Zones.Where(z=>z.RoofPlaneId==plane.Id))
        {
            var fill=new SolidColorBrush(zone.ValidationStatus==ZoneGeometryStatus.Invalid?Color.FromArgb(80,220,40,40):Color.FromArgb(70,25,145,190));
            var polygon=Polygon(zone.Vertices,fill,zone.Id==selectedZone?Brushes.DarkBlue:Brushes.SteelBlue,zone.Id==selectedZone?3:2);
            polygon.MouseLeftButtonDown+=(_,e)=>{if(creating)return;SelectZone(zone.Id);e.Handled=true;};Surface.Children.Add(polygon);
            if(zone.Id!=selectedZone)continue;
            for(var i=0;i<zone.Vertices.Length;i++)
            {
                var index=i;var point=zone.Vertices[index];
                var handle=Handle(ToScreen(point),index==selectedVertex?Brushes.OrangeRed:Brushes.DarkBlue);handle.Tag=index;handle.IsEnabled=vm.ZoneEditor.Editable;
                handle.DragStarted+=(_,_)=>{RequireEditable();selectedVertex=index;dragDelta=default;};
                handle.DragDelta+=(_,e)=>{dragDelta=new(dragDelta.U+e.HorizontalChange/scale,dragDelta.V-e.VerticalChange/scale);Canvas.SetLeft(handle,ToScreen(new(point.U+dragDelta.U,point.V+dragDelta.V)).X-5);Canvas.SetTop(handle,ToScreen(new(point.U+dragDelta.U,point.V+dragDelta.V)).Y-5);};
                handle.DragCompleted+=(_,e)=>vm.Run(()=>{if(e.Canceled)return;var target=new UvPoint(point.U+dragDelta.U,point.V+dragDelta.V);if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)target=vm.ZoneEditor.Snap(plane.Id,target,.1);vm.ZoneEditor.MoveVertex(zone.Id,index,target,vm.User,vm.Reason);});
                Surface.Children.Add(handle);AddText($"{index}: ({point.U:F2}, {point.V:F2}) m",point);
            }
            var center=new UvPoint(zone.Vertices.Average(p=>p.U),zone.Vertices.Average(p=>p.V));var mover=Handle(ToScreen(center),Brushes.Teal);mover.Tag="whole-zone";mover.Cursor=Cursors.SizeAll;mover.IsEnabled=vm.ZoneEditor.Editable;
            mover.DragStarted+=(_,_)=>{RequireEditable();dragDelta=default;};
            mover.DragDelta+=(_,e)=>{dragDelta=new(dragDelta.U+e.HorizontalChange/scale,dragDelta.V-e.VerticalChange/scale);Canvas.SetLeft(mover,ToScreen(new(center.U+dragDelta.U,center.V+dragDelta.V)).X-5);Canvas.SetTop(mover,ToScreen(new(center.U+dragDelta.U,center.V+dragDelta.V)).Y-5);};
            mover.DragCompleted+=(_,e)=>vm.Run(()=>{if(!e.Canceled)vm.ZoneEditor.Translate(zone.Id,dragDelta,vm.User,vm.Reason);});Surface.Children.Add(mover);
            var error=ZonePolygonEngine.Validate(zone.Vertices);
            var areaText=error is null?$"{ZonePolygonEngine.Area(zone.Vertices):F3} m²":"unavailable • "+error;
            detail.Text=$"{zone.Label}\nID {zone.Id}\nRevision {zone.Revision} • {zone.ValidationStatus}\nArea {areaText}\nNotes: {zone.Notes}\n{vm.SelectedGeometryReviewStatus}";
        }
        if(creating){if(drawing.Count>=2)Surface.Children.Add(Polygon(drawing,Brushes.Transparent,Brushes.OrangeRed,2));foreach(var p in drawing)AddText("•",p);}
        if(selectedZone is null || !vm.ZoneEditor.Zones.Any(z=>z.Id==selectedZone))detail.Text=$"{plane.Id}\nSlope {plane.SlopeDegrees:F2}° • azimuth {plane.AzimuthDegrees:F2}°\nU/V in surface meters. Geometric normal is independent of CSI Local 3 / outward normal.\n{vm.SelectedGeometryReviewStatus}";
    }
    private Polygon Polygon(IEnumerable<UvPoint> points,Brush fill,Brush stroke,double thickness)=>new(){Points=new PointCollection(points.Select(ToScreen)),Fill=fill,Stroke=stroke,StrokeThickness=thickness};
    private static Thumb Handle(Point p,Brush color)
    {var t=new Thumb{Width=11,Height=11,Background=color,Cursor=Cursors.SizeAll};Canvas.SetLeft(t,p.X-5);Canvas.SetTop(t,p.Y-5);return t;}
    private void AddText(string text,UvPoint point)
    {var p=ToScreen(point);var t=new TextBlock{Text=text,FontSize=11,IsHitTestVisible=false};Canvas.SetLeft(t,p.X+7);Canvas.SetTop(t,p.Y+5);Surface.Children.Add(t);}
    public void Capture(string path)
    {
        UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Max(1,ActualWidth),(int)Math.Max(1,ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(this);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
}
