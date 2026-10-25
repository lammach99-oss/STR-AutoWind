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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);

        string? edgePath = FindChromiumPath();
        if (edgePath == null)
        {
            throw new InvalidOperationException(
                "Offline Chromium renderer (Microsoft Edge) was not found on this Windows system. " +
                "Ensure Microsoft Edge or Edge WebView2 runtime is installed.");
        }

        string tempHtml = Path.Combine(Path.GetTempPath(), $"wind_report_{Guid.NewGuid():N}.html");
        File.WriteAllText(tempHtml, html, System.Text.Encoding.UTF8);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPdfPath))!);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = string.Format("--headless=new --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --print-to-pdf=\"{0}\" \"{1}\"", Path.GetFullPath(outputPdfPath), Path.GetFullPath(tempHtml)),
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to launch headless Chromium process.");

            if (!proc.WaitForExit(timeoutSeconds * 1000))
            {
                try { proc.Kill(); } catch { }
                throw new TimeoutException($"PDF rendering timed out after {timeoutSeconds} seconds.");
            }

            if (!File.Exists(outputPdfPath) || new FileInfo(outputPdfPath).Length == 0)
            {
                throw new InvalidOperationException(
                    $"Headless Chromium completed with exit code {proc.ExitCode}, but no PDF output was produced at {outputPdfPath}.");
            }

            // Verify valid PDF header
            byte[] header = new byte[4];
            using (var fs = File.OpenRead(outputPdfPath))
            {
                fs.ReadExactly(header, 0, 4);
            }
            if (System.Text.Encoding.ASCII.GetString(header) != "%PDF")
            {
                throw new InvalidOperationException("Generated file does not have a valid %PDF header.");
            }
        }
        finally
        {
            try { if (File.Exists(tempHtml)) File.Delete(tempHtml); } catch { }
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
