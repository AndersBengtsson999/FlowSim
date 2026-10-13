using System.Text.Json;
using ModelValidationV2;
using Simulation.Application;
using Simulation.Core;
var folder=args.FirstOrDefault()??"docs/verification/testing-release-capacity-v1";
Directory.CreateDirectory(folder);
var options=new JsonSerializerOptions{WriteIndented=true};
var runs=new System.Collections.Concurrent.ConcurrentBag<object>();var trajectories=new System.Collections.Concurrent.ConcurrentBag<object>();var failures=new System.Collections.Concurrent.ConcurrentBag<object>();var repeats=new System.Collections.Concurrent.ConcurrentBag<object>();
var baselineDocument=JsonDocument.Parse(File.ReadAllText("docs/verification/specialist-capacity-v1/raw-results.json"));
var baseline=Validation.Complex with{Skills=Validation.Complex.Skills with{Specialists=2}};
var savedBaseline=baselineDocument.RootElement.GetProperty("Runs").EnumerateArray().Single(r=>r.GetProperty("Scenario").GetString()=="S2"&&r.GetProperty("Seed").GetInt32()==12345).GetProperty("Request");
Validation.Require(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(baseline)),System.Text.Json.Nodes.JsonNode.Parse(savedBaseline.GetRawText())),"Exact saved S2 request equivalence");
var variants=(from testers in new[]{2,3,4} from capacity in new[]{5,7,10,int.MaxValue} select new {Name=$"T{testers}-R{(capacity==int.MaxValue?"U":capacity.ToString())}",Testers=testers,Capacity=capacity}).ToArray();
SimulationRequest Request(string name,int seed){var v=variants.Single(v=>v.Name==name);return baseline with{RandomSeed=seed,SimulationDays=500,TesterCount=v.Testers,Release=baseline.Release with{Capacity=v.Capacity}};}
LiveSimulation Run(string name,int seed){var live=LiveSimulation.Start(Request(name,seed),WorkArrivalMode.AlwaysAvailable);Validation.Until(live,500);return live;}
var unlimited=new ReleaseSettings(ReleaseMode.Scheduled,int.MaxValue,5);unlimited.Validate();
Validation.Require(unlimited.Description.Contains("Unlimited")&&!unlimited.IsOpportunity(4)&&unlimited.IsOpportunity(5),"Scheduled Unlimited supported and cadence retained");
Directory.CreateDirectory(Path.Combine(folder,"histories"));Directory.CreateDirectory(Path.Combine(folder,"runs"));
File.WriteAllText(Path.Combine(folder,"configurations.json"),JsonSerializer.Serialize(variants.Select(v=>new{v.Name,Request=Request(v.Name,12345)}),options));
Console.WriteLine("PASS Scheduled Unlimited accepted, preserving five-day cadence.");
Dictionary<string,double?> Metrics(LiveSimulation live){var s=live.Session;var p=LivePerformance.Rolling(s,50);var ds=s.Days.TakeLast(50).ToArray();var d=ds[^1];return new(){
 ["Throughput"]=p.Throughput,["CompletionRate"]=p.CompletionRate,["DevelopmentCycleTime"]=p.DevelopmentCycleTime,["DeliveryCycleTime"]=p.CycleTime,["ReleaseWaitTime"]=p.ReleaseWaitTime,["AverageWip"]=p.AverageWip,
 ["ReviewQueue"]=ds.Average(x=>x.WaitingForCodeReviewCount),["TestingQueue"]=ds.Average(x=>x.WaitingForTestingCount),["DependencyQueue"]=ds.Average(x=>x.WaitingForDependencyCount),["SpecialistQueue"]=ds.Average(x=>x.SpecialistWorkWaiting),["ReleaseQueue"]=ds.Average(x=>x.ReadyForReleaseCount),
 ["DeveloperUtilization"]=p.DeveloperUtilization,["TesterUtilization"]=p.TesterUtilization,["DeliveryWorkCost"]=p.DeliveryWorkCostPerItem,["SystemCost"]=p.SystemCostPerReleasedItem,["DebtRatio"]=s.DebtState.Ratio,
 ["ReleasedCumulative"]=s.WorkItems.Count(x=>x.ReleasedDay.HasValue),["FinalBacklog"]=d.BacklogCount,["FinalReviewQueue"]=d.WaitingForCodeReviewCount,["FinalTestingQueue"]=d.WaitingForTestingCount,["FinalDependencyQueue"]=d.WaitingForDependencyCount,["FinalSpecialistQueue"]=d.SpecialistWorkWaiting,["FinalReleaseQueue"]=d.ReadyForReleaseCount,["FinalReworkQueue"]=d.WaitingForReworkCount,
 ["DevelopmentCompletedWindow"]=s.WorkItems.Count(w=>w.DevelopmentCompletedDay>=451&&w.DevelopmentCompletedDay<=500),
 ["ReviewCompletedWindow"]=s.WorkItems.Count(w=>w.CodeReviewCompletedDay>=451&&w.CodeReviewCompletedDay<=500),
 ["DevelopmentCompletedCumulative"]=s.WorkItems.Count(w=>w.DevelopmentCompletedDay.HasValue),
 ["ReviewCompletedCumulative"]=s.WorkItems.Count(w=>w.CodeReviewCompletedDay.HasValue),
 ["ReviewCapacityWindow"]=ds.Sum(x=>x.UsedReviewCapacity),["DevelopmentCapacityWindow"]=ds.Sum(x=>x.UsedDevelopmentCapacity),
 ["CollaborationCapacityWindow"]=ds.Sum(x=>x.CollaborationDevelopmentCapacity),["RepaymentCapacityWindow"]=ds.Sum(x=>x.UsedDebtRepaymentCapacity),
 ["FinalDevelopmentCount"]=d.DevelopmentCount,["FinalTotalWip"]=d.TotalWip,
 ["TesterCapacityWindow"]=ds.Sum(x=>x.UsedTesterCapacity),["ReleaseOpportunitiesFullWindow"]=ds.Count(x=>s.Configuration.Release.IsOpportunity(x.Day+1)&&s.WorkItems.Count(w=>w.ReleasedDay==x.Day+1)==s.Configuration.Release.Capacity),
 ["LastDevelopmentCompletionDay"]=s.WorkItems.Where(w=>w.DevelopmentCompletedDay.HasValue).Select(w=>(double)w.DevelopmentCompletedDay!.Value).DefaultIfEmpty(0).Max(),
 ["LastReleaseDay"]=s.WorkItems.Where(w=>w.ReleasedDay.HasValue).Select(w=>(double)w.ReleasedDay!.Value).DefaultIfEmpty(0).Max()};}
Parallel.ForEach(Enumerable.Range(12345,30),new ParallelOptions{MaxDegreeOfParallelism=4},seed=>{

 foreach(var name in variants.Select(v=>v.Name))try{
 var live=Run(name,seed);var s=live.Session;var audit=Validation.Check(live);var metrics=Metrics(live);Validation.Require(metrics.Values.All(x=>x is null||double.IsFinite(x.Value)),"Invalid final metric");
 foreach(var day in s.Days){Validation.Near(day.AvailableDeveloperCapacity,5,"Constant developer capacity");Validation.Near(day.AvailableTesterCapacity,Request(name,seed).TesterCount,"Tester capacity");Validation.Require(s.Configuration.Skills.Specialists==2&&s.Configuration.Release.Interval==5&&s.Configuration.Release.Mode==ReleaseMode.Scheduled,"Fixed skills/cadence");}
 var fingerprint=Validation.Hash(s.Capture());var resultHash=Validation.Hash(s.GetResult());
 var series=new Dictionary<string,object>();foreach(var metric in new[]{LiveTrendMetric.Throughput,LiveTrendMetric.CompletionRate,LiveTrendMetric.CycleTime,LiveTrendMetric.DevelopmentCycleTime,LiveTrendMetric.ReleaseWaitTime,LiveTrendMetric.AverageWip,LiveTrendMetric.TestingQueue,LiveTrendMetric.SpecialistWorkWaiting,LiveTrendMetric.ReviewQueue,LiveTrendMetric.ReadyForRelease,LiveTrendMetric.TechnicalDebtRatio,LiveTrendMetric.DeveloperUtilization,LiveTrendMetric.TesterUtilization,LiveTrendMetric.SystemCost})series[metric.ToString()]=LivePerformanceTrend.Project(s,metric,50,null).Points.Select(p=>new{p.Day,p.Value}).ToArray();
 var report=new{Scenario=name,Seed=seed,Request=Request(name,seed),Changes=s.Changes,Metrics=metrics,Fingerprint=fingerprint,ResultFingerprint=resultHash,Audit=audit};runs.Add(report);trajectories.Add(new{Scenario=name,Seed=seed,Series=series});
 File.WriteAllText(Path.Combine(folder,"runs",$"{name}-{seed}.json"),JsonSerializer.Serialize(report));
 using(var file=File.Create(Path.Combine(folder,"histories",$"{name}-{seed}.json.gz")))using(var gzip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionLevel.SmallestSize))JsonSerializer.Serialize(gzip,s.Days);

 if(new[]{12345,12359,12374}.Contains(seed)){var again=Run(name,seed);Validation.Require(fingerprint==Validation.Hash(again.Session.Capture())&&resultHash==Validation.Hash(again.Session.GetResult())&&Validation.Hash(metrics)==Validation.Hash(Metrics(again)),"Exact determinism failure");repeats.Add(new{Scenario=name,Seed=seed,ExactState=true,ExactResult=true,ExactMetrics=true});}
 Console.WriteLine($"PASS {name} seed {seed}");
 }catch(Exception e){failures.Add(new{Scenario=name,Seed=seed,Error=e.ToString()});Console.WriteLine($"FAIL {name} seed {seed}: {e}");}
});
 File.WriteAllText(Path.Combine(folder,"raw-results.json"),JsonSerializer.Serialize(new{Runs=runs,Repeats=repeats,Failures=failures},options));
 File.WriteAllText(Path.Combine(folder,"time-series.json"),JsonSerializer.Serialize(trajectories));
Validation.Require(failures.Count==0&&runs.Count==360,"Validation failures or incomplete runs");
Console.WriteLine("PASS 360 runs, 180000 audited days, 36 exact repeat runs.");
