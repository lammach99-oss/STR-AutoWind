using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Reporting;

namespace StructuralLoadAssistant.Tests;
public class ReportTests
{
    [Fact] public async Task ReportRejectsLoadValuesThatDifferFromApprovedProposal()
    {
        var fake=new FakeCsiAdapter();var model=await fake.ScanModelAsync();var wf=CsiTests.Workflow(model);wf.Validate();
        var approval=wf.Approve("Engineer",DemoFixtures.FixtureTime);var run=await fake.ApplyAreaLoadsAsync(approval);
        var report=Model() with {Model=model,Approval=approval,Assignment=run};
        var altered=run with {AreaLoads=run.AreaLoads.SetItem(0,run.AreaLoads[0] with {SignedCsiAreaLoadPa=9999})};
        Assert.Throws<InvalidOperationException>(()=>new MarkdownReportExporter().Export(report with {Assignment=altered}));
    }
    [Fact] public void ReportRejectsMismatchedSnapshotLinks()
    {
        var m=Model();var exporter=new MarkdownReportExporter();
        Assert.Throws<InvalidOperationException>(()=>exporter.Export(m with {Calculation=m.Calculation with {ManifestHash="other"}}));
        Assert.Throws<InvalidOperationException>(()=>exporter.Export(m with {Readback=new("unrelated-assignment",true,1e-6,[])}));
        var wrong=new AssignmentRun("a","approval","hash",m.Calculation.Id,m.Model.Id,DemoFixtures.FixtureTime,true,[],2);
        Assert.Throws<InvalidOperationException>(()=>exporter.Export(m with {Assignment=wrong}));
    }
    public static ReportModel Model() => new(new("demo-project","Demo <script>","SI"),DemoFixtures.Calculation(),
        DemoFixtures.Manifest(),DemoFixtures.Model(),[new("B1-B2","B1","EqualSplit",0,10,2100)],
        [new("B1","WALL","Illustrative",3,-750)],null,null,null,new("report-demo"));
    [Fact] public void Export_ReproducibleAndSyntheticDisclosure()
    {
        var r=new MarkdownReportExporter();var m=Model();var a=r.Export(m);
        Assert.Equal(a,r.Export(m));Assert.Contains("DEMONSTRATION DATA - NOT FOR ENGINEERING DESIGN",a);
        Assert.Contains(m.Calculation.Id,a);Assert.Contains(m.Manifest.Hash,a);Assert.Contains("2100",a);
    }
    [Fact] public void Html_EncodesInputAndRendersRoofDiagram()
    {
        var text=new HtmlReportExporter().Export(Model());
        Assert.DoesNotContain("<script>",text);Assert.Contains("&lt;script&gt;",text);Assert.Contains("<svg",text);
    }
    [Fact] public void Definition_ControlsSectionsWithoutChangingSnapshots()
    {
        var m=Model();var before=SnapshotJson.Serialize(m.Calculation);
        var text=new MarkdownReportExporter().Export(m with {Definition=new("no-facade",false,false,false)});
        Assert.DoesNotContain("Frame line loads",text);Assert.Equal(before,SnapshotJson.Serialize(m.Calculation));
    }
}
