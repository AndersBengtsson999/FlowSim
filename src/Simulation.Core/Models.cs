namespace Simulation.Core;

public enum WorkItemStatus
{
    Backlog, Development, WaitingForCodeReview, CodeReview, WaitingForTesting, Testing, Done, WaitingForRework, Rework
}

// Capacity is an abstract work unit, not hours. These are nominal daily capacities.
public sealed record Team(int DeveloperCount = 5, int TesterCount = 2,
    double DeveloperCapacityPerDay = 1, double TesterCapacityPerDay = 1)
{
    public double TotalDeveloperCapacity => DeveloperCount * DeveloperCapacityPerDay;
    public double TotalTesterCapacity => TesterCount * TesterCapacityPerDay;
}

public sealed record SimulationScenario(string Name, int SimulationDays, Team Team,
    int DevelopmentWipLimit, int CodeReviewWipLimit, int TestingWipLimit,
    IReadOnlyList<WorkItem> WorkItems, int RandomSeed = 12345)
{
    public DefectSettings Quality { get; init; } = new();
}

public sealed record StateTransition(WorkItemStatus From, WorkItemStatus To, int Day);

public sealed record WorkItemDaySnapshot(string Id, WorkItemStatus State,
    double RemainingDevelopmentEffort, double RemainingCodeReviewEffort, double RemainingTestingEffort,
    double DevelopmentWork, double CodeReviewWork, double TestingWork,
    int CreatedDay, WorkItemStatus StateDuringDay, bool DependencyBlocked,
    double ReworkWork = 0, double RemainingReworkEffort = 0);

// Day is zero-based. Occupancy is sampled after admission; item states are sampled at day end.
public sealed record DailySnapshot(int Day, int DevelopmentWip, int ReviewWip, int TestingWip,
    int BlockedItems, int UnfinishedItems, double DevelopmentWork, double ReviewWork, double TestingWork,
    IReadOnlyList<WorkItemDaySnapshot> Items,
    double AvailableDeveloperCapacity, double AvailableTesterCapacity,
    double UsedReworkDeveloperCapacity = 0, int ReworkWip = 0)
{
    public int Wip => DevelopmentWip + ReviewWip + TestingWip + ReworkWip;
    private int Count(WorkItemStatus state) => Items.Count(w => w.CreatedDay <= Day && w.State == state);
    public int BacklogCount => Count(WorkItemStatus.Backlog);
    public int DevelopmentCount => Count(WorkItemStatus.Development);
    public int WaitingForCodeReviewCount => Count(WorkItemStatus.WaitingForCodeReview);
    public int CodeReviewCount => Count(WorkItemStatus.CodeReview);
    public int WaitingForTestingCount => Count(WorkItemStatus.WaitingForTesting);
    public int TestingCount => Count(WorkItemStatus.Testing);
    public int WaitingForReworkCount => Count(WorkItemStatus.WaitingForRework);
    public int ReworkCount => Count(WorkItemStatus.Rework);
    public int DoneCount => Count(WorkItemStatus.Done);
    public int TotalWip => DevelopmentCount + WaitingForCodeReviewCount + CodeReviewCount + WaitingForTestingCount + TestingCount + WaitingForReworkCount + ReworkCount;
    public double UsedDeveloperCapacity => DevelopmentWork + ReviewWork + UsedReworkDeveloperCapacity;
    public double UsedTesterCapacity => TestingWork;
}

public sealed record SimulationResult(string ScenarioName, int CompletedWorkItems, double Throughput,
    double AverageLeadTime, double AverageCycleTime, double AverageWip, double BlockedTimeFraction,
    double DeveloperUtilization, double TesterUtilization, double ReviewUtilization,
    double DevelopmentUtilization, double AverageReviewWip, double AverageReviewTime,
    IReadOnlyList<WorkItemResult> WorkItems, IReadOnlyList<DailySnapshot> Days,
    int SimulationDays, int TotalWorkItems, double AverageActiveTime, double AverageWaitingTime,
    double AverageBlockedTime, int MaximumWaitingForCodeReviewQueue, int MaximumWaitingForTestingQueue)
{
    public int TotalDefectsFound { get; init; }
    public int CodeReviewDefectsFound { get; init; }
    public int TestingDefectsFound { get; init; }
    public int WorkItemsWithDefects { get; init; }
    public int TotalReworkCount { get; init; }
    public double TotalReworkEffort { get; init; }
    public double AverageReworkEffortPerCompletedItem { get; init; }
    public double ReworkDeveloperCapacityShare { get; init; }
    public double AverageCodeReviewAttempts { get; init; }
    public double AverageTestingAttempts { get; init; }
    public int RandomSeed { get; init; }
    public int IncompleteWorkItems => TotalWorkItems - CompletedWorkItems;
    public double ThroughputPerDay => Throughput;
    public double ThroughputPerFiveDays => ThroughputPerDay * 5;
}
