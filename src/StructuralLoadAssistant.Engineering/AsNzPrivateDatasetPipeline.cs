using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace StructuralLoadAssistant.Engineering;

/// <summary>Local source-cell staging and packaging; never authorizes normative calculation or CSI writes.</summary>
public static class AsNzPrivateDatasetPipeline
{
    public const string Edition="AS/NZS 1170.2:2021";
    public static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    static bool Same(string? a,string? b)=>!string.IsNullOrWhiteSpace(a)&&string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    static bool Expected(Exception e)=>e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or NotSupportedException or NullReferenceException;
    static PrivateStagingReport Failure(string code,string message)=>new("BLOCKED",0,0,0,0,[new(code,"Staging",message)]);

    // Structured JSON and sorted dictionaries avoid delimiter or insertion-order ambiguity.
    public static string CellHash(AsNzSourceCell cell)=>Sha(JsonSerializer.SerializeToUtf8Bytes(new
    {
        cell.SourceId,cell.Table,cell.PdfPage,cell.PrintedPage,cell.Row,cell.Column,cell.Alternative,cell.WindIncidence,cell.Transcriber,
        Record=cell.Record is not { } r?null:new {r.RecordKey,r.TableOrFigure,r.ClauseReference,r.ParameterCategory,
            Categorical=r.CategoricalConditions.ToImmutableSortedDictionary(StringComparer.Ordinal),
            Numerical=r.NumericalConditions.ToImmutableSortedDictionary(StringComparer.Ordinal),r.Value,r.Unit,r.SignConvention,r.Interpolation,r.ApplicabilityNotes}
    },AsNzPrivateStagingJson.Options));

    internal static string SafePath(string directory,string relative)
    {
        StandardsDataBundle.ValidateSafeRelativePath(relative);
        string root=Path.GetFullPath(directory),path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Path leaves private staging directory");
        for(string? item=path;item!=null;item=Path.GetDirectoryName(item))
        {
            if((File.Exists(item)||Directory.Exists(item))&&(File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Reparse paths cannot provide private evidence");
        }
        return path;
    }
    internal static byte[] ReadChecked(string directory,PrivateEvidenceFile evidence)
    {
        var bytes=File.ReadAllBytes(SafePath(directory,evidence.File));
        if(!Same(Sha(bytes),evidence.Sha256))throw new InvalidDataException("Evidence bytes differ from declared SHA-256");
        return bytes;
    }
    public static PrivateStagingReport Validate(AsNzSourceStaging stage,string directory)
    {
        try{return ValidateCore(stage,directory);}
        catch(Exception e)when(Expected(e)){return Failure("STAGING_INVALID",e.Message);}
    }
    static PrivateStagingReport ValidateCore(AsNzSourceStaging stage,string directory)
    {
        var issues=ImmutableArray.CreateBuilder<PrivateStagingIssue>();
        void Issue(string code,string at,string text)=>issues.Add(new(code,at,text));
        bool Evidence(PrivateEvidenceFile? file,string code,string at)
        {
            try{if(file==null)throw new InvalidDataException("Evidence reference required");ReadChecked(directory,file);return true;}
            catch(Exception e)when(Expected(e)){Issue(code,at,e.Message);return false;}
        }
        if(stage.SchemaVersion!="1.0"||string.IsNullOrWhiteSpace(stage.DatasetId))Issue("STAGING_IDENTITY_INVALID","Manifest","Schema 1.0 and explicit dataset ID required");
        if(stage.Edition!=Edition)Issue("EDITION_UNSUPPORTED","Manifest", "Exact base-2021 technical reference required");
        if(stage.Amendments.IsDefault||stage.Amendments.Any(a=>a is not ("Amendment 1:2023" or "Amendment 2:2024"))||stage.Amendments.Distinct(StringComparer.Ordinal).Count()!=stage.Amendments.Length)
            Issue("AMENDMENT_IDENTITY_INVALID","Manifest","Only unique explicit Amendment 1:2023 / Amendment 2:2024 identities supported");
        if(stage.Country is not ("AU" or "NZ")||string.IsNullOrWhiteSpace(stage.Jurisdiction))Issue("JURISDICTION_REQUIRED","Manifest","Country and project-specific jurisdiction must be explicit");
        var sources=stage.Sources.IsDefault?ImmutableArray<AsNzSourceDocument>.Empty:stage.Sources;
        var cells=stage.Cells.IsDefault?ImmutableArray<AsNzSourceCell>.Empty:stage.Cells;
        if(sources.Length==0)Issue("SOURCE_REQUIRED","Sources","At least one identified source document required");
        if(sources.GroupBy(s=>s.Id,StringComparer.Ordinal).Any(g=>g.Count()!=1))Issue("SOURCE_ID_DUPLICATE","Sources","Source IDs must be unique");
        foreach(var source in sources)
        {
            if(source.Edition!=stage.Edition&&!stage.Amendments.Contains(source.Edition,StringComparer.Ordinal))Issue("SOURCE_EDITION_MISMATCH",source.Id,"Source edition differs from staging edition or selected amendments");
            try{ReadChecked(directory,new(source.File,source.Sha256));}
            catch(ArgumentException e){Issue("SOURCE_PATH_UNSAFE",source.Id,e.Message);}
            catch(Exception e)when(Expected(e)){Issue("SOURCE_HASH_MISMATCH",source.Id,e.Message);}
        }
        var rights=stage.Rights;
        Evidence(rights?.Evidence,"RIGHTS_EVIDENCE_REQUIRED","Rights");
        if(rights==null||rights.Decision!="PERMITTED_LOCAL_REVIEW"||!rights.ExtractionPermitted||!rights.PrivateStoragePermitted||!rights.SoftwareUsePermitted||
            string.IsNullOrWhiteSpace(rights.Reviewer)||string.IsNullOrWhiteSpace(rights.DecisionReference)||rights.SourceHashes.IsDefault||sources.Any(s=>!rights.SourceHashes.Contains(s.Sha256,StringComparer.OrdinalIgnoreCase)))
            Issue("RIGHTS_DECISION_REQUIRED","Rights","Documented source-bound extraction/storage/software-use review required; flags alone do not establish rights");
        var adoption=stage.Adoption;
        Evidence(adoption?.Evidence,"ADOPTION_EVIDENCE_REQUIRED","Adoption");
        if(adoption==null||adoption.Country!=stage.Country||adoption.Jurisdiction!=stage.Jurisdiction||adoption.Edition!=stage.Edition)
            Issue("ADOPTION_SCOPE_MISMATCH","Adoption","Country, jurisdiction and edition must match exactly");
        var decisions=adoption?.Tables.IsDefault==false?adoption.Tables:[];
        if(decisions.GroupBy(t=>t.Table,StringComparer.Ordinal).Any(g=>g.Count()!=1))Issue("TABLE_ADOPTION_UNRESOLVED","Adoption","Duplicate table decision");
        if(cells.Length==0)Issue("SOURCE_CELLS_EMPTY","Cells","No source rows staged; numerical dataset unavailable");
        if(cells.Where(c=>c.Record!=null).GroupBy(c=>c.Record!.RecordKey,StringComparer.Ordinal).Any(g=>g.Count()!=1))Issue("RECORD_KEY_DUPLICATE","Cells","Record IDs must be unique");
        int transcribed=0,checkedCount=0,engineer=0,missing=0;
        foreach(var cell in cells)
        {
            string at=cell.Record?.RecordKey??cell.Table;
            if(cell.Status==SourceCellStatus.NOT_AVAILABLE){missing++;Issue("SOURCE_CELL_NOT_AVAILABLE",at,"Missing source value; no numeric substitute allowed");continue;}
            transcribed++;
            var r=cell.Record;
            if(r==null){Issue("RECORD_REQUIRED",at,"Transcribed cell needs a numerical record");continue;}
            if(!Enum.IsDefined(cell.Status))Issue("SOURCE_CELL_STATUS_INVALID",at,"Unknown source status");
            var source=sources.FirstOrDefault(s=>s.Id==cell.SourceId);
            if(source==null)Issue("SOURCE_ID_MISSING",at,"Cell source not declared");
            if(cell.PdfPage<=0||new[]{cell.Table,cell.PrintedPage,cell.Row,cell.Column,cell.Alternative,cell.WindIncidence,cell.Transcriber}.Any(string.IsNullOrWhiteSpace)||cell.Table!=r.TableOrFigure)
                Issue("SOURCE_CELL_LOCATOR_REQUIRED",at,"Exact page, row, column, case, incidence and transcriber required");
            if((r.CategoricalConditions.TryGetValue("Case",out var sourceCase)&&cell.Alternative!=sourceCase)||
                (r.CategoricalConditions.TryGetValue("RoofFace",out var sourceFace)&&cell.WindIncidence!=sourceFace))
                Issue("SOURCE_CELL_SCOPE_MISMATCH",at,"Source-cell case/face must match the numerical lookup record exactly");
            if(!double.IsFinite(r.Value)||r.NumericalConditions.Values.Any(v=>!double.IsFinite(v))||!Enum.IsDefined(r.Interpolation)||string.IsNullOrWhiteSpace(r.ClauseReference)||string.IsNullOrWhiteSpace(r.SignConvention))Issue("RECORD_DOMAIN_INVALID",at,"Finite operands, explicit interpolation and source/sign references required");
            string unit=r.ParameterCategory is "VR" or "MinimumSpeed"?"m/s":r.ParameterCategory=="rho"?"kg/m3":"dimensionless";
            if(r.Unit!=unit)Issue("RECORD_UNIT_INVALID",at,"Source record must use declared SI category unit");
            if(new[]{"Country","Region","System","ProcedureId","Method","RoofForm","LimitState"}.Any(k=>!r.CategoricalConditions.TryGetValue(k,out var v)||string.IsNullOrWhiteSpace(v))||
                r.CategoricalConditions.GetValueOrDefault("Country")!=stage.Country||r.CategoricalConditions.GetValueOrDefault("ProcedureId")!=AsNz2021LocalAutoLookupService.RoofMethod||r.CategoricalConditions.GetValueOrDefault("Method")!="RoofFormScoped"||r.CategoricalConditions.GetValueOrDefault("System")!="MWFRS"||r.CategoricalConditions.GetValueOrDefault("LimitState") is not ("Uls" or "Sls"))
                Issue("PROCEDURE_SCOPE_INVALID",at,"Exact country, region, review procedure, method, roof form, system and limit state required");
            foreach(var code in RecordScopeIssues(r))Issue(code,at,"Required category domain or roof/surface applicability is missing or contradictory");
            var decision=decisions.FirstOrDefault(t=>t.Table==cell.Table);
            bool Resolved(string? d)=>d is "APPLIED" or "NOT_APPLICABLE";
            if(decision==null||decision.BaseTextDecision!="BASE_TEXT_REVIEW_ONLY"||!Resolved(decision.Amendment1Decision)||!Resolved(decision.Amendment2Decision)||string.IsNullOrWhiteSpace(decision.EvidenceLocator))Issue("TABLE_ADOPTION_UNRESOLVED",at,"Separate base and Amendment 1/2 table decisions required");
            else
            {
                foreach(var (id,d) in new[]{("Amendment 1:2023",decision.Amendment1Decision),("Amendment 2:2024",decision.Amendment2Decision)})
                {
                    bool selected=stage.Amendments.Contains(id,StringComparer.Ordinal);
                    if((d=="APPLIED")!=selected)Issue("AMENDMENT_IDENTITY_MISMATCH",at,"Table decision contradicts selected amendments");
                    if(selected&&!sources.Any(s=>s.Edition==id))Issue("AMENDMENT_SOURCE_REQUIRED",at,"Applied amendment text requires separately identified source evidence");
                }
            }
            if(cell.Status==SourceCellStatus.TRANSCRIBED_UNREVIEWED){Issue("SOURCE_CELL_UNREVIEWED",at,"A second source-cell read is required");continue;}
            var reconciliation=cell.Reconciliation;
            if(reconciliation==null||string.IsNullOrWhiteSpace(reconciliation.Checker)||string.Equals(reconciliation.Checker.Trim(),cell.Transcriber.Trim(),StringComparison.OrdinalIgnoreCase))Issue("RECONCILIATION_REQUIRED",at,"Checker must be separate from transcriber");
            else
            {
                bool valid=Same(reconciliation.CellHash,CellHash(cell))&&source!=null&&Same(reconciliation.SourceSha256,source.Sha256)&&reconciliation.ObservedValue==r.Value&&double.IsFinite(reconciliation.ObservedValue);
                bool evidence=Evidence(reconciliation.Evidence,"RECONCILIATION_EVIDENCE_REQUIRED",at);
                if(!valid)Issue("RECONCILIATION_MISMATCH",at,"Second reading must match entire cell identity, source bytes and exact numerical value");
                if(valid&&evidence)checkedCount++;
            }
            if(cell.Status==SourceCellStatus.ENGINEER_REVIEWED&&Evidence(cell.EngineerReview,"ENGINEER_EVIDENCE_REQUIRED",at))engineer++;
        }
        return new(issues.Count==0?"READY_FOR_PRIVATE_REVIEW":"BLOCKED",transcribed,checkedCount,engineer,missing,issues.ToImmutable());
    }
    /// <summary>Parse the authenticated snapshot, never reopening a mutable staging file.</summary>
    public static AsNzSourceStaging ParseVerifiedStage(byte[] bytes,string expectedSha256)
    {
        if(!Same(Sha(bytes),expectedSha256))throw new InvalidDataException("Staging snapshot hash mismatch");
        return ParseStage(new System.Text.UTF8Encoding(false,true).GetString(bytes));
    }
    internal static AsNzSourceStaging ReadStage(string file)=>ParseStage(File.ReadAllText(file));
    static AsNzSourceStaging ParseStage(string text)
    {
        text=text.TrimStart('\uFEFF');
        using var doc=JsonDocument.Parse(text);
        void Unique(JsonElement element)
        {
            if(element.ValueKind==JsonValueKind.Object)
            {
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var p in element.EnumerateObject()){if(!keys.Add(p.Name))throw new JsonException("Duplicate JSON property is not permitted");Unique(p.Value);}
            }
            else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())Unique(item);
        }
        Unique(doc.RootElement);
        return JsonSerializer.Deserialize<AsNzSourceStaging>(text,AsNzPrivateStagingJson.Options)??throw new InvalidDataException("Null source staging document");
    }
    static IEnumerable<string> RecordScopeIssues(CoefficientRecord r)
    {
        var cat=r.CategoricalConditions;var nums=r.NumericalConditions;
        if(cat.GetValueOrDefault("RoofForm") is not ("Flat" or "LowSlope" or "Monoslope" or "Gable" or "Hip"))yield return "ROOF_FORM_UNSUPPORTED";
        var required=r.ParameterCategory switch
        {
            "VR"=>new[]{"R"},"Md"=>new[]{"Bearing"},"Mz"=>new[]{"Height","Terrain"},"Ms"=>new[]{"Parameter","Height"},"Cpi"=>new[]{"Height"},
            "Mc" or "rho" or "Cdyn" or "MinimumSpeed" or "Cpe" or "Ka" or "Kce" or "Kl" or "Kp" or "Kci" or "Kv"=>[],_=>null
        };
        if(required==null){yield return "PARAMETER_CATEGORY_UNSUPPORTED";yield break;}
        if(required.Any(k=>!nums.ContainsKey(k)))yield return "NUMERICAL_DOMAIN_REQUIRED";
        if(r.ParameterCategory=="VR"&&nums.TryGetValue("R",out var period)&&period<=0)yield return "NUMERICAL_DOMAIN_INVALID";
        if(r.ParameterCategory=="Md"&&nums.TryGetValue("Bearing",out var bearing)&&(bearing<0||bearing>=360||bearing%45!=0))yield return "NUMERICAL_DOMAIN_INVALID";
        if(r.ParameterCategory=="Mz"&&(nums.GetValueOrDefault("Height")<=0||nums.GetValueOrDefault("Terrain")<1||nums.GetValueOrDefault("Terrain")>4))yield return "NUMERICAL_DOMAIN_INVALID";
        if(r.ParameterCategory!="Cpe")yield break;
        string surface=cat.GetValueOrDefault("Surface","");
        int c=-1,z=-1;
        if(!cat.TryGetValue("Case",out var alternative)||!int.TryParse(alternative,out c)||!cat.TryGetValue("Zone",out var zone)||!int.TryParse(zone,out z)||z<0)yield return "SURFACE_SCOPE_REQUIRED";
        if(surface=="Roof")
        {
            if(!nums.TryGetValue("Pitch",out var pitch)||!nums.TryGetValue("hd",out var hd)||hd<=0||!cat.TryGetValue("RoofFace",out var faceText)||!Enum.TryParse<AsNzRoofFace>(faceText,out var face)||!Enum.IsDefined(face))
            {yield return "ROOF_DOMAIN_REQUIRED";yield break;}
            var branch=AsNzRoofApplicability.Evaluate(cat.GetValueOrDefault("RoofForm",""),pitch,face);
            if(!branch.Supported||branch.Table!=r.TableOrFigure)yield return "ROOF_TABLE_MISMATCH";
            if(branch.Table=="Table 5.3(C)"&&pitch>=25&&nums.GetValueOrDefault("bd")<=0)yield return "NUMERICAL_DOMAIN_REQUIRED";
            if(c is not (1 or 2)||z>4||(branch.Table!="Table 5.3(A)"&&z!=0))yield return "SURFACE_SCOPE_REQUIRED";
        }
        else if(surface=="Windward")
        {
            if(nums.GetValueOrDefault("Height")<=0||cat.GetValueOrDefault("Support") is not ("GroundSupported" or "Elevated")||cat.GetValueOrDefault("SpeedBasis")!="AtMeanRoofHeight")yield return "WINDWARD_SCOPE_UNSUPPORTED";
        }
        else if(surface=="Leeward")
        {
            if(cat.GetValueOrDefault("RoofForm")=="Monoslope"||nums.GetValueOrDefault("db")<=0||!nums.ContainsKey("Pitch")||!cat.ContainsKey("Incidence"))yield return "LEEWARD_SCOPE_UNSUPPORTED";
        }
        else if(surface is not ("SideLeft" or "SideRight"))yield return "SURFACE_SCOPE_REQUIRED";
    }
    public static PrivateStagingReport ValidateFile(string file)
    {
        try{return Validate(ReadStage(file),Path.GetDirectoryName(Path.GetFullPath(file))!);}
        catch(Exception e)when(Expected(e)){return Failure("STAGING_JSON_INVALID",e.Message);}
    }
    internal static StandardsDataBundle BuildBundle(AsNzSourceStaging stage)
    {
        var records=stage.Cells.Select(c=>c.Record! with {ClauseReference=c.Record!.ClauseReference+$" | source {c.SourceId} SHA256 {stage.Sources.Single(s=>s.Id==c.SourceId).Sha256}; PDF {c.PdfPage}, printed {c.PrintedPage}; row {c.Row}; column {c.Column}; case {c.Alternative}; incidence {c.WindIncidence}; {c.Status}; cell SHA256 {CellHash(c)}",ApplicabilityNotes=(c.Record.ApplicabilityNotes??"")+" | transcription status "+c.Status+"; cell SHA256 "+CellHash(c)}).ToImmutableArray();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(records,AsNzPrivateStagingJson.Options);
        var raw=ImmutableDictionary<string,ImmutableArray<byte>>.Empty.Add("records.json",bytes.ToImmutableArray());
        var manifest=new StandardsDataBundleManifest(stage.DatasetId,"1.0",StandardFamily.AsNz1170_2,stage.Edition,stage.Amendments,DataRightsClassification.Restricted,
            "Private source-cell review; documentary permissions declared, not independently legally certified","",ImmutableDictionary<string,string>.Empty.Add("records.json",Sha(bytes)),
            new("Staging pipeline","Local",DateTimeOffset.UnixEpoch,"","",null,"No independent engineering qualification","","",false),[],
            "PRIVATE / NOT FOR REDISTRIBUTION / NOT QUALIFIED FOR DESIGN",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"3.0");
        var provisional=new StandardsDataBundle(manifest,records,rawComponentBytes:raw,isBytesVerified:true);
        return new(manifest with {ContentHash=provisional.ComputeCanonicalHash()},records,rawComponentBytes:raw,isBytesVerified:true);
    }
    public static PrivateStagingReport Package(string stageFile,string newOutputDirectory)
    {
        try
        {
            // Parse once: the reviewed snapshot is the snapshot subsequently packaged.
            var original=ReadStage(stageFile);string input=Path.GetDirectoryName(Path.GetFullPath(stageFile))!;
            var report=Validate(original,input);if(!report.ReadyForPrivateReview)return report;
            var output=Path.GetFullPath(newOutputDirectory);
            if(Directory.Exists(output)||File.Exists(output))return Failure("OUTPUT_EXISTS","Existing dataset versions are immutable; choose a new output directory");
            // The built-in publisher is deliberately restricted to the ignored private hierarchy.
            var parent=new DirectoryInfo(Path.GetDirectoryName(output)!);
            bool privateRoot=false;
            for(var d=parent;d!=null;d=d.Parent)if(d.Name=="private"&&d.Parent?.Name=="standards"&&d.Parent.Parent?.Name=="fixtures")privateRoot=true;
            if(!privateRoot)return Failure("OUTPUT_NOT_PRIVATE","Output must be beneath fixtures/standards/private");
            SafePath(parent.FullName,Path.GetFileName(output));
            PrivateEvidenceFile? Map(PrivateEvidenceFile? f)=>f==null?null:f with {File="evidence/"+f.File.Replace('\\','/')};
            var staged=original with {Sources=original.Sources.Select(s=>s with {File="evidence/"+s.File.Replace('\\','/')}).ToImmutableArray(),
                Rights=original.Rights! with {Evidence=Map(original.Rights!.Evidence)},Adoption=original.Adoption! with {Evidence=Map(original.Adoption!.Evidence)},
                Cells=original.Cells.Select(c=>c with {Reconciliation=c.Reconciliation! with {Evidence=Map(c.Reconciliation!.Evidence)},EngineerReview=Map(c.EngineerReview)}).ToImmutableArray()};
            var files=original.Sources.Select(s=>new PrivateEvidenceFile(s.File,s.Sha256)).Concat(new[]{original.Rights!.Evidence!,original.Adoption!.Evidence!})
                .Concat(original.Cells.Select(c=>c.Reconciliation!.Evidence!)).Concat(original.Cells.Where(c=>c.EngineerReview!=null).Select(c=>c.EngineerReview!)).ToArray();
            // Read and verify all input bytes before creating output; never overwrite existing versions.
            var captured=files.GroupBy(f=>f.File,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>ReadChecked(input,g.First()),StringComparer.OrdinalIgnoreCase);
            var bundle=BuildBundle(staged);
            string pending=Path.Combine(parent.FullName,".pending-asnz-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending);
            try
            {
            foreach(var file in captured){var target=Path.Combine(pending,"evidence",file.Key);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllBytes(target,file.Value);}
            File.WriteAllBytes(Path.Combine(pending,"records.json"),bundle.RawComponentBytes["records.json"].ToArray());
            File.WriteAllText(Path.Combine(pending,"source-staging.json"),JsonSerializer.Serialize(staged,AsNzPrivateStagingJson.Options));
            File.WriteAllText(Path.Combine(pending,"manifest.json"),JsonSerializer.Serialize(bundle.Manifest,AsNzPrivateStagingJson.Options));
            var auth=new PrivateAutoAuthorization(true,staged.Rights!.Evidence!.File,staged.Rights.Evidence.Sha256,staged.Adoption!.Evidence!.File,staged.Adoption.Evidence.Sha256,
                staged.Adoption.Tables.ToImmutableDictionary(t=>t.Table,t=>t.EvidenceLocator),Sha(File.ReadAllBytes(Path.Combine(pending,"source-staging.json"))));
            File.WriteAllText(Path.Combine(pending,"auto-authorization.json"),JsonSerializer.Serialize(auth,AsNzPrivateStagingJson.Options));
            var finalReport=ValidateFile(Path.Combine(pending,"source-staging.json"));
            if(!finalReport.ReadyForPrivateReview)return finalReport;
            Directory.Move(pending,output); // Atomic publication refuses a concurrently created destination.
            return finalReport;
            }
            finally
            {
                if(Directory.Exists(pending)&&Path.GetDirectoryName(Path.GetFullPath(pending))==parent.FullName&&Path.GetFileName(pending).StartsWith(".pending-asnz-",StringComparison.Ordinal))Directory.Delete(pending,true);
            }
        }
        catch(Exception e)when(Expected(e)){return Failure("PACKAGE_FAILED",e.Message);}
    }
}
