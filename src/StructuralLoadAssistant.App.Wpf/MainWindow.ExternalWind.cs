using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StructuralLoadAssistant.App.Wpf.Controls;
namespace StructuralLoadAssistant.App.Wpf;
public sealed partial class MainWindow
{
    private WindPdfViewer? externalViewer;
    private UIElement ExternalPage()
    {
        var p=Stack();p.Children.Add(Text("AS/NZS External Pressure Only — read-only review",22));
        p.Children.Add(Text("Load source-labeled factors, physical zone polygons and reviewed actual support IDs. Internal/net pressure is engineer controlled separately. No live Apply action. Native model geometry is normalized to SI by the read adapter. Inspect model/selection on CSI tab first."));
        p.Children.Add(Row(Button("Load external review input…",()=>vm.Run(()=>{var d=new OpenFileDialog{Filter="External review JSON|*.json"};if(d.ShowDialog(this)==true)vm.SetExternalInput(JsonSerializer.Deserialize<ExternalReviewInput>(File.ReadAllText(d.FileName))??throw new InvalidOperationException("Invalid input"));}),false),Button("Calculate / preview PDFs",()=>_=vm.RunAsync(()=>vm.CalculateExternalAsync()),false),Button("Cancel",()=>{vm.InvalidateExternalReview();vm.Refresh();},false)));
        p.Children.Add(ExternalInputEditor());
        p.Children.Add(BoundText("ExternalStatus"));
        var viewer=new WindPdfViewer{Height=580};externalViewer=viewer;viewer.SaveOverride=path=>vm.ExportExternalPdfAsync(path,viewer.CurrentPdfPath==vm.ExternalAuditPath);p.Children.Add(viewer);
        p.Children.Add(Button("Show detailed audit PDF",()=>vm.Run(()=>{if(vm.ExternalPreview is {} preview&&vm.ExternalAuditPath is {} path)viewer.LoadPdf(path,preview.ContentHash,null,vm.ExternalStatus);}),false));
        vm.Refreshed+=()=>{if(vm.ExternalPreview is {} preview&&vm.ExternalPdfPath is {} path){if(viewer.CurrentRunHash!=preview.ContentHash)viewer.LoadPdf(path,preview.ContentHash,null,vm.ExternalStatus);}else viewer.Clear();};
        return p;
    }
}
