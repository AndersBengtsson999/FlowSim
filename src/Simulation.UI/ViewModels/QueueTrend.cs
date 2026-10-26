using Simulation.Application;
using Simulation.Core;
namespace Simulation.UI.ViewModels;

public enum QueueTrendKind { Growing, Shrinking, Stable, InsufficientData }
public sealed record QueueTrend(QueueTrendKind Kind,int? FirstDay,int? LastDay,int Samples,double? Slope)
{
    public const int Window=10, Minimum=4;
    public string Label => Kind==QueueTrendKind.InsufficientData?"Insufficient data":Kind.ToString();
    public string Period => Samples==0?"No post-change observations":$"Days {FirstDay}–{LastDay} · {Samples}/{Window} observations";
    public string Evidence => $"{Period}. {Method}";
    public const string Method="Trend: up to 10 simulated days, at least 4 observations. OLS fitted change must exceed 2 items and twice the residual RMS to enter a direction; retention uses 1 item and one residual RMS. A new direction requires two consecutive eligible windows. These noise bands are stabilization rules, not confidence intervals. History restarts after configuration changes. Trend is not capacity pressure or proof of a bottleneck.";
    public static readonly QueueTrend Empty=new(QueueTrendKind.InsufficientData,null,null,0,null);
    public static QueueTrend Calculate(IEnumerable<(int Day,int Count)> source)
    {
        var history=source.OrderBy(x=>x.Day).ToArray();
        var state=QueueTrendKind.InsufficientData;var pending=QueueTrendKind.Stable;var confirmations=0;
        QueueTrend result=Empty;
        var start=0;
        for(var end=0;end<history.Length;end++)
        {
            var day=history[end].Day;
            while(history[start].Day<=day-Window)start++;
            var samples=history[start..(end+1)];
            if(samples.Length<Minimum) { state=QueueTrendKind.InsufficientData;confirmations=0;pending=QueueTrendKind.Stable;result=new(QueueTrendKind.InsufficientData,samples[0].Day,day,samples.Length,null);continue; }
            var slope=LivePerformance.Slope(samples.Select(x=>((double)x.Day,(double)x.Count)))??0;
            var meanDay=samples.Average(x=>(double)x.Day);var mean=samples.Average(x=>(double)x.Count);
            var noise=Math.Sqrt(samples.Average(x=>Math.Pow(x.Count-(mean+slope*(x.Day-meanDay)),2)));
            var change=slope*(samples[^1].Day-samples[0].Day);
            var candidate=Math.Abs(change)>Math.Max(2,2*noise)?change>0?QueueTrendKind.Growing:QueueTrendKind.Shrinking:QueueTrendKind.Stable;
            if(candidate==pending)confirmations++;else { pending=candidate;confirmations=1; }
            if(state==QueueTrendKind.InsufficientData)state=QueueTrendKind.Stable;
            var retains=state==QueueTrendKind.Growing?change>Math.Max(1,noise):state==QueueTrendKind.Shrinking && change< -Math.Max(1,noise);
            if(!retains)state=QueueTrendKind.Stable;
            if(candidate!=QueueTrendKind.Stable && confirmations>=2)state=candidate;
            result=new(state,samples[0].Day,day,samples.Length,slope);
        }
        return result;
    }
    public static IReadOnlyDictionary<string,QueueTrend> From(LiveSimulation live)
    {
        var changed=live.Session.Changes.LastOrDefault()?.Day??-1;
        var days=live.Session.Days.Where(d=>d.Day>=changed).ToArray();
        QueueTrend Trend(Func<DailySnapshot,int> count)=>Calculate(days.Select(d=>(d.Day+1,count(d))));
        return new Dictionary<string,QueueTrend> {
            ["backlog"]=Trend(d=>Math.Max(0,d.BacklogCount-d.WaitingForDependencyCount)),
            ["dependency"]=Trend(d=>d.WaitingForDependencyCount),
            ["review-wait"]=Trend(d=>d.WaitingForCodeReviewCount),
            ["testing-wait"]=Trend(d=>d.WaitingForTestingCount),
            ["rework-wait"]=Trend(d=>d.WaitingForReworkCount),
            ["ready"]=Trend(d=>d.ReadyForReleaseCount)
        };
    }
}
