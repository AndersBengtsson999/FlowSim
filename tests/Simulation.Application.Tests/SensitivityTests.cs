using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class SensitivityTests
{
    private static SimulationRequest Small => new() { SimulationDays = 30, NumberOfWorkItems = 15 };
    private static SensitivityAnalysisResult Run(SimulationRequest s, SensitivityParameter p, params double[] values) =>
        new SensitivityAnalysisRunner().Run(new(s, p, values));

    [Theory]
    [InlineData(SensitivityParameter.Developers, 8)]
    [InlineData(SensitivityParameter.Testers, 8)]
    [InlineData(SensitivityParameter.DevelopmentWipLimit, 8)]
    [InlineData(SensitivityParameter.CodeReviewWipLimit, 8)]
    [InlineData(SensitivityParameter.TestingWipLimit, 8)]
    [InlineData(SensitivityParameter.ReworkWipLimit, 8)]
    [InlineData(SensitivityParameter.DevelopmentEffort, 2)]
    [InlineData(SensitivityParameter.CodeReviewEffort, 2)]
    [InlineData(SensitivityParameter.TestingEffort, 2)]
    [InlineData(SensitivityParameter.CodeReviewDefectProbability, .7)]
    [InlineData(SensitivityParameter.TestingDefectProbability, .7)]
    public void OnlySelectedParameterChangesAndBaseIsImmutable(SensitivityParameter parameter, double value)
    {
        var original = Small with { Quality = new() { Enabled = true } };
        var before = JsonSerializer.Serialize(original);
        var changed = SensitivityParameters.Apply(original, parameter, value);
        Assert.Equal(value, SensitivityParameters.Value(changed, parameter));
        foreach (var p in Enum.GetValues<SensitivityParameter>().Where(p => p != parameter))
            Assert.Equal(SensitivityParameters.Value(original, p), SensitivityParameters.Value(changed, p));
        Assert.Equal(original, SensitivityParameters.Apply(changed, parameter, SensitivityParameters.Value(original, parameter)));
        Assert.Equal(before, JsonSerializer.Serialize(original));
    }
    [Fact]
    public void TriangularModePreservesBoundsAndRejectsOutOfRangeValues()
    {
        var s = Small with { TestingDistribution = new TriangularEffort(1, 2, 5) };
        var changed = SensitivityParameters.Apply(s, SensitivityParameter.TestingEffort, 3);
        Assert.Equal(new TriangularEffort(1, 3, 5), changed.TestingDistribution);
        Assert.Throws<ScenarioValidationException>(() => SensitivityParameters.Apply(s, SensitivityParameter.TestingEffort, 8));
        Assert.Equal(s.TestingEffort, changed.TestingEffort);
    }
    [Fact]
    public void OrderedPointsDuplicatesBaseAndSeedAreReproducible()
    {
        var a = Run(Small, SensitivityParameter.Testers, 3, 1, 3);
        var b = Run(Small, SensitivityParameter.Testers, 3, 1, 3);
        Assert.Equal(new double[] { 3, 1, 3 }, a.Points.Select(p => p.ParameterValue));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(Small.TesterCount, a.BasePoint.ParameterValue);
    }
    [Fact]
    public void ControlledDeveloperAndTesterChangesAffectThroughput()
    {
        var s = Small with { NumberOfWorkItems = 100, TesterCount = 20, DevelopmentWipLimit = 20, TestingWipLimit = 20, TestingEffort = 1 };
        var dev = Run(s, SensitivityParameter.Developers, 1, 10);
        Assert.True(Throughput(dev, 1) > Throughput(dev, 0));
        var test = Run(s with { DeveloperCount = 20, DevelopmentEffort = 1, TestingEffort = 10 }, SensitivityParameter.Testers, 1, 10);
        Assert.True(Throughput(test, 1) > Throughput(test, 0));
        var effort = Run(s with { DeveloperCount = 20, DevelopmentEffort = 1, TesterCount = 1 }, SensitivityParameter.TestingEffort, 1, 10);
        Assert.True(Throughput(effort, 0) > Throughput(effort, 1));
    }
    [Fact]
    public void DefectStressAffectsReworkWithoutChangingOtherInputs()
    {
        var s = Small with { Quality = new() { Enabled = true, CodeReviewReworkEffortDistribution = new FixedEffort(3) } };
        var r = Run(s, SensitivityParameter.CodeReviewDefectProbability, 0, 1);
        Assert.Equal(0d, r.Points[0].MeasurementWindow[AnalysisMetric.TotalReworkEffort].P50);
        Assert.True(r.Points[1].MeasurementWindow[AnalysisMetric.TotalReworkEffort].P50 > 0);
        Assert.Null(r.Points[1].MeasurementWindow[AnalysisMetric.AverageLeadTime].P50);
        Assert.Equal(0, r.Points[1].MeasurementWindow[AnalysisMetric.AverageLeadTime].SampleCount);
    }
    [Fact]
    public void WarmUpUsesCompletionBoundariesAndCapacityWindowButKeepsWholeItemTimes()
    {
        // One item completes at boundary 3: development [0,1), review [1,2), testing [2,3).
        var s = new SimulationRequest { NumberOfWorkItems = 1, SimulationDays = 5, DeveloperCount = 1,
            TesterCount = 1, DevelopmentEffort = 1, CodeReviewEffort = 1, TestingEffort = 1,
            DevelopmentWipLimit = 1, CodeReviewWipLimit = 1, TestingWipLimit = 1 };
        var simulation = new SimulationRunner().Run(s);
        var at2 = AnalysisMetrics.Measure(s, simulation, 2).Values;
        Assert.Equal(5, simulation.Days.Count);
        Assert.Equal(1d, at2[AnalysisMetric.CompletedWorkItems]);
        Assert.Equal(5d / 3, at2[AnalysisMetric.ThroughputPerFiveDays]);
        Assert.Equal(3d, at2[AnalysisMetric.AverageLeadTime]);
        Assert.Equal(3d, at2[AnalysisMetric.AverageActiveTime]);
        Assert.Equal(0d, at2[AnalysisMetric.AverageUsedDeveloperCapacity]);
        Assert.Equal(1d / 3, at2[AnalysisMetric.TesterUtilization]);
        Assert.Equal(1d / 3, at2[AnalysisMetric.TestingWipSaturation]);
        var at3 = AnalysisMetrics.Measure(s, simulation, 3).Values;
        Assert.Equal(0d, at3[AnalysisMetric.CompletedWorkItems]);
        Assert.Null(at3[AnalysisMetric.AverageLeadTime]);
        Assert.Equal(0d, at3[AnalysisMetric.TesterUtilization]);
    }
    [Fact]
    public void WipSaturationUsesAdmissionOccupancyRatherThanEndOfDayCounts()
    {
        var s = Small with { NumberOfWorkItems = 1, SimulationDays = 3, DevelopmentEffort = 1,
            CodeReviewEffort = 1, TestingEffort = 1, DevelopmentWipLimit = 1 };
        var r = new SimulationRunner().Run(s);
        Assert.Equal(0, r.Days[0].DevelopmentCount);
        Assert.Equal(1, r.Days[0].DevelopmentWip);
        Assert.Equal(1d / 3, AnalysisMetrics.Measure(s, r, 0).Values[AnalysisMetric.DevelopmentWipSaturation]);
    }
    [Fact]
    public void FullWindowMatchesExistingMetricsAndNonApplicableQualityIsOmitted()
    {
        var s = Small; var r = new SimulationRunner().Run(s); var v = AnalysisMetrics.Measure(s, r, 0).Values;
        Assert.Equal(r.ThroughputPerFiveDays, v[AnalysisMetric.ThroughputPerFiveDays]!.Value, 12);
        Assert.Equal(r.AverageLeadTime, v[AnalysisMetric.AverageLeadTime]);
        Assert.Equal(r.AverageWip, v[AnalysisMetric.AverageWip]);
        Assert.Equal(r.DeveloperUtilization, v[AnalysisMetric.DeveloperUtilization]);
        Assert.DoesNotContain(AnalysisMetric.TotalDefectsFound, v.Keys);
    }
    [Fact]
    public void DeltaHandlesZeroMissingAndRatios()
    {
        Assert.Equal(new MetricDelta(-2, -25), MetricDelta.Between(8, 6));
        Assert.Equal(new MetricDelta(2, null), MetricDelta.Between(0, 2));
        Assert.Equal(new MetricDelta(null, null), MetricDelta.Between(null, 2));
        Assert.Equal(.04, MetricDelta.Between(.5, .54).Absolute!.Value, 12);
    }
    [Fact]
    public void MonteCarloUsesSameSeedSequenceAtEveryPointAndMatchesSingleRuns()
    {
        var s = Small with { TestingDistribution = new TriangularEffort(1, 2, 4), Quality = new() { Enabled = true, TestingDefectProbability = .2 } };
        var request = new SensitivityAnalysisRequest(s, SensitivityParameter.Testers, [1, 3], SensitivityMode.MonteCarlo, 4, 5);
        var a = new SensitivityAnalysisRunner().Run(request); var b = new SensitivityAnalysisRunner().Run(request);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        var expected = Enumerable.Range(0, 4).Select(i =>
        {
            var run = s with { TesterCount = 1, RandomSeed = MonteCarloRunner.DeriveSeed(s.RandomSeed, i) };
            return AnalysisMetrics.Measure(run, new SimulationRunner().Run(run), 5).Values[AnalysisMetric.ThroughputPerFiveDays]!.Value;
        });
        var distribution = DistributionStatistics.Summarize(expected);
        Assert.Equal(distribution.P85, a.Points[0].MeasurementWindow[AnalysisMetric.ThroughputPerFiveDays].P85);
        Assert.Equal(distribution.P95, a.Points[0].MeasurementWindow[AnalysisMetric.ThroughputPerFiveDays].P95);
    }
    [Fact]
    public void InvalidAnalysisFailsBeforeProgressAndCancellationIsHonored()
    {
        int progress = 0;
        var reporter = new InlineProgress(_ => progress++);
        Assert.Throws<ScenarioValidationException>(() => new SensitivityAnalysisRunner().Run(new(Small, SensitivityParameter.Testers, [1, -2]), reporter));
        Assert.Equal(0, progress);
        Assert.Throws<ScenarioValidationException>(() => Run(Small, SensitivityParameter.Testers, 1.5));
        Assert.Throws<ScenarioValidationException>(() => Run(Small, SensitivityParameter.TestingWipLimit, 0));
        Assert.Throws<ScenarioValidationException>(() => Run(Small, SensitivityParameter.TestingDefectProbability, .1));
        Assert.Throws<ScenarioValidationException>(() => new SensitivityAnalysisRunner().Run(new(Small, SensitivityParameter.Testers, [1], WarmUpDays: 30)));
        Assert.Throws<OperationCanceledException>(() => new SensitivityAnalysisRunner().Run(new(Small, SensitivityParameter.Testers, [1]), cancellationToken: new(true)));
    }
    [Fact]
    public void ExtremeWarningOnlyFlagsNearUnchangedSelectedMetrics()
    {
        AnalysisMeasurements M(double value) => new(0, 10, new Dictionary<AnalysisMetric, double?> { [AnalysisMetric.ThroughputPerFiveDays] = value });
        Assert.True(ModelValidationRunner.IsWeakResponse(M(10), M(10.4)));
        Assert.False(ModelValidationRunner.IsWeakResponse(M(10), M(10.5)));
        Assert.False(ModelValidationRunner.IsWeakResponse(M(0), M(.01)));
        Assert.True(ModelValidationRunner.IsWeakResponse(M(0), M(0)));
    }
    [Fact]
    public void ReworkWindowCountsOnlyIncludedDiscoveryBoundariesAndActualCapacity()
    {
        var s = new SimulationRequest { NumberOfWorkItems = 1, SimulationDays = 7, DeveloperCount = 1,
            DevelopmentEffort = 1, CodeReviewEffort = 1,
            Quality = new() { Enabled = true, CodeReviewDefectProbability = 1, ReworkWipLimit = 1,
                CodeReviewReworkEffortDistribution = new FixedEffort(1) } };
        var r = new SimulationRunner().Run(s);
        var window = AnalysisMetrics.Measure(s, r, 2).Values;
        Assert.Equal(3, r.TotalDefectsFound);
        Assert.Equal(2d, window[AnalysisMetric.TotalDefectsFound]);
        Assert.Equal(3d, window[AnalysisMetric.TotalReworkEffort]);
        Assert.Equal(.6, window[AnalysisMetric.ReworkDeveloperCapacityShare]);
        Assert.Equal(.6, window[AnalysisMetric.ReworkWipSaturation]);
        Assert.Equal(1d, window[AnalysisMetric.MaximumWaitingForReworkQueue]);
    }

    private sealed class InlineProgress(Action<AnalysisProgress> report) : IProgress<AnalysisProgress>
    { public void Report(AnalysisProgress value) => report(value); }
    private static double Throughput(SensitivityAnalysisResult r, int index) => r.Points[index].MeasurementWindow[AnalysisMetric.ThroughputPerFiveDays].P50!.Value;
}
