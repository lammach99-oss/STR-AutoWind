using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Reporting;

namespace StructuralLoadAssistant.Tests;

/// <summary>All files and numerical operands here are invented software fixtures, never licensed source cells.</summary>
public sealed class AsNzPrivateStagingTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"asnz-stage-"+Guid.NewGuid().ToString("N"),"fixtures","standards","private","synthetic");
    public AsNzPrivateStagingTests()=>Directory.CreateDirectory(root);
    public void Dispose()=>Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(root)!)!)!)!,true);
    PrivateEvidenceFile FileEvidence(string name,string text)
    {
        File.WriteAllText(Path.Combine(root,name),text);
        return new(name,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }
    AsNzSourceStaging Valid()
    {
        var source=FileEvidence("source.txt","INVENTED SOURCE FOR SOFTWARE TESTS ONLY");
        var rights=FileEvidence("rights.txt","INVENTED TEST EVIDENCE / NOT A REAL LICENSE");
        var adoption=FileEvidence("adoption.txt","INVENTED BASE REVIEW DECISION / NOT CODE ADOPTION");
        var check=FileEvidence("reconciliation.txt","INVENTED SECOND SOURCE READ / NOT ENGINEERING REVIEW");
        var r=AsNzAutoLookupFixture.Source().Bundle.Records.First(r=>r.ParameterCategory=="VR");
        r=r with {CategoricalConditions=r.CategoricalConditions.SetItem("ProcedureId",AsNz2021LocalAutoLookupService.RoofMethod).SetItem("Method","RoofFormScoped")};
        var cell=new AsNzSourceCell(r,"source",r.TableOrFigure,1,"TEST","row-500","column-test","ULS","NotApplicable",SourceCellStatus.SOURCE_CELL_CHECKED,"Transcriber");
        cell=cell with {Reconciliation=new("Second reader",AsNzPrivateDatasetPipeline.CellHash(cell),source.Sha256,r.Value,check)};
        return new("1.0","SYNTHETIC-PRIVATE-STAGE","AS/NZS 1170.2:2021",[],"AU","SYNTHETIC TEST JURISDICTION",
            new("PERMITTED_LOCAL_REVIEW","Test evidence reviewer","Synthetic license scope",rights,true,true,true,[source.Sha256]),
            new("AU","SYNTHETIC TEST JURISDICTION","AS/NZS 1170.2:2021",adoption,[new(r.TableOrFigure,"BASE_TEXT_REVIEW_ONLY","NOT_APPLICABLE","NOT_APPLICABLE","Synthetic scope decision")]),
            [new("source",source.File,source.Sha256,"AS/NZS 1170.2:2021")],[cell]);
    }
    string WriteStage(AsNzSourceStaging stage)
    {
        var path=Path.Combine(root,"source-staging.json");File.WriteAllText(path,JsonSerializer.Serialize(stage,AsNzPrivateStagingJson.Options));return path;
    }
    [Fact]
    public void VerifiedStageParserUsesCapturedBytesAndRejectsDifferentBytesUnderSameHash()
    {
        var original=Valid();var path=WriteStage(original);var verified=File.ReadAllBytes(path);
        var hash=AsNzPrivateDatasetPipeline.Sha(verified);
        WriteStage(original with {DatasetId="REPLACED AFTER HASH CHECK"});
        var parsed=AsNzPrivateDatasetPipeline.ParseVerifiedStage(verified,hash);
        Assert.Equal(original.DatasetId,parsed.DatasetId);
        Assert.Throws<InvalidDataException>(()=>AsNzPrivateDatasetPipeline.ParseVerifiedStage(File.ReadAllBytes(path),hash));
    }
    [Fact]
    public void CellCheckedStageIsReviewReadyButNeverNormativelyQualified()
    {
        var report=AsNzPrivateDatasetPipeline.Validate(Valid(),root);
        Assert.True(report.ReadyForPrivateReview,string.Join(";",report.Issues));Assert.Equal(1,report.SourceCellChecked);
        Assert.False(report.CanCalculateNormativePressures);Assert.False(report.CanMutateCsi);
        Assert.Equal("NOT_ASSESSED",report.WorkflowCoverageStatus);Assert.False(report.CompleteFlatRoofCoverage);
    }
    [Fact]
    public void InventoryOnlySourceCannotBecomeReadyEvenWhenAnOlderCopyAndRightsFilesExist()
    {
        var stage=Valid();stage=stage with {Sources=[stage.Sources[0] with {ReferenceOnly=true}]};
        var report=AsNzPrivateDatasetPipeline.Validate(stage,root);
        Assert.False(report.ReadyForPrivateReview);
        Assert.Contains(report.Issues,i=>i.Code=="SOURCE_REFERENCE_ONLY");
        Assert.False(report.CompleteFlatRoofCoverage);
    }
    [Theory]
    [InlineData("rights","RIGHTS_EVIDENCE_REQUIRED")]
    [InlineData("source","SOURCE_HASH_MISMATCH")]
    [InlineData("edition","EDITION_UNSUPPORTED")]
    [InlineData("adoption","TABLE_ADOPTION_UNRESOLVED")]
    [InlineData("selfcheck","RECONCILIATION_REQUIRED")]
    [InlineData("value","RECONCILIATION_MISMATCH")]
    [InlineData("locator","SOURCE_CELL_LOCATOR_REQUIRED")]
    [InlineData("draft","SOURCE_CELL_UNREVIEWED")]
    [InlineData("forgedhash","RECONCILIATION_MISMATCH")]
    public void InvalidOrSelfAssertedEvidenceFailsClosed(string mutation,string code)
    {
        var s=Valid();var c=s.Cells[0];
        switch(mutation)
        {
            case "rights":s=s with {Rights=s.Rights! with {Evidence=null}};break;
            case "source":File.AppendAllText(Path.Combine(root,"source.txt"),"changed");break;
            case "edition":s=s with {Edition="AS/NZS 1170.2:2011"};break;
            case "adoption":s=s with {Adoption=s.Adoption! with {Tables=[]}};break;
            case "selfcheck":s=s with {Cells=[c with {Reconciliation=c.Reconciliation! with {Checker=c.Transcriber}}]};break;
            case "value":s=s with {Cells=[c with {Record=c.Record! with {Value=c.Record.Value+1}}]};break;
            case "locator":s=s with {Cells=[c with {Column=""}]};break;
            case "draft":s=s with {Cells=[c with {Status=SourceCellStatus.TRANSCRIBED_UNREVIEWED,Reconciliation=null}]};break;
            case "forgedhash":s=s with {Cells=[c with {Reconciliation=c.Reconciliation! with {SourceSha256=new string('0',64)}}]};break;
        }
        var report=AsNzPrivateDatasetPipeline.Validate(s,root);
        Assert.False(report.ReadyForPrivateReview);Assert.Contains(report.Issues,i=>i.Code==code);
    }
    [Fact]
    public void ContentHashBindsSourceLocatorApplicabilityAndNumericalRecord()
    {
        var cell=Valid().Cells[0];var hash=AsNzPrivateDatasetPipeline.CellHash(cell);
        Assert.Equal(64,hash.Length);
        Assert.NotEqual(hash,AsNzPrivateDatasetPipeline.CellHash(cell with {Row="another"}));
        Assert.NotEqual(hash,AsNzPrivateDatasetPipeline.CellHash(cell with {Record=cell.Record! with {ApplicabilityNotes="changed"}}));
    }
    [Fact]
    public void MalformedStageAndUnsafeSourcePathReturnStructuredIssues()
    {
        var stage=Valid();var path=WriteStage(stage);File.WriteAllText(path,"{");
        Assert.Contains(AsNzPrivateDatasetPipeline.ValidateFile(path).Issues,i=>i.Code=="STAGING_JSON_INVALID");
        var unsafeStage=stage with {Sources=[stage.Sources[0] with {File="../outside.txt"}]};
        Assert.Contains(AsNzPrivateDatasetPipeline.Validate(unsafeStage,root).Issues,i=>i.Code=="SOURCE_PATH_UNSAFE");
    }
    [Fact]
    public void PackageUsesV3AndRoundTripsThroughActualPrivateSourceSelector()
    {
        var stage=Valid();var output=Path.Combine(Path.GetDirectoryName(root)!,"package");
        var result=AsNzPrivateDatasetPipeline.Package(WriteStage(stage),output);
        Assert.True(result.ReadyForPrivateReview,string.Join(";",result.Issues));
        var bundle=StandardsDataBundle.LoadFromDirectory(output);
        Assert.Equal("3.0",bundle.Manifest.CanonicalHashAlgorithmVersion);
        Assert.Equal(bundle.Manifest.ContentHash,bundle.ComputeCanonicalHash());
        Assert.True(StandardsDataBundleValidator.GenerateIntakePreflight(bundle).ComponentBytesCoherent);
        var source=AsNzPrivateAutoSourceLoader.Load(output);var row=bundle.Records[0];
        var selected=new AsNz2021LocalAutoLookupService().Select(source,"VR","VR",row.CategoricalConditions,row.NumericalConditions);
        Assert.Equal(AutoLookupState.Resolved,selected.State);Assert.Equal(row.Value,selected.Value);
        File.AppendAllText(Path.Combine(output,"records.json")," ");
        Assert.ThrowsAny<Exception>(()=>AsNzPrivateAutoSourceLoader.Load(output));
    }
    [Fact]
    public void BlockedDraftCannotCreateAPackageOrOverwriteExistingOutput()
    {
        var stage=Valid();var output=Path.Combine(Path.GetDirectoryName(root)!,"blocked-package");
        var path=WriteStage(stage with {Rights=null});
        Assert.False(AsNzPrivateDatasetPipeline.Package(path,output).ReadyForPrivateReview);Assert.False(Directory.Exists(output));
        WriteStage(stage);Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"preserved.txt"),"keep");
        Assert.Contains(AsNzPrivateDatasetPipeline.Package(path,output).Issues,i=>i.Code=="OUTPUT_EXISTS");
        Assert.Equal("keep",File.ReadAllText(Path.Combine(output,"preserved.txt")));
    }
    [Fact]
    public void TruthyLegacyAuthorizationCannotReplaceSourceReconciliation()
    {
        var source=AsNzAutoLookupFixture.Source();
        var row=source.Bundle.Records.First(r=>r.ParameterCategory=="VR");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(source.Bundle.Records,AsNzPrivateStagingJson.Options);
        File.WriteAllBytes(Path.Combine(root,"records.json"),bytes);
        var manifest=source.Bundle.Manifest with {Rights=DataRightsClassification.Restricted,ComponentFiles=ImmutableDictionary<string,string>.Empty.Add("records.json",Convert.ToHexString(SHA256.HashData(bytes)))};
        var bundle=new StandardsDataBundle(manifest,source.Bundle.Records,rawComponentBytes:ImmutableDictionary<string,ImmutableArray<byte>>.Empty.Add("records.json",bytes.ToImmutableArray()),isBytesVerified:true);
        File.WriteAllText(Path.Combine(root,"manifest.json"),JsonSerializer.Serialize(manifest with {ContentHash=bundle.ComputeCanonicalHash()},AsNzPrivateStagingJson.Options));
        var evidence=FileEvidence("boolean-only.txt","true");
        File.WriteAllText(Path.Combine(root,"auto-authorization.json"),JsonSerializer.Serialize(new PrivateAutoAuthorization(true,evidence.File,evidence.Sha256,evidence.File,evidence.Sha256,ImmutableDictionary<string,string>.Empty.Add(row.TableOrFigure,"claimed"))));
        Assert.ThrowsAny<Exception>(()=>AsNzPrivateAutoSourceLoader.Load(root));
    }
    [Fact]
    public void PackagedSourceProvenanceReachesRequestTraceAndBlockedWorksheetAudit()
    {
        var stage=Valid();var path=WriteStage(stage);var output=Path.Combine(Path.GetDirectoryName(root)!,"report-package");
        Assert.True(AsNzPrivateDatasetPipeline.Package(path,output).ReadyForPrivateReview);
        var source=AsNzPrivateAutoSourceLoader.Load(output);
        var result=new AsNz2021LocalAutoLookupService().Resolve(AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site(),source);
        Assert.False(result.NumericalReviewPossible); // A source cell is not a complete standards dataset.
        var coverage=AsNzFlatRoofCoverage.Evaluate(new("single-cell-package",AsNzAutoLookupFixture.Geometry(),AsNzAutoLookupFixture.Site()),source);
        Assert.False(coverage.ScenarioInputsResolved);Assert.Contains(coverage.Lookups,v=>v.State!=AutoLookupState.Resolved);
        var requestEvidence=result.Request.ExplicitCoefficients["VR_Uls"];
        Assert.Contains(stage.Sources[0].Sha256,requestEvidence.ClauseOrTable);
        var run=AsNz2021ReviewCalculation.Execute(result.Request);
        Assert.Contains(stage.Sources[0].Sha256,run.TraceNodes.Single(t=>t.NodeId=="INPUT-VR_Uls").SourceReference);
        Assert.False(run.NumericalComputationPossible);Assert.All(run.AsNz2021Cases,c=>Assert.Null(c.NetPa));
        var doc=WindPdfReportDocumentBuilder.Build(new ReportModel(new Project(run.Request.ProjectId,"Synthetic staged source review","SI"),DemoFixtures.Calculation(run.Request.StandardEdition),DemoFixtures.Manifest(run.Request.StandardEdition),DemoFixtures.Model(),[],[],null,null,null,new ReportDefinition("staged-source")),run);
        var renderer=new AsNz2021WorkbookPdfRenderer();
        Assert.Contains(stage.Sources[0].Sha256,renderer.BuildAuditHtml(doc));
        Assert.Contains("SOURCE_CELL_CHECKED",renderer.BuildAuditHtml(doc));
        Assert.Contains("NOT CALCULATED",renderer.BuildHtml(doc));
    }
}
