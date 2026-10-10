using Simulation.Core;

namespace Simulation.UI.ViewModels;

public static class FlowPresentation
{
    public static string Productivity(double value) => value.ToString(value < .01 || value >= 1e12 ? "G" : "0.00###############", System.Globalization.CultureInfo.InvariantCulture) + "x";
    public const string DeveloperCountHelp = "Number of developers contributing to the shared developer capacity pool.";
    public const string TesterCountHelp = "Number of testers contributing to the tester capacity pool.";
    public const string DeveloperAvailabilityHelp = "Percentage of nominal developer capacity available for simulation work. Range 0–100%.";
    public const string TesterAvailabilityHelp = "Percentage of nominal tester capacity available for Testing. Range 0–100%.";
    public const string DevelopmentProductivityHelp = "Effective Development work per consumed Development capacity unit; collaboration also applies its existing efficiency.";
    public const string CodeReviewProductivityHelp = "Effective Code Review work per consumed Code Review capacity unit.";
    public const string TestingProductivityHelp = "Effective Testing work per consumed tester capacity unit.";
    public const string AdvancedCapacityHelp = "Advanced scaling of nominal capacity per person. Normal simulations use 1.0.";
    public const string DevelopmentWipHelp = "Limits active Work Items, not developers. Spare developer capacity can collaborate on active Development items.";
    public static IReadOnlyList<MetricRow> DevelopmentAllocations(DailySnapshot? day) => day is null ? [] : day.Items
        .Where(w => w.StateDuringDay == WorkItemStatus.Development)
        .Select(w => new MetricRow(w.Id + (w.State == WorkItemStatus.Development ? " · active" : " · completed Development"),
            $"Remaining {w.RemainingDevelopmentEffort:0.###} · Primary {w.PrimaryDevelopmentCapacity:0.###} · Collaboration {w.CollaborationDevelopmentCapacity:0.###}\nCapacity used {w.UsedDevelopmentCapacity:0.###} · Effective work {w.DevelopmentWork:0.###}",
            "Selected day's allocation. Primary work equals consumed capacity × Development Productivity; collaboration also applies 50% efficiency. Remaining effort is measured at day end.")).ToArray();

    public static IReadOnlyList<FlowStateRow> Rows(DailySnapshot d, SessionConfiguration? configuration = null, bool rework = false)
    {
        string Active(string description, int count, int? limit) => limit is null ? description : $"{count} / {limit} · {description}";
        var rows = new List<FlowStateRow>
        {
            new("Backlog", d.BacklogCount - d.WaitingForDependencyCount, "Not started", "#F1F5F9", "↓"),
            new("Development", d.DevelopmentCount, Active("active items", d.DevelopmentCount, configuration?.DevelopmentWipLimit) + $"\nCapacity used {d.UsedDevelopmentCapacity:0.##} · Effective work {d.DevelopmentWork:0.##}", "#E8F1F7", "↓"),
            new("Waiting for Code Review", d.WaitingForCodeReviewCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
            new("Code Review", d.CodeReviewCount, Active("Active stage · shared developer pool", d.CodeReviewCount, configuration?.CodeReviewWipLimit), "#E8F1F7", "↓"),
            new("Waiting for Testing", d.WaitingForTestingCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
            new("Testing", d.TestingCount, Active("Active stage · tester capacity", d.TestingCount, configuration?.TestingWipLimit), "#E8F1F7", "↓"),
            new("Ready for Release", d.ReadyForReleaseCount, configuration?.Release.Description ?? "Awaiting release", "#FFF2D8", "↓"),
            new("Released", d.DoneCount, "Delivered", "#E6F2ED", "")
        };
        if (rework)
        {
            rows.Add(new("Waiting for Rework", d.WaitingForReworkCount, "QUEUE · feedback from inspections", "#FFF2D8", "↓"));
            rows.Add(new("Rework", d.ReworkCount, Active("Returns to Code Review", d.ReworkCount, configuration?.Quality.ReworkWipLimit), "#E8F1F7", "↩"));
        }
        bool WaitingDependency(WorkItemDaySnapshot w) => w.State == WorkItemStatus.Backlog && w.CreatedDay <= d.Day && w.ResidualDependency is { } dependency && dependency.ResolutionDay > (long)d.Day + 1;
        var showDependencies = configuration?.ResidualDependencies.Rate > 0 || d.Items.Any(w => w.ResidualDependency is not null);
        if (showDependencies)
            rows.Insert(1, new("Waiting for Dependency", d.WaitingForDependencyCount, "Backlog subset · no active WIP", "#F3F1ED", ""));
        var itemsByState = d.Items.Where(w => w.CreatedDay <= d.Day).ToLookup(w => w.State);
        return rows.Select(row => row with {
            CompactQueueRows = showDependencies,
            Items = row.IsDependencyQueue ? d.Items.Where(WaitingDependency).ToArray() : row.State == WorkItemStatus.Backlog ? itemsByState[row.State].Where(w => !WaitingDependency(w)).ToArray() : row.State == WorkItemStatus.Released ? d.Items.Where(w => w.State.IsDelivered()).ToArray() : itemsByState[row.State].ToArray(),
            WipLimit = row.State switch {
                WorkItemStatus.Development => configuration?.DevelopmentWipLimit,
                WorkItemStatus.CodeReview => configuration?.CodeReviewWipLimit,
                WorkItemStatus.Testing => configuration?.TestingWipLimit,
                WorkItemStatus.Rework => configuration?.Quality.ReworkWipLimit,
                _ => null
            },
            SpecialistWorkWaiting = d.SpecialistWorkWaiting,
            ShowSkills = configuration?.Skills is { } skills && (skills.Specialists > 0 || skills.SpecialistWorkRate > 0) || d.Items.Any(w => w.RequiresSpecialist),
            DevelopmentCapacityUsed = d.UsedDevelopmentCapacity,
            EffectiveDevelopmentWork = d.DevelopmentWork
        }).ToArray();
    }
}
