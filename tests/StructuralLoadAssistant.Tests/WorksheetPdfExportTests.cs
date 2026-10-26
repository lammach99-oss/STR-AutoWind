using StructuralLoadAssistant.Reporting;
using System.Text.Json;
namespace StructuralLoadAssistant.Tests;
public sealed class WorksheetPdfExportTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"export-test-"+Guid.NewGuid().ToString("N"));
    public WorksheetPdfExportTests()=>Directory.CreateDirectory(root);
    public void Dispose()=>Directory.Delete(root,true);
    WorksheetExportSnapshot Snapshot(string status="Ready")
    {
        string source=Path.Combine(root,"source.pdf");File.WriteAllText(source,"%PDF-review bytes");
        return new(source,WorksheetPdfExport.Hash(source),"run","document","model","request",status);
    }
    [Theory]
    [InlineData("Ready")][InlineData("Blocked")]
    public async Task MainAuditBlockedAndRepeatedExportsHaveMatchingImmutableMetadata(string status)
    {
        var snapshot=Snapshot(status);
        foreach(string name in new[]{"main.pdf","audit.pdf","main.pdf"})
        {
            string output=Path.Combine(root,name);
            await WorksheetPdfExport.PublishAsync(snapshot,output,()=>{});
            var metadata=JsonSerializer.Deserialize<WorksheetExportSnapshot>(File.ReadAllText(output+".integrity.json"));
            Assert.Equal(snapshot,metadata);Assert.False(metadata!.NormativeQualified);Assert.False(metadata.CanMutateCsi);
            Assert.Equal(metadata.PdfSha256,WorksheetPdfExport.Hash(output));
        }
        Assert.Empty(Directory.GetDirectories(root));
    }
    [Theory]
    [InlineData("BeforeSidecarWrite")][InlineData("BeforePublish")][InlineData("AfterPdfPublish")][InlineData("AfterSidecarPublish")]
    public async Task InjectedWriteOrPublicationFailureRestoresPriorPair(string phase)
    {
        var snapshot=Snapshot();string output=Path.Combine(root,"result.pdf");
        File.WriteAllText(output,"prior PDF");File.WriteAllText(output+".integrity.json","prior metadata");
        await Assert.ThrowsAsync<IOException>(()=>WorksheetPdfExport.PublishAsync(snapshot,output,()=>{},checkpoint:p=>{if(p==phase)throw new IOException("Injected failure");}));
        Assert.Equal("prior PDF",File.ReadAllText(output));Assert.Equal("prior metadata",File.ReadAllText(output+".integrity.json"));
        Assert.Empty(Directory.GetDirectories(root));
    }
    [Theory]
    [InlineData("BeforePublish")][InlineData("AfterPdfPublish")][InlineData("AfterSidecarPublish")]
    public async Task CancellationPublishesNeitherAndRemovesStaging(string phase)
    {
        var snapshot=Snapshot();string output=Path.Combine(root,"result.pdf");using var ct=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>WorksheetPdfExport.PublishAsync(snapshot,output,()=>{},ct.Token,p=>{if(p==phase)ct.Cancel();}));
        Assert.False(File.Exists(output));Assert.False(File.Exists(output+".integrity.json"));Assert.Empty(Directory.GetDirectories(root));
    }
    [Theory]
    [InlineData("request")][InlineData("model")]
    public async Task IdentityMutationImmediatelyBeforePublishIsRejected(string identity)
    {
        var snapshot=Snapshot();string output=Path.Combine(root,"result.pdf");bool changed=false;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>WorksheetPdfExport.PublishAsync(snapshot,output,()=>{if(changed)throw new InvalidOperationException(identity+" changed");},checkpoint:p=>{if(p=="BeforePublish")changed=true;}));
        Assert.False(File.Exists(output));Assert.False(File.Exists(output+".integrity.json"));Assert.Empty(Directory.GetDirectories(root));
    }
    [Fact]
    public async Task SourceTamperAndCanceledEntryNeverPublish()
    {
        var snapshot=Snapshot();string output=Path.Combine(root,"result.pdf");File.AppendAllText(snapshot.SourcePath,"changed");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>WorksheetPdfExport.PublishAsync(snapshot,output,()=>{}));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>WorksheetPdfExport.PublishAsync(snapshot,output,()=>{},new CancellationToken(true)));
        Assert.False(File.Exists(output));Assert.Empty(Directory.GetDirectories(root));
    }
}
