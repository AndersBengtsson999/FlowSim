using System.Collections.ObjectModel;
using Simulation.Core;

namespace Simulation.Application;

public static class ScenarioParameters
{
    public static IReadOnlyDictionary<string, string> Describe(SimulationRequest s)
    {
        string N(double d) => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        string E(IEffortDistribution? d, double fallback = 0) => d switch
        {
            null => "Fixed " + N(fallback), FixedEffort f => "Fixed " + N(f.Effort),
            TriangularEffort t => $"Triangular {N(t.Minimum)} / {N(t.MostLikely)} / {N(t.Maximum)}",
            _ => throw new ScenarioValidationException("Only Fixed and Triangular distributions can be described and saved.")
        };
        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Name"] = s.Name, ["Simulation Days"] = s.SimulationDays.ToString(), ["Work Items"] = s.NumberOfWorkItems.ToString(),
            ["Developers"] = s.DeveloperCount.ToString(), ["Testers"] = s.TesterCount.ToString(),
            ["Developer Capacity / Day"] = N(s.DeveloperCapacityPerDay), ["Tester Capacity / Day"] = N(s.TesterCapacityPerDay),
            ["Development WIP"] = s.DevelopmentWipLimit.ToString(), ["Code Review WIP"] = s.CodeReviewWipLimit.ToString(),
            ["Testing WIP"] = s.TestingWipLimit.ToString(), ["Rework WIP"] = s.Quality.ReworkWipLimit.ToString(),
            ["Development Effort"] = E(s.DevelopmentDistribution, s.DevelopmentEffort),
            ["Code Review Effort"] = E(s.CodeReviewDistribution, s.CodeReviewEffort), ["Testing Effort"] = E(s.TestingDistribution, s.TestingEffort),
            ["Defects Enabled"] = s.Quality.Enabled.ToString(), ["Code Review Defect Probability"] = N(s.Quality.CodeReviewDefectProbability),
            ["Testing Defect Probability"] = N(s.Quality.TestingDefectProbability),
            ["Code Review Rework Effort"] = E(s.Quality.CodeReviewReworkEffortDistribution), ["Testing Rework Effort"] = E(s.Quality.TestingReworkEffortDistribution),
            ["Configured Random Seed"] = s.RandomSeed.ToString()
        });
    }
}
