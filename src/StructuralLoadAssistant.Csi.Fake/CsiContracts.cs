using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Csi.Fake;

// These contracts deliberately have no live writer implementation.
public interface ICsiReadAdapter
{
    Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default);
}
public interface ISimulatedCsiWriteAdapter
{
    Task<AssignmentRun> ApplyAreaLoadsAsync(RoofMappingApproval approval, CancellationToken cancellationToken = default);
    Task<ReadbackVerification> VerifyReadbackAsync(string assignmentId, CancellationToken cancellationToken = default);
}
public sealed record AssignmentRun(string Id, string ApprovalId, string ContentHash, string CalculationRunId,
    string ModelSnapshotId, DateTimeOffset CreatedAt, bool IsSimulation, ImmutableArray<AreaLoadProposal> AreaLoads,
    int PreservedManualLoadCount);
public sealed record ReadbackVerification(string AssignmentId, bool Matches, double TolerancePa, ImmutableArray<string> Issues);
public sealed record AssignmentPreview(string ApprovalId, ImmutableArray<ExistingLoad> ExistingLoads,
    ImmutableArray<AreaLoadProposal> ProposedLoads, string Disclosure);
