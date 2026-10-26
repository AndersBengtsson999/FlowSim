using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using System.Globalization;
var root="docs/verification/queue-trend-diagnosis";
var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,RandomSeed=12345,TestingEffort=5,TesterCount=1},WorkArrivalMode.ContinuousArrival,.4m);
for(var day=0;day<70;day++)live.Step();
Write("simulation",live.Session.Days.Select(d=>(Day:d.Day+1,Count:d.WaitingForTestingCount)).ToArray());
int[] supplied=[2,2,3,3,4,4,5,6,6,7,8,8,9,10,11];
Write("supplied",supplied.Select((v,i)=>(Day:i+1,Count:v)).ToArray());
void Write(string name,(int Day,int Count)[] history) {
    var expected=File.ReadAllLines($"{root}/{name}-history.csv").Skip(1).ToArray();
    if(!expected.SequenceEqual(history.Select(p=>$"{p.Day},{p.Count}")))throw new InvalidOperationException("Simulation history differs from captured baseline");
    using var writer=new StreamWriter($"{root}/{name}-after.csv");
    writer.WriteLine("day,count,start,end,samples,slope,entry_threshold_items,retention_threshold_items,fitted_change,raw,pending,confirmations,stabilized");
    foreach(var t in QueueTrend.Trace(history)) {
        string N(double? v)=>v?.ToString("0.######",CultureInfo.InvariantCulture)??"";
        writer.WriteLine($"{t.LastDay},{t.EndingCount},{t.FirstDay},{t.LastDay},{t.Samples},{N(t.Slope)},{N(t.EntryThreshold)},{N(t.RetentionThreshold)},{N(t.FittedChange)},{t.RawKind},{t.PendingKind},{t.Confirmations},{t.Label}");
    }
    Console.WriteLine(name+": "+QueueTrend.Calculate(history).Evidence);
}
