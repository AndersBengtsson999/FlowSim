using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class ExperimentRunnerTests
{
    private static TeamParameters SameTeam(SimulationRequest r) => new(r.DeveloperCount, r.TesterCount,
        r.DeveloperCapacityPerDay, r.TesterCapacityPerDay, r.DevelopmentWipLimit, r.CodeReviewWipLimit, r.TestingWipLimit);

    [Fact]
    public void AlternativeSharesImmutableWorkloadAndOnlyChangesResourceSettings()
    {
        var a = BaselineScenario.Create();
        var b = new TeamParameters(2, 3, 0.5, 0.75, 4, 2, 1).ApplyTo(a);
        Assert.Same(a.WorkItems, b.WorkItems);
        Assert.Equal(a.SimulationDays, b.SimulationDays);
        Assert.Equal(new Team(2, 3, 0.5, 0.75), b.Team);
        Assert.Equal(4, b.DevelopmentWipLimit);
        Assert.Equal(2, b.CodeReviewWipLimit);
        Assert.Equal(1, b.TestingWipLimit);
    }

    [Fact]
    public void IdenticalTeamsProduceIdenticalReports()
    {
        var request = new SimulationRequest();
        var result = new ExperimentRunner().Run(request, SameTeam(request));
        Assert.Equal(JsonSerializer.Serialize(result.A), JsonSerializer.Serialize(result.B));
        Assert.Equal(RunMetrics.From(new SimulationRunner().Run(request)), result.A.Metrics);
    }

    [Fact]
    public void SingleRunDoesNotCreateAnAlternative()
    {
        Assert.Null(new ExperimentRunner().Run(new()).B);
    }

    [Fact]
    public void SevenStatusCountsConserveTheWorkloadAndExposeWaitingStates()
    {
        var report = new ExperimentRunner().Run(new SimulationRequest
        {
            NumberOfWorkItems = 1, DevelopmentEffort = 1, CodeReviewEffort = 1, TestingEffort = 1, SimulationDays = 3
        }).A;
        Assert.Equal(1, report.History[0].WaitingForCodeReview);
        Assert.Equal(1, report.History[1].WaitingForTesting);
        Assert.Equal(1, report.History[2].Done);
        Assert.All(report.History, d => Assert.Equal(1, d.Total));
    }

    [Fact]
    public void InvalidAlternativeAndCancellationAreRejected()
    {
        var request = new SimulationRequest();
        Assert.Throws<ScenarioValidationException>(() => new ExperimentRunner().Run(request,
            SameTeam(request) with { CodeReviewWipLimit = 0 }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ExperimentRunner().Run(request, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void EmptyScenarioHasFiniteZeroReports()
    {
        var report = new ExperimentRunner().Run(new SimulationRequest { NumberOfWorkItems = 0 }).A;
        Assert.Equal(0, report.Unfinished.Count);
        Assert.Equal(0, report.Unfinished.AverageAge);
        Assert.Empty(report.OldestItems);
        Assert.All(report.History, p => Assert.Equal(0, p.Total));
    }
}
