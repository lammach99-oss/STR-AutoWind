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

    [Fact]
    public async Task CancellationDuringChromiumRenderingCleansIsolatedProfileAndAllowsRestart()
    {
        Assert.True(WindPdfRenderService.IsRendererAvailable,"Actual Chromium required");
        var existing=Directory.GetDirectories(Path.GetTempPath(),"wind-render-*").ToHashSet();
        string target=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");
        using var ct=new CancellationTokenSource();
        string html="<html><body>"+string.Concat(Enumerable.Repeat("<p>Cancellation workload</p>",50000))+"</body></html>";
        var render=new WindPdfRenderService().RenderHtmlToPdfAsync(html,target,ct.Token);
        string? workspace=null;
        for(int i=0;i<200;i++)
        {
            workspace=Directory.GetDirectories(Path.GetTempPath(),"wind-render-*").FirstOrDefault(d=>!existing.Contains(d)&&Directory.Exists(Path.Combine(d,"profile")));
            if(workspace!=null)break;
            await Task.Delay(20);
        }
        Assert.NotNull(workspace);
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>render);
        Assert.False(File.Exists(target));
        Assert.False(Directory.Exists(workspace));
        try {await new WindPdfRenderService().RenderHtmlToPdfAsync("<html><body>Restart successful</body></html>",target);Assert.True(File.Exists(target));}
        finally {if(File.Exists(target))File.Delete(target);}
    }
}
