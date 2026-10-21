using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Csi.Abstractions;

public interface ICsiReadAdapter
{
    Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default);
}
public enum CsiProduct { Etabs, Sap2000 }
public enum CsiScanOutcome { Disconnected, Scanning, Succeeded, EmptySelection, PartialScan, Failed }
public enum CsiIssueSeverity { Information, Warning, Error }
public sealed record CsiReadIssue(string Code, string Operation, string Message, CsiIssueSeverity Severity = CsiIssueSeverity.Error, int? ReturnCode = null);
public sealed record CsiCapabilities(bool Stories, bool FrameDistributedLoads, bool AreaUniformLoads, bool LocalAxes, bool IsReadOnly = true);
public sealed record CsiUnits(string NativeUnitId, double LengthToM, double ForceToN, string Description);
public sealed record CsiStory(string Name, double ElevationM, double? HeightM);
public sealed record CsiProcessInfo(int ProcessId, CsiProduct Product, string MainWindowTitle, DateTimeOffset StartTime);
public sealed record CsiConnectionOptions(CsiProduct Product, string AssemblyPath, int? ProcessId = null);
public sealed record CsiConnectionResult(bool Connected, string Product, string Version, CsiCapabilities Capabilities, ImmutableArray<CsiReadIssue> Issues, int? ProcessId = null, string? ModelId = null);
public sealed record CsiAreaReadInfo(string AreaId, string RoofPlaneId, Vec3? VerifiedOutwardNormal, string NormalStatus);
public enum PhysicalQualificationOutcome { Pending, Inconclusive, Passed, Failed }
public sealed record BenchmarkComparison(string Category, string ElementId, string Property, double Expected, double Actual, double AbsoluteTolerance, double? RelativeTolerance, bool Passed, string Details);
public sealed record BenchmarkChecklist(int ExpectedCount, int ExecutedCount, int PassedCount, int FailedCount, ImmutableArray<string> MissingChecks, ImmutableArray<string> FailedChecks);
public sealed record PhysicalQualificationResult(PhysicalQualificationOutcome Outcome, string SoftwareProduct, string SoftwareVersion, int? ProcessId, string ModelId, DateTimeOffset Timestamp, string BenchmarkName, ImmutableArray<BenchmarkComparison> Comparisons, ImmutableArray<string> Issues, string Summary, BenchmarkChecklist? Checklist = null, string? ScanFingerprint = null, string? FixtureHash = null);
public sealed record CsiReadScan(CsiModelSnapshot Model, CsiUnits Units, bool IsLocked, CsiCapabilities Capabilities,
    ImmutableArray<CsiStory> Stories, ImmutableArray<CsiAreaReadInfo> AreaMetadata, ImmutableArray<CsiReadIssue> Issues,
    CsiScanOutcome Outcome = CsiScanOutcome.Succeeded);
public interface ICsiSessionReadAdapter : ICsiReadAdapter, IAsyncDisposable
{
    Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options, CancellationToken cancellationToken = default);
    Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken = default);
}
