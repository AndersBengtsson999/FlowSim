using System.Text;
using Simulation.Application;

namespace Simulation.Infrastructure;

public static class ComparisonCsv
{
    public static string Export(ScenarioComparisonResult comparison)
    {
        var text = new StringBuilder();
        void Row(params string[] cells) => text.AppendLine(string.Join(",", cells.Select(s => "\"" + s.Replace("\"", "\"\"") + "\"")));
        Row("SchemaVersion", "SimulationModelVersion", "Experiment", "BaselineId", "ScenarioId", "ScenarioName", "Metric", "Value", "DeltaFromBaseline", "PercentageDelta", "PercentagePointDelta",
            "P50", "P75", "P85", "P95", "SampleCount", "PairedDeltaP50", "PairedDeltaP75", "PairedDeltaP85", "PairedDeltaP95", "PairedSampleCount",
            "RunId", "RunMode", "RunCount", "CommonRandomNumbers", "EffectiveBaseSeed", "ExecutedAtUtc", "OriginalConfigurationJson", "EffectiveConfigurationJson");
        foreach (var run in comparison.Runs)
        foreach (var metric in comparison.Metrics)
        {
            var cell = metric.Cells.Single(c => c.ScenarioId == run.ScenarioSnapshot.Id);
            var d = cell.Distribution; var p = cell.PairedDelta;
            Row(ExperimentJson.SchemaVersion.ToString(), comparison.SimulationModelVersion, comparison.ExperimentSnapshot.Name,
                comparison.ExperimentSnapshot.BaselineId.ToString(), run.ScenarioSnapshot.Id.ToString(), run.ScenarioSnapshot.Name, metric.Metric.ToString(),
                N(d.P50), N(cell.Delta.Absolute), ScenarioComparisonRunner.IsRatio(metric.Metric) ? "" : N(cell.Delta.Percentage), N(cell.PercentagePoints),
                N(d.P50), N(d.P75), N(d.P85), N(d.P95), d.SampleCount.ToString(), N(p?.P50), N(p?.P75), N(p?.P85), N(p?.P95), p?.SampleCount.ToString() ?? "",
                run.RunId.ToString(), run.Options.Mode.ToString(), (run.MonteCarlo?.NumberOfRuns ?? 1).ToString(), run.Options.CommonRandomNumbers.ToString(),
                run.EffectiveConfiguration.RandomSeed.ToString(), run.ExecutedAtUtc.ToString("O"),
                ExperimentJson.ConfigurationSnapshot(run.ScenarioSnapshot.Configuration), ExperimentJson.ConfigurationSnapshot(run.EffectiveConfiguration));
        }
        return text.ToString();
    }
    private static string N(double? v) => v?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "";
}
