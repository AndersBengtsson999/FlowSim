using Simulation.Core;

namespace Simulation.Application;

/// <summary>Resource-only A/B comparison; no randomized runs.</summary>
public sealed record TeamParameters(int Developers, int Testers, double DeveloperCapacity,
    double TesterCapacity, int DevelopmentWipLimit, int CodeReviewWipLimit, int TestingWipLimit)
{
    public SimulationScenario ApplyTo(SimulationScenario scenario) => scenario with
    {
        Team = new Team(Developers, Testers, DeveloperCapacity, TesterCapacity),
        DevelopmentWipLimit = DevelopmentWipLimit,
        CodeReviewWipLimit = CodeReviewWipLimit,
        TestingWipLimit = TestingWipLimit
    };
}

public sealed record StatusPoint(int Day, double Backlog, double Development,
    double CodeReview, double Testing, double Done, double WaitingForCodeReview, double WaitingForTesting,
    double WaitingForRework = 0, double Rework = 0)
{
    public double Total => Backlog + Development + CodeReview + Testing + Done + WaitingForCodeReview + WaitingForTesting + WaitingForRework + Rework;
}

public sealed record UnfinishedItem(string Id, string Title, WorkItemStatus Status,
    int Age, int? CycleAge, bool DependencyBlocked);

public sealed record UnfinishedSummary(double Count, double Backlog, double Development,
    double CodeReview, double Testing, double DependencyBlocked, double ReadyBacklog,
    double AverageAge, double OldestAge, double WaitingForCodeReview, double WaitingForTesting,
    double WaitingForRework = 0, double Rework = 0);

public sealed record ScenarioReport(RunMetrics Metrics, IReadOnlyList<StatusPoint> History,
    UnfinishedSummary Unfinished, IReadOnlyList<UnfinishedItem> OldestItems);

public sealed record ExperimentResult(ScenarioReport A, ScenarioReport? B);

public static class ResultAnalysis
{
    private static ScenarioReport AnalyzeBase(SimulationResult result)
    {
        var horizon = result.Days.Count;
        var unfinished = result.WorkItems.Where(w => w.CreatedDay < horizon && w.State != WorkItemStatus.Done).ToArray();
        // Dependency-blocked at the horizon is supplied separately by the scenario-aware overload.
        var history = result.Days.Select(d => new StatusPoint(d.Day + 1,
            d.BacklogCount, d.DevelopmentCount, d.CodeReviewCount, d.TestingCount, d.DoneCount,
            d.WaitingForCodeReviewCount, d.WaitingForTestingCount, d.WaitingForReworkCount, d.ReworkCount)).ToArray();
        var items = unfinished.Select(w => new UnfinishedItem(w.Id, w.Name, w.State,
            horizon - w.CreatedDay, w.DevelopmentStartedDay is int start ? horizon - start : null, false)).ToArray();
        return new ScenarioReport(RunMetrics.From(result), history,
            new UnfinishedSummary(unfinished.Length,
                unfinished.Count(w => w.State == WorkItemStatus.Backlog),
                unfinished.Count(w => w.State == WorkItemStatus.Development),
                unfinished.Count(w => w.State == WorkItemStatus.CodeReview),
                unfinished.Count(w => w.State == WorkItemStatus.Testing), 0,
                unfinished.Count(w => w.State == WorkItemStatus.Backlog),
                items.Length == 0 ? 0 : items.Average(w => w.Age),
                items.Length == 0 ? 0 : items.Max(w => w.Age),
                unfinished.Count(w => w.State == WorkItemStatus.WaitingForCodeReview),
                unfinished.Count(w => w.State == WorkItemStatus.WaitingForTesting),
                unfinished.Count(w => w.State == WorkItemStatus.WaitingForRework),
                unfinished.Count(w => w.State == WorkItemStatus.Rework)),
            items.OrderByDescending(w => w.Age).ThenBy(w => w.Id, StringComparer.Ordinal).Take(20).ToArray());
    }

    public static ScenarioReport Analyze(SimulationResult result, SimulationScenario scenario)
    {
        var report = AnalyzeBase(result);
        var done = result.WorkItems.Where(w => w.State == WorkItemStatus.Done).Select(w => w.Id).ToHashSet();
        var blockedIds = scenario.WorkItems.Where(w => w.Dependencies.Any(id => !done.Contains(id)))
            .Select(w => w.Id).ToHashSet();
        var blocked = result.WorkItems.Count(w => w.CreatedDay < scenario.SimulationDays
            && w.State == WorkItemStatus.Backlog && blockedIds.Contains(w.Id));
        return report with
        {
            Unfinished = report.Unfinished with
            {
                DependencyBlocked = blocked, ReadyBacklog = report.Unfinished.Backlog - blocked
            },
            OldestItems = report.OldestItems.Select(w => w with
            { DependencyBlocked = w.Status == WorkItemStatus.Backlog && blockedIds.Contains(w.Id) }).ToArray()
        };
    }


}

public sealed class ExperimentRunner
{
    public ExperimentResult Run(SimulationRequest request, TeamParameters? alternative = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var a = request.ToScenario();
        var b = alternative?.ApplyTo(a);
        if (b is not null) ScenarioValidator.Validate(b);
        var engine = new SimulationEngine();
        var reportA = ResultAnalysis.Analyze(engine.Run(a, cancellationToken), a);
        var reportB = b is null ? null : ResultAnalysis.Analyze(engine.Run(b, cancellationToken), b);
        return new ExperimentResult(reportA, reportB);
    }
}
