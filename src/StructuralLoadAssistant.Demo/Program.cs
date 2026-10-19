using System.Text.Json;
using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.RoofMapping;

var output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/demo");Directory.CreateDirectory(output);
var session=new DemoSession(Path.Combine(output,"project.db"));session.LoadDemoFixture();
Console.WriteLine("DEMO MODE | SYNTHETIC WIND PRESSURES | NOT FOR ENGINEERING DESIGN | REAL CSI WRITES DISABLED");
foreach(var method in Enum.GetValues<FacadeMethod>())
{
    session.RecalculateFacade(method);
    foreach(var row in session.FacadeRows) Console.WriteLine($"{method}: {row.Frame}: {row.LineLoadNPerM/1000:F3} kN/m (net inward)");
}
var blocked=session.ValidateRoof();Console.WriteLine($"Initial mapping blockers: {blocked.Count(i=>i.Blocking)}");
session.ExcludeShell("RX","Demo engineer","NeedsSplitInCSI: crossing shell excluded from this simulated assignment");
session.SetSign("R1",ManualLocal3Direction.Positive,false,"Demo engineer","Local3 points outward, suction requires positive load");
session.SetSign("R2",ManualLocal3Direction.Negative,false,"Demo engineer","Local3 points inward, suction requires negative load");
var issues=session.ValidateRoof();
if(issues.Any(i=>i.Blocking)) throw new InvalidOperationException(string.Join("; ",issues.Select(i=>i.Message)));
var approval=session.ApproveRoof("Demo engineer");var preview=session.PreviewAssignment();
foreach(var p in preview.ProposedLoads) Console.WriteLine($"{p.ShellId}: p_net={p.NetPressurePa} Pa -> Local3={p.SignedCsiAreaLoadPa} Pa");
var run=await session.ApplyAsync();var verification=await session.VerifyAsync();
if(!verification.Matches) throw new InvalidOperationException("Fake readback failed");
await session.PreviewFacadeAsync();await session.ApproveFacadeAsync("Demo engineer");
var facadeRun=await session.ApplyFacadeAsync();var facadeReadback=await session.VerifyFacadeAsync();
if(!facadeReadback.Matches)throw new InvalidOperationException("Facade fake readback failed");
var reportId=session.SaveReport();session.ExportReport(Path.Combine(output,"report.md"));session.ExportReport(Path.Combine(output,"report.html"));
var summary=new {SchemaVersion=1,session.Project.Id,CalculationRunId=session.Calculation.Id,ApprovalId=approval.Id,
    AssignmentId=run.Id,ReportId=reportId,IsSimulation=run.IsSimulation,ReadbackMatches=verification.Matches,
    run.PreservedManualLoadCount,FacadeAssignmentId=facadeRun.Id,FacadeReadback=facadeReadback.Matches,RoofState=session.Roof.State.ToString(),StandardStatus=session.Manifest.Status.ToString(),RealWritesEnabled=false};
File.WriteAllText(Path.Combine(output,"session-state.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"fixture-model.json"),SnapshotJson.Serialize(DemoFixtures.Model()));
Console.WriteLine(JsonSerializer.Serialize(summary));Console.WriteLine("Output: "+output);
