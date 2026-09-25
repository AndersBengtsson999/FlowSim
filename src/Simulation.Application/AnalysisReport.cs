using System.Globalization;
using System.Text;
using Simulation.Core;

namespace Simulation.Application;

/// <summary>Plain report formatting of measured results; no interpretation outside explicit extreme-test signal.</summary>
public static class AnalysisReport
{
    public static string Number(double? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "n/a";
    public static string Configuration(SimulationRequest s)
    {
        string Effort(IEffortDistribution? d, double f) => d switch
        {
            TriangularEffort t => $"Triangular({Number(t.Minimum)}/{Number(t.MostLikely)}/{Number(t.Maximum)})",
            FixedEffort x => $"Fixed({Number(x.Effort)})", _ => $"Fixed({Number(f)})"
        };
        return $"{s.Name}: days={s.SimulationDays}, items={s.NumberOfWorkItems}, seed={s.RandomSeed}; developers/testers={s.DeveloperCount}/{s.TesterCount}; capacity/person={Number(s.DeveloperCapacityPerDay)}/{Number(s.TesterCapacityPerDay)}; WIP development/review/testing/rework={s.DevelopmentWipLimit}/{s.CodeReviewWipLimit}/{s.TestingWipLimit}/{s.Quality.ReworkWipLimit}; effort development/review/testing={Effort(s.DevelopmentDistribution, s.DevelopmentEffort)}, {Effort(s.CodeReviewDistribution, s.CodeReviewEffort)}, {Effort(s.TestingDistribution, s.TestingEffort)}; defects enabled={s.Quality.Enabled}; probabilities review/testing={Number(s.Quality.CodeReviewDefectProbability)}/{Number(s.Quality.TestingDefectProbability)}; rework efforts={Effort(s.Quality.CodeReviewReworkEffortDistribution, 0)}, {Effort(s.Quality.TestingReworkEffortDistribution, 0)}.";
    }
    public static string Validation(ModelValidationResult result)
    {
        var text = new StringBuilder($"Model validation — measurement starts at day {result.WarmUpDays}. Ratios use 0–1; percentage deltas use %.\n\n");
        foreach (var c in result.Comparisons)
        {
            text.AppendLine(c.Experiment.Name).AppendLine("A: " + Configuration(c.Experiment.ScenarioA))
                .AppendLine("B: " + Configuration(c.Experiment.ScenarioB)).AppendLine(c.Observation);
            foreach (var (metric, a) in c.A.Values)
            {
                var delta = c.Deltas[metric];
                text.AppendLine($"{metric}: {Number(a)} → {Number(c.B.Values[metric])}; Δ {Number(delta.Absolute)}; {Number(delta.Percentage)}%"
                    + (metric is AnalysisMetric.DeveloperUtilization or AnalysisMetric.TesterUtilization
                        ? $"; {Number(delta.Absolute * 100)} percentage points" : ""));
            }
            text.AppendLine();
        }
        return text.ToString();
    }
}
