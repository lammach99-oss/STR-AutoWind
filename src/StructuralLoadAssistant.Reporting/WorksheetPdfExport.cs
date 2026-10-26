using System.Security.Cryptography;
using System.Text.Json;
namespace StructuralLoadAssistant.Reporting;

public sealed record WorksheetExportSnapshot(string SourcePath,string PdfSha256,string RunHash,string DocumentHash,string ModelFingerprint,string RequestHash,string Status)
{
    public bool NormativeQualified => false;
    public bool CanMutateCsi => false;
}

/// <summary>Stages both files, withdraws old metadata, publishes PDF then metadata last.
/// Caught failures restore the previous pair. Not crash-atomic: consumers must require a
/// matching sidecar; a crash during replacement can leave metadata absent, never trusted.
/// Callers serialize publication on their UI context and revalidate immediately before it.</summary>
public static class WorksheetPdfExport
{
    public static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static async Task PublishAsync(WorksheetExportSnapshot snapshot,string destination,Action validate,CancellationToken ct=default,Action<string>? checkpoint=null)
    {
        destination=Path.GetFullPath(destination);
        string sidecar=destination+".integrity.json";
        if(Path.GetFullPath(snapshot.SourcePath).Equals(destination,StringComparison.OrdinalIgnoreCase))throw new IOException("Export destination must differ from rendered source");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var publicationLock=new FileStream(destination+".export.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose);
        string staging=Path.Combine(Path.GetDirectoryName(destination)!,".wind-export-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string pdf=Path.Combine(staging,"new.pdf"),meta=Path.Combine(staging,"new.json"),oldPdf=Path.Combine(staging,"old.pdf"),oldMeta=Path.Combine(staging,"old.json");
        bool withdrewPdf=false,withdrewMeta=false,publishedPdf=false,publishedMeta=false,committed=false;
        try
        {
            ct.ThrowIfCancellationRequested();
            await Task.Run(()=>File.Copy(snapshot.SourcePath,pdf),ct);
            if(Hash(pdf)!=snapshot.PdfSha256)throw new InvalidOperationException("Source PDF integrity mismatch");
            checkpoint?.Invoke("BeforeSidecarWrite");
            await File.WriteAllTextAsync(meta,JsonSerializer.Serialize(snapshot),ct);
            if(JsonSerializer.Deserialize<WorksheetExportSnapshot>(await File.ReadAllTextAsync(meta,ct))!=snapshot)throw new IOException("Staged metadata mismatch");
            checkpoint?.Invoke("BeforePublish");
            ct.ThrowIfCancellationRequested();validate();
            if(Hash(snapshot.SourcePath)!=snapshot.PdfSha256)throw new InvalidOperationException("Source PDF changed during export");
            if(Directory.Exists(destination)||Directory.Exists(sidecar))throw new IOException("Output path is a directory");
            // No await in the commit/rollback window. Cancellation after commit starts is
            // observed at checkpoints and rolled back; successful publication is a full pair.
            if(File.Exists(sidecar)){File.Move(sidecar,oldMeta);withdrewMeta=true;}
            if(File.Exists(destination)){File.Move(destination,oldPdf);withdrewPdf=true;}
            File.Move(pdf,destination);publishedPdf=true;
            checkpoint?.Invoke("AfterPdfPublish");ct.ThrowIfCancellationRequested();validate();
            File.Move(meta,sidecar);publishedMeta=true;
            checkpoint?.Invoke("AfterSidecarPublish");ct.ThrowIfCancellationRequested();validate();
            if(Hash(destination)!=snapshot.PdfSha256 || JsonSerializer.Deserialize<WorksheetExportSnapshot>(File.ReadAllText(sidecar))!=snapshot)throw new IOException("Published pair mismatch");
            committed=true;
        }
        catch
        {
            if(publishedMeta)File.Delete(sidecar);
            if(publishedPdf)File.Delete(destination);
            if(withdrewPdf)File.Move(oldPdf,destination);
            if(withdrewMeta)File.Move(oldMeta,sidecar);
            throw;
        }
        finally
        {
            // Preserve backups if rollback itself failed; do not erase recovery evidence.
            if(!File.Exists(oldPdf)&&!File.Exists(oldMeta))Directory.Delete(staging,true);
            else if(committed)Directory.Delete(staging,true);
        }
    }
}
