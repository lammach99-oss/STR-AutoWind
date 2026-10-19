using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.RoofMapping;

namespace StructuralLoadAssistant.Csi.Fake;

public sealed class FakeCsiAdapter : ICsiReadAdapter, ISimulatedCsiWriteAdapter
{
    private CsiModelSnapshot _model;
    private ImmutableArray<ExistingLoad> _loads;
    private readonly Dictionary<string, AssignmentRun> _runs = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    public FakeCsiAdapter(CsiModelSnapshot? fixture = null)
    {
        _model = fixture ?? DemoFixtures.Model();
        if (_model.Product != "FAKE CSI") throw new ArgumentException("Fake models only");
        _loads = _model.ExistingLoads;
    }
    public Task<CsiModelSnapshot> ScanModelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) return Task.FromResult(_model with { ExistingLoads = _loads,
            ExistingLoadsFingerprint = SnapshotJson.Hash(_loads) });
    }
    public Task<ImmutableArray<ExistingLoad>> GetExistingLoadsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) return Task.FromResult(_loads);
    }
    public AssignmentPreview Preview(RoofMappingApproval approval)
    {
        lock (_sync)
        {
            Preflight(approval);
            return new(approval.Id, _loads, approval.Proposals,
                "SIMULATION ONLY. Existing manual loads will be preserved; proposed owned loads are additive.");
        }
    }
    private void Preflight(RoofMappingApproval approval)
    {
        if (!approval.Simulation) throw new InvalidOperationException("Fake adapter requires simulation approval");
        RoofApprovalValidator.Validate(approval, _model with { ExistingLoads = _loads, ExistingLoadsFingerprint = SnapshotJson.Hash(_loads) });
        if (approval.Proposals.IsEmpty) throw new InvalidOperationException("No proposed loads");
        foreach (var load in approval.Proposals)
        {
            if (!_model.Patterns.Contains(load.Pattern)) throw new InvalidOperationException("Existing load pattern required");
            if (!_model.Shells.Any(s => s.Id == load.ShellId) || !double.IsFinite(load.SignedCsiAreaLoadPa))
                throw new InvalidOperationException("Invalid target/value");
            if (_loads.Any(l => l.ObjectId == load.ShellId && l.Pattern == load.Pattern && l.Owner != "Manual"))
                throw new InvalidOperationException("An owned load already occupies this shell/pattern; explicit conflict resolution required");
        }
    }
    public Task<AssignmentRun> ApplyAreaLoadsAsync(RoofMappingApproval approval, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var id = "simulation-" + approval.ContentHash;
            if (_runs.TryGetValue(id, out var prior))
            {
                if (approval.ContentHash != RoofApprovalValidator.ComputeHash(approval) ||
                    approval.Model.GeometryFingerprint != _model.GeometryFingerprint ||
                    approval.Model.ModelId != _model.ModelId)
                    throw new InvalidOperationException("Stale or modified approval");
                return Task.FromResult(prior);
            }
            Preflight(approval);
            var run = new AssignmentRun(id, approval.Id, approval.ContentHash, approval.Calculation.Id,
                approval.Model.Id, DateTimeOffset.UtcNow, true, approval.Proposals, _loads.Count(l=>l.Owner=="Manual"));
            // Atomic in-memory fake mutation after complete validation; manual loads are never removed.
            _loads = _loads.AddRange(approval.Proposals.Select(l => new ExistingLoad(l.ShellId,l.Pattern,"AreaLocal3",l.SignedCsiAreaLoadPa,id)));
            _runs.Add(id,run);
            return Task.FromResult(run);
        }
    }
    public Task<ReadbackVerification> VerifyReadbackAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_runs.TryGetValue(assignmentId,out var run)) throw new InvalidOperationException("Unknown simulation run");
            var issues = ImmutableArray.CreateBuilder<string>();
            foreach(var p in run.AreaLoads)
            {
                var matches = _loads.Where(l=>l.Owner==assignmentId && l.ObjectId==p.ShellId && l.Pattern==p.Pattern).ToArray();
                if(matches.Length!=1 || Math.Abs(matches[0].ValueSI-p.SignedCsiAreaLoadPa)>1e-6) issues.Add($"Readback mismatch: {p.ShellId}");
            }
            return Task.FromResult(new ReadbackVerification(assignmentId,issues.Count==0,1e-6,issues.ToImmutable()));
        }
    }
    public void ChangeGeometryForTest()
    {
        lock (_sync) _model = _model with { GeometryFingerprint = _model.GeometryFingerprint + "-changed" };
    }
}
