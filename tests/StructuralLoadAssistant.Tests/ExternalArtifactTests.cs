using System.Collections.Immutable;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Reporting;
namespace StructuralLoadAssistant.Tests;
public class ExternalArtifactTests
{
    [Fact]
    public async Task GenerateFlatAndSingleFace14DegreeReviewExamples()
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/external-only/examples"));
        foreach(double pitch in new[]{0d,14d})
        {
            string folder=Path.Combine(root,pitch==0?"flat":"single-face-14");Directory.CreateDirectory(folder);
            var request=ExternalWindFixture.Request(pitch);var wall=request.Surfaces[0].Polygon;var roof=request.Surfaces[1].Polygon;
            var model=ExternalWindFixture.Model() with{Frames=ExternalWindFixture.Model().Frames.AddRange(ImmutableArray.Create(new FrameGeometry("eave","roof",roof.Vertices[0],roof.Vertices[1]),new FrameGeometry("upper","roof",roof.Vertices[3],roof.Vertices[2])))};
            var transfers=request.Surfaces.Select(s=>new ExternalTransferSelection($"{s.State}_{s.WindFrom}_{s.Id}_{s.Alternative}",s.Id=="wall"?ExternalWindFixture.Support():new("roof",["eave"],["upper"],"Synthetic reviewer","One-way plane to its two support chains",true))).ToImmutableArray();
            var preview=await ExternalWindReviewCoordinator.PreviewAsync(request,model,transfers);
            Assert.Equal(16,preview.Plans.Length);Assert.Empty(preview.Blockers);
            foreach(var p in preview.Plans){var fake=new FakePerimeterFrameLoadAdapter(model);fake.Apply(p,model);Assert.True(fake.Verify(p));Assert.All(p.Checks,c=>Assert.True(c.Passed));}
            foreach(bool audit in new[]{false,true})
            {
                string html=ExternalWindReportBuilder.BuildHtml(preview.Run,model,preview.Plans,preview.Blockers,audit),stem=audit?"audit":"worksheet";
                File.WriteAllText(Path.Combine(folder,stem+".html"),html);
                await new WindPdfRenderService().RenderHtmlToPdfAsync(html,Path.Combine(folder,stem+".pdf"));
            }
            File.WriteAllText(Path.Combine(folder,"frame-schedule.json"),ExternalWindReportBuilder.ScheduleJson(preview.Run,model,preview.Plans));
            File.WriteAllText(Path.Combine(folder,"frame-schedule.csv"),ExternalWindReportBuilder.ScheduleCsv(preview.Run,model,preview.Plans));
            File.WriteAllText(Path.Combine(folder,"calculation.json"),SnapshotJson.Serialize(preview.Run));
            File.WriteAllText(Path.Combine(folder,"request.json"),SnapshotJson.Serialize(new {Request=request,Transfers=transfers}));
            File.WriteAllText(Path.Combine(folder,"model.json"),SnapshotJson.Serialize(model));
        }
    }
}
