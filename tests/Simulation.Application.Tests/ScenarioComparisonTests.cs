using System.Globalization;
using System.Text.Json;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class ScenarioComparisonTests
{
    private static Experiment Pair(bool variable = false, bool common = true, ExperimentRunMode mode = ExperimentRunMode.SingleRun, int count = 5)
    {
        var s = (variable ? BaselineScenario.DefectsAndReworkExample() : BaselineScenario.CreateRequest()) with { NumberOfWorkItems = 12, SimulationDays = 40 };
        var a = ScenarioDefinition.Create(s with { Name = "Reference", RandomSeed = 11 });
        var b = a.Duplicate() with { Configuration = s with { Name = "More Testers", TesterCount = 4, RandomSeed = 37 } };
        return new(Guid.NewGuid(), "Test Experiment", "Description", Array.AsReadOnly(new[] { a, b }), a.Id, new(mode, common, 12345, count));
    }
    [Fact]
    public void DuplicateIsIndependentAndCopiesAllConfiguration()
    {
        var session = new ExperimentSession(Pair(true)); var original = session.Experiment.Scenarios[0];
        var copy = session.Duplicate(original.Id);
        Assert.NotEqual(original.Id, copy.Id); Assert.Equal(original.Name + " Copy", copy.Name);
        Assert.Equal(original.Configuration, copy.Configuration with { Name = original.Name });
        session.Update(copy.Id, copy.Configuration with { TesterCount = 8, Quality = copy.Configuration.Quality with { TestingDefectProbability = .7 } });
        Assert.Equal(original, session.Find(original.Id)); Assert.Equal(2, original.Configuration.TesterCount);
    }
    [Fact]
    public void RunAllBaselineSwitchAndDeltasUseTheSelectedReference()
    {
        var e = Pair(); var runner = new ScenarioComparisonRunner(); var runs = runner.RunAll(e);
        Assert.Equal(2, runs.Count);
        var comparison = runner.Compare(e, runs);
        var row = comparison.Metrics.Single(m => m.Metric == AnalysisMetric.TesterUtilization);
        var actual = runs[1].SingleRun!.TesterUtilization - runs[0].SingleRun!.TesterUtilization;
        Assert.Equal(actual, row.Cells[1].Delta.Absolute!.Value, 12);
        Assert.Equal(actual * 100, row.Cells[1].PercentagePoints!.Value, 12);
        var reverse = runner.Compare(e with { BaselineId = e.Scenarios[1].Id }, runs);
        var reversed = reverse.Metrics.Single(m => m.Metric == AnalysisMetric.TesterUtilization);
        Assert.Equal(-actual, reversed.Cells[0].Delta.Absolute!.Value, 12);
        Assert.Equal(0d, reversed.Cells[1].Delta.Absolute);
    }
    [Fact]
    public void PercentageAndZeroBaselineRulesAreExplicit()
    {
        Assert.Equal(new MetricDelta(3, 50), MetricDelta.Between(6, 9));
        Assert.Equal(new MetricDelta(3, null), MetricDelta.Between(0, 3));
        Assert.Equal(new MetricDelta(null, null), MetricDelta.Between(null, 3));
    }
    [Fact]
    public void ConfigurationEditsAndDraftsMarkResultsStaleWithoutChangingHistory()
    {
        var e = Pair(); var session = new ExperimentSession(e); var runs = new ScenarioComparisonRunner().RunAll(e); session.StoreAll(runs);
        var id = e.Scenarios[1].Id; var old = runs[1].ScenarioSnapshot.Configuration;
        Assert.Equal("Current", session.Status(id));
        session.BeginEdit(id); Assert.Equal("Out of Date", session.Status(id));
        Assert.Throws<ScenarioValidationException>(() => session.Compare(e.Scenarios.Select(s => s.Id)));
        session.CancelEdit(id); Assert.Equal("Current", session.Status(id));
        session.Update(id, old with { TesterCount = 9 });
        Assert.Equal("Out of Date", session.Status(id)); Assert.Equal(old, runs[1].ScenarioSnapshot.Configuration);
        Assert.Equal(4, runs[1].EffectiveConfiguration.TesterCount);
    }
    [Fact]
    public void CommonSeedsPreserveUnderlyingEffortAndIndependentSeedsRemainConfigured()
    {
        var common = Pair(true); var runs = new ScenarioComparisonRunner().RunAll(common);
        Assert.All(runs, r => Assert.Equal(common.Options.BaseSeed, r.EffectiveConfiguration.RandomSeed));
        Assert.Equal(runs[0].SingleRun!.WorkItems.Select(w => (w.DevelopmentEffort, w.CodeReviewEffort, w.TestingEffort)),
            runs[1].SingleRun!.WorkItems.Select(w => (w.DevelopmentEffort, w.CodeReviewEffort, w.TestingEffort)));
        var independent = new ScenarioComparisonRunner().RunAll(common with { Options = common.Options with { CommonRandomNumbers = false } });
        Assert.Equal(new[] { 11, 37 }, independent.Select(r => r.EffectiveConfiguration.RandomSeed));
        Assert.Equal(11, runs[0].ScenarioSnapshot.Configuration.RandomSeed);
    }
    [Fact]
    public void MonteCarloPairsEachRunAndCalculatesSignedDeltaPercentiles()
    {
        var e = Pair(true, mode: ExperimentRunMode.MonteCarlo, count: 12); var runner = new ScenarioComparisonRunner(); var runs = runner.RunAll(e);
        var comparison = runner.Compare(e, runs);
        Assert.Equal(runs[0].MonteCarlo!.Runs.Select(r => r.RandomSeed), runs[1].MonteCarlo!.Runs.Select(r => r.RandomSeed));
        Assert.Equal(Enumerable.Range(12345, 12), runs[0].MonteCarlo!.Runs.Select(r => r.RandomSeed));
        var differences = runs[1].MonteCarlo!.Runs.Zip(runs[0].MonteCarlo!.Runs)
            .Where(p => p.First.CompletedWorkItems > 0 && p.Second.CompletedWorkItems > 0).Select(p => p.First.AverageCycleTime - p.Second.AverageCycleTime).ToArray();
        var expected = DistributionStatistics.Summarize(differences);
        var actual = comparison.Metrics.Single(m => m.Metric == AnalysisMetric.AverageCycleTime).Cells[1].PairedDelta!;
        Assert.Equal(expected.P50, actual.P50); Assert.Equal(expected.P75, actual.P75); Assert.Equal(expected.P85, actual.P85); Assert.Equal(expected.P95, actual.P95);
        Assert.Equal(differences.Length, actual.SampleCount);
        var repeat = runner.RunAll(e);
        Assert.Equal(JsonSerializer.Serialize(runs.Select(r => r.MonteCarlo)), JsonSerializer.Serialize(repeat.Select(r => r.MonteCarlo)));
        Assert.All(runs, r => { Assert.Equal(SimulationModel.Version, r.SimulationModelVersion); Assert.NotEqual(default, r.ExecutedAtUtc); Assert.NotEqual(Guid.Empty, r.RunId); });
    }
    [Fact]
    public void IndependentMonteCarloDoesNotInventPairs()
    {
        var e = Pair(true, false, ExperimentRunMode.MonteCarlo); var runner = new ScenarioComparisonRunner(); var comparison = runner.Compare(e, runner.RunAll(e));
        Assert.All(comparison.Metrics.SelectMany(r => r.Cells), c => Assert.Null(c.PairedDelta));
    }
    [Fact]
    public void MissingCompletionsExcludeOnlyInvalidTimePairs()
    {
        var e = Pair(mode: ExperimentRunMode.MonteCarlo);
        e = e with { Scenarios = Array.AsReadOnly(e.Scenarios.Select((s, i) => i == 0 ? s : s with { Configuration = s.Configuration with { TesterCount = 0 } }).ToArray()) };
        var runner = new ScenarioComparisonRunner(); var result = runner.Compare(e, runner.RunAll(e));
        var time = result.Metrics.Single(r => r.Metric == AnalysisMetric.AverageCycleTime).Cells[1];
        Assert.Null(time.Distribution.P50); Assert.Equal(0, time.PairedDelta!.SampleCount);
        Assert.Equal(e.Options.MonteCarloRuns, result.Metrics.Single(r => r.Metric == AnalysisMetric.ThroughputPerFiveDays).Cells[1].PairedDelta!.SampleCount);
    }
    [Fact]
    public void SingleRunIncludesAllApplicableMetricsAndBlockedTime()
    {
        var e = Pair(true); var runner = new ScenarioComparisonRunner(); var result = runner.Compare(e, runner.RunAll(e));
        Assert.Equal(ScenarioComparisonRunner.DisplayMetrics.Count, result.Metrics.Count);
        Assert.Contains(result.Metrics, r => r.Metric == AnalysisMetric.AverageBlockedTime);
        var off = Pair(); var offResult = runner.Compare(off, runner.RunAll(off));
        Assert.DoesNotContain(offResult.Metrics, r => r.Metric == AnalysisMetric.TotalDefectsFound);
    }
    [Fact]
    public void DeleteResetAndOptionChangesHaveExplicitLifecycle()
    {
        var e = Pair(); var session = new ExperimentSession(e); session.StoreAll(new ScenarioComparisonRunner().RunAll(e));
        session.SetOptions(e.Options with { BaseSeed = 42 }); Assert.Equal("Out of Date", session.Status(e.BaselineId));
        session.Reset(e.Scenarios[1].Id); Assert.Equal(2, session.Find(e.Scenarios[1].Id).Configuration.TesterCount);
        session.Delete(e.BaselineId); Assert.Equal(e.Scenarios[1].Id, session.Experiment.BaselineId);
        Assert.Throws<ScenarioValidationException>(() => session.Delete(session.Experiment.BaselineId));
    }
    [Theory]
    [InlineData("Developer Capacity")]
    [InlineData("Testing Capacity")]
    [InlineData("Development WIP")]
    [InlineData("Quality / Rework")]
    public void DemonstrationsValidateAndHaveClearProvenance(string name)
    {
        var e = DemonstrationExperiments.Create(name); e.Validate(); Assert.Contains("Demonstration", e.Name);
        Assert.InRange(e.Scenarios.Count, 4, 5); Assert.Contains(e.Scenarios, s => s.Id == e.BaselineId);
    }
    [Fact]
    public void CancellationNeverPublishesPartialBatch()
    {
        var runner = new ScenarioComparisonRunner(); Assert.Throws<OperationCanceledException>(() => runner.RunAll(Pair(), cancellationToken: new(true)));
    }
    [Fact]
    public void ScenarioAndExperimentJsonRoundTripAllParametersAndVersions()
    {
        var e = Pair(true, false, ExperimentRunMode.MonteCarlo, 19);
        var json = ExperimentJson.SaveExperiment(e); var copy = ExperimentJson.LoadExperiment(json);
        Assert.Contains("\"SchemaVersion\": 1", json); Assert.Contains("\"SimulationModelVersion\": \"0.1\"", json);
        Assert.Equal(json, ExperimentJson.SaveExperiment(copy));
        Assert.Equal(e.Scenarios, copy.Scenarios); Assert.Equal(e.BaselineId, copy.BaselineId); Assert.Equal(e.Options, copy.Options);
        var scenario = e.Scenarios[0]; Assert.Equal(scenario, ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(scenario)));
        // Disabled quality settings also survive persistence; no silent replacement by defaults.
        var disabled = scenario with { Configuration = scenario.Configuration with { Quality = scenario.Configuration.Quality with { Enabled = false } } };
        Assert.Equal(disabled, ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(disabled)));
    }
    [Fact]
    public void UnknownSchemaModelKindAndInvalidDistributionsAreRejected()
    {
        var json = ExperimentJson.SaveExperiment(Pair(true));
        Assert.Throws<JsonException>(() => ExperimentJson.LoadExperiment(json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 99")));
        Assert.Throws<JsonException>(() => ExperimentJson.LoadExperiment(json.Replace("\"SimulationModelVersion\": \"0.1\"", "\"SimulationModelVersion\": \"99\"")));
        Assert.Throws<JsonException>(() => ExperimentJson.LoadScenario(json));
        Assert.Throws<JsonException>(() => ExperimentJson.LoadExperiment(json.Replace("\"Triangular\"", "\"Unknown\"")));
    }
    [Fact]
    public async Task PersistenceWritesAndReadsRealFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); var e = Pair(true);
        try { await ExperimentJson.WriteAsync(path, ExperimentJson.SaveExperiment(e)); Assert.Equal(e.Scenarios, ExperimentJson.LoadExperiment(await ExperimentJson.ReadAsync(path)).Scenarios); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void CsvIncludesEscapedNamesSnapshotsAndPairedMetricsUsingInvariantNumbers()
    {
        var e = Pair(true, mode: ExperimentRunMode.MonteCarlo);
        e = e with { Scenarios = Array.AsReadOnly(e.Scenarios.Select((s, i) => i == 1 ? s with { Configuration = s.Configuration with { Name = "Quoted \"name\", with\nnewline" } } : s).ToArray()) };
        var runner = new ScenarioComparisonRunner(); var result = runner.Compare(e, runner.RunAll(e));
        var csv = ComparisonCsv.Export(result);
        Assert.Contains("\"Quoted \"\"name\"\", with\nnewline\"", csv);
        Assert.Contains("\"PairedDeltaP95\"", csv); Assert.Contains("\"PercentagePointDelta\"", csv);
        Assert.Contains("\"OriginalConfigurationJson\"", csv); Assert.Contains(result.Runs[0].RunId.ToString(), csv);
        Assert.Contains(result.Runs[0].ExecutedAtUtc.ToString("O"), csv);
        Assert.Contains("\"SimulationModelVersion\"", csv);
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new("sv-SE"); Assert.Equal(csv, ComparisonCsv.Export(result)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void CsvParsesBackToEveryMeasuredValueAndSnapshot()
    {
        var e = Pair(true, mode: ExperimentRunMode.MonteCarlo);
        var runner = new ScenarioComparisonRunner(); var comparison = runner.Compare(e, runner.RunAll(e));
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(ComparisonCsv.Export(comparison)))
        { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(","); var headers = parser.ReadFields()!; int count = 0;
        while (!parser.EndOfData)
        {
            var cells = parser.ReadFields()!; Assert.Equal(headers.Length, cells.Length);
            string Field(string key) => cells[Array.IndexOf(headers, key)];
            var id = Guid.Parse(Field("ScenarioId")); var metric = Enum.Parse<AnalysisMetric>(Field("Metric"));
            var expected = comparison.Metrics.Single(m => m.Metric == metric).Cells.Single(c => c.ScenarioId == id);
            Assert.Equal(expected.Distribution.P50, double.Parse(Field("Value"), CultureInfo.InvariantCulture));
            Assert.Equal(expected.PairedDelta!.P95, double.Parse(Field("PairedDeltaP95"), CultureInfo.InvariantCulture));
            using var snapshot = JsonDocument.Parse(Field("EffectiveConfigurationJson"));
            Assert.Equal(12345, snapshot.RootElement.GetProperty("RandomSeed").GetInt32());
            Assert.Equal(comparison.Runs.Single(r => r.ScenarioSnapshot.Id == id).EffectiveConfiguration.TesterCount,
                snapshot.RootElement.GetProperty("TesterCount").GetInt32());
            count++;
        }
        Assert.Equal(comparison.Runs.Count * comparison.Metrics.Count, count);
    }

}
