using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.App.Wpf;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly Canvas roof = new() { Width = 540, Height = 420, Background = Brushes.WhiteSmoke };
    private readonly Roof3DViewport roof3D = new() { Width = 540, Height = 420 };
    private readonly RoofZoneEditor zoneEditor;
    public TabControl Tabs { get; } = new();
    public MainWindow(MainViewModel viewModel)
    {
        vm = viewModel; DataContext = vm; zoneEditor=new(vm);
        Title = "Structural Load Assistant | Demo / CSI Read-Only"; Width = 1380; Height = 920;
        MinWidth = 1000; MinHeight = 700; Background = Brush("#EEF2F6");
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = Text("STRUCTURAL LOAD ASSISTANT", 25); header.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var modes = Row(Text("Workspace mode:"), Combo("Mode", Enum.GetValues<MainViewModel.WorkspaceMode>(), 220), BoundText("ModeBanner"));
        DockPanel.SetDock(modes, Dock.Top); root.Children.Add(modes);
        var status = BoundText("Status"); status.Padding = new Thickness(10); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        root.Children.Add(Tabs); Content = root;
        AddPage("Project", ProjectPage()); AddPage("Wind", WindPage()); AddPage("CSI", CsiPage());
        AddPage("Facade", FacadePage()); AddPage("Roof", RoofPage()); AddPage("Wall", WallPage()); AddPage("Report", ReportPage());
        roof3D.ShellClicked += id => vm.SelectShell(id);
        zoneEditor.SelectionChanged+=DrawRoof;
        vm.Refreshed += DrawRoof; DrawRoof();
        Closing += (_, e) =>
        {
            try { vm.ProtectUnsavedDraft(); }
            catch (Exception error) { e.Cancel = true; vm.Run(() => throw new InvalidOperationException("Close blocked: " + error.Message)); }
        };
    }
    public void AssertReadOnlyVisualIsolation()
    {
        if (vm.IsDemo || Tabs.SelectedItem is not TabItem tab || tab.Content is not DependencyObject page) return;
        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        var visible = Descendants(page).OfType<FrameworkElement>().Where(e => e.IsVisible).ToArray();
        if (tab.Header?.ToString() is "Project" or "Wind" or "Wall" or "Report" && visible.Any(e => e is TextBox or ComboBox or DataGrid or System.Windows.Controls.Button or CheckBox))
            throw new InvalidOperationException($"{tab.Header} exposed demo controls in Real Read-Only mode.");
        foreach (var grid in visible.OfType<DataGrid>())
        {
            var path = BindingOperations.GetBindingExpression(grid, ItemsControl.ItemsSourceProperty)?.ParentBinding.Path?.Path;
            if (path?.StartsWith("Session.", StringComparison.Ordinal) == true)
                throw new InvalidOperationException($"{tab.Header} exposed synthetic session data in Real Read-Only mode: {path}");
        }
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock Text(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(0, 6, 0, 6), TextWrapping = TextWrapping.Wrap };
    private static TextBlock BoundText(string path) { var t = Text(""); t.SetBinding(TextBlock.TextProperty, path); return t; }
    private static StackPanel Stack() => new() { Margin = new Thickness(14) };
    private void AddPage(string title, UIElement body)
    {
        var panel = new DockPanel();
        var banner = BoundText("ModeBanner");
        banner.Background = Brush("#FFF0C2"); banner.Padding = new Thickness(12); banner.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(banner, Dock.Top); panel.Children.Add(banner);
        if (title is "Project" or "Wind" or "Wall" or "Report")
        {
            var demo = new StackPanel(); demo.SetBinding(VisibilityProperty, new Binding("IsDemo") { Converter = new BooleanToVisibilityConverter() }); demo.Children.Add(body);
            var wrapper = new StackPanel(); var notice = BoundText("ManifestSummary"); notice.SetBinding(VisibilityProperty, new Binding("IsReal") { Converter = new BooleanToVisibilityConverter() }); wrapper.Children.Add(notice); wrapper.Children.Add(demo); body = wrapper;
        }
        panel.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Tabs.Items.Add(new TabItem { Header = title, Content = panel, Padding = new Thickness(18, 10, 18, 10) });
    }
    private Button Button(string label, Action action, bool demoOnly = true)
    {
        var button = new Button { Content = label, Command = vm.Command(action), Margin = new Thickness(0, 5, 9, 5), Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left };
        if (demoOnly) button.SetBinding(IsEnabledProperty, "IsDemo");
        return button;
    }
    private static Button Disabled(string label, string reason)
    {
        var b = new Button { Content = label, IsEnabled = false, ToolTip = reason, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 5, 9, 5) };
        ToolTipService.SetShowOnDisabled(b, true); return b;
    }
    private static WrapPanel Row(params UIElement[] children) { var row = new WrapPanel(); foreach (var c in children) row.Children.Add(c); return row; }
    private static TextBox Input(string path, double width = 280)
    {
        var t = new TextBox { Width = width, Margin = new Thickness(0, 5, 10, 5), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left };
        t.SetBinding(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return t;
    }
    private static ComboBox Combo(string selected, IEnumerable items, double width = 250)
    {
        var combo = new ComboBox { ItemsSource = items, Width = width, Margin = new Thickness(0, 5, 10, 5), Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left };
        combo.SetBinding(ComboBox.SelectedItemProperty, new Binding(selected) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return combo;
    }
    private static ComboBox BoundCombo(string selected, string items, bool demoOnly = false)
    {
        var combo = Combo(selected, Array.Empty<string>()); combo.SetBinding(ItemsControl.ItemsSourceProperty, items);
        if (demoOnly) combo.SetBinding(IsEnabledProperty, "IsDemo"); return combo;
    }
    private static DataGrid Table(string path, double height = 150)
    {
        var grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = true, Height = height, Margin = new Thickness(0, 5, 0, 10), CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column };
        grid.SetBinding(ItemsControl.ItemsSourceProperty, path); return grid;
    }
    private static CheckBox Check(string label, string path)
    {
        var c = new CheckBox { Content = label, Margin = new Thickness(0, 8, 16, 8) };
        c.SetBinding(CheckBox.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay }); return c;
    }
    private UIElement ProjectPage()
    {
        var p = Stack(); p.Children.Add(Text("Local project", 22)); p.Children.Add(BoundText("ProjectSummary"));
        p.Children.Add(Text("Project database path — create a new .db or open an existing snapshot store")); p.Children.Add(Input("DatabasePath", 760));
        p.Children.Add(Row(Button("Create / open local project", vm.OpenProject), Button("Browse existing database", () => {
            var d = new OpenFileDialog { Filter = "SQLite project (*.db)|*.db|All files|*.*" }; if (d.ShowDialog(this) == true) { vm.DatabasePath = d.FileName; vm.OpenProject(); }
        }), Button("New database…", () => {
            var d = new SaveFileDialog { Filter = "SQLite project (*.db)|*.db", FileName = "structural-demo.db" }; if (d.ShowDialog(this) == true) { vm.DatabasePath = d.FileName; vm.OpenProject(); }
        })));
        p.Children.Add(Text("Selected CSI model: FAKE CSI / fictional-building. Stored reports are immutable snapshots; opening a database reloads the synthetic workspace and exposes its stored report IDs."));
        p.Children.Add(Button("Load simulated model + crossing-shell fixture", () => vm.LoadFixture())); return p;
    }
    private UIElement WindPage()
    {
        var p = Stack(); p.Children.Add(Text("Wind calculation / standards gate", 22)); p.Children.Add(Combo("Edition", vm.Editions));
        p.Children.Add(Button("Load Demo Pressure Fixture", () => vm.LoadFixture())); p.Children.Add(BoundText("ManifestSummary"));
        p.Children.Add(Text("Engineering inputs (locked until verified datasets are installed): wind speed [m/s], terrain / exposure, ground datum [m], importance / risk, directionality, building geometry, enclosure / internal pressure."));
        p.Children.Add(Row(Button("Validate engineering input", vm.ValidateEngineeringInput), Button("Calculate", vm.CalculateEngineeringRun), Button("Save engineering run", vm.SaveEngineeringRun))); p.Children.Add(BoundText("CurrentEngineeringRunSummary"));
        p.Children.Add(Text("Source pressure profile — elevation [m], net pressure [Pa]")); p.Children.Add(Table("Session.Calculation.Profile"));
        p.Children.Add(Text("Calculation trace / provenance")); p.Children.Add(Table("Session.Calculation.Trace", 160)); return p;
    }
    private UIElement CsiPage()
    {
        var p = Stack(); p.Children.Add(Text("CSI connection / selected geometry inspection", 22));
        p.Children.Add(Row(Combo("Product", Enum.GetValues<StructuralLoadAssistant.Csi.Abstractions.CsiProduct>(), 180), Input("AssemblyPath", 650), Button("Browse installed API…", () => {
            var dialog = new OpenFileDialog { Filter = "CSI API DLL (*.dll)|*.dll", Title = "Choose installed ETABSv1.dll / SAP2000v1.dll" };
            var installed = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Computers and Structures");
            if (System.IO.Directory.Exists(installed)) dialog.InitialDirectory = installed;
            if (dialog.ShowDialog(this) == true) { vm.AssemblyPath = dialog.FileName; vm.Refresh(); }
        }, false)));
        p.Children.Add(Row(Text("Target PID:"), Input("SelectedProcessId", 110), Button("Scan running instances", vm.RefreshAvailableProcesses, false), Button("Verify independent benchmark", () => vm.RunBenchmarkQualification(), false)));
        p.Children.Add(Row(Button("Attach existing process (read-only)", () => _ = vm.RunAsync(vm.ConnectReal), false), Button("Refresh current CSI selection", () => _ = vm.RunAsync(vm.RefreshReal), false), Button("Disconnect read-only", vm.DisconnectReal, false), Button("Rescan demo fixture", () => vm.LoadFixture())));
        p.Children.Add(BoundText("CsiSummary"));
        p.Children.Add(Text("Issues / unsupported reads / return codes")); p.Children.Add(Table("ReadIssues", 95));
        p.Children.Add(Row(DefinitionList("PATTERNS", "Patterns"), DefinitionList("Analysis CASES", "Cases"), DefinitionList("COMBINATIONS", "Combinations")));
        p.Children.Add(Text("Stories [SI metres]")); p.Children.Add(Table("Stories", 80));
        p.Children.Add(Text("Selected frames — endpoint XYZ [m]")); p.Children.Add(Table("FrameRows", 105));
        p.Children.Add(Text("Selected shells — vertices XYZ [m], Local 3 axis, outward normal status")); p.Children.Add(Table("ShellRows", 110));
        p.Children.Add(Text("Existing assignments (read only; SI values)")); p.Children.Add(Table("ExistingAssignments", 95)); return p;
    }
    private static UIElement DefinitionList(string title, string path)
    {
        var panel = new StackPanel { Width = 350, Margin = new Thickness(0, 0, 12, 0) }; panel.Children.Add(Text(title));
        var list = new ListBox { Height = 75 }; list.SetBinding(ItemsControl.ItemsSourceProperty, path); panel.Children.Add(list); return panel;
    }
    private UIElement FacadePage()
    {
        var p = Stack(); p.Children.Add(Text("Facade wind distribution", 22));
        p.Children.Add(Combo("Method", Enum.GetValues<FacadeMethod>()));
        p.Children.Add(BoundText("FacadeSummary"));
        var lower = new ComboBox { Width = 230, Margin = new Thickness(0, 5, 10, 5) }; lower.SetBinding(ItemsControl.ItemsSourceProperty, "FrameIds"); lower.SetBinding(ComboBox.SelectedItemProperty, new Binding("LowerFrame") { Mode = BindingMode.TwoWay });
        var upper = new ComboBox { Width = 230, Margin = new Thickness(0, 5, 10, 5) }; upper.SetBinding(ItemsControl.ItemsSourceProperty, "FrameIds"); upper.SetBinding(ComboBox.SelectedItemProperty, new Binding("UpperFrame") { Mode = BindingMode.TwoWay });
        p.Children.Add(Text("Explicit lower / upper selected frames — geometry overlap review"));
        p.Children.Add(Row(lower, upper, Button("Review selected pair geometry", vm.SelectFacadePair, false), Button("Clear pairs", vm.ClearFacadePairs, false)));
        p.Children.Add(Text("Real models: explicit physical facade grouping requires reviewer and reason; projected overlap is still validated."));
        p.Children.Add(Row(Input("FacadeGroup", 210), Input("User", 150), Input("Reason", 330), Button("Review group for lower + upper", vm.ReviewFacadeGroup, false)));
        p.Children.Add(Table("FacadePairRows", 105));
        p.Children.Add(Text("Physical facade grouping review history")); p.Children.Add(Table("FacadeGroupReviews", 80));
        p.Children.Add(Row(Button("Preview fake frame loads", () => _ = vm.PreviewFacade()), Button("Approve facade simulation", () => _ = vm.ApproveFacade()), Button("Simulate facade apply", () => _ = vm.ApplyFacade()), Button("Verify facade readback", () => _ = vm.VerifyFacade())));
        var demo = Stack(); demo.SetBinding(VisibilityProperty, new Binding("IsDemo") { Converter = new BooleanToVisibilityConverter() });
        demo.Children.Add(Text("Selected demo pairs: B1 → B2 (3 → 6 m, H=3 m), B2 → B3 (6 → 9 m, H=3 m).\nSource p_net: 1200 → 1600 → 1800 Pa. EqualSplit halves each panel resultant; StaticallyEquivalent preserves resultant and moment.\nB-invalid has no projected overlap and requires manual mapping; it is excluded from automatic proposals."));
        demo.Children.Add(Text("Individual panel contributions — StartM / EndM [m], LineLoadNPerM [N/m]")); demo.Children.Add(Table("Session.PanelRows", 190));
        demo.Children.Add(Text("Cumulative frame schedule — middle beam accumulates both adjacent panels")); demo.Children.Add(Table("Session.FacadeRows", 180));
        demo.Children.Add(Text("Global frame vector proposals [N/m] — outward-normal conversion")); demo.Children.Add(Table("Session.FacadePreview.FrameLoads", 130)); p.Children.Add(demo); return p;
    }
    private UIElement RoofPage()
    {
        var p = Stack(); p.Children.Add(Text("Roof wind mapper / projected XY & 3D geometry", 22)); p.Children.Add(BoundText("RoofSummary"));
        p.Children.Add(BoundText("ActiveWindStateSummary"));
        p.Children.Add(Row(
            Combo("Direction", Enum.GetValues<WindDirection>(), 150),
            Combo("InternalPressure", new[] { "IP_POS", "IP_NEG" }, 125),
            Combo("RoofCase", new[] { "RoofCase1", "RoofCase2" }, 125),
            Button("Switch wind state & restore draft", vm.LoadSelectedState, false),
            Button("Save Geometry Draft", vm.SaveCurrentDraft, false)));
        p.Children.Add(BoundText("RoofLegend"));
        p.Children.Add(Row(Button("Clean fixture", () => vm.LoadFixture(false)), Button("Validate Mapping", vm.Validate), Button("Approve Mapping", vm.Approve), Button("Preview loads", vm.Preview), Button("Simulate Apply to Fake CSI", () => _ = vm.Apply()), Button("Verify Fake Readback", () => _ = vm.Verify())));
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var viewTabs = new TabControl { Width = 550, Height = 460 };
        var tab2D = new TabItem { Header = "2D Plan View", Content = roof };
        var tab3D = new TabItem { Header = "3D Roof Orbit", Content = roof3D };
        viewTabs.Items.Add(tab2D); viewTabs.Items.Add(tab3D);
        p.Children.Add(new TextBlock{Text="Interactive surface geometry editor • no pressures or CSI mutations",FontSize=18,Margin=new Thickness(0,12,0,4)});
        var editorTabs=new TabControl();editorTabs.Items.Add(new TabItem{Header="Surface UV Zone Polygons",Content=zoneEditor});p.Children.Add(editorTabs);
        var viewPanel = new StackPanel();
        viewPanel.Children.Add(viewTabs);
        var viewControls = Row(
            Button("Reset 3D Camera", () => roof3D.ResetCamera(), false),
            Check("Show Local 3 Arrows (3D)", "ShowLocal3Arrows"));
        viewPanel.Children.Add(viewControls);
        grid.Children.Add(viewPanel); var inspector = new StackPanel { Margin = new Thickness(16, 0, 0, 0) }; Grid.SetColumn(inspector, 1); grid.Children.Add(inspector);
        inspector.Children.Add(BoundText("ShellSummary")); inspector.Children.Add(Text("Geometry zone label (unverified)"));
        var geometryZone = BoundCombo("SelectedZone", "ZoneIds");
        geometryZone.SetBinding(IsEnabledProperty, "CanEditGeometry");
        inspector.Children.Add(geometryZone); inspector.Children.Add(Button("Assign geometry zone label", vm.EditZone, false));
        inspector.Children.Add(BoundText("SelectedExcluded"));
        inspector.Children.Add(Row(Button("Validate geometry draft", vm.ValidateGeometryDraft, false), Button("Complete geometry review", vm.ReviewGeometryDraft, false)));
        inspector.Children.Add(Row(Combo("SelectedSign", Enum.GetValues<ManualLocal3Direction>(), 185), Check("Acknowledge physical sign mismatch", "AcknowledgeMismatch")));
        inspector.Children.Add(Text("Reviewer / engineering reason (required for changes)")); inspector.Children.Add(Row(Input("User", 150), Input("Reason", 290))); inspector.Children.Add(Button("Review Local 3 sign (no real loads)", vm.ReviewLocal3, false));
        inspector.Children.Add(Row(BoundCombo("SelectedPattern", "Patterns", true), Button("Set load pattern", vm.Pattern))); inspector.Children.Add(Button("Exclude selected shell with reason", vm.Exclude, false)); p.Children.Add(grid);
        p.Children.Add(Text("Selected shell existing assignments (read only)")); p.Children.Add(Table("SelectedShellAssignments", 95));
        p.Children.Add(Text("Shell pressure mapping / Local 3 review metadata")); p.Children.Add(Table("RoofReviewRows", 100));
        p.Children.Add(Text("Local 3 review audit — no generated loads")); p.Children.Add(Table("Local3Reviews", 90));
        p.Children.Add(Text("Validation issues — blockers must be resolved before approval")); p.Children.Add(Table("Issues", 115)); p.Children.Add(Text("Assignment preview — p_net [Pa] versus signed Local 3 [Pa]")); p.Children.Add(Table("ProposedLoads", 125)); return p;
    }
    private void DrawRoof()
    {
        roof.Children.Clear();
        roof3D.ShowLocal3Arrows = vm.ShowLocal3Arrows;
        roof3D.UpdateGeometry(vm.DisplayShells3D, vm.SelectedShell, vm.DisplayShellZones,zoneEditor.SelectedShellIds);
        roof3D.UpdateZoneGeometry(vm.ZoneEditor.Zones,vm.ZoneEditor.Planes,zoneEditor.SelectedZoneId);
        if (!vm.IsDemo) { DrawRealRoof(); return; }
        foreach (var zone in vm.Session.Zones)
        {
            var boundary = new Polygon { Points = new PointCollection(zone.Vertices.Select(v => new Point(45 + v.X * 43, 385 - v.Y * 29))), Stroke = Brushes.SlateGray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 }, Fill = Brush(zone.Id == "Edge" ? "#E1F4EF" : "#E9EFFB"), IsHitTestVisible = false };
            roof.Children.Add(boundary);
            var zoneLabel = Text($"{zone.Id}: {zone.NetPressurePa:G6} Pa", 12); zoneLabel.IsHitTestVisible = false;
            Canvas.SetLeft(zoneLabel, 50 + zone.Vertices.Min(v => v.X) * 43); Canvas.SetTop(zoneLabel, 28); roof.Children.Add(zoneLabel);
        }
        foreach (var shell in vm.Session.Model.Shells)
        {
            var map = vm.Session.Roof.Mappings.FirstOrDefault(m => m.ShellId == shell.Id);
            var polygon = new Polygon { Points = new PointCollection(shell.Vertices.Select(v => new Point(45 + v.X * 43, 385 - v.Y * 29))), Stroke = shell.Id == vm.SelectedShell ? Brushes.Black : Brushes.SlateGray, StrokeThickness = shell.Id == vm.SelectedShell ? 4 : 2, Fill = Brush(map?.ZoneId == "Edge" ? "#79C9BC" : map?.ZoneId == "Interior" ? "#A7BCE9" : "#F4BD7C"), ToolTip = $"{shell.Id} | {map?.ZoneId ?? "unmapped"} | {map?.Classification}" };
            polygon.MouseLeftButtonDown += (_, _) => vm.SelectShell(shell.Id); roof.Children.Add(polygon);
            var label = Text($"{shell.Id} · {map?.ZoneId ?? "unmapped"}"); label.IsHitTestVisible = false; label.FontWeight = FontWeights.Bold;
            Canvas.SetLeft(label, 48 + shell.Vertices.Average(v => v.X) * 43); Canvas.SetTop(label, 365 - shell.Vertices.Average(v => v.Y) * 29); roof.Children.Add(label);
        }
        var axes = Text("XY projection [m] · +Y ↑ · +X →"); Canvas.SetLeft(axes, 14); Canvas.SetTop(axes, 2); roof.Children.Add(axes);
    }
    private void DrawRealRoof()
    {
        var vertices = vm.DisplayShells.SelectMany(s => s.Vertices).ToArray();
        if (vertices.Length == 0) { roof.Children.Add(Text("No selected CSI areas. Select in CSI and refresh.")); return; }
        var minX = vertices.Min(v => v.X); var maxY = vertices.Max(v => v.Y);
        var scale = Math.Min(460 / Math.Max(.01, vertices.Max(v => v.X) - minX), 330 / Math.Max(.01, maxY - vertices.Min(v => v.Y)));
        Point Project(Vec3 v) => new(35 + (v.X - minX) * scale, 55 + (maxY - v.Y) * scale);
        foreach (var shell in vm.DisplayShells.Where(s => s.Vertices.Length >= 3))
        {
            var zone = vm.DisplayShellZones.TryGetValue(shell.Id, out var z) ? z : "Unmapped";
            var fillBrush = zone.Contains("Edge", StringComparison.OrdinalIgnoreCase) ? Brush("#79C9BC")
                : zone.Contains("Interior", StringComparison.OrdinalIgnoreCase) || zone.Contains("Field", StringComparison.OrdinalIgnoreCase) ? Brush("#A7BCE9")
                : zone.Contains("Excluded", StringComparison.OrdinalIgnoreCase) ? Brushes.LightGray
                : zone.Contains("Cross", StringComparison.OrdinalIgnoreCase) ? Brush("#E2847A")
                : Brush("#F4BD7C");
            var polygon = new Polygon { Points = new PointCollection(shell.Vertices.Select(Project)), Stroke = shell.Id == vm.SelectedShell ? Brushes.Black : Brushes.SlateGray, StrokeThickness = shell.Id == vm.SelectedShell ? 4 : 2, Fill = fillBrush, ToolTip = $"{shell.Id} | {zone} | no normative pressure data" };
            polygon.MouseLeftButtonDown += (_, _) => vm.SelectShell(shell.Id); roof.Children.Add(polygon);
            var label = Text($"{shell.Id} · {zone}", 12); label.IsHitTestVisible = false;
            Canvas.SetLeft(label, shell.Vertices.Average(v => Project(v).X)); Canvas.SetTop(label, shell.Vertices.Average(v => Project(v).Y)); roof.Children.Add(label);
        }
        var axes = Text("CSI selected polygons · XY [m] · +Y ↑ · +X →"); Canvas.SetLeft(axes, 12); roof.Children.Add(axes);
    }
    private UIElement WallPage()
    {
        var p = Stack(); p.Children.Add(Text("Independent wall gravity loads", 22)); var type = Combo("SelectedWall", vm.WallTypes, 450); type.DisplayMemberPath = "Id"; p.Children.Add(type);
        p.Children.Add(Text("Wall height [m] — independent of wind tributary height")); p.Children.Add(Input("WallHeight", 140));
        p.Children.Add(Text("Optional direct override [N/m]; blank uses (thickness × unit weight + finishes) × height")); p.Children.Add(Input("WallOverride", 200));
        p.Children.Add(Button("Preview wall loads on demo frames / WALL pattern", vm.PreviewWall)); p.Children.Add(Text("Target: B1 / B2 / B3, pattern WALL, global -Z. Material library is illustrative and unverified.")); p.Children.Add(Table("Session.Walls", 240)); return p;
    }
    private UIElement ReportPage()
    {
        var p = Stack(); p.Children.Add(Text("Report builder / immutable stored snapshots", 22));
        p.Children.Add(Text("Source: current synthetic calculation; current optional simulated assignment and readback are included automatically.")); p.Children.Add(BoundText("CurrentEngineeringRunSummary"));
        p.Children.Add(Row(Check("Facade", "IncludeFacade"), Check("Roof", "IncludeRoof"), Check("Wall", "IncludeWall")));
        p.Children.Add(Row(Button("Preview current report", vm.PreviewReport), Button("Save immutable report", vm.SaveReport), Button("Export XLSX / PDF / HTML / MD / JSON", () => {
            var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx|PDF (*.pdf)|*.pdf|HTML (*.html)|*.html|Markdown (*.md)|*.md|JSON (*.json)|*.json", FileName = "wind-load-audit.xlsx" };
            if (dialog.ShowDialog(this) == true) vm.ExportDetailedReport(dialog.FileName);
        })));
        var stored = new ComboBox { Width = 470, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 10, 5) }; stored.SetBinding(ItemsControl.ItemsSourceProperty, "ReportIds"); stored.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedReport") { Mode = BindingMode.TwoWay });
        p.Children.Add(Row(stored, Button("Open stored report ID", vm.OpenReport)));
        var preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(12) }; preview.SetBinding(TextBox.TextProperty, new Binding("ReportText") { Mode = BindingMode.OneWay }); p.Children.Add(preview); return p;
    }
}
