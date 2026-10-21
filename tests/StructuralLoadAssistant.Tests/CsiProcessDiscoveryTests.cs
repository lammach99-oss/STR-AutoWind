using System.Diagnostics;
using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.ApiV1;
using Xunit;

namespace StructuralLoadAssistant.Tests;

public sealed class CsiProcessDiscoveryTests
{
    [Fact]
    public void Running_processes_can_be_queried_safely()
    {
        var etabsProcs = CsiProcessDiscovery.GetRunningProcesses(CsiProduct.Etabs);
        Assert.False(etabsProcs.IsDefault);

        var sapProcs = CsiProcessDiscovery.GetRunningProcesses(CsiProduct.Sap2000);
        Assert.False(sapProcs.IsDefault);
    }

    [Fact]
    public void ValidateProcessActive_throws_for_nonexistent_pid()
    {
        int nonExistentPid = 999999;
        while (Process.GetProcesses().Any(p => p.Id == nonExistentPid))
        {
            nonExistentPid++;
        }

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CsiProcessDiscovery.ValidateProcessActive(CsiProduct.Etabs, nonExistentPid));

        Assert.Contains("terminated", ex.Message);
    }

    [Fact]
    public void ValidateProcessActive_throws_for_mismatched_process_name()
    {
        // Current test process is dotnet, not ETABS
        int currentPid = Environment.ProcessId;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CsiProcessDiscovery.ValidateProcessActive(CsiProduct.Etabs, currentPid));

        Assert.Contains("reused", ex.Message);
    }
}
