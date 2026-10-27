using System.Text.Json;
using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;

public sealed class AsNzPrivateIntakeCommandTests
{
    [Fact]
    public void InitializeMakesAZeroCoefficientPrivateTemplateAndReportsPreciseBlockers()
    {
        string temp=Path.Combine(Path.GetTempPath(),"asnz-cli-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var pdf=Path.Combine(temp,"source.pdf");var book=Path.Combine(temp,"source.xlsm");
            File.WriteAllText(pdf,"SYNTHETIC SOURCE FILE, NOT A PUBLICATION");File.WriteAllText(book,"SYNTHETIC WORKBOOK");
            var destination=Path.Combine(temp,"fixtures","standards","private","synthetic-v1");
            var output=new StringWriter();
            Assert.Equal(2,AsNzPrivateIntakeCommand.Run(["initialize",destination,pdf,book],output));
            var stage=JsonSerializer.Deserialize<AsNzSourceStaging>(File.ReadAllText(Path.Combine(destination,"source-staging.json")),AsNzPrivateStagingJson.Options)!;
            Assert.Equal(13,stage.Cells.Length);Assert.All(stage.Cells,c=>{Assert.Null(c.Record);Assert.Equal(SourceCellStatus.NOT_AVAILABLE,c.Status);});
            Assert.Equal("UNCONFIRMED",stage.Rights!.Decision);Assert.Empty(stage.Country);
            Assert.True(File.Exists(Path.Combine(destination,"manifest.template.json")));
            Assert.True(File.Exists(Path.Combine(destination,"source-staging.schema.json")));
            Assert.False(File.Exists(Path.Combine(destination,"manifest.json")));
            Assert.False(File.Exists(Path.Combine(destination,"auto-authorization.json")));
            Assert.False(Directory.Exists(Path.Combine(destination,"source"))); // No controlled copies before rights review.
            Assert.DoesNotContain(Directory.GetFiles(destination,"*",SearchOption.AllDirectories),f=>f.EndsWith(".pdf")||f.EndsWith(".xlsm"));
            using var inventory=JsonDocument.Parse(File.ReadAllText(Path.Combine(destination,"source-inventory.json")));
            Assert.False(inventory.RootElement.GetProperty("ControlledReferencesCopied").GetBoolean());
            Assert.Contains("RIGHTS",output.ToString());
            Assert.Contains("JURISDICTION",output.ToString());
            Assert.Equal("SYNTHETIC SOURCE FILE, NOT A PUBLICATION",File.ReadAllText(pdf));
            Assert.Equal(2,AsNzPrivateIntakeCommand.Run(["validate",Path.Combine(destination,"source-staging.json")],new StringWriter()));
            Assert.Equal(2,AsNzPrivateIntakeCommand.Run(["initialize",destination,pdf,book],new StringWriter()));
        }
        finally{Directory.Delete(temp,true);}
    }
    [Fact]
    public void InvalidInvocationReturnsUsageWithoutDemoSideEffects()
    {
        var output=new StringWriter();Assert.Equal(1,AsNzPrivateIntakeCommand.Run([],output));Assert.Contains("Usage",output.ToString());
    }
}
