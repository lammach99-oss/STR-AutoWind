using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructuralLoadAssistant.Application;

namespace StructuralLoadAssistant.App.Wpf;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var smoke = args.Contains("--smoke", StringComparer.Ordinal);
        string? Option(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        var output = Path.GetFullPath(Option("--smoke-output") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StructuralLoadAssistant", "smoke"));
        var database = Option("--database") ?? Path.Combine(smoke ? output : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StructuralLoadAssistant"), "demo.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(database))!);
        if (smoke) Directory.CreateDirectory(output);
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
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
                    await vm.SmokeAsync();
                    for (var i = 0; i < window.Tabs.Items.Count; i++)
                    {
                        window.Tabs.SelectedIndex = i;
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        window.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output, $"{i + 1:00}-{((System.Windows.Controls.TabItem)window.Tabs.Items[i]).Header}.png")); encoder.Save(stream);
                    }
                    vm.ExportReport(Path.Combine(output, "desktop-report.html"));
                    await File.WriteAllTextAsync(Path.Combine(output, "smoke-summary.json"), JsonSerializer.Serialize(new { Success = true, RenderedTabs = window.Tabs.Items.Count, vm.Session.Project.Id, State = vm.Session.Roof.State.ToString(), Readback = vm.Session.Readback?.Matches, FacadeReadback = vm.Session.FacadeReadback?.Matches, Assignment = vm.Session.Assignment?.Id, StoredReports = vm.ReportIds.Count, Database = database }, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception error)
                {
                    exitCode = 1; logger.LogError(error, "Smoke failed"); await File.WriteAllTextAsync(Path.Combine(output, "smoke-error.txt"), error.ToString());
                }
                finally { app.Shutdown(exitCode); }
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
        return exitCode;
    }
}
