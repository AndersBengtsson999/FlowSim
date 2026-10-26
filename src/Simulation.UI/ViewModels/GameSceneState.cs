using Simulation.Application;
using Simulation.Core;
using System.Runtime.CompilerServices;
using Simulation.UI.Controls.GameWorld;

namespace Simulation.UI.ViewModels;

/// <summary>A bounded, read-only presentation of one authoritative Live snapshot. No worker scheduler.</summary>
public sealed record GameArea(string Name, int Active, int Waiting, string Detail)
{
    public int Count => Active + Waiting;
    public int VisiblePackages => Math.Min(8, Count);
    // Reserve space for both categories when both are present; eight glyphs total.
    public int VisibleActivePackages => Math.Min(Active, Math.Max(4, 8 - Waiting));
    public int VisibleWaitingPackages => Math.Min(Waiting, 8 - VisibleActivePackages);
    public int GroupedPackages => Math.Max(0, Count - VisiblePackages);
}
public sealed record GameSceneState(int Day, IReadOnlyList<GameArea> Areas, int Developers, int Testers,
    int Specialists, double Debt, double DebtRatio, int ReleasedToday, string ReleasePolicy,
    string Capacity, bool HasSession)
{
    private sealed class Identity { public Guid Value { get; } = Guid.NewGuid(); }
    private static readonly ConditionalWeakTable<SimulationSession, Identity> identities = new();
    public IReadOnlyList<InspectedWorkItem> InspectionItems { get; init; } = [];
    public Guid SessionIdentity { get; init; }
    public IReadOnlyList<ItemJourney> Journeys { get; init; } = [];
    public int ChangedItems { get; init; }
    public IReadOnlyDictionary<string, QueuePressure> QueuePressures { get; init; } = new Dictionary<string, QueuePressure>();
    public IReadOnlyDictionary<string,QueueTrend> QueueTrends { get; init; } = new Dictionary<string,QueueTrend>();
    public int AwaitingCustomerLaunch { get; init; }
    public int ActiveDevelopment { get; init; }
    public int ActiveRework { get; init; }
    public int WaitingRework { get; init; }
    public int DependencyWaiting { get; init; }
    public double AvailableDeveloperCapacity { get; init; }
    public double AvailableTesterCapacity { get; init; }
    public int DevelopmentWipLimit { get; init; }
    public int ReviewWipLimit { get; init; }
    public int TestingWipLimit { get; init; }
    public double UsedDeveloperCapacity { get; init; }
    public double UsedDevelopmentCapacity { get; init; }
    public double UsedReviewCapacity { get; init; }
    public double UsedReworkCapacity { get; init; }
    public double UsedDebtRepaymentCapacity { get; init; }
    public double UsedTesterCapacity { get; init; }
    public double CollaborationCapacity { get; init; }
    // Conservative pose ceilings, not inferred employee assignments. Never exceed either the
    // completed day's capacity or the current configured team's capacity/available headcount.
    public int DeveloperPoseCeiling { get; init; }
    public int TesterPoseCeiling { get; init; }
    public int VisibleDevelopers => Math.Min(6, Developers);
    public int VisibleTesters => Math.Min(4, Testers);
    public int VisibleSpecialists => Math.Min(VisibleDevelopers, Specialists);
    public string Summary => !HasSession ? "Start a Live session to populate the game world." :
        $"Day {Day}. " + string.Join(". ", Areas.Select(a => $"{a.Name}: {a.Count}. {a.Detail}")) +
        $". Technical debt {Debt:0.##}, ratio {DebtRatio:P1}. Released this day {ReleasedToday}. {Capacity}. Workers illustrate aggregate capacity, not individual assignments.";
    public static GameSceneState From(LiveSimulation? live)
    {
        if (live is null) return new(0, [], 0, 0, 0, 0, 0, 0, "No session", "No observed capacity yet", false);
        var s = live.Session; var d = live.CurrentSnapshot; var c = s.Configuration;
        var previousDone = s.Days.Count > 1 ? s.Days[^2].DoneCount : 0;
        var released = s.CurrentDay == 0 ? 0 : d.DoneCount - previousDone;
        // Read only the current day's suffix, retaining within-day order. Bound copied animation data.
        var journeys = new List<ItemJourney>(); var changed = 0;
        foreach (var item in s.WorkItems)
        {
            var transitions = item.Transitions;
            if (transitions.Count == 0 || transitions[^1].Day != s.CurrentDay) continue;
            changed++;
            if (journeys.Count >= PackageMotion.MaximumPackages) continue;
            var first = transitions.Count - 1;
            while (first > 0 && transitions[first - 1].Day == s.CurrentDay) first--;
            if (transitions.Count - first + 1 > PackageMotion.MaximumStages) continue;
            journeys.Add(new(item.Id, new[] { transitions[first].From }.Concat(transitions.Skip(first).Select(t => t.To)).ToArray()));
        }
        var observations = d.Items.ToDictionary(w => w.Id);
        var completed = s.WorkItems.Where(w => w.State.IsWorkComplete()).Select(w => w.Id).ToHashSet();
        return new(s.CurrentDay, [
            new("Backlog", 0, d.BacklogCount, $"{d.WaitingForDependencyCount} waiting for dependency (included)"),
            new("Development", d.DevelopmentCount + d.ReworkCount, d.WaitingForReworkCount,
                $"Active {d.DevelopmentCount} / limit {c.DevelopmentWipLimit} · Rework {d.ReworkCount} active + {d.WaitingForReworkCount} waiting"),
            new("Code Review", d.CodeReviewCount, d.WaitingForCodeReviewCount, $"Active {d.CodeReviewCount} / limit {c.CodeReviewWipLimit} · Queue {d.WaitingForCodeReviewCount}"),
            new("Testing", d.TestingCount, d.WaitingForTestingCount, $"Active {d.TestingCount} / limit {c.TestingWipLimit} · Queue {d.WaitingForTestingCount}"),
            new("Ready for Release", 0, d.ReadyForReleaseCount, "Tested work awaiting delivery"),
            new("Release", d.DoneCount, 0, $"{released} released on Day {s.CurrentDay} · {d.DoneCount} cumulative")
        ], c.Team.DeveloperCount, c.Team.TesterCount, c.Skills.Specialists,
            s.DebtState.Amount, s.DebtState.Ratio, released, c.Release.Description,
            s.CurrentDay == 0 ? "No completed day yet" : $"Day {s.CurrentDay} used / available · Dev {d.UsedDeveloperCapacity:0.##} / {d.AvailableDeveloperCapacity:0.##} · Test {d.UsedTesterCapacity:0.##} / {d.AvailableTesterCapacity:0.##}", true)
        {
            InspectionItems = s.WorkItems.Where(w => w.CreatedDay <= Math.Max(0, s.CurrentDay - 1)).Select(w => InspectedWorkItem.From(w, s.CurrentDay, completed, s.CurrentDay > 0 ? observations.GetValueOrDefault(w.Id) : null)).ToArray(),
            QueuePressures = QueuePressure.From(live),
            QueueTrends = QueueTrend.From(live),
            AwaitingCustomerLaunch = CustomerDeliveryMetrics.From(s.ProductDelivery, new Dictionary<string, int?>()).InternallyReleasedItemsAwaitingLaunch,
            SessionIdentity = identities.GetValue(s, _ => new()).Value, Journeys = journeys.ToArray(), ChangedItems = changed,
            ActiveDevelopment = d.DevelopmentCount, ActiveRework = d.ReworkCount,
            WaitingRework = d.WaitingForReworkCount, DependencyWaiting = d.WaitingForDependencyCount,
            AvailableDeveloperCapacity = d.AvailableDeveloperCapacity, AvailableTesterCapacity = d.AvailableTesterCapacity,
            DevelopmentWipLimit = c.DevelopmentWipLimit, ReviewWipLimit = c.CodeReviewWipLimit, TestingWipLimit = c.TestingWipLimit,
            UsedDeveloperCapacity = d.UsedDeveloperCapacity, UsedTesterCapacity = d.UsedTesterCapacity,
            UsedDevelopmentCapacity = d.UsedDevelopmentCapacity, UsedReviewCapacity = d.UsedReviewCapacity,
            UsedReworkCapacity = d.UsedReworkDeveloperCapacity, UsedDebtRepaymentCapacity = d.UsedDebtRepaymentCapacity,
            CollaborationCapacity = d.CollaborationDevelopmentCapacity,
            DeveloperPoseCeiling = s.CurrentDay == 0 ? 0 : (int)Math.Floor(Math.Min(d.AvailableDeveloperCapacity,
                Math.Min(c.Team.AvailableDeveloperCapacity, c.Team.DeveloperCount * c.Team.DeveloperAvailability))),
            TesterPoseCeiling = s.CurrentDay == 0 ? 0 : (int)Math.Floor(Math.Min(d.AvailableTesterCapacity,
                Math.Min(c.Team.AvailableTesterCapacity, c.Team.TesterCount * c.Team.TesterAvailability)))
        };
    }
}
