using System.Collections.Immutable;
using StructuralLoadAssistant.Engineering;
using StructuralLoadAssistant.Domain;
using StructuralLoadAssistant.Csi.Fake;
namespace StructuralLoadAssistant.Tests;
public class ExternalPlanTests
{
    [Fact]
    public void PlannerAndFakeRejectStaleSnapshotAndPreserveUnrelatedLoads()
    {
        var model=DemoFixtures.Model();var plan=new FrameLoadPlan(SnapshotJson.Hash(model),"calc","EXT_Uls_PX_A","Uls/PX/A",[],[]);
        Assert.Throws<ArgumentException>(()=>PerimeterFrameLoadPlanner.Validate(plan,model));
        var fake=new FakePerimeterFrameLoadAdapter(model);
        Assert.Throws<ArgumentException>(()=>fake.Apply(plan,model));
        Assert.Equal(model.ExistingLoads,fake.ManualLoads);
    }
}
