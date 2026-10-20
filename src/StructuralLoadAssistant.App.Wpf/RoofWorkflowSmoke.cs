using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.App.Wpf;

public sealed partial class MainWindow
{
    // Operates the actual bound controls and canvas event handlers on the WPF dispatcher.
    public async Task RunRoofWorkflowSmokeAsync(bool reopen, Action changeMockGeometry)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Roof control regression: " + message);
        }
        IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var descendant in Walk(child)) yield return descendant;
        }
        T Bound<T>(string path) where T : FrameworkElement => Walk(this).OfType<T>().Single(c =>
            BindingOperations.GetBinding(c, c is ComboBox ? ComboBox.SelectedItemProperty : TextBox.TextProperty)?.Path?.Path == path);
        void Click(string label)
        {
            var button = Walk(this).OfType<Button>().Single(b => Equals(b.Content, label));
            Check(button.IsEnabled && button.Command?.CanExecute(null) == true, label + " disabled");
            button.Command!.Execute(null);
        }
        async Task Idle() { await Dispatcher.InvokeAsync(() => { UpdateLayout(); }, DispatcherPriority.ApplicationIdle); }
        async Task State(WindDirection direction, string pressure, string roofCase)
        {
            Bound<ComboBox>("Direction").SelectedItem = direction;
            Bound<ComboBox>("InternalPressure").SelectedItem = pressure;
            Bound<ComboBox>("RoofCase").SelectedItem = roofCase;
            Click("Switch wind state & restore draft"); await Idle();
        }
        void Shell(string id)
        {
            var polygon = roof.Children.OfType<Polygon>().Single(p => p.ToolTip?.ToString()?.StartsWith(id + " |", StringComparison.Ordinal) == true);
            polygon.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent });
            Check(vm.SelectedShell == id, "2D shell selection handler");
        }
        void Zone(string value)
        {
            var combo = Bound<ComboBox>("SelectedZone");
            Check(combo.IsEnabled, "zone dropdown must be enabled for valid read-only geometry");
            combo.SelectedItem = value; Click("Assign geometry zone label");
        }
        async Task Shell3D(string id)
        {
            var views = (TabControl)((TabItem)LogicalTreeHelper.GetParent(roof3D)).Parent;
            views.SelectedIndex = 1; await Idle();
            var found = false;
            for (var y = 12; y < roof3D.ActualHeight && !found; y += 8)
                for (var x = 12; x < roof3D.ActualWidth && !found; x += 8)
                    found = roof3D.SelectShellAt(new Point(x, y)) == id;
            Check(found && vm.SelectedShell == id, "3D ray hit must select shell " + id);
            views.SelectedIndex = 0; await Idle();
        }
        void Sign(ManualLocal3Direction direction)
        {
            Bound<ComboBox>("SelectedSign").SelectedItem = direction;
            Click("Review Local 3 sign (no real loads)");
        }
        void Mapping(string zone, ManualLocal3Direction sign)
        {
            Check(vm.SelectedZone == zone && vm.SelectedSign == sign, "independent zone/sign restoration");
            Check(vm.ShellSummary.StartsWith(vm.SelectedShell + " |", StringComparison.Ordinal), "shell metadata synchronization");
        }

        Bound<ComboBox>("Mode").SelectedItem = MainViewModel.WorkspaceMode.RealReadOnly;
        Tabs.SelectedIndex = 2; await Idle();
        Click("Attach existing process (read-only)");
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (vm.DisplayShells.Count == 0 && DateTime.UtcNow < deadline) { await Task.Delay(20); await Idle(); }
        Check(vm.IsMockAdapter && vm.DisplayShells.Count == 2, "mock attachment and selection");
        Tabs.SelectedIndex = 4; await Idle();
        if (reopen)
        {
            Shell("MOCK-A2");
            Check(vm.SelectedExcluded && vm.SelectedZone == "Excluded", "closing must save pending exclusions before process exit");
        }
        Shell("MOCK-A1");
        if (reopen) Mapping("Edge", ManualLocal3Direction.Positive);
        else
        {
            Zone("Edge"); Sign(ManualLocal3Direction.Positive);
            await Shell3D("MOCK-A2"); Mapping("Unmapped", ManualLocal3Direction.Unacknowledged);
            Check(!vm.SelectedExcluded, "shell B exclusion must be independent");
            await Shell3D("MOCK-A1"); Mapping("Edge", ManualLocal3Direction.Positive);
            Click("Validate geometry draft");
            Check(vm.SelectedGeometryReviewStatus == GeometryReviewStatus.GeometryValidated, "explicit geometry validation");
            Click("Complete geometry review");
            Check(vm.SelectedGeometryReviewStatus == GeometryReviewStatus.GeometryReviewed, "separate geometry review");
            Zone("Edge");
            Check(vm.SelectedGeometryReviewStatus == GeometryReviewStatus.GeometryDraft, "editing invalidates review");
            Click("Save Geometry Draft");
            Bound<ComboBox>("Direction").SelectedItem = WindDirection.NegativeY;
            Bound<ComboBox>("InternalPressure").SelectedItem = "IP_NEG";
            Bound<ComboBox>("RoofCase").SelectedItem = "RoofCase2";
            Click("Save Geometry Draft");
            Check(vm.ActiveWindState.Direction == WindDirection.PositiveX, "pending dropdown must not change saved state");
        }
        await State(WindDirection.NegativeY, "IP_NEG", "RoofCase2");
        if (reopen) Mapping("Corner", ManualLocal3Direction.Negative);
        else
        {
            Mapping("Unmapped", ManualLocal3Direction.Unacknowledged);
            Zone("Corner"); Sign(ManualLocal3Direction.Negative); Click("Save Geometry Draft");
        }
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await State(WindDirection.PositiveX, "IP_POS", "RoofCase1"); Mapping("Edge", ManualLocal3Direction.Positive);
            await State(WindDirection.NegativeY, "IP_NEG", "RoofCase2"); Mapping("Corner", ManualLocal3Direction.Negative);
        }
        for (var revision = 0; revision < 12; revision++) Click("Save Geometry Draft");
        Zone("Corner"); // Leave an applied edit dirty for the refresh guard.
        Tabs.SelectedIndex = 2; await Idle(); Click("Refresh current CSI selection"); await Idle();
        Tabs.SelectedIndex = 4; await Idle(); Mapping("Corner", ManualLocal3Direction.Negative);
        Check(!vm.HasUnsavedChanges, "refresh saves outstanding edits");
        foreach (var label in new[] { "Approve Mapping", "Preview loads", "Simulate Apply to Fake CSI", "Validate Mapping" })
            Check(!Walk(this).OfType<Button>().Single(b => Equals(b.Content, label)).IsEnabled, label + " must stay disabled");
        Check(vm.Session.Assignment is null && vm.Session.Roof.Approval is null, "no hidden demo approval or assignment");

        // Leave WX+ active so the next OS process must restore it on reconnect.
        await State(WindDirection.PositiveX, "IP_POS", "RoofCase1");
        if (!reopen)
        {
            Zone("Edge");
            Tabs.SelectedIndex = 2; await Idle(); Click("Disconnect read-only");
            Check(!vm.HasUnsavedChanges && vm.DisplayShells.Count == 0, "disconnect saves before clearing");
            Click("Attach existing process (read-only)"); await Idle();
            Tabs.SelectedIndex = 4; await Idle(); Mapping("Edge", ManualLocal3Direction.Positive);
            Zone("Edge");
            Bound<ComboBox>("Mode").SelectedItem = MainViewModel.WorkspaceMode.Demo;
            Check(vm.IsDemo && !vm.HasUnsavedChanges, "mode switching saves outstanding geometry edits");
            Bound<ComboBox>("Mode").SelectedItem = MainViewModel.WorkspaceMode.RealReadOnly;
            Tabs.SelectedIndex = 2; await Idle(); Click("Attach existing process (read-only)"); await Idle();
            Tabs.SelectedIndex = 4; await Idle(); Mapping("Edge", ManualLocal3Direction.Positive);
            Shell("MOCK-A2"); Click("Exclude selected shell with reason");
            Check(vm.SelectedExcluded && vm.HasUnsavedChanges, "leave pending exclusion for actual window close");
            // A changed database destination must block destructive lifecycle actions.
            var originalDatabase = vm.DatabasePath;
            Bound<TextBox>("DatabasePath").Text = originalDatabase + ".different.db";
            Tabs.SelectedIndex = 2; await Idle();
            Click("Refresh current CSI selection"); await Idle();
            Check(vm.HasUnsavedChanges && vm.SelectedExcluded, "failed refresh save retains edits");
            Click("Disconnect read-only");
            Check(vm.HasUnsavedChanges && vm.DisplayShells.Count == 2, "failed disconnect save retains connection and edits");
            Close();
            Check(IsVisible && vm.HasUnsavedChanges, "failed close save must cancel window close");
            Bound<TextBox>("DatabasePath").Text = originalDatabase;
        }
        if (reopen)
        {
            changeMockGeometry();
            Tabs.SelectedIndex = 2; await Idle(); Click("Refresh current CSI selection"); await Idle();
            Check(vm.SelectedGeometryReviewStatus == GeometryReviewStatus.StaleNeedsRescan, "changed geometry must be stale");
            Check(!vm.CanEditGeometry && vm.DisplayShellZones.Values.All(z => z == "Unmapped"), "stale mappings cannot be silently reused");
        }
    }
}
