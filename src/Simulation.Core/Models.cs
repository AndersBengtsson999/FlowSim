namespace Simulation.Core;

public enum WorkItemStatus { Backlog, Development, CodeReview, Testing, Done }

public sealed record Organization(string Name, IReadOnlyList<Team> Teams);
public sealed record Team(string Name, int NumberOfDevelopers, int NumberOfTesters, int WipLimit);

// Dates are elapsed simulation days; day zero is the scenario start.
public sealed record WorkItem(int Id, string Title, double Size, double Complexity,
    int Priority = 0, int CreatedAt = 0)
{
    public WorkItemStatus Status { get; init; } = WorkItemStatus.Backlog;
    public int? StartedAt { get; init; }
    public int? CompletedAt { get; init; }
}

public sealed record Dependency(int WorkItemId, int DependsOnWorkItemId);
public sealed record Sprint(int Start, int End)
{
    public int Duration => End - Start;
}
public sealed record Release(int Date, IReadOnlyList<WorkItem> IncludedWorkItems);

public sealed record SimulationScenario(
    Organization Organization,
    IReadOnlyList<WorkItem> WorkItems,
    IReadOnlyList<Dependency> Dependencies,
    int DurationDays,
    double DeveloperCapacityPerDay,
    double TesterCapacityPerDay,
    int SprintLength,
    int ReleaseInterval,
    int RandomSeed);

public sealed record DailySnapshot(int Day, int Wip, int BlockedItems,
    int UnfinishedItems, double DevelopmentWork, double TestingWork,
    IReadOnlyDictionary<int, WorkItemStatus> Statuses);

public sealed record SimulationResult(
    int RandomSeed,
    int CompletedWorkItems,
    double Throughput,
    double AverageLeadTime,
    double AverageCycleTime,
    double AverageWip,
    double BlockedTimeFraction,
    double DeveloperUtilization,
    double TesterUtilization,
    IReadOnlyList<WorkItem> WorkItems,
    IReadOnlyList<DailySnapshot> Days,
    IReadOnlyList<Sprint> Sprints,
    IReadOnlyList<Release> Releases);
