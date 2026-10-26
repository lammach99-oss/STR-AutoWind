using StructuralLoadAssistant.Engineering;
namespace StructuralLoadAssistant.Tests;
public sealed class AsNzMethodDecisionTests
{
    [Theory]
    [InlineData(0,"FLAT_OR_NEAR_FLAT")][InlineData(9.9,"MONOSLOPE")][InlineData(10,"MONOSLOPE")][InlineData(14,"MONOSLOPE")][InlineData(25,"MONOSLOPE")]
    public void RoofMethodUsesPhysicalFormNotWorksheetCaption(double pitch,string classification)
    {
        var decision=AsNz2021MethodDecision.Evaluate("Monoslope",pitch,0,0,0.5,1,"AU","D",500,[]);
        Assert.Equal(classification,decision.GeometryClass);
        Assert.False(decision.NormativeLookupAvailable);
        Assert.Contains("SOURCE RIGHTS REQUIRED",decision.Status);
        Assert.Contains("5.3",decision.SourceReference);
    }
    [Fact]
    public void UnsupportedRoofOrJurisdictionNeverBecomesApplicable()
    {
        Assert.False(AsNz2021MethodDecision.Evaluate("Gable",14,0,90,0.25,2,"AU","D",500,[]).ReviewGeometrySupported);
        Assert.False(AsNz2021MethodDecision.Evaluate("Monoslope",14,0,0,0.5,1,"UNKNOWN","D",500,[]).JurisdictionRecognized);
    }
}
