using Simulation.Core;

namespace Simulation.Application;

public static class BaselineScenario
{
    public static SimulationRequest CreateRequest() => new();
    public static SimulationScenario Create() => CreateRequest().ToScenario();
    public static SimulationRequest VariableEffortExample() => CreateRequest() with
    {
        Name = "Variable Effort Example",
        DevelopmentDistribution = new TriangularEffort(2, 5, 12),
        CodeReviewDistribution = new TriangularEffort(.5, 1, 3),
        TestingDistribution = new TriangularEffort(1, 2, 5)
    };
    public static SimulationRequest DefectsAndReworkExample() => VariableEffortExample() with
    {
        Name = "Defects & Rework Example",
        Quality = new DefectSettings
        {
            Enabled = true, CodeReviewDefectProbability = .15, TestingDefectProbability = .10, ReworkWipLimit = 3,
            CodeReviewReworkEffortDistribution = new TriangularEffort(.5, 1, 3),
            TestingReworkEffortDistribution = new TriangularEffort(1, 2, 5)
        }
    };
}
