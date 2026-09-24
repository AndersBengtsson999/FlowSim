using Simulation.Core;

namespace Simulation.Application;

/// <summary>Only resource settings vary; workload and simulation horizon stay paired.</summary>
public sealed record TeamParameters(int Developers, int Testers, double DeveloperCapacity,
    double TesterCapacity, int WipLimit)
{
    public SimulationScenario ApplyTo(SimulationScenario scenario) => scenario with
    {
        Organization = new Organization(scenario.Organization.Name,
            [new Team("Team B", Developers, Testers, WipLimit)]),
        DeveloperCapacityPerDay = DeveloperCapacity,
        TesterCapacityPerDay = TesterCapacity
    };
}

public sealed record StatusPoint(int Day, double Backlog, double Development,
    double CodeReview, double Testing, double Done)
{
    public double Total => Backlog + Development + CodeReview + Testing + Done;
}

public sealed record UnfinishedItem(int Id, string Title, WorkItemStatus Status,
    int Age, int? CycleAge, bool DependencyBlocked);

public sealed record UnfinishedSummary(double Count, double Backlog, double Development,
    double CodeReview, double Testing, double DependencyBlocked, double ReadyBacklog,
    double AverageAge, double OldestAge);

public sealed record ScenarioReport(RunMetrics Metrics, IReadOnlyList<StatusPoint> History,
    UnfinishedSummary Unfinished, IReadOnlyList<UnfinishedItem> OldestItems, int RunCount);

public sealed record ExperimentResult(ScenarioReport A, ScenarioReport? B);

public static class ResultAnalysis
{
    private static ScenarioReport AnalyzeBase(SimulationResult result)
    {
        var horizon = result.Days.Count;
        var unfinished = result.WorkItems.Where(w => w.CreatedAt < horizon && w.Status != WorkItemStatus.Done).ToArray();
        // Dependency-blocked at the horizon is supplied separately by the scenario-aware overload.
        var arrivals = result.WorkItems.ToDictionary(w => w.Id, w => w.CreatedAt);
        var history = result.Days.Select(d => new StatusPoint(d.Day + 1,
            Count(d, arrivals, WorkItemStatus.Backlog), Count(d, arrivals, WorkItemStatus.Development),
            Count(d, arrivals, WorkItemStatus.CodeReview), Count(d, arrivals, WorkItemStatus.Testing), Count(d, arrivals, WorkItemStatus.Done))).ToArray();
        var items = unfinished.Select(w => new UnfinishedItem(w.Id, w.Title, w.Status,
            horizon - w.CreatedAt, w.StartedAt is int start ? horizon - start : null, false)).ToArray();
        return new ScenarioReport(RunMetrics.From(result), history,
            new UnfinishedSummary(unfinished.Length,
                unfinished.Count(w => w.Status == WorkItemStatus.Backlog),
                unfinished.Count(w => w.Status == WorkItemStatus.Development),
                unfinished.Count(w => w.Status == WorkItemStatus.CodeReview),
                unfinished.Count(w => w.Status == WorkItemStatus.Testing), 0,
                unfinished.Count(w => w.Status == WorkItemStatus.Backlog),
                items.Length == 0 ? 0 : items.Average(w => w.Age),
                items.Length == 0 ? 0 : items.Max(w => w.Age)),
            items.OrderByDescending(w => w.Age).ThenBy(w => w.Id).Take(20).ToArray(), 1);
    }

    public static ScenarioReport Analyze(SimulationResult result, SimulationScenario scenario)
    {
        var report = AnalyzeBase(result);
        var done = result.WorkItems.Where(w => w.Status == WorkItemStatus.Done).Select(w => w.Id).ToHashSet();
        var blockedIds = scenario.Dependencies.Where(d => !done.Contains(d.DependsOnWorkItemId))
            .Select(d => d.WorkItemId).ToHashSet();
        var blocked = result.WorkItems.Count(w => w.CreatedAt < scenario.DurationDays
            && w.Status == WorkItemStatus.Backlog && blockedIds.Contains(w.Id));
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

    private static int Count(DailySnapshot day, IReadOnlyDictionary<int, int> arrivals, WorkItemStatus status) =>
        day.Statuses.Count(p => p.Value == status && arrivals[p.Key] <= day.Day);

    internal static ScenarioReport Mean(IReadOnlyList<ScenarioReport> reports)
    {
        if (reports.Count == 1) return reports[0];
        var first = reports[0];
        double M(Func<RunMetrics, double> get) => reports.Average(r => get(r.Metrics));
        double U(Func<UnfinishedSummary, double> get) => reports.Average(r => get(r.Unfinished));
        var metrics = new RunMetrics(first.Metrics.Seed, M(m => m.CompletedWorkItems), M(m => m.Throughput),
            M(m => m.AverageLeadTime), M(m => m.AverageCycleTime), M(m => m.AverageWip),
            M(m => m.BlockedTimeFraction), M(m => m.DeveloperUtilization), M(m => m.TesterUtilization));
        var history = Enumerable.Range(0, first.History.Count).Select(i => new StatusPoint(i + 1,
            reports.Average(r => r.History[i].Backlog), reports.Average(r => r.History[i].Development),
            reports.Average(r => r.History[i].CodeReview), reports.Average(r => r.History[i].Testing),
            reports.Average(r => r.History[i].Done))).ToArray();
        return new ScenarioReport(metrics, history,
            new UnfinishedSummary(U(u => u.Count), U(u => u.Backlog), U(u => u.Development), U(u => u.CodeReview),
                U(u => u.Testing), U(u => u.DependencyBlocked), U(u => u.ReadyBacklog), U(u => u.AverageAge), U(u => u.OldestAge)),
            [], reports.Count);
    }
}

public sealed class ExperimentRunner
{
    public ExperimentResult Run(SimulationRequest request, TeamParameters? alternative = null, int runs = 1,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (runs is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(runs));
        var a = new List<ScenarioReport>();
        var b = new List<ScenarioReport>();
        var engine = new SimulationEngine();
        for (var index = 0; index < runs; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenarioA = (request with { RandomSeed = unchecked(request.RandomSeed + index) }).ToScenario();
            // The B scenario shares the exact same immutable items/dependencies and seed.
            var scenarioB = alternative?.ApplyTo(scenarioA);
            if (scenarioB is not null) ScenarioValidator.Validate(scenarioB);
            a.Add(ResultAnalysis.Analyze(engine.Run(scenarioA, cancellationToken), scenarioA));
            if (scenarioB is not null)
                b.Add(ResultAnalysis.Analyze(engine.Run(scenarioB, cancellationToken), scenarioB));
            progress?.Report(index + 1);
        }
        return new ExperimentResult(ResultAnalysis.Mean(a), b.Count == 0 ? null : ResultAnalysis.Mean(b));
    }
}
