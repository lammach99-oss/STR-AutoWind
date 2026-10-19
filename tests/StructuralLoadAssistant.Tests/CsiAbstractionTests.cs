using StructuralLoadAssistant.Csi.Abstractions;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Tests;

public class CsiAbstractionTests
{
    [Fact]
    public async Task FakeAdapterRetainsReadContractAndSimulationBehavior()
    {
        ICsiReadAdapter adapter = new FakeCsiAdapter();
        var model = await adapter.ScanModelAsync();
        Assert.Equal("FAKE CSI",model.Product);
        Assert.Equal(4,model.Frames.Length);
        Assert.Equal(2,(await adapter.GetExistingLoadsAsync()).Length);
    }

    [Fact]
    public void ReadBoundaryExposesNoModelMutationOperations()
    {
        Assert.DoesNotContain(typeof(ICsiSessionReadAdapter).GetMethods(), m =>
            m.Name.StartsWith("Set",StringComparison.Ordinal) || m.Name.StartsWith("Apply",StringComparison.Ordinal) ||
            m.Name.Contains("Save",StringComparison.Ordinal) || m.Name.Contains("Unlock",StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(ICsiReadAdapter).Assembly.GetReferencedAssemblies(), a =>
            a.Name!.Contains("Fake",StringComparison.Ordinal) || a.Name.Contains("ApiV1",StringComparison.Ordinal) ||
            a.Name.Contains("CSiAPI",StringComparison.Ordinal));
    }
}
