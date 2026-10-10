namespace Simulation.Core;

/// <summary>Identifies simulation semantics, independent of the assembly/application version.</summary>
public static class SimulationModel
{
    public const string Version = "0.8";
    public static bool CanLoad(string version) => version is "0.2" or "0.3" or "0.4" or "0.5" or "0.6" or "0.7" or Version;
}
