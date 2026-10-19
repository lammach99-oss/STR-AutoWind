using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Csi.Abstractions;

public interface ICsiReadAdapter
{
    Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default);
}
public enum CsiProduct { Etabs, Sap2000 }
public enum CsiIssueSeverity { Information, Warning, Error }
public sealed record CsiReadIssue(string Code, string Operation, string Message, CsiIssueSeverity Severity = CsiIssueSeverity.Error, int? ReturnCode = null);
public sealed record CsiCapabilities(bool Stories, bool FrameDistributedLoads, bool AreaUniformLoads, bool LocalAxes, bool IsReadOnly = true);
public sealed record CsiUnits(string NativeUnitId, double LengthToM, double ForceToN, string Description);
public sealed record CsiStory(string Name, double ElevationM, double? HeightM);
public sealed record CsiConnectionOptions(CsiProduct Product, string AssemblyPath);
public sealed record CsiConnectionResult(bool Connected, string Product, string Version, CsiCapabilities Capabilities, ImmutableArray<CsiReadIssue> Issues);
public sealed record CsiAreaReadInfo(string AreaId, string RoofPlaneId, Vec3? VerifiedOutwardNormal, string NormalStatus);
public sealed record CsiReadScan(CsiModelSnapshot Model, CsiUnits Units, bool IsLocked, CsiCapabilities Capabilities,
    ImmutableArray<CsiStory> Stories, ImmutableArray<CsiAreaReadInfo> AreaMetadata, ImmutableArray<CsiReadIssue> Issues);
public interface ICsiSessionReadAdapter : ICsiReadAdapter, IAsyncDisposable
{
    Task<CsiConnectionResult> ConnectAsync(CsiConnectionOptions options, CancellationToken cancellationToken = default);
    Task<CsiReadScan> RefreshSelectionAsync(CancellationToken cancellationToken = default);
}
