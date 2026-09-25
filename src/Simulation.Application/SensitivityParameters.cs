using Simulation.Core;

namespace Simulation.Application;

public enum SensitivityParameter
{
    Developers, Testers, DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit, ReworkWipLimit,
    DevelopmentEffort, CodeReviewEffort, TestingEffort, CodeReviewDefectProbability, TestingDefectProbability
}
public enum SensitivityMode { SingleRun, MonteCarlo }

public static class SensitivityParameters
{
    public static IReadOnlyList<double> Defaults(SensitivityParameter p) => Array.AsReadOnly(p switch
    {
        SensitivityParameter.Developers or SensitivityParameter.Testers => new double[] { 1, 2, 3, 4, 5, 6, 8, 10 },
        SensitivityParameter.DevelopmentEffort => [1, 2, 3, 5, 8, 13],
        SensitivityParameter.CodeReviewEffort or SensitivityParameter.TestingEffort => [.5, 1, 2, 3, 5, 8, 13],
        SensitivityParameter.CodeReviewDefectProbability or SensitivityParameter.TestingDefectProbability => [0, .05, .1, .2, .3, .5, .7],
        _ => [1, 2, 3, 5, 8, 10, 15]
    });
    public static bool IsQuality(SensitivityParameter p) => p is SensitivityParameter.ReworkWipLimit
        or SensitivityParameter.CodeReviewDefectProbability or SensitivityParameter.TestingDefectProbability;
    private static double Effort(IEffortDistribution? distribution, double fixedValue) => distribution switch
    {
        null => fixedValue, FixedEffort f => f.Effort, TriangularEffort t => t.MostLikely,
        _ => throw new ScenarioValidationException("Sensitivity supports Fixed or Triangular effort only.")
    };
    public static double Value(SimulationRequest s, SensitivityParameter p) => p switch
    {
        SensitivityParameter.Developers => s.DeveloperCount, SensitivityParameter.Testers => s.TesterCount,
        SensitivityParameter.DevelopmentWipLimit => s.DevelopmentWipLimit,
        SensitivityParameter.CodeReviewWipLimit => s.CodeReviewWipLimit,
        SensitivityParameter.TestingWipLimit => s.TestingWipLimit,
        SensitivityParameter.ReworkWipLimit => s.Quality.ReworkWipLimit,
        SensitivityParameter.DevelopmentEffort => Effort(s.DevelopmentDistribution, s.DevelopmentEffort),
        SensitivityParameter.CodeReviewEffort => Effort(s.CodeReviewDistribution, s.CodeReviewEffort),
        SensitivityParameter.TestingEffort => Effort(s.TestingDistribution, s.TestingEffort),
        SensitivityParameter.CodeReviewDefectProbability => s.Quality.CodeReviewDefectProbability,
        SensitivityParameter.TestingDefectProbability => s.Quality.TestingDefectProbability,
        _ => throw new ScenarioValidationException("Unknown sensitivity parameter.")
    };
    public static SimulationRequest Apply(SimulationRequest s, SensitivityParameter p, double v)
    {
        if (!double.IsFinite(v)) throw new ScenarioValidationException("Parameter values must be finite.");
        if (IsQuality(p) && !s.Quality.Enabled)
            throw new ScenarioValidationException("Select a base scenario with defects enabled to vary a quality parameter. Other parameters are never silently changed.");
        int Count() => v >= 0 && v <= int.MaxValue && v == Math.Truncate(v) ? (int)v
            : throw new ScenarioValidationException("Counts and WIP limits must be nonnegative integers.");
        IEffortDistribution Change(IEffortDistribution d) => d switch
        {
            FixedEffort => new FixedEffort(v), TriangularEffort t => new TriangularEffort(t.Minimum, v, t.Maximum),
            _ => throw new ScenarioValidationException("Sensitivity supports Fixed or Triangular effort only.")
        };
        return p switch
        {
            SensitivityParameter.Developers => s with { DeveloperCount = Count() },
            SensitivityParameter.Testers => s with { TesterCount = Count() },
            SensitivityParameter.DevelopmentWipLimit => s with { DevelopmentWipLimit = Count() },
            SensitivityParameter.CodeReviewWipLimit => s with { CodeReviewWipLimit = Count() },
            SensitivityParameter.TestingWipLimit => s with { TestingWipLimit = Count() },
            SensitivityParameter.ReworkWipLimit => s with { Quality = s.Quality with { ReworkWipLimit = Count() } },
            SensitivityParameter.DevelopmentEffort => s.DevelopmentDistribution is null ? s with { DevelopmentEffort = new FixedEffort(v).Effort } : s with { DevelopmentDistribution = Change(s.DevelopmentDistribution) },
            SensitivityParameter.CodeReviewEffort => s.CodeReviewDistribution is null ? s with { CodeReviewEffort = new FixedEffort(v).Effort } : s with { CodeReviewDistribution = Change(s.CodeReviewDistribution) },
            SensitivityParameter.TestingEffort => s.TestingDistribution is null ? s with { TestingEffort = new FixedEffort(v).Effort } : s with { TestingDistribution = Change(s.TestingDistribution) },
            SensitivityParameter.CodeReviewDefectProbability => s with { Quality = s.Quality with { CodeReviewDefectProbability = v } },
            SensitivityParameter.TestingDefectProbability => s with { Quality = s.Quality with { TestingDefectProbability = v } },
            _ => throw new ScenarioValidationException("Unknown sensitivity parameter.")
        };
    }
}
