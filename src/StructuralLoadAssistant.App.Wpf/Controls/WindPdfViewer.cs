using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace StructuralLoadAssistant.App.Wpf.Controls;

/// <summary>
/// Professional WPF Embedded PDF Viewer control with toolbar controls:
/// - Page navigation &amp; zoom
/// - Print
/// - Save As PDF
/// - Content Hash &amp; Real File Hash &amp; Review Status display
///
/// Hosts Microsoft WebView2 browser control targeting the local generated PDF file.
/// Provides graceful fallback panel if WebView2 runtime is initializing or unavailable.
/// </summary>
public sealed class WindPdfViewer : DockPanel
{
    private readonly ToolBar _toolbar;
    private readonly TextBlock _statusText;
    private readonly TextBlock _zoomText;
    private readonly Border _contentHost;
    private readonly Button _btnPrint;
    private readonly Button _btnSave;
    private readonly Button _btnZoomIn;
    private readonly Button _btnZoomOut;
    private readonly Button _btnZoomReset;
    private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
    private TextBlock? _fallbackText;

    private bool pdfNavigated;
    private string? _currentPdfPath;
    private string? _currentRunHash;
    private string? _currentDocHash;
    private string? _currentFileSha256;
    private double _zoomFactor = 1.0;

    public string? CurrentPdfPath => _currentPdfPath;
    public string? CurrentRunHash => _currentRunHash;
    public string? CurrentDocHash => _currentDocHash;
    public string? CurrentFileSha256 => _currentFileSha256;
    public string? CurrentHash => _currentRunHash;

    public WindPdfViewer()
    {
        LastChildFill = true;
        Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));

        _toolbar = new ToolBar { Height = 36 };
        DockPanel.SetDock(_toolbar, Dock.Top);

        _btnPrint = new Button { Content = "Print", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(2), IsEnabled = false };
        _btnPrint.Click += (_, _) => Print();
        _toolbar.Items.Add(_btnPrint);

        _btnSave = new Button { Content = "Save PDF As...", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(2), IsEnabled = false };
        _btnSave.Click += (_, _) => SaveAs();
        _toolbar.Items.Add(_btnSave);

        _toolbar.Items.Add(new Separator());

        _btnZoomIn = new Button { Content = "Zoom +", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(2), IsEnabled = false };
        _btnZoomIn.Click += (_, _) => ZoomIn();
        _toolbar.Items.Add(_btnZoomIn);

        _btnZoomOut = new Button { Content = "Zoom -", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(2), IsEnabled = false };
        _btnZoomOut.Click += (_, _) => ZoomOut();
        _toolbar.Items.Add(_btnZoomOut);

        _btnZoomReset = new Button { Content = "100%", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(2), IsEnabled = false };
        _btnZoomReset.Click += (_, _) => ZoomReset();
        _toolbar.Items.Add(_btnZoomReset);

        _zoomText = new TextBlock
        {
            Text = "100%",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 10, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            FontWeight = FontWeights.SemiBold
        };
        _toolbar.Items.Add(_zoomText);

        _toolbar.Items.Add(new Separator());

        _statusText = new TextBlock
        {
            Text = "No calculation PDF loaded",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            FontWeight = FontWeights.Bold
        };
        _toolbar.Items.Add(_statusText);

        Children.Add(_toolbar);

        _contentHost = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Margin = new Thickness(4)
        };
        Children.Add(_contentHost);

        Loaded += async (_, _) => await InitializeViewerAsync();
    }

    private async System.Threading.Tasks.Task InitializeViewerAsync()
    {
        if (_webView?.CoreWebView2 != null) return;
        try
        {
            _webView ??= new Microsoft.Web.WebView2.Wpf.WebView2();
            _contentHost.Child = _webView;
            var profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StructuralLoadAssistant","WebView2");
            var environment=await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null,profile);
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.NavigationCompleted += (_, e) => pdfNavigated=e.IsSuccess && _currentPdfPath!=null && _webView.Source?.IsFile==true;
            if (_currentPdfPath != null) _webView.Source = new Uri(Path.GetFullPath(_currentPdfPath));
        }
        catch (Exception ex)
        {
            _fallbackText = new TextBlock
            {
                Text = $"WebView2 runtime not initialized: {ex.Message}\nPDF file is generated and can be saved or opened externally.",
                Foreground = Brushes.DarkRed,
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _contentHost.Child = _fallbackText;
        }
    }

    public void Clear()
    {
        pdfNavigated=false;
        _currentPdfPath = null;
        _currentRunHash = null;
        _currentDocHash = null;
        _currentFileSha256 = null;

        _btnPrint.IsEnabled = false;
        _btnSave.IsEnabled = false;
        _btnZoomIn.IsEnabled = false;
        _btnZoomOut.IsEnabled = false;
        _btnZoomReset.IsEnabled = false;

        _statusText.Text = "No calculation PDF loaded";

        if (_webView != null)
        {
            try
            {
                _webView.Source = new Uri("about:blank");
            }
            catch { }
        }
    }

    public void LoadPdf(string pdfPath, string runContentHash, string? docHash = null, string reviewStatus = "Pending Review")
    {
        if (!File.Exists(pdfPath))
        {
            Clear();
            _statusText.Text = $"PDF not found at {pdfPath}";
            return;
        }

        byte[] bytes = File.ReadAllBytes(pdfPath);
        if (bytes.Length < 4 || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "%PDF")
        {
            Clear();
            _statusText.Text = "Corrupt or invalid PDF file (missing %PDF header)";
            return;
        }

        using var sha = SHA256.Create();
        string fileHash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();

        pdfNavigated=false;
        _currentPdfPath = pdfPath;
        _currentRunHash = runContentHash;
        _currentDocHash = docHash ?? runContentHash;
        _currentFileSha256 = fileHash;

        string runShort = runContentHash.Length > 8 ? runContentHash[..8] : runContentHash;
        string fileShort = fileHash.Length > 8 ? fileHash[..8] : fileHash;

        _statusText.Text = $"PDF ready - run: {runShort} - file: {fileShort} - {reviewStatus} ({bytes.Length / 1024} KB)";

        _btnPrint.IsEnabled = true;
        _btnSave.IsEnabled = true;
        _btnZoomIn.IsEnabled = true;
        _btnZoomOut.IsEnabled = true;
        _btnZoomReset.IsEnabled = true;

        if (_webView != null)
        {
            try
            {
                var uri = new Uri(Path.GetFullPath(pdfPath));
                _webView.Source = uri;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Loaded PDF ({runShort}) - View error: {ex.Message}";
            }
        }
    }

    public async System.Threading.Tasks.Task CapturePdfPreviewAsync(string path)
    {
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(!pdfNavigated && DateTime.UtcNow<deadline)await System.Threading.Tasks.Task.Delay(100);
        if(!pdfNavigated||_webView?.CoreWebView2==null)throw new InvalidOperationException("Embedded PDF navigation not proven");
        await System.Threading.Tasks.Task.Delay(2000);
        using var stream=File.Create(path);
        await _webView.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,stream);
    }
    public void ZoomIn()
    {
        _zoomFactor = Math.Min(3.0, _zoomFactor + 0.15);
        ApplyZoom();
    }

    public void ZoomOut()
    {
        _zoomFactor = Math.Max(0.5, _zoomFactor - 0.15);
        ApplyZoom();
    }

    public void ZoomReset()
    {
        _zoomFactor = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        _zoomText.Text = $"{_zoomFactor * 100:F0}%";
        if (_webView != null)
        {
            try { _webView.ZoomFactor = _zoomFactor; } catch { }
        }
    }

    public void Print()
    {
        if (string.IsNullOrEmpty(_currentPdfPath) || !File.Exists(_currentPdfPath))
        {
            MessageBox.Show("No calculation PDF is currently available to print.", "Print PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentPdfPath,
                UseShellExecute = true,
                Verb = "print"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to trigger print automatically: {ex.Message}", "Print PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public Func<string,System.Threading.Tasks.Task>? SaveOverride {get;set;}
    public async void SaveAs(string? defaultFileName = null)
    {
        if (string.IsNullOrEmpty(_currentPdfPath) || !File.Exists(_currentPdfPath))
        {
            MessageBox.Show("No calculation PDF is currently available to save.", "Save PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = defaultFileName ?? Path.GetFileName(_currentPdfPath)
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                if(SaveOverride!=null)await SaveOverride(dlg.FileName);
                else File.Copy(_currentPdfPath, dlg.FileName, overwrite: true);
                MessageBox.Show($"Successfully saved PDF to:\n{dlg.FileName}", "Save PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save PDF: {ex.Message}", "Save PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
