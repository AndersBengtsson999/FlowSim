using System.Text.Json;
using ModelValidationV2;
using Simulation.Application;
using Simulation.Core;
var folder=args.FirstOrDefault()??"docs/verification/specialist-capacity-v1";
Directory.CreateDirectory(folder);
var options=new JsonSerializerOptions{WriteIndented=true};
var runs=new System.Collections.Concurrent.ConcurrentBag<object>();var trajectories=new System.Collections.Concurrent.ConcurrentBag<object>();var failures=new System.Collections.Concurrent.ConcurrentBag<object>();var repeats=new System.Collections.Concurrent.ConcurrentBag<object>();
SimulationRequest Request(string name,int seed)=>Validation.Complex with{RandomSeed=seed,DeveloperCount=name=="G6"?6:5,Skills=Validation.Complex.Skills with{Specialists=name=="G6"?2:int.Parse(name[1..])}};
LiveSimulation Run(string name,int seed){var live=LiveSimulation.Start(Request(name,seed),WorkArrivalMode.AlwaysAvailable);Validation.Until(live,300);return live;}
Dictionary<string,double?> Metrics(LiveSimulation live){var s=live.Session;var p=LivePerformance.Rolling(s,50);var ds=s.Days.TakeLast(50).ToArray();var d=ds[^1];return new(){
 ["Throughput"]=p.Throughput,["CompletionRate"]=p.CompletionRate,["DevelopmentCycleTime"]=p.DevelopmentCycleTime,["DeliveryCycleTime"]=p.CycleTime,["ReleaseWaitTime"]=p.ReleaseWaitTime,["AverageWip"]=p.AverageWip,
 ["ReviewQueue"]=ds.Average(x=>x.WaitingForCodeReviewCount),["TestingQueue"]=ds.Average(x=>x.WaitingForTestingCount),["DependencyQueue"]=ds.Average(x=>x.WaitingForDependencyCount),["SpecialistQueue"]=ds.Average(x=>x.SpecialistWorkWaiting),["ReleaseQueue"]=ds.Average(x=>x.ReadyForReleaseCount),
 ["DeveloperUtilization"]=p.DeveloperUtilization,["TesterUtilization"]=p.TesterUtilization,["DeliveryWorkCost"]=p.DeliveryWorkCostPerItem,["SystemCost"]=p.SystemCostPerReleasedItem,["DebtRatio"]=s.DebtState.Ratio,
 ["ReleasedCumulative"]=s.WorkItems.Count(x=>x.ReleasedDay.HasValue),["FinalBacklog"]=d.BacklogCount,["FinalReviewQueue"]=d.WaitingForCodeReviewCount,["FinalTestingQueue"]=d.WaitingForTestingCount,["FinalDependencyQueue"]=d.WaitingForDependencyCount,["FinalSpecialistQueue"]=d.SpecialistWorkWaiting,["FinalReleaseQueue"]=d.ReadyForReleaseCount,["FinalReworkQueue"]=d.WaitingForReworkCount,
 ["DevelopmentCompletedWindow"]=s.WorkItems.Count(w=>w.DevelopmentCompletedDay>=251&&w.DevelopmentCompletedDay<=300),
 ["ReviewCompletedWindow"]=s.WorkItems.Count(w=>w.CodeReviewCompletedDay>=251&&w.CodeReviewCompletedDay<=300),
 ["DevelopmentCompletedCumulative"]=s.WorkItems.Count(w=>w.DevelopmentCompletedDay.HasValue),
 ["ReviewCompletedCumulative"]=s.WorkItems.Count(w=>w.CodeReviewCompletedDay.HasValue),
 ["ReviewCapacityWindow"]=ds.Sum(x=>x.UsedReviewCapacity),["DevelopmentCapacityWindow"]=ds.Sum(x=>x.UsedDevelopmentCapacity),
 ["CollaborationCapacityWindow"]=ds.Sum(x=>x.CollaborationDevelopmentCapacity),["RepaymentCapacityWindow"]=ds.Sum(x=>x.UsedDebtRepaymentCapacity),
 ["FinalDevelopmentCount"]=d.DevelopmentCount,
 ["LastDevelopmentCompletionDay"]=s.WorkItems.Where(w=>w.DevelopmentCompletedDay.HasValue).Select(w=>(double)w.DevelopmentCompletedDay!.Value).DefaultIfEmpty(0).Max(),
 ["LastReleaseDay"]=s.WorkItems.Where(w=>w.ReleasedDay.HasValue).Select(w=>(double)w.ReleasedDay!.Value).DefaultIfEmpty(0).Max()};}
Parallel.ForEach(Enumerable.Range(12345,30),new ParallelOptions{MaxDegreeOfParallelism=4},seed=>{

 foreach(var name in new[]{"S0","S1","S2","S3","S4","S5","G6"})try{
 var live=Run(name,seed);var s=live.Session;var audit=Validation.Check(live);var metrics=Metrics(live);Validation.Require(metrics.Values.All(x=>x is null||double.IsFinite(x.Value)),"Invalid final metric");
 foreach(var day in s.Days){Validation.Near(day.AvailableDeveloperCapacity,name=="G6"?6:5,"Constant developer capacity");Validation.Require(s.Configuration.Skills.Specialists<=s.Configuration.Team.DeveloperCount,"Specialist subset");}
 if(name=="S0")Validation.Require(s.Days.SelectMany(d=>d.Items).Where(w=>w.RequiresSpecialist).All(w=>w.UsedDevelopmentCapacity==0),"S0 specialist work receives no Development capacity");
 var fingerprint=Validation.Hash(s.Capture());var resultHash=Validation.Hash(s.GetResult());
 var series=new Dictionary<string,object>();foreach(var metric in new[]{LiveTrendMetric.Throughput,LiveTrendMetric.CycleTime,LiveTrendMetric.TestingQueue,LiveTrendMetric.SpecialistWorkWaiting,LiveTrendMetric.ReviewQueue,LiveTrendMetric.ReadyForRelease,LiveTrendMetric.TechnicalDebtRatio})series[metric.ToString()]=LivePerformanceTrend.Project(s,metric,50,null).Points.Select(p=>new{p.Day,p.Value}).ToArray();
 runs.Add(new{Scenario=name,Seed=seed,Request=Request(name,seed),Changes=s.Changes,Metrics=metrics,Fingerprint=fingerprint,ResultFingerprint=resultHash,Audit=audit});trajectories.Add(new{Scenario=name,Seed=seed,Series=series});
 if(new[]{12345,12359,12374}.Contains(seed)){var again=Run(name,seed);Validation.Require(fingerprint==Validation.Hash(again.Session.Capture())&&resultHash==Validation.Hash(again.Session.GetResult())&&Validation.Hash(metrics)==Validation.Hash(Metrics(again)),"Exact determinism failure");repeats.Add(new{Scenario=name,Seed=seed,ExactState=true,ExactResult=true,ExactMetrics=true});}
 Console.WriteLine($"PASS {name} seed {seed}");
 }catch(Exception e){failures.Add(new{Scenario=name,Seed=seed,Error=e.ToString()});Console.WriteLine($"FAIL {name} seed {seed}: {e}");}
});
 File.WriteAllText(Path.Combine(folder,"raw-results.json"),JsonSerializer.Serialize(new{Runs=runs,Repeats=repeats,Failures=failures},options));
 File.WriteAllText(Path.Combine(folder,"time-series.json"),JsonSerializer.Serialize(trajectories));
Validation.Require(failures.Count==0&&runs.Count==210,"Validation failures or incomplete runs");
Console.WriteLine("PASS 210 runs, 63000 audited days, 21 exact repeat runs.");
