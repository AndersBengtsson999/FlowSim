namespace Simulation.UI.Insights;

/// <summary>Display-only wording and grouping; never changes observations or their classification.</summary>
public static class InsightPresentation
{
    public static int Group(TrackedInsight item) => item.Insight.Relevant ? 0 : item.Status == InsightStatus.NoLongerRelevant ? 2 : 1;
    public static string GroupTitle(TrackedInsight item) => Group(item) switch { 0 => "Active findings", 1 => "Other current observations", _ => "Previous findings · no longer relevant" };
    public static IReadOnlyList<TrackedInsight> Ordered(IEnumerable<TrackedInsight> items) => items.OrderBy(Group).ThenBy(i=>i.Insight.Category).ThenBy(i=>i.Insight.Id,StringComparer.Ordinal).ToArray();
    public static string Plain(string text) => text.Replace("No sustained pressure established", "No consistent queue pressure detected",StringComparison.Ordinal);
    public static string Title(TrackedInsight item) => item.Insight.Id switch {
        "imbalance.testing-wait" => "Testing queue grew during Review output",
        "imbalance.review-wait" => "Review queue grew during Development output",
        "imbalance.testing-supply" => "Unused Testing capacity",
        _ => Plain(item.Insight.Title).TrimEnd('.') };
    public static string Range(InsightPeriod period) => period.Observations==0 ? "No completed days" : $"Days {period.FirstDay}–{period.LastDay}";
    public static string Overview(IEnumerable<TrackedInsight> source)
    {
        var items=source.ToArray();var active=items.Count(i=>i.Insight.Relevant);var previous=items.Count(i=>Group(i)==2);
        return $"{active} active finding{(active==1?"":"s")} · {items.Length-active-previous} other current observation{(items.Length-active-previous==1?"":"s")} · {previous} previous finding{(previous==1?"":"s")}. Select a finding to explore it.";
    }
}
