using Simulation.Core;

namespace Simulation.Application;

public static class ComparisonScenarios
{
    public static IReadOnlyList<SimulationScenario> Create()
    {
        var baseline = BaselineScenario.Create();
        return Array.AsReadOnly(new[]
        {
            baseline with { Name = "A: one tester", Team = baseline.Team with { TesterCount = 1 } },
            baseline with { Name = "B: eight developers", Team = baseline.Team with { DeveloperCount = 8 } },
            baseline with { Name = "C: development WIP three", DevelopmentWipLimit = 3 }
        });
    }
}
