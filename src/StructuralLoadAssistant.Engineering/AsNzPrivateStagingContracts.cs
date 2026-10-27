using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructuralLoadAssistant.Engineering;

public enum SourceCellStatus { NOT_AVAILABLE, TRANSCRIBED_UNREVIEWED, SOURCE_CELL_CHECKED, ENGINEER_REVIEWED }
public sealed record PrivateEvidenceFile(string File,string Sha256);
public sealed record AsNzSourceDocument(string Id,string File,string Sha256,string Edition);
public sealed record PrivateRightsReview(string Decision,string Reviewer,string DecisionReference,
    PrivateEvidenceFile? Evidence,bool ExtractionPermitted,bool PrivateStoragePermitted,bool SoftwareUsePermitted,
    ImmutableArray<string> SourceHashes);
public sealed record PrivateTableAdoption(string Table,string BaseTextDecision,string Amendment1Decision,
    string Amendment2Decision,string EvidenceLocator);
public sealed record PrivateAdoptionReview(string Country,string Jurisdiction,string Edition,
    PrivateEvidenceFile? Evidence,ImmutableArray<PrivateTableAdoption> Tables);
public sealed record SourceCellReconciliation(string Checker,string CellHash,string SourceSha256,
    double ObservedValue,PrivateEvidenceFile? Evidence);
public sealed record AsNzSourceCell(CoefficientRecord? Record,string SourceId,string Table,int PdfPage,
    string PrintedPage,string Row,string Column,string Alternative,string WindIncidence,
    SourceCellStatus Status,string Transcriber,SourceCellReconciliation? Reconciliation=null,
    PrivateEvidenceFile? EngineerReview=null);
public sealed record AsNzSourceStaging(string SchemaVersion,string DatasetId,string Edition,
    ImmutableArray<string> Amendments,string Country,string Jurisdiction,PrivateRightsReview? Rights,
    PrivateAdoptionReview? Adoption,ImmutableArray<AsNzSourceDocument> Sources,ImmutableArray<AsNzSourceCell> Cells);
public sealed record PrivateStagingIssue(string Code,string Location,string Message);
public sealed record PrivateStagingReport(string Status,int Transcribed,int SourceCellChecked,int EngineerReviewed,
    int NotAvailable,ImmutableArray<PrivateStagingIssue> Issues)
{
    public bool ReadyForPrivateReview=>Status=="READY_FOR_PRIVATE_REVIEW";
    public bool CanCalculateNormativePressures=>false;
    public bool CanMutateCsi=>false;
    public string RightsMeaning=>"Document/evidence checks only; not a legal determination or independent engineering certification";
}
public static class AsNzPrivateStagingJson
{
    public static JsonSerializerOptions Options=>new() {WriteIndented=true,PropertyNameCaseInsensitive=false,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        Converters={new JsonStringEnumConverter()}};
}
