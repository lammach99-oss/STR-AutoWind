using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;

namespace StructuralLoadAssistant.Engineering;

public interface IWindCalculationStrategy
{
    string StandardFamily { get; }
    string StandardEdition { get; }
    ImmutableArray<string> SupportedProcedures { get; }
    EngineeringCalculationRun Execute(EngineeringCalculationRequest request);
}

