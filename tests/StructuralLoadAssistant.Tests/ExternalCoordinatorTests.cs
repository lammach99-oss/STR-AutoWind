using StructuralLoadAssistant.Application;
using StructuralLoadAssistant.Csi.Fake;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Tests;
public class ExternalCoordinatorTests
{
    [Fact]
    public async Task PreviewBindsCalculationActualSelectionAndFakeReadback()
    {
        var req=ExternalWindFixture.Request();var model=ExternalWindFixture.Model();
        var result=await ExternalWindReviewCoordinator.PreviewAsync(req,model,[new ExternalTransferSelection("Uls_PositiveX_wall_COMMON",ExternalWindFixture.Support())]);
        Assert.Equal(2,result.Plans.Length);var plan=result.Plans[0];Assert.Equal(2,plan.Segments.Length);
        var fake=new FakePerimeterFrameLoadAdapter(model);fake.Apply(plan,model);Assert.True(fake.Verify(plan));
        Assert.Throws<ArgumentException>(()=>fake.Apply(plan,model));
    }
}
