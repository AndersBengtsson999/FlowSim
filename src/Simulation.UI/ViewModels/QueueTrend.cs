using Simulation.Application;
using Simulation.Core;
using System.Globalization;
namespace Simulation.UI.ViewModels;

public enum QueueTrendKind { Growing, Shrinking, Stable, InsufficientData }
public sealed record QueueTrend(QueueTrendKind Kind,int? FirstDay,int? LastDay,int Samples,double? Slope)
{
    public const int Window=10, Minimum=4;
    private static string Name(QueueTrendKind kind)=>kind==QueueTrendKind.InsufficientData?"Insufficient data":kind.ToString();
    public string Label => Name(Kind);
    public string Period => Samples==0?"No post-change observations":$"Days {FirstDay}–{LastDay} · {Samples}/{Window} observations";
    public int? StartingCount { get; init; }
    public int? EndingCount { get; init; }
    public double? FittedChange { get; init; }
    public double? EntryThreshold { get; init; }
    public double? RetentionThreshold { get; init; }
    public QueueTrendKind RawKind { get; init; } = QueueTrendKind.InsufficientData;
    public QueueTrendKind PendingKind { get; init; } = QueueTrendKind.Stable;
    public int Confirmations { get; init; }
    public string Decision { get; init; } = "Fewer than four observations; a direction cannot yet be determined.";
    private static string Number(double? value)=>value?.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)??"N/A";
    public string Evidence => $"{Label} · {Period}. Queue size: {StartingCount?.ToString()??"N/A"} → {EndingCount?.ToString()??"N/A"} items. Estimated slope: {Number(Slope)} items/day. " +
        $"Fitted change: {Number(FittedChange)} items; entry threshold: {Number(EntryThreshold)} items; retention threshold: {Number(RetentionThreshold)} items. " +
        $"Raw classification: {Name(RawKind)}; pending direction: {Name(PendingKind)}; confirmations: {Confirmations}/2. {Decision} {Method}";
    public const string Method="Trend: up to 10 simulated days, at least 4 observations. OLS fitted change must exceed 2 items and twice the residual RMS to enter a direction; retention uses 1 item and one residual RMS. A new direction requires one window above the entry threshold, followed by a window in the same direction above the retention threshold. A contrary or insufficient window clears confirmation. These noise bands are stabilization rules, not confidence intervals. History restarts after configuration changes. Trend is not capacity pressure or proof of a bottleneck.";
    public static readonly QueueTrend Empty=new(QueueTrendKind.InsufficientData,null,null,0,null);
    public static QueueTrend Calculate(IEnumerable<(int Day,int Count)> source)=>Trace(source).LastOrDefault()??Empty;
    public static IReadOnlyList<QueueTrend> Trace(IEnumerable<(int Day,int Count)> source)
    {
        var history=source.OrderBy(x=>x.Day).ToArray();
        var state=QueueTrendKind.InsufficientData;var pending=QueueTrendKind.Stable;var confirmations=0;
        var results=new List<QueueTrend>();
        var start=0;
        for(var end=0;end<history.Length;end++)
        {
            var day=history[end].Day;
            while(history[start].Day<=day-Window)start++;
            var samples=history[start..(end+1)];
            if(samples.Length<Minimum) { state=QueueTrendKind.InsufficientData;confirmations=0;pending=QueueTrendKind.Stable;results.Add(new(QueueTrendKind.InsufficientData,samples[0].Day,day,samples.Length,null){StartingCount=samples[0].Count,EndingCount=samples[^1].Count});continue; }
            var slope=LivePerformance.Slope(samples.Select(x=>((double)x.Day,(double)x.Count)))??0;
            var meanDay=samples.Average(x=>(double)x.Day);var mean=samples.Average(x=>(double)x.Count);
            var noise=Math.Sqrt(samples.Average(x=>Math.Pow(x.Count-(mean+slope*(x.Day-meanDay)),2)));
            var change=slope*(samples[^1].Day-samples[0].Day);
            var entry=Math.Max(2,2*noise);var retention=Math.Max(1,noise);
            var candidate=Math.Abs(change)>entry?change>0?QueueTrendKind.Growing:QueueTrendKind.Shrinking:QueueTrendKind.Stable;
            bool Supports(QueueTrendKind direction)=>direction==QueueTrendKind.Growing?change>retention:direction==QueueTrendKind.Shrinking && change< -retention;
            // The confirmation window uses the lower Schmitt threshold, just like retention.
            // Requiring the high threshold twice prevented periodic, gradual growth from ever entering.
            var confirmedPending=confirmations>0 && Supports(pending);
            if(confirmedPending)confirmations=Math.Min(2,confirmations+1);
            else {pending=candidate;confirmations=candidate==QueueTrendKind.Stable?0:1;}
            if(state==QueueTrendKind.InsufficientData)state=QueueTrendKind.Stable;
            var retained=Supports(state);
            if(!retained)state=QueueTrendKind.Stable;
            if(pending!=QueueTrendKind.Stable && confirmations>=2)state=pending;
            var decision=state!=QueueTrendKind.Stable
                ? confirmedPending && candidate==QueueTrendKind.Stable?"Direction confirmed/retained above the lower threshold; the high entry threshold need not be exceeded again.":"Directional evidence confirmed across successive windows."
                : confirmations==1?"Entry threshold exceeded; awaiting a second window supporting the same direction.":"No confirmed direction above the noise and item-resolution thresholds.";
            results.Add(new(state,samples[0].Day,day,samples.Length,slope){StartingCount=samples[0].Count,EndingCount=samples[^1].Count,
                FittedChange=change,EntryThreshold=entry,RetentionThreshold=retention,RawKind=candidate,PendingKind=pending,Confirmations=confirmations,Decision=decision});
        }
        return results;
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
