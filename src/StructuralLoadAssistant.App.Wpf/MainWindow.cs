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

public sealed class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly Canvas roof = new() { Width = 540, Height = 420, Background = Brushes.WhiteSmoke };
    public TabControl Tabs { get; } = new();
    public MainWindow(MainViewModel viewModel)
    {
        vm = viewModel; DataContext = vm;
        Title = "Structural Load Assistant | DEMO MODE"; Width = 1380; Height = 920;
        MinWidth = 1000; MinHeight = 700; Background = Brush("#EEF2F6");
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = Text("STRUCTURAL LOAD ASSISTANT", 25); header.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var status = BoundText("Status"); status.Padding = new Thickness(10); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        root.Children.Add(Tabs); Content = root;
        AddPage("Project", ProjectPage()); AddPage("Wind", WindPage()); AddPage("CSI", CsiPage());
        AddPage("Facade", FacadePage()); AddPage("Roof", RoofPage()); AddPage("Wall", WallPage()); AddPage("Report", ReportPage());
        vm.Refreshed += DrawRoof; DrawRoof();
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock Text(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(0, 6, 0, 6), TextWrapping = TextWrapping.Wrap };
    private static TextBlock BoundText(string path) { var t = Text(""); t.SetBinding(TextBlock.TextProperty, path); return t; }
    private static StackPanel Stack() => new() { Margin = new Thickness(14) };
    private void AddPage(string title, UIElement body)
    {
        var panel = new DockPanel();
        var banner = Text("DEMO MODE  •  SYNTHETIC WIND PRESSURES  •  NOT FOR ENGINEERING DESIGN  •  REAL CSI WRITES DISABLED");
        banner.Background = Brush("#FFF0C2"); banner.Padding = new Thickness(12); banner.FontWeight = FontWeights.SemiBold;
        DockPanel.SetDock(banner, Dock.Top); panel.Children.Add(banner);
        panel.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Tabs.Items.Add(new TabItem { Header = title, Content = panel, Padding = new Thickness(18, 10, 18, 10) });
    }
    private Button Button(string label, Action action)
    {
        return new Button { Content = label, Command = vm.Command(action), Margin = new Thickness(0, 5, 9, 5), Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left };
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
        p.Children.Add(Row(Disabled("Validate engineering input", "Verified licensed standards datasets and typed calculation contract are unavailable."), Disabled("Calculate", "Synthetic fixtures are available; normative coefficients are missing."), Disabled("Save engineering run", "No verified engineering calculation exists.")));
        p.Children.Add(Text("Source pressure profile — elevation [m], net pressure [Pa]")); p.Children.Add(Table("Session.Calculation.Profile"));
        p.Children.Add(Text("Calculation trace / provenance")); p.Children.Add(Table("Session.Calculation.Trace", 160)); return p;
    }
    private UIElement CsiPage()
    {
        var p = Stack(); p.Children.Add(Text("CSI model / fake adapter", 22)); p.Children.Add(BoundText("CsiSummary"));
        p.Children.Add(Row(Button("Rescan demo fixture (resets mapping)", () => vm.LoadFixture()), Disabled("Connect ETABS / SAP2000", "Real CSI connection not enabled. No live writer implementation.")));
        p.Children.Add(Text("Existing load PATTERNS (assignment targets)")); var patterns = new ListBox { Height = 100 }; patterns.SetBinding(ItemsControl.ItemsSourceProperty, "Session.Model.Patterns"); p.Children.Add(patterns);
        p.Children.Add(Text("Analysis cases — never used as patterns")); var cases = new ListBox { Height = 60 }; cases.SetBinding(ItemsControl.ItemsSourceProperty, "Session.Model.Cases"); p.Children.Add(cases);
        p.Children.Add(Text("Combinations — never used as patterns")); var combos = new ListBox { Height = 60 }; combos.SetBinding(ItemsControl.ItemsSourceProperty, "Session.Model.Combinations"); p.Children.Add(combos);
        p.Children.Add(Text("Scanned frames / roof shells")); p.Children.Add(Table("Session.Model.Frames", 110)); p.Children.Add(Table("Session.Model.Shells", 110)); return p;
    }
    private UIElement FacadePage()
    {
        var p = Stack(); p.Children.Add(Text("Facade wind distribution", 22));
        p.Children.Add(Combo("Method", Enum.GetValues<FacadeMethod>()));
        p.Children.Add(BoundText("FacadeSummary"));
        p.Children.Add(Row(Button("Preview fake frame loads", () => _ = vm.PreviewFacade()), Button("Approve facade simulation", () => _ = vm.ApproveFacade()), Button("Simulate facade apply", () => _ = vm.ApplyFacade()), Button("Verify facade readback", () => _ = vm.VerifyFacade())));
        p.Children.Add(Text("Selected demo pairs: B1 → B2 (3 → 6 m, H=3 m), B2 → B3 (6 → 9 m, H=3 m).\nSource p_net: 1200 → 1600 → 1800 Pa. EqualSplit halves each panel resultant; StaticallyEquivalent preserves resultant and moment.\nB-invalid has no projected overlap and requires manual mapping; it is excluded from automatic proposals."));
        p.Children.Add(Text("Individual panel contributions — StartM / EndM [m], LineLoadNPerM [N/m]")); p.Children.Add(Table("Session.PanelRows", 190));
        p.Children.Add(Text("Cumulative frame schedule — middle beam accumulates both adjacent panels")); p.Children.Add(Table("Session.FacadeRows", 180));
        p.Children.Add(Text("Global frame vector proposals [N/m] — outward-normal conversion")); p.Children.Add(Table("Session.FacadePreview.FrameLoads", 130)); return p;
    }
    private UIElement RoofPage()
    {
        var p = Stack(); p.Children.Add(Text("Roof wind mapper / projected XY geometry", 22)); p.Children.Add(BoundText("RoofSummary"));
        p.Children.Add(Row(Combo("Direction", Enum.GetValues<WindDirection>(), 150), Combo("InternalPressure", new[] { "IP_POS", "IP_NEG" }, 125), Combo("RoofCase", new[] { "RoofCase1", "RoofCase2" }, 125), Button("Load selected independent state (resets approval)", vm.LoadSelectedState)));
        p.Children.Add(Text("Green = Edge; blue = Interior; amber = crossing/unmapped. Click a polygon to inspect. Each state reload resets mapping, signs and approval; pressure and Local 3 are independent quantities."));
        p.Children.Add(Row(Button("Clean fixture", () => vm.LoadFixture(false)), Button("Validate Mapping", vm.Validate), Button("Approve Mapping", vm.Approve), Button("Preview loads", vm.Preview), Button("Simulate Apply to Fake CSI", () => _ = vm.Apply()), Button("Verify Fake Readback", () => _ = vm.Verify())));
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(roof); var inspector = new StackPanel { Margin = new Thickness(16, 0, 0, 0) }; Grid.SetColumn(inspector, 1); grid.Children.Add(inspector);
        inspector.Children.Add(BoundText("ShellSummary")); inspector.Children.Add(Text("Pressure zone")); inspector.Children.Add(Combo("SelectedZone", vm.Session.Zones.Select(z => z.Id).Distinct().ToArray())); inspector.Children.Add(Button("Set zone / invalidate approval", vm.EditZone));
        inspector.Children.Add(Row(Combo("SelectedSign", Enum.GetValues<ManualLocal3Direction>(), 185), Check("Acknowledge physical sign mismatch", "AcknowledgeMismatch")));
        inspector.Children.Add(Text("Reviewer / engineering reason (required for changes)")); inspector.Children.Add(Row(Input("User", 150), Input("Reason", 290))); inspector.Children.Add(Button("Set Local 3 sign / invalidate approval", vm.EditSign));
        inspector.Children.Add(Row(Combo("SelectedPattern", vm.Session.Model.Patterns, 240), Button("Set load pattern", vm.Pattern))); inspector.Children.Add(Button("Exclude selected shell with reason", vm.Exclude)); p.Children.Add(grid);
        p.Children.Add(Text("Validation issues — blockers must be resolved before approval")); p.Children.Add(Table("Issues", 115)); p.Children.Add(Text("Assignment preview — p_net [Pa] versus signed Local 3 [Pa]")); p.Children.Add(Table("ProposedLoads", 125)); return p;
    }
    private void DrawRoof()
    {
        roof.Children.Clear();
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
        p.Children.Add(Text("Source: current synthetic calculation; current optional simulated assignment and readback are included automatically."));
        p.Children.Add(Row(Check("Facade", "IncludeFacade"), Check("Roof", "IncludeRoof"), Check("Wall", "IncludeWall")));
        p.Children.Add(Row(Button("Preview current report", vm.PreviewReport), Button("Save immutable report", vm.SaveReport), Button("Export Markdown / HTML", () => {
            var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md|HTML (*.html)|*.html", FileName = "structural-demo.md" };
            if (dialog.ShowDialog(this) == true) vm.ExportReport(dialog.FileName);
        })));
        var stored = new ComboBox { Width = 470, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 10, 5) }; stored.SetBinding(ItemsControl.ItemsSourceProperty, "ReportIds"); stored.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedReport") { Mode = BindingMode.TwoWay });
        p.Children.Add(Row(stored, Button("Open stored report ID", vm.OpenReport)));
        var preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(12) }; preview.SetBinding(TextBox.TextProperty, new Binding("ReportText") { Mode = BindingMode.OneWay }); p.Children.Add(preview); return p;
    }
}
