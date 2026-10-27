using System.Collections.Immutable;
using System.IO;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.App.Wpf;
public sealed record ExternalReviewInput(ExternalWindRequest Request,ImmutableArray<ExternalTransferSelection> Transfers);
public sealed partial class MainViewModel
{
    private ExternalReviewInput? externalInput;
    private CancellationTokenSource? externalCancellation;
    private long externalVersion;
    private string? externalMainHash,externalAuditHash;
    public ExternalWindPreview? ExternalPreview {get;private set;}
    public string? ExternalPdfPath {get;private set;}
    public string? ExternalAuditPath {get;private set;}
    public string ExternalStatus {get;private set;}="EXTERNAL ONLY / engineer source-labeled input required / live writes disabled";
    public void InvalidateExternalReview()
    {
        externalCancellation?.Cancel();externalVersion++;ExternalStatus="External review invalidated / recalculate current inputs and model";ExternalPreview=null;ExternalPdfPath=null;ExternalAuditPath=null;externalMainHash=null;externalAuditHash=null;
    }
    public void SetExternalInput(ExternalReviewInput input)
    {
        InvalidateWindReport();externalInput=input;ExternalStatus="External inputs loaded / recalculate for current selection";Refresh();
    }
    public Task ExportExternalPdfAsync(string destination,bool audit=false,CancellationToken ct=default)
    {
        var preview=ExternalPreview??throw new InvalidOperationException("No current external preview");
        string source=(audit?ExternalAuditPath:ExternalPdfPath)??throw new InvalidOperationException("No current external PDF");
        string hash=(audit?externalAuditHash:externalMainHash)??throw new InvalidOperationException("No PDF integrity evidence");
        long generation=externalVersion;
        void Validate(){if(generation!=externalVersion||!ReferenceEquals(ExternalPreview,preview)||SnapshotJson.Hash(IsDemo?Session.Model:inspection.Scan?.Model)!=SnapshotJson.Hash(preview.Model)||WorksheetPdfExport.Hash(source)!=hash)throw new InvalidOperationException("Stale or changed external PDF/model");}
        Validate();
        return WorksheetPdfExport.PublishAsync(new(source,hash,preview.Run.ContentHash,preview.ContentHash,SnapshotJson.Hash(preview.Model),SnapshotJson.Hash(preview.Run.Request),ExternalStatus),destination,Validate,ct);
    }    public async Task CalculateExternalAsync(string? output=null,CancellationToken ct=default)
    {
        InvalidateExternalReview();long generation=externalVersion;
        var input=externalInput??throw new InvalidOperationException("Load explicit external review request first");
        var model=IsDemo?Session.Model:inspection.Scan?.Model??throw new InvalidOperationException("Read actual selected model first");
        externalCancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);var token=externalCancellation.Token;
        using var operation=externalCancellation;
        string root=output??Path.Combine(Path.GetTempPath(),"external-review-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        ExternalStatus="Calculating external-only review in background";Refresh();
        try
        {
            var preview=IsDemo?await ExternalWindReviewCoordinator.PreviewAsync(input.Request,model,input.Transfers,token):await ExternalWindReviewCoordinator.PreviewReadOnlyAsync(input.Request,inspection.Scan!,input.Transfers,token);
            string main=Path.Combine(root,"external-worksheet.pdf"),audit=Path.Combine(root,"external-audit.pdf");
            var renderer=new WindPdfRenderService();
            await renderer.RenderHtmlToPdfAsync(ExternalWindReportBuilder.BuildHtml(preview.Run,model,preview.Plans,preview.Blockers,false),main,token);
            await renderer.RenderHtmlToPdfAsync(ExternalWindReportBuilder.BuildHtml(preview.Run,model,preview.Plans,preview.Blockers,true),audit,token);
            token.ThrowIfCancellationRequested();
            if(generation!=externalVersion||SnapshotJson.Hash(model)!=SnapshotJson.Hash(IsDemo?Session.Model:inspection.Scan?.Model))return;
            await File.WriteAllTextAsync(Path.Combine(root,"frame-schedule.json"),ExternalWindReportBuilder.ScheduleJson(preview.Run,model,preview.Plans),token);
            token.ThrowIfCancellationRequested();if(generation!=externalVersion)return;
            await File.WriteAllTextAsync(Path.Combine(root,"frame-schedule.csv"),ExternalWindReportBuilder.ScheduleCsv(preview.Run,model,preview.Plans),token);
            token.ThrowIfCancellationRequested();if(generation!=externalVersion)return;
            ExternalPreview=preview;ExternalPdfPath=main;ExternalAuditPath=audit;externalMainHash=WorksheetPdfExport.Hash(main);externalAuditHash=WorksheetPdfExport.Hash(audit);
            ExternalStatus=preview.Run.Status+" / "+preview.Blockers.Length+" blocked or unassigned transfers / "+(IsDemo?"DEMO":"REAL MODEL READ-ONLY PREVIEW - NOT ASSIGNED");
        }
        catch(OperationCanceledException){if(generation==externalVersion)ExternalStatus="External review cancelled / no current PDF";}
        catch{ExternalStatus="External review failed / no current PDF";throw;}
        finally{if(ReferenceEquals(externalCancellation,operation))externalCancellation=null;Refresh();}
    }
}
