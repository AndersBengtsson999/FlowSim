using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public enum QueueAttentionState { Neutral, Attention, Strong }

// Read-only presentation: queue size and the same OLS/Stable semantics as Team Performance.
public sealed record QueueAttention(int Count, double Scale, string ScaleName, double? Slope)
{
    public double RelativeSize => Count / Math.Max(1, Scale);
    public string TrendText => LivePerformancePresentation.Trend(Slope);
    public string Arrow => TrendText.StartsWith("Rising") ? "↑" : TrendText.StartsWith("Falling") ? "↓" : TrendText.StartsWith("Stable") ? "→" : "—";
    public QueueAttentionState State => Count == 0 ? QueueAttentionState.Neutral
        : RelativeSize >= 4 || RelativeSize >= 2 && Arrow == "↑" ? QueueAttentionState.Strong
        : RelativeSize >= 2 || RelativeSize >= 1 && Arrow == "↑" ? QueueAttentionState.Attention
        : QueueAttentionState.Neutral;
    public bool Attention => State == QueueAttentionState.Attention;
    public bool Strong => State == QueueAttentionState.Strong;
    public string Label => State switch { QueueAttentionState.Strong => "Strong accumulation", QueueAttentionState.Attention => "Accumulating", _ => "" };
    public string Summary => $"{Count} {Arrow}";
    public string Detail => $"{Count} items waiting" + (Label.Length == 0 ? "" : $" · {Label}") + $"\nTrend: {TrendText}\nRelative queue: {RelativeSize:0.0}× normalization scale ({Math.Max(1, Scale):0.##}). Reference: {ScaleName} = {Scale:0.##}." +
        (Scale < 1 ? " Normalization uses a minimum of 1 to avoid division by zero or fractional amplification." : "") +
        "\nOLS uses the selected performance window. Attention: ≥2×, or ≥1× and increasing. Strong: ≥4×, or ≥2× and increasing. Expand or collapse Work Items with Enter/Space.";

    public static QueueAttention For(int count, double scale, string name, IEnumerable<DailySnapshot> days, Func<DailySnapshot, double> select) =>
        new(count, scale, name, LivePerformance.Slope(days.Select(d => ((double)d.Day, select(d)))));

    public static FlowStateRow Apply(FlowStateRow row, SessionConfiguration c, IReadOnlyList<DailySnapshot> days)
    {
        var queue = row.IsDependencyQueue ? For(row.Count, c.DevelopmentWipLimit, "Development WIP limit", days, d => d.WaitingForDependencyCount) : row.State switch
        {
            WorkItemStatus.WaitingForCodeReview => For(row.Count, c.CodeReviewWipLimit, "Code Review WIP limit", days, d => d.WaitingForCodeReviewCount),
            WorkItemStatus.WaitingForTesting => For(row.Count, c.TestingWipLimit, "Testing WIP limit", days, d => d.WaitingForTestingCount),
            WorkItemStatus.WaitingForRework => For(row.Count, c.Quality.ReworkWipLimit, "Rework WIP limit", days, d => d.WaitingForReworkCount),
            WorkItemStatus.ReadyForRelease => For(row.Count, c.Release.Capacity == int.MaxValue ? c.DevelopmentWipLimit : (double)c.Release.Capacity / (c.Release.Mode == ReleaseMode.Scheduled ? c.Release.Interval : 1),
                c.Release.Capacity == int.MaxValue ? "Development WIP limit (unlimited release)" : "configured release items/day", days, d => d.ReadyForReleaseCount),
            _ => null
        };
        return row with { Queue = queue, SpecialistQueue = row.IsDevelopment && row.ShowSkills ? For(row.SpecialistWorkWaiting, c.Skills.Specialists, "specialists", days, d => d.SpecialistWorkWaiting) : null };
    }
}
