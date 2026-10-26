using System;
using System.Diagnostics;
using System.IO;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// High-fidelity offline HTML-to-PDF rendering service.
/// Utilizes the local Windows Chromium / Microsoft Edge engine in headless print-to-pdf mode.
/// Produces exact A4 vector PDFs with real selectable text, clean vector SVG diagrams,
/// and accurate pagination matching the owner's workbook layouts.
/// </summary>
public sealed class WindPdfRenderService
{
    private static readonly string[] PossibleEdgePaths =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe")
    ];

    public static string? FindChromiumPath()
    {
        foreach (var p in PossibleEdgePaths)
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static bool IsRendererAvailable => FindChromiumPath() != null;

    public byte[] RenderHtmlToPdfBytes(string html, int timeoutSeconds = 30)
    {
        string tempHtml = Path.Combine(Path.GetTempPath(), $"wind_report_{Guid.NewGuid():N}.html");
        string tempPdf = Path.Combine(Path.GetTempPath(), $"wind_report_{Guid.NewGuid():N}.pdf");

        try
        {
            RenderHtmlToPdf(html, tempPdf, timeoutSeconds);
            return File.ReadAllBytes(tempPdf);
        }
        finally
        {
            try { if (File.Exists(tempHtml)) File.Delete(tempHtml); } catch { }
            try { if (File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
        }
    }

    public void RenderHtmlToPdf(string html, string outputPdfPath, int timeoutSeconds = 30)
        => RenderHtmlToPdfAsync(html, outputPdfPath, default, timeoutSeconds).GetAwaiter().GetResult();

    public async Task RenderHtmlToPdfAsync(string html, string outputPdfPath, CancellationToken cancellationToken = default, int timeoutSeconds = 30)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);
        string edge = FindChromiumPath() ?? throw new InvalidOperationException("Offline Chromium renderer unavailable.");
        string workspace = Path.Combine(Path.GetTempPath(), "wind-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        string source = Path.Combine(workspace, "source.html"), staged = Path.Combine(workspace, "output.pdf");
        try
        {
            await File.WriteAllTextAsync(source, html, System.Text.Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var start = new ProcessStartInfo(edge) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (string arg in new[] { "--headless=new", "--disable-gpu", "--no-first-run", "--no-pdf-header-footer", "--run-all-compositor-stages-before-draw", "--user-data-dir=" + Path.Combine(workspace,"profile"), "--print-to-pdf=" + staged, source }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start offline renderer.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try { await process.WaitForExitAsync(linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("PDF rendering timed out.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(staged)) throw new InvalidOperationException("PDF rendering failed.");
            byte[] bytes = await File.ReadAllBytesAsync(staged, cancellationToken).ConfigureAwait(false);
            if(bytes.Length < 4 || System.Text.Encoding.ASCII.GetString(bytes,0,4) != "%PDF") throw new InvalidOperationException("Invalid rendered PDF.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPdfPath))!);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(staged, outputPdfPath, overwrite: true);
        }
        finally
        {
            // Chromium descendants may release profile handles just after the parent exits.
            for(int attempt=0;attempt<30 && Directory.Exists(workspace);attempt++)
            {
                try { Directory.Delete(workspace,recursive:true); }
                catch(IOException) when(attempt<29) { await Task.Delay(100).ConfigureAwait(false); }
                catch(UnauthorizedAccessException) when(attempt<29) { await Task.Delay(100).ConfigureAwait(false); }
            }
        }
    }

    public void RenderDocument(WindPdfReportDocument doc, string outputPdfPath, int timeoutSeconds = 30)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);
        RenderHtmlToPdf(html, outputPdfPath, timeoutSeconds);
    }

    public byte[] RenderDocumentBytes(WindPdfReportDocument doc, int timeoutSeconds = 30)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var builder = new WindWorkbookHtmlReportBuilder();
        string html = builder.BuildHtml(doc);
        return RenderHtmlToPdfBytes(html, timeoutSeconds);
    }
}
