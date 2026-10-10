namespace Simulation.Core;

public enum WorkItemStatus
{
    Backlog, Development, WaitingForCodeReview, CodeReview, WaitingForTesting, Testing, Done, WaitingForRework, Rework, ReadyForRelease, Released
}

// Capacity is an abstract work unit, not hours. These are nominal daily capacities.
public sealed record Team(int DeveloperCount = 5, int TesterCount = 2,
    double DeveloperCapacityPerDay = 1, double TesterCapacityPerDay = 1)
{
    public double DeveloperAvailability { get; init; } = 1;
    public double TesterAvailability { get; init; } = 1;
    public double AvailableDeveloperCapacity => TotalDeveloperCapacity * DeveloperAvailability;
    public double AvailableTesterCapacity => TotalTesterCapacity * TesterAvailability;
    public double TotalDeveloperCapacity => DeveloperCount * DeveloperCapacityPerDay;
    public double TotalTesterCapacity => TesterCount * TesterCapacityPerDay;
}

public sealed record SimulationScenario(string Name, int SimulationDays, Team Team,
    int DevelopmentWipLimit, int CodeReviewWipLimit, int TestingWipLimit,
    IReadOnlyList<WorkItem> WorkItems, int RandomSeed = 12345)
{
    public DependencySettings ResidualDependencies { get; init; } = new();
    public ReleaseSettings Release { get; init; } = new();
    public SkillSettings Skills { get; init; } = new();
    public TechnicalDebtSettings Debt { get; init; } = new();
    public StageProductivity Productivity { get; init; } = new();
    public DefectSettings Quality { get; init; } = new();
    public WorkArrivalMode ArrivalMode { get; init; } = WorkArrivalMode.FixedBacklog;
    public decimal WorkItemsPerDay { get; init; } = 0.8m;
    public IEffortDistribution DevelopmentArrivalEffort { get; init; } = new FixedEffort(5);
    public IEffortDistribution CodeReviewArrivalEffort { get; init; } = new FixedEffort(1);
    public IEffortDistribution TestingArrivalEffort { get; init; } = new FixedEffort(2);
}

public sealed record StateTransition(WorkItemStatus From, WorkItemStatus To, int Day);

public sealed record WorkItemDaySnapshot(string Id, WorkItemStatus State,
    double RemainingDevelopmentEffort, double RemainingCodeReviewEffort, double RemainingTestingEffort,
    double DevelopmentWork, double CodeReviewWork, double TestingWork,
    int CreatedDay, WorkItemStatus StateDuringDay, bool DependencyBlocked,
    double ReworkWork = 0, double RemainingReworkEffort = 0, double CollaborationDevelopmentCapacity = 0)
{
    public ResidualDependency? ResidualDependency { get; init; }
    public bool RequiresSpecialist { get; init; }
    public string SkillMarker => RequiresSpecialist ? "Specialist Development" : "";
    public DeliveryCost? DeliveryCost { get; init; }
    public DevelopmentPlan? DevelopmentPlan { get; init; }
    public string ImplementationSummary => DevelopmentPlan is { } p ? $"{(p.IsShortcut ? "Shortcut" : "Normal")} · Base {p.BaseEffort:0.##} · Start overhead {p.Overhead:P1} · Final {p.FinalEffort:0.##} · Saved {p.SavedEffort:0.##}" : "";
    // Absent in pre-0.4 history: work then implied capacity at productivity 1x.
    public StageCapacity? ConsumedCapacity { get; init; }
    public double PrimaryDevelopmentCapacity => ConsumedCapacity is null ? DevelopmentWork - 0.5 * CollaborationDevelopmentCapacity : ConsumedCapacity.Development - CollaborationDevelopmentCapacity;
    public double UsedDevelopmentCapacity => ConsumedCapacity?.Development ?? (DevelopmentWork + 0.5 * CollaborationDevelopmentCapacity);
}

// Day is zero-based. Occupancy is sampled after admission; item states are sampled at day end.
public sealed record DailySnapshot(int Day, int DevelopmentWip, int ReviewWip, int TestingWip,
    int BlockedItems, int UnfinishedItems, double DevelopmentWork, double ReviewWork, double TestingWork,
    IReadOnlyList<WorkItemDaySnapshot> Items,
    double AvailableDeveloperCapacity, double AvailableTesterCapacity,
    double UsedReworkDeveloperCapacity = 0, int ReworkWip = 0, double CollaborationDevelopmentCapacity = 0)
{
    // Active Development at day end with positive remaining effort and no raw Development capacity received that day.
    public int SpecialistWorkWaiting { get; init; }
    public DebtObservation? Debt { get; init; }
    public double UsedDebtRepaymentCapacity => Debt?.RepaymentCapacity ?? 0;
    public int Wip => DevelopmentWip + ReviewWip + TestingWip + ReworkWip;
    private int Count(WorkItemStatus state) => Items.Count(w => w.CreatedDay <= Day && w.State == state);
    public int WaitingForDependencyCount => Items.Count(w => w.CreatedDay <= Day && w.State == WorkItemStatus.Backlog && w.ResidualDependency is { } d && d.ResolutionDay > (long)Day + 1);
    public int BacklogCount => Count(WorkItemStatus.Backlog);
    public int DevelopmentCount => Count(WorkItemStatus.Development);
    public int WaitingForCodeReviewCount => Count(WorkItemStatus.WaitingForCodeReview);
    public int CodeReviewCount => Count(WorkItemStatus.CodeReview);
    public int WaitingForTestingCount => Count(WorkItemStatus.WaitingForTesting);
    public int TestingCount => Count(WorkItemStatus.Testing);
    public int WaitingForReworkCount => Count(WorkItemStatus.WaitingForRework);
    public int ReworkCount => Count(WorkItemStatus.Rework);
    public int DoneCount => Count(WorkItemStatus.Done) + Count(WorkItemStatus.Released);
    public int ReadyForReleaseCount => Count(WorkItemStatus.ReadyForRelease);
    public int TotalWip => DevelopmentCount + WaitingForCodeReviewCount + CodeReviewCount + WaitingForTestingCount + TestingCount + WaitingForReworkCount + ReworkCount + ReadyForReleaseCount;
    // Absent in pre-0.4 history: work then implied capacity at productivity 1x.
    public StageCapacity? ConsumedCapacity { get; init; }
    public double PrimaryDevelopmentCapacity => ConsumedCapacity is null ? DevelopmentWork - 0.5 * CollaborationDevelopmentCapacity : ConsumedCapacity.Development - CollaborationDevelopmentCapacity;
    public double UsedDevelopmentCapacity => ConsumedCapacity?.Development ?? (DevelopmentWork + 0.5 * CollaborationDevelopmentCapacity);
    public double UsedReviewCapacity => ConsumedCapacity?.CodeReview ?? ReviewWork;
    public double UsedDeveloperCapacity => UsedDevelopmentCapacity + UsedReviewCapacity + UsedReworkDeveloperCapacity + UsedDebtRepaymentCapacity;
    public double UsedTesterCapacity => ConsumedCapacity?.Testing ?? TestingWork;
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
    public string SimulationModelVersion { get; init; } = SimulationModel.Version;
    public int RandomSeed { get; init; }
    public int IncompleteWorkItems => TotalWorkItems - CompletedWorkItems;
    public double ThroughputPerDay => Throughput;
    public double ThroughputPerFiveDays => ThroughputPerDay * 5;
}
