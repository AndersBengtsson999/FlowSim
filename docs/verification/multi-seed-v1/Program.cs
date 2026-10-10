using System.Text.Json;
using ModelValidationV2;
using Simulation.Application;
using Simulation.Core;
var folder=args.FirstOrDefault()??"docs/verification/multi-seed-v1";
Directory.CreateDirectory(folder);
var options=new JsonSerializerOptions{WriteIndented=true};
var runs=new System.Collections.Concurrent.ConcurrentBag<object>();var trajectories=new System.Collections.Concurrent.ConcurrentBag<object>();var failures=new System.Collections.Concurrent.ConcurrentBag<object>();var repeats=new System.Collections.Concurrent.ConcurrentBag<object>();
LiveSimulation Run(string name,int seed){
 var live=LiveSimulation.Start(Validation.Request(name) with{RandomSeed=seed},WorkArrivalMode.AlwaysAvailable);
 if(name=="D"){
 Validation.Until(live,150);var old=Validation.Hash(live.Session.Capture().WorkItems);var history=Validation.Hash(live.Session.Days);var c=live.Session.Configuration;
 live.Session.ApplyChanges(c with{Skills=c.Skills with{Specialists=2},ResidualDependencies=c.ResidualDependencies with{Rate=.1},Release=c.Release with{Capacity=10}},"Recovery assumptions");
 Validation.Require(old==Validation.Hash(live.Session.Capture().WorkItems)&&history==Validation.Hash(live.Session.Days),"Intervention mutated history/items");
 Validation.Require(live.Session.Changes.Single().Day==150,"Intervention day");
 }Validation.Until(live,300);return live;
}
Dictionary<string,double?> Metrics(LiveSimulation live){var s=live.Session;var p=LivePerformance.Rolling(s,50);var ds=s.Days.TakeLast(50).ToArray();var d=ds[^1];return new(){
 ["Throughput"]=p.Throughput,["CompletionRate"]=p.CompletionRate,["DevelopmentCycleTime"]=p.DevelopmentCycleTime,["DeliveryCycleTime"]=p.CycleTime,["ReleaseWaitTime"]=p.ReleaseWaitTime,["AverageWip"]=p.AverageWip,
 ["ReviewQueue"]=ds.Average(x=>x.WaitingForCodeReviewCount),["TestingQueue"]=ds.Average(x=>x.WaitingForTestingCount),["DependencyQueue"]=ds.Average(x=>x.WaitingForDependencyCount),["SpecialistQueue"]=ds.Average(x=>x.SpecialistWorkWaiting),["ReleaseQueue"]=ds.Average(x=>x.ReadyForReleaseCount),
 ["DeveloperUtilization"]=p.DeveloperUtilization,["TesterUtilization"]=p.TesterUtilization,["DeliveryWorkCost"]=p.DeliveryWorkCostPerItem,["SystemCost"]=p.SystemCostPerReleasedItem,["DebtRatio"]=s.DebtState.Ratio,
 ["ReleasedCumulative"]=s.WorkItems.Count(x=>x.ReleasedDay.HasValue),["FinalBacklog"]=d.BacklogCount,["FinalReviewQueue"]=d.WaitingForCodeReviewCount,["FinalTestingQueue"]=d.WaitingForTestingCount,["FinalDependencyQueue"]=d.WaitingForDependencyCount,["FinalSpecialistQueue"]=d.SpecialistWorkWaiting,["FinalReleaseQueue"]=d.ReadyForReleaseCount,["FinalReworkQueue"]=d.WaitingForReworkCount};}
Parallel.ForEach(Enumerable.Range(12345,30),new ParallelOptions{MaxDegreeOfParallelism=4},seed=>{
 string? prefix=null;
 foreach(var name in new[]{"A","B","C","D"})try{
 var live=Run(name,seed);var s=live.Session;var audit=Validation.Check(live);var metrics=Metrics(live);Validation.Require(metrics.Values.All(x=>x is null||double.IsFinite(x.Value)),"Invalid final metric");
 if(name=="C")prefix=Validation.Hash(s.Days.Take(150));if(name=="D")Validation.Require(prefix==Validation.Hash(s.Days.Take(150)),"C/D common history");
 var fingerprint=Validation.Hash(s.Capture());var resultHash=Validation.Hash(s.GetResult());
 var series=new Dictionary<string,object>();foreach(var metric in new[]{LiveTrendMetric.Throughput,LiveTrendMetric.CycleTime,LiveTrendMetric.TestingQueue,LiveTrendMetric.ReadyForRelease,LiveTrendMetric.TechnicalDebtRatio})series[metric.ToString()]=LivePerformanceTrend.Project(s,metric,50,null).Points.Select(p=>new{p.Day,p.Value}).ToArray();
 runs.Add(new{Scenario=name,Seed=seed,Request=Validation.Request(name) with{RandomSeed=seed},Changes=s.Changes,Metrics=metrics,Fingerprint=fingerprint,ResultFingerprint=resultHash,Audit=audit});trajectories.Add(new{Scenario=name,Seed=seed,Series=series});
 if(new[]{12345,12359,12374}.Contains(seed)){var again=Run(name,seed);Validation.Require(fingerprint==Validation.Hash(again.Session.Capture())&&resultHash==Validation.Hash(again.Session.GetResult())&&Validation.Hash(metrics)==Validation.Hash(Metrics(again)),"Exact determinism failure");repeats.Add(new{Scenario=name,Seed=seed,ExactState=true,ExactResult=true,ExactMetrics=true});}
 Console.WriteLine($"PASS {name} seed {seed}");
 }catch(Exception e){failures.Add(new{Scenario=name,Seed=seed,Error=e.ToString()});Console.WriteLine($"FAIL {name} seed {seed}: {e}");}
});
 File.WriteAllText(Path.Combine(folder,"raw-results.json"),JsonSerializer.Serialize(new{Runs=runs,Repeats=repeats,Failures=failures},options));
 File.WriteAllText(Path.Combine(folder,"time-series.json"),JsonSerializer.Serialize(trajectories));
Validation.Require(failures.Count==0&&runs.Count==120,"Validation failures or incomplete runs");
Console.WriteLine("PASS 120 runs, 36000 audited days, 12 exact repeat runs.");
