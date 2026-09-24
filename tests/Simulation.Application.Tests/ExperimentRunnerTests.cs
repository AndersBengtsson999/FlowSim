using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class ExperimentRunnerTests
{
    private static TeamParameters SameTeam(SimulationRequest r) => new(r.NumberOfDevelopers,
        r.NumberOfTesters, r.DeveloperCapacity, r.TesterCapacity, r.WipLimit);

    [Fact]
    public void AlternativeSharesWorkloadDependenciesSeedAndHorizon()
    {
        var a = new SimulationRequest { DependencyProbability = 1 }.ToScenario();
        var b = new TeamParameters(2, 3, 4, 5, 6).ApplyTo(a);
        Assert.Same(a.WorkItems, b.WorkItems);
        Assert.Same(a.Dependencies, b.Dependencies);
        Assert.Equal(a.RandomSeed, b.RandomSeed);
        Assert.Equal(a.DurationDays, b.DurationDays);
        Assert.Equal(a.SprintLength, b.SprintLength);
        Assert.Equal(a.ReleaseInterval, b.ReleaseInterval);
        Assert.Equal(new Team("Team B", 2, 3, 6), b.Organization.Teams[0]);
        Assert.Equal(4, b.DeveloperCapacityPerDay);
        Assert.Equal(5, b.TesterCapacityPerDay);
    }

    [Fact]
    public void IdenticalSettingsProduceIdenticalReports()
    {
        var request = new SimulationRequest();
        var result = new ExperimentRunner().Run(request, SameTeam(request));
        Assert.Equal(JsonSerializer.Serialize(result.A), JsonSerializer.Serialize(result.B));
        Assert.Equal(RunMetrics.From(new SimulationRunner().Run(request)), result.A.Metrics);
    }

    [Fact]
    public void RemovingTestBottleneckChangesDeliveryAndUnfinishedWork()
    {
        var request = new SimulationRequest { NumberOfTesters = 0, NumberOfWorkItems = 8,
            Size = 1, Complexity = 1, DurationDays = 12, DependencyProbability = 0 };
        var result = new ExperimentRunner().Run(request, SameTeam(request) with { Testers = 2 });
        Assert.Equal(0, result.A.Metrics.CompletedWorkItems);
        Assert.Equal(8, result.A.Unfinished.Testing);
        Assert.NotNull(result.B);
        Assert.Equal(8, result.B.Metrics.CompletedWorkItems);
        Assert.Equal(0, result.B.Unfinished.Count);
        Assert.Empty(result.B.OldestItems);
        Assert.Equal(0, result.B.Unfinished.AverageAge);
    }

    [Fact]
    public void HistoryConservesItemsAndUsesEndOfDayStatus()
    {
        var result = new ExperimentRunner().Run(new SimulationRequest
            { NumberOfWorkItems = 1, Size = 1, Complexity = 1, DurationDays = 3 }).A;
        Assert.Equal(new StatusPoint(1, 0, 0, 1, 0, 0), result.History[0]);
        Assert.Equal(new StatusPoint(2, 0, 0, 0, 1, 0), result.History[1]);
        Assert.Equal(new StatusPoint(3, 0, 0, 0, 0, 1), result.History[2]);
        Assert.All(result.History, p => Assert.Equal(1, p.Total));
        Assert.Equal(result.Metrics.CompletedWorkItems, result.History[^1].Done);
    }

    [Fact]
    public void HorizonBlockersUseFinalStatusRatherThanLastDayStart()
    {
        var scenario = new SimulationScenario(new Organization("O", [new Team("T", 1, 1, 2)]),
            [new(1, "A", 1, 1), new(2, "B", 1, 1)], [new(2, 1)], 3, 1, 1, 3, 3, 42);
        var raw = new SimulationEngine().Run(scenario);
        var report = ResultAnalysis.Analyze(raw, scenario);
        Assert.Equal(1, raw.Days[^1].BlockedItems); // blocked at start of last day
        Assert.Equal(0, report.Unfinished.DependencyBlocked); // predecessor is now Done
        Assert.Equal(1, report.Unfinished.ReadyBacklog);
        Assert.Equal(3, report.Unfinished.AverageAge);
        Assert.Null(Assert.Single(report.OldestItems).CycleAge);
    }

    [Fact]
    public void UnfinishedAgesRespectArrivalsAndDistinguishCycleAge()
    {
        var scenario = new SimulationScenario(new Organization("O", [new Team("T", 1, 0, 1)]),
            [new(1, "A", 1, 1, CreatedAt: 2), new(2, "Future", 1, 1, CreatedAt: 20)], [],
            5, 1, 1, 5, 5, 42);
        var report = ResultAnalysis.Analyze(new SimulationEngine().Run(scenario), scenario);
        var item = Assert.Single(report.OldestItems);
        Assert.Equal(3, item.Age);
        Assert.Equal(3, item.CycleAge);
        Assert.Equal(1, report.Unfinished.Count);
        Assert.Equal(0, report.History[0].Total);
        Assert.Equal(1, report.History[^1].Total);
    }

    [Fact]
    public void BatchMeansMatchPairedIndividualRunsIncludingDailyCounts()
    {
        var request = new SimulationRequest { NumberOfWorkItems = 6, DurationDays = 8, RandomSeed = int.MaxValue };
        var b = SameTeam(request) with { Testers = 0 };
        var runner = new ExperimentRunner();
        var batch = runner.Run(request, b, 3);
        var individual = Enumerable.Range(0, 3).Select(i => runner.Run(
            request with { RandomSeed = unchecked(request.RandomSeed + i) }, b)).ToArray();
        Assert.Equal(3, batch.A.RunCount);
        Assert.Empty(batch.A.OldestItems);
        Assert.Equal(individual.Average(r => r.A.Metrics.Throughput), batch.A.Metrics.Throughput);
        Assert.Equal(individual.Average(r => r.B!.Unfinished.Count), batch.B!.Unfinished.Count);
        Assert.Equal(individual.Average(r => r.A.Unfinished.OldestAge), batch.A.Unfinished.OldestAge);
        for (var day = 0; day < 8; day++)
        {
            Assert.Equal(individual.Average(r => r.B!.History[day].Testing), batch.B.History[day].Testing);
            Assert.Equal(6, batch.A.History[day].Total, 10);
        }
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(runner.Run(request, b, 3)));
    }

    [Fact]
    public void All100PairsUseTheSameSettingsAndRemainReproducible()
    {
        var request = new SimulationRequest { NumberOfWorkItems = 2, DurationDays = 5 };
        var result = new ExperimentRunner().Run(request, SameTeam(request), 100);
        Assert.Equal(100, result.A.RunCount);
        Assert.Equal(JsonSerializer.Serialize(result.A), JsonSerializer.Serialize(result.B));
    }

    [Fact]
    public void EmptyWorkloadProducesEmptyFiniteReports()
    {
        var report = new ExperimentRunner().Run(new SimulationRequest { NumberOfWorkItems = 0 }).A;
        Assert.Equal(0, report.Unfinished.Count);
        Assert.Equal(0, report.Unfinished.OldestAge);
        Assert.Empty(report.OldestItems);
        Assert.All(report.History, p => Assert.Equal(0, p.Total));
    }

    [Fact]
    public void InvalidAlternativeAndCancellationAreRejected()
    {
        var runner = new ExperimentRunner();
        var request = new SimulationRequest();
        Assert.Throws<ArgumentException>(() => runner.Run(request, SameTeam(request) with { WipLimit = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(request, runs: 101));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => runner.Run(request, cancellationToken: cancellation.Token));
    }
}
