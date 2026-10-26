using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public sealed class PdfCancellationTests
{
    [Fact]
    public async Task CancelledRenderNeverPublishesOutput()
    {
        string target=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new WindPdfRenderService().RenderHtmlToPdfAsync("<html>cancelled</html>",target,new CancellationToken(true)));
            Assert.False(File.Exists(target));
        }
        finally { if(File.Exists(target))File.Delete(target); }
    }
}
