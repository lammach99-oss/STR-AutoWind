using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.App.Wpf;
public sealed partial class MainWindow
{
    public async Task RunExternalWindSmokeAsync(string output)
    {
        await vm.SmokeReadOnlyAsync();
        // Current mock selection, with reviewed panel spanning its actual top/bottom frames.
        var polygon=new PressureZonePolygon("wall",[new(1,0,3),new(7,0,3),new(7,0,6),new(1,0,6)],new(0,-1,0),"Mock reviewer","Vertical span / software test only");
        var request=ExternalWindFixture.Request();
        request=request with{Surfaces=request.Surfaces.Where(s=>s.Id=="wall").Select(s=>s with{Polygon=polygon,ParticipatingAreaM2=18}).ToImmutableArray()};
        var transfers=request.Surfaces.Select(s=>new ExternalTransferSelection($"{s.State}_{s.WindFrom}_{s.Id}_{s.Alternative}",new("wall",["MOCK-F1"],["MOCK-F2"],"Mock reviewer","Verified mock support lines / not physical qualification",true))).ToImmutableArray();
        vm.SetExternalInput(new(request,transfers));await vm.CalculateExternalAsync(output);
        if(vm.ExternalPreview is not {} preview||preview.Plans.Length!=8||preview.Blockers.Length!=0||!File.Exists(vm.ExternalPdfPath)||!File.Exists(vm.ExternalAuditPath)||preview.CanMutateCsi)throw new InvalidOperationException("External read-only preview incomplete");
        await vm.ExportExternalPdfAsync(Path.Combine(output,"external-saved.pdf"));
        if(!File.Exists(Path.Combine(output,"external-saved.pdf.integrity.json")))throw new InvalidOperationException("External export sidecar missing");
        string current=vm.ExternalPdfPath!;byte[] bytes=File.ReadAllBytes(current);File.AppendAllText(current,"tampered");
        bool rejected=false;try{await vm.ExportExternalPdfAsync(Path.Combine(output,"tampered.pdf"));}catch(InvalidOperationException){rejected=true;}
        File.WriteAllBytes(current,bytes);if(!rejected)throw new InvalidOperationException("Tampered external export accepted");
        vm.Direction=WindDirection.NegativeY;
        if(vm.ExternalPdfPath!=null||vm.ExternalPreview!=null)throw new InvalidOperationException("Stale external report survived wind change");
        await vm.CalculateExternalAsync(output);
        Tabs.SelectedItem=Tabs.Items.OfType<System.Windows.Controls.TabItem>().Single(t=>t.Header?.ToString()=="External");
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await externalViewer!.CapturePdfPreviewAsync(Path.Combine(output,"embedded-external-pdf.png"));
        File.WriteAllText(Path.Combine(output,"external-proof.json"),System.Text.Json.JsonSerializer.Serialize(new{MockOnly=true,ActualCsiValidated=false,PlanCount=vm.ExternalPreview!.Plans.Length,vm.ExternalPreview.ContentHash,CanMutateCsi=false}));
    }
}
