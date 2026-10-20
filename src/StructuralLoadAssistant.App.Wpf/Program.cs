using System.IO;
using System.Collections.Immutable;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.ApiV1;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.App.Wpf;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var workflowSmoke = args.Contains("--smoke-roof-workflow", StringComparer.Ordinal);
        var readOnlySmoke = workflowSmoke || args.Contains("--smoke-readonly", StringComparer.Ordinal);
        var smoke = readOnlySmoke || args.Contains("--smoke", StringComparer.Ordinal);
        string? Option(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        var output = Path.GetFullPath(Option("--smoke-output") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StructuralLoadAssistant", "smoke"));
        var database = Option("--database") ?? Path.Combine(smoke ? output : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StructuralLoadAssistant"), "demo.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(database))!);
        if (smoke) Directory.CreateDirectory(output);
        var services = new ServiceCollection();
        ICsiSessionReadAdapter readAdapter = readOnlySmoke ? new MockInspectionAdapter() : new InstalledCsiReadAdapter();
        var inspection = new CsiInspectionSession(readAdapter);
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton(inspection);
        services.AddSingleton(new DemoSession(database)); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Desktop");
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var exitCode = 0;
        app.DispatcherUnhandledException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unhandled desktop error"); e.Handled = true;
            if (smoke) { exitCode = 1; File.WriteAllText(Path.Combine(output, "dispatcher-error.txt"), e.Exception.ToString()); app.Shutdown(exitCode); }
            else MessageBox.Show(e.Exception.Message, "Structural Load Assistant");
        };
        try
        {
            var window = provider.GetRequiredService<MainWindow>();
            if (smoke) window.Loaded += async (_, _) =>
            {
                try
                {
                    var vm = provider.GetRequiredService<MainViewModel>();
                    if (readOnlySmoke)
                    {
                        if (workflowSmoke) await window.RunRoofWorkflowSmokeAsync(args.Contains("--reopen"), () => ((MockInspectionAdapter)readAdapter).ChangedGeometry = true);
                        else await vm.SmokeReadOnlyAsync();
                    }
                    else await vm.SmokeAsync();
                    void Capture(string fileName)
                    {
                        window.UpdateLayout();
                        window.AssertReadOnlyVisualIsolation();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output, fileName)); encoder.Save(stream);
                    }
                    for (var i = 0; i < window.Tabs.Items.Count; i++)
                    {
                        window.Tabs.SelectedIndex = i;
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        var tab = (System.Windows.Controls.TabItem)window.Tabs.Items[i];
                        Capture($"{i + 1:00}-{tab.Header}.png");
                        if (tab.Header?.ToString() is "CSI" or "Roof" && tab.Content is System.Windows.Controls.DockPanel page)
                        {
                            var scroll = page.Children.OfType<System.Windows.Controls.ScrollViewer>().Single();
                            scroll.ScrollToBottom();
                            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                            Capture($"{i + 1:00}-{tab.Header}-detail.png"); scroll.ScrollToTop();
                        }
                    }
                    if (!readOnlySmoke) vm.ExportReport(Path.Combine(output, "desktop-report.html"));
                    await File.WriteAllTextAsync(Path.Combine(output, "smoke-summary.json"), JsonSerializer.Serialize(new { Success = true, Mode = vm.Mode.ToString(), Source = readOnlySmoke ? "MOCK LIVE ADAPTER / deterministic contract-test data / NOT actual CSI" : "SyntheticDemoFixture", RenderedTabs = window.Tabs.Items.Count, vm.Session.Project.Id, State = vm.Session.Roof.State.ToString(), Readback = vm.Session.Readback?.Matches, FacadeReadback = vm.Session.FacadeReadback?.Matches, Assignment = vm.Session.Assignment?.Id, StoredReports = vm.ReportIds.Count, Database = database }, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception error)
                {
                    exitCode = 1; logger.LogError(error, "Smoke failed"); await File.WriteAllTextAsync(Path.Combine(output, "smoke-error.txt"), error.ToString());
                }
                finally { window.Close(); app.Shutdown(exitCode); }
            };
            logger.LogInformation("Starting desktop in simulation mode"); app.Run(window);
            if (smoke) File.WriteAllText(Path.Combine(output, "shutdown-complete.txt"), "WPF dispatcher shutdown completed.");
        }
        catch (Exception error)
        {
            logger.LogError(error, "Desktop startup failed");
            if (smoke) File.WriteAllText(Path.Combine(output, "smoke-error.txt"), error.ToString());
            else MessageBox.Show(error.Message, "Structural Load Assistant startup");
            exitCode = 1;
        }
        inspection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return exitCode;
    }

    // Dedicated getter-contract test data. This never attaches to a CSI process and
    // deliberately does not use the demo adapter or a real user's model.
    private sealed class MockInspectionAdapter : ICsiSessionReadAdapter
    {
        public bool ChangedGeometry { get; set; }
        private static readonly CsiCapabilities Capabilities = new(true, true, true, true);
        private static readonly CsiModelSnapshot Model = new("mock-scan", "MOCK LIVE ADAPTER / not actual CSI", "MOCK LIVE ADAPTER", "contract-test 1", "mock-geometry", "mock-loads",
            ["MOCK_EXISTING_PATTERN"], ["MOCK_ANALYSIS_CASE"], ["MOCK_COMBINATION"],
            [new("MOCK-F1", "MOCK-FACADE", new(0, 0, 3), new(8, 0, 3)), new("MOCK-F2", "MOCK-FACADE", new(1, 0, 6), new(7, 0, 6))],
            [new("MOCK-A1", "MOCK-ROOF", [new(0, 0, 6), new(5, 0, 6), new(5, 4, 7), new(0, 4, 7)], new(0, -.242535625, .9701425), default),
             new("MOCK-A2", "MOCK-ROOF", [new(5, 0, 6), new(8, 0, 6), new(8, 4, 7), new(5, 4, 7)], new(0, -.242535625, .9701425), default)],
            [new("MOCK-A1", "MOCK_EXISTING_PATTERN", "AreaUniform/Local3", -450, "MOCK existing assignment (not generated)")]);
        public Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new CsiConnectionResult(true, "MOCK LIVE ADAPTER / NOT actual CSI", "getter contract-test 1", Capabilities, []));
        public Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CsiReadScan(ChangedGeometry ? Model with { GeometryFingerprint = "mock-changed-geometry", Shells = Model.Shells.SetItem(0, Model.Shells[0] with { Vertices = Model.Shells[0].Vertices.Select(v => new Vec3(v.X, v.Y, v.Z + 1)).ToImmutableArray() }) } : Model, new("N_m_C", 1, 1, "MOCK native N, m; geometry converted to SI"), true, Capabilities,
            [new("MOCK-L1", 3, 3), new("MOCK-L2", 6, 3)],
            [new("MOCK-A1", "MOCK-ROOF", null, "Unknown / no verified physical outward normal"), new("MOCK-A2", "MOCK-ROOF", null, "Unknown / no verified physical outward normal")],
            [new("MOCK_SOURCE", "Smoke", "Dedicated synthetic getter-contract geometry; never attached to actual CSI.", CsiIssueSeverity.Information)]));
        public Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default) => Task.FromResult(Model);
        public Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Model.ExistingLoads);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
