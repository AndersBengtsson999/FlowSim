using Simulation.Application;
using Simulation.Core;
namespace Simulation.UI.Insights;

public sealed record InsightChart(string Title, string Unit, string PrimaryLabel, LiveTrendSeries Primary,
    string? SecondaryLabel, LiveTrendSeries? Secondary, InsightPeriod Period)
{
    public override string ToString()=>Title;
    public IEnumerable<string> Measurements => Primary.Points.Select(p=>$"Day {p.Day}: {PrimaryLabel} = {Number(p.Value)} {Unit}"+
        (Secondary is null?"":$"; {SecondaryLabel} = {Number(Secondary.Points.FirstOrDefault(x=>x.Day==p.Day)?.Value)} {Unit}"));
    private static string Number(double? value)=>value?.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)??"N/A";
}
/// <summary>Direct recorded samples only, with no interpolation, rollups or KPI calculations.</summary>
public static class InsightEvidence
{
    public static IReadOnlyList<InsightChart> Build(SimulationInsight insight,IReadOnlyList<DailySnapshot> history)
    {
        var days=history.Where(d=>insight.Period.FirstDay is { } first && insight.Period.LastDay is { } last && d.Day+1>=first && d.Day+1<=last).OrderBy(d=>d.Day).ToArray();
        if(days.Length==0)return [];
        var period=new InsightPeriod(days[0].Day+1,days[^1].Day+1,days.Length,insight.Period.RequestedDays);
        LiveTrendSeries Series(Func<DailySnapshot,double?> get)=>new(days.Select(d=>new LiveTrendPoint(d.Day+1,get(d))).ToArray(),[]);
        var testing=insight.Id.Contains("testing",StringComparison.Ordinal);
        var development=insight.Id=="queue.backlog";
        var charts=new List<InsightChart>();
        if(insight.Id!="imbalance.testing-supply")charts.Add(new("Queue size · end of day","items",development?"Eligible Backlog":testing?"Waiting for Testing":"Waiting for Code Review",
            Series(d=>development?Math.Max(0,d.BacklogCount-d.WaitingForDependencyCount):testing?d.WaitingForTestingCount:d.WaitingForCodeReviewCount),null,null,period));
        charts.Add(new(testing?"Tester capacity · daily samples":"Shared developer capacity · daily samples","raw units","Used",Series(d=>testing?d.UsedTesterCapacity:d.UsedDeveloperCapacity),
            "Available",Series(d=>testing?d.AvailableTesterCapacity:d.AvailableDeveloperCapacity),period));
        return charts;
    }
}
