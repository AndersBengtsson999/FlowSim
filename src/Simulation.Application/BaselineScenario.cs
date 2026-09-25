using Simulation.Core;

namespace Simulation.Application;

public static class BaselineScenario
{
    public static SimulationScenario Create() => new SimulationRequest().ToScenario();
}
