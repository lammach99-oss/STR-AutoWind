using System.Collections.Immutable;
using System.Text.Json;
namespace StructuralLoadAssistant.Engineering;

public static class AsNzPrivateIntakeCommand
{
    public static int Run(string[] args,TextWriter output)
    {
        try
        {
            if(args.Length==3&&args[0]=="coverage")
            {
                var coverage=AsNzFlatRoofCoverage.EvaluateFiles(args[1],args[2]);
                output.WriteLine(JsonSerializer.Serialize(coverage,AsNzPrivateStagingJson.Options));
                return coverage.ScenarioInputsResolved?0:2;
            }
            PrivateStagingReport report;
            if(args.Length==4&&args[0]=="initialize")report=Initialize(args[1],args[2],args[3]);
            else if(args.Length==2&&args[0]=="validate")report=AsNzPrivateDatasetPipeline.ValidateFile(args[1]);
            else if(args.Length==3&&args[0]=="package")report=AsNzPrivateDatasetPipeline.Package(args[1],args[2]);
            else {output.WriteLine("Usage: --asnz-private-intake initialize <new-private-directory> <existing-pdf> <existing-workbook> | validate <source-staging.json> | package <source-staging.json> <new-private-directory> | coverage <private-package-directory> <scenario.json>");return 1;}
            output.WriteLine(JsonSerializer.Serialize(report,AsNzPrivateStagingJson.Options));
            return report.ReadyForPrivateReview?0:2;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            output.WriteLine(JsonSerializer.Serialize(new PrivateStagingReport("BLOCKED",0,0,0,0,[new("INTAKE_COMMAND_FAILED","Command",e.Message)]),AsNzPrivateStagingJson.Options));return 2;
        }
    }
    static PrivateStagingReport Initialize(string directory,string pdf,string workbook)
    {
        string target=Path.GetFullPath(directory);
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("Existing staging versions are never overwritten");
        bool privateRoot=false;
        for(var d=new DirectoryInfo(Path.GetDirectoryName(target)!);d!=null;d=d.Parent)
            if(d.Name=="private"&&d.Parent?.Name=="standards"&&d.Parent.Parent?.Name=="fixtures")privateRoot=true;
        if(!privateRoot)throw new ArgumentException("Staging directory must be under fixtures/standards/private");
        AsNzPrivateDatasetPipeline.SafePath(Path.GetDirectoryName(target)!,Path.GetFileName(target));
        string HashShared(string path)
        {
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        }
        var sourceHash=HashShared(pdf);var workbookHash=HashShared(workbook);
        var tables=new (string Id,int Page,string Unit)[]{("3.1(A)",35,"m/s"),("3.1(B)",36,"m/s"),("3.2(A)",37,"dimensionless"),("3.2(B)",37,"dimensionless"),("3.3",37,"dimensionless"),("4.1",41,"dimensionless"),("4.2",42,"dimensionless"),("5.2(A)",59,"dimensionless"),("5.2(B)",60,"dimensionless"),("5.2(C)",60,"dimensionless"),("5.3(A)",61,"dimensionless"),("5.3(B)",61,"dimensionless"),("5.3(C)",61,"dimensionless")};
        var stage=new AsNzSourceStaging("1.0",Path.GetFileName(target),AsNzPrivateDatasetPipeline.Edition,[],"","",
            new("UNCONFIRMED","","",null,false,false,false,[]),new("","",AsNzPrivateDatasetPipeline.Edition,null,
                tables.Select(t=>new PrivateTableAdoption("Table "+t.Id,"BASE_TEXT_REVIEW_ONLY","UNCONFIRMED","UNCONFIRMED","")).ToImmutableArray()),
            [new("publication","source/publication.pdf",sourceHash,AsNzPrivateDatasetPipeline.Edition,ReferenceOnly:true)],
            tables.Select(t=>new AsNzSourceCell(null,"publication","Table "+t.Id,t.Page,(t.Page-10).ToString(System.Globalization.CultureInfo.InvariantCulture),"","","","",SourceCellStatus.NOT_AVAILABLE,"")).ToImmutableArray());
        // Inventory the existing files without materializing any controlled source bytes.
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target,"source-staging.json"),JsonSerializer.Serialize(stage,AsNzPrivateStagingJson.Options));
        var template=new StandardsDataBundleManifest(stage.DatasetId,"1.0",StandardFamily.AsNz1170_2,stage.Edition,[],DataRightsClassification.Unknown,
            "UNCONFIRMED - no extraction/storage/software-use authorization asserted","",ImmutableDictionary<string,string>.Empty,
            new("","",DateTimeOffset.UnixEpoch,"","",null,"NOT REVIEWED","","",false),[],"PRIVATE STAGING TEMPLATE ONLY",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"3.0");
        File.WriteAllText(Path.Combine(target,"manifest.template.json"),JsonSerializer.Serialize(template,AsNzPrivateStagingJson.Options));
        File.WriteAllText(Path.Combine(target,"source-staging.schema.json"),Schema);
        File.WriteAllText(Path.Combine(target,"source-inventory.json"),JsonSerializer.Serialize(new {TechnicalReference="AS/NZS 1170.2:2021 BASE TEXT — REFERENCE/REVIEW ONLY",SourceSha256=sourceHash,WorkbookSha256=workbookHash,ExistingSourcePath=Path.GetFullPath(pdf),ExistingWorkbookPath=Path.GetFullPath(workbook),ControlledReferencesCopied=false,WorkbookRole="Layout/arithmetic reference only; not independent benchmark",Rights="UNCONFIRMED",Amendment1="UNCONFIRMED",Amendment2="UNCONFIRMED",NumericalCellsTranscribed=0,Tables=tables.Select(t=>new {Table=t.Id,PdfPage=t.Page,PrintedPage=t.Page-10,t.Unit})},AsNzPrivateStagingJson.Options));
        return AsNzPrivateDatasetPipeline.Validate(stage,target);
    }
    const string Schema="""
    {
      "$schema":"https://json-schema.org/draft/2020-12/schema",
      "title":"Private ASNZ source staging 1.0; runtime validator is authoritative",
      "type":"object","additionalProperties":false,
      "required":["SchemaVersion","DatasetId","Edition","Amendments","Country","Jurisdiction","Rights","Adoption","Sources","Cells"],
      "properties":{
        "SchemaVersion":{"const":"1.0"},"DatasetId":{"type":"string","minLength":1},
        "Edition":{"const":"AS/NZS 1170.2:2021"},"Amendments":{"type":"array","items":{"type":"string"}},
        "Country":{"type":"string","enum":["","AU","NZ"]},"Jurisdiction":{"type":"string"},
        "Rights":{"type":["object","null"]},"Adoption":{"type":["object","null"]},
        "Sources":{"type":"array","items":{"type":"object","required":["Id","File","Sha256","Edition"]}},
        "Cells":{"type":"array","items":{"$ref":"#/$defs/sourceCell"}}
      },
      "$defs":{"sourceCell":{"type":"object","additionalProperties":false,
        "required":["Record","SourceId","Table","PdfPage","PrintedPage","Row","Column","Alternative","WindIncidence","Status","Transcriber"],
        "properties":{
          "Record":{"type":["object","null"]},"SourceId":{"type":"string"},"Table":{"type":"string"},
          "PdfPage":{"type":"integer","minimum":1},"PrintedPage":{"type":"string"},"Row":{"type":"string"},"Column":{"type":"string"},
          "Alternative":{"type":"string"},"WindIncidence":{"type":"string"},"Transcriber":{"type":"string"},
          "Status":{"enum":["NOT_AVAILABLE","TRANSCRIBED_UNREVIEWED","SOURCE_CELL_CHECKED","ENGINEER_REVIEWED"]},
          "Reconciliation":{"type":["object","null"]},"EngineerReview":{"type":["object","null"]}
      }}}
    }
    """;
}
