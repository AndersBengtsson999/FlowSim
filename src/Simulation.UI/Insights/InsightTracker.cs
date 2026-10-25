namespace Simulation.UI.Insights;

public enum InsightStatus { New, Reviewed, Updated, NoLongerRelevant, Informational }
public sealed record TrackedInsight(SimulationInsight Insight, InsightStatus Status)
{
    public string ShortTitle => Insight.Id switch {
        "queue.backlog"=>"Development queue", "queue.review-wait"=>"Code Review queue", "queue.testing-wait"=>"Testing queue",
        "imbalance.review-wait"=>"Development → Review", "imbalance.testing-wait"=>"Review → Testing",
        "imbalance.testing-supply"=>"Unused Testing capacity", _=>Insight.Title };
    public string StatusLabel => Status==InsightStatus.NoLongerRelevant?"No longer relevant":Status.ToString();
    public string Secondary => $"{StatusLabel} · {(Insight.Period.Observations==0?"No history":$"Days {Insight.Period.FirstDay}–{Insight.Period.LastDay}")}";
    public string Label => $"{Insight.Title} · {Status switch { InsightStatus.NoLongerRelevant=>"No longer relevant",_=>Status.ToString() }}";
    public override string ToString()=>Label;
}
/// <summary>Ephemeral UI acknowledgements. Reset on session replacement/restore/load, never serialized.
/// Material changes are classification changes, not advancing dates or changing raw queue counts.</summary>
public sealed class InsightTracker
{
    private readonly Dictionary<string,TrackedInsight> entries = new(StringComparer.Ordinal);
    public IReadOnlyList<TrackedInsight> Items => entries.Values.OrderBy(x=>x.Insight.Category).ThenBy(x=>x.Insight.Id,StringComparer.Ordinal).ToArray();
    public int NewCount => entries.Values.Count(x=>x.Status==InsightStatus.New);
    public int UpdatedCount => entries.Values.Count(x=>x.Status==InsightStatus.Updated);
    public string Indicator => "Insights" + (NewCount>0?$" · {NewCount} new":"") + (UpdatedCount>0?$" · {UpdatedCount} updated":"");
    public void Reset()=>entries.Clear();
    public void Observe(InsightReport report)
    {
        var ids=report.Insights.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var (id,old) in entries.ToArray())
            if(!ids.Contains(id))entries[id]=old with { Status=InsightStatus.NoLongerRelevant,Insight=old.Insight with { Relevant=false } };
        foreach(var insight in report.Insights)
        {
            entries.TryGetValue(insight.Id,out var previous);
            var status=!insight.Relevant
                ? previous is not null && (previous.Insight.Relevant || previous.Status==InsightStatus.NoLongerRelevant)?InsightStatus.NoLongerRelevant:InsightStatus.Informational
                : previous is null || previous.Status==InsightStatus.Informational ? InsightStatus.New
                : previous.Status==InsightStatus.NoLongerRelevant || previous.Insight.MaterialState!=insight.MaterialState ? InsightStatus.Updated
                : previous.Status;
            entries[insight.Id]=new(insight,status);
        }
    }
    public void Review(string id)
    {
        if(entries.TryGetValue(id,out var item) && item.Status is InsightStatus.New or InsightStatus.Updated)
            entries[id]=item with { Status=InsightStatus.Reviewed };
    }
}
