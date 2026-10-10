using System.Globalization;
using System.Text.Json;
using ModelValidationV2;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
var folder=args.FirstOrDefault()??"docs/verification/model-validation-v2";Directory.CreateDirectory(folder);
var reports=new List<object>();var checks=new List<object>();
foreach(var name in new[]{"A","B","C","D"})
{
    var live=Validation.Run(name);var s=live.Session;var before=Validation.Hash(s.Capture());
    var audit=Validation.Check(live);
    foreach(var d in s.Days)
    {
        var c=Validation.Config(s,d.Day);var rows=FlowPresentation.Rows(d,c,true).Select(r=>QueueAttention.Apply(r,c,s.Days.Where(x=>x.Day<=d.Day).TakeLast(50).ToArray())).ToArray();
        Validation.Require(rows.Sum(r=>r.Count)==d.Items.Count,"Flow Board partition");
        Validation.Require(rows.SelectMany(r=>r.Items).Select(w=>w.Id).Distinct().Count()==d.Items.Count,"Flow Board item disclosure partition");
        foreach(var row in rows.Where(r=>r.Queue is not null))
        {
            var q=row.Queue!;var growing=q.Slope>=.05;var expected=q.Count==0?QueueAttentionState.Neutral:q.RelativeSize>=4||q.RelativeSize>=2&&growing?QueueAttentionState.Strong:q.RelativeSize>=2||q.RelativeSize>=1&&growing?QueueAttentionState.Attention:QueueAttentionState.Neutral;
            Validation.Require(q.State==expected,"Queue attention threshold");
        }
    }
    Validation.Require(before==Validation.Hash(s.Capture()),"Read-only projections mutated state");
    var again=Validation.Run(name);Validation.Require(before==Validation.Hash(again.Session.Capture()),name+" exact repeated determinism");
    if(name!="D")Validation.Require(Validation.Hash(new SimulationRunner().Run(Validation.Request(name)))==Validation.Hash(s.GetResult()),name+" Run/Live equivalence");
    var p=LivePerformance.Rolling(s,50);var ds=s.Days.TakeLast(50).ToArray();
    object Queue(Func<DailySnapshot,double> value)=>new {Current=value(ds[^1]),Average=ds.Average(value),Maximum=ds.Max(value),Slope=LivePerformance.Slope(ds.Select(d=>((double)d.Day,value(d))))};
    var report=new {Scenario=name,ThroughDay=s.CurrentDay,Period=new[]{p.FirstDay,p.LastDay},Materialized=s.WorkItems.Count,DevelopmentCompleted=s.WorkItems.Count(w=>w.DevelopmentCompletedDay.HasValue),WorkCompleted=s.WorkItems.Count(w=>w.ReadyForReleaseDay.HasValue),Released=s.WorkItems.Count(w=>w.ReleasedDay.HasValue),PeriodReleased=p.Completed,PeriodWorkCompleted=p.WorkCompleted,p.Throughput,p.CompletionRate,p.DevelopmentCycleTime,DeliveryCycleTime=p.CycleTime,p.ReleaseWaitTime,p.AverageWip,p.DeveloperUtilization,p.TesterUtilization,p.DeliveryWorkCostPerItem,p.SystemCostPerReleasedItem,
        DebtRatio=s.DebtState.Ratio,Debt=s.DebtState.Amount,DebtMaximum=s.Days.Max(d=>d.Debt!.State.Amount),DebtCreated=s.Days.Sum(d=>d.Debt!.Created),DebtRepaid=s.Days.Sum(d=>d.Debt!.Repaid),PeriodRepaymentCapacity=ds.Sum(d=>d.UsedDebtRepaymentCapacity),MaximumStartOverhead=s.WorkItems.Max(w=>w.DevelopmentPlan?.Overhead??0),
        Dependency=Queue(d=>d.WaitingForDependencyCount),Specialist=Queue(d=>d.SpecialistWorkWaiting),Release=Queue(d=>d.ReadyForReleaseCount),Review=Queue(d=>d.WaitingForCodeReviewCount),Testing=Queue(d=>d.WaitingForTestingCount),Rework=Queue(d=>d.WaitingForReworkCount),DevelopmentWip=ds.Average(d=>d.DevelopmentWip),EffectiveDevelopment=ds.Sum(d=>d.DevelopmentWork),DevelopmentCapacity=ds.Sum(d=>d.UsedDevelopmentCapacity),CollaborationCapacity=ds.Sum(d=>d.CollaborationDevelopmentCapacity),Fingerprint=before};
    reports.Add(report);checks.Add(new {Scenario=name,Audit=audit,Deterministic=true,ProjectionsReadOnly=true,RunLiveEquivalent=name=="D"?(bool?)null:true});Console.WriteLine(JsonSerializer.Serialize(report));
    if(name=="D")File.WriteAllText(Path.Combine(folder,"intervention.json"),JsonSerializer.Serialize(new {Change=s.Changes.Single(),Immediate=LivePerformance.Compare(s,s.Changes.Single(),50),Post= p},new JsonSerializerOptions{WriteIndented=true}));
}
var cLive=LiveSimulation.Start(Validation.Complex,WorkArrivalMode.AlwaysAvailable);Validation.Until(cLive,150);var checkpoint=cLive.CreateCheckpoint("Day 150");var json=LiveSessionJson.Save(cLive);
var checkpointQueues=new {Dependencies=cLive.CurrentSnapshot.WaitingForDependencyCount,Specialists=cLive.CurrentSnapshot.SpecialistWorkWaiting,Release=cLive.CurrentSnapshot.ReadyForReleaseCount};
Validation.Until(cLive,300);var target=Validation.Hash(cLive.Session.Capture());cLive.RestoreCheckpoint(checkpoint.Id);Validation.Until(cLive,300);Validation.Require(target==Validation.Hash(cLive.Session.Capture()),"C checkpoint exact equivalence");var loaded=LiveSessionJson.Load(json);Validation.Until(loaded,300);Validation.Require(target==Validation.Hash(loaded.Session.Capture()),"C JSON exact equivalence");
var dLive=Validation.Run("D");Validation.Require(Validation.Hash(cLive.Session.Days.Take(150))==Validation.Hash(dLive.Session.Days.Take(150)),"C/D exact common pre-intervention history");
var extras=new List<object>();
foreach(var (name,request) in new[]{("availability",Validation.Complex with{DeveloperAvailability=.6,TesterAvailability=.7}),("rework",Validation.Complex with{Quality=new(){Enabled=true,CodeReviewDefectProbability=.2,TestingDefectProbability=.2}}),("no-release",Validation.Complex with{Release=new(Capacity:0)})})
{
    var live=LiveSimulation.Start(request,WorkArrivalMode.AlwaysAvailable);Validation.Until(live,200);var result=Validation.Check(live);var p=LivePerformance.Rolling(live.Session,50);
    if(name=="no-release")Validation.Require(p.Completed==0&&p.SystemCostPerReleasedItem is null&&p.DeliveryWorkCostPerItem is not null,"No release no-data and separate completion population");
    extras.Add(new{Name=name,Audit=result,Final= p});Console.WriteLine("PASS supplemental "+name);
}
var mix=LiveSimulation.Start(Validation.Complex with{ResidualDependencies=new(1,20),Release=new(Capacity:0)},WorkArrivalMode.AlwaysAvailable);
while(mix.Session.CurrentDay<200){mix.Step();if(mix.CurrentSnapshot.WaitingForDependencyCount>0&&mix.CurrentSnapshot.SpecialistWorkWaiting>0&&mix.CurrentSnapshot.ReadyForReleaseCount>0)break;}
Validation.Require(mix.Session.CurrentDay<200,"Mixed queue checkpoint coverage");var mixDay=mix.Session.CurrentDay;var mixCheckpoint=mix.CreateCheckpoint("Mixed queues");var mixQueues=new{Day=mixDay,Dependencies=mix.CurrentSnapshot.WaitingForDependencyCount,Specialists=mix.CurrentSnapshot.SpecialistWorkWaiting,Release=mix.CurrentSnapshot.ReadyForReleaseCount};Validation.Until(mix,mixDay+100);var mixExpected=Validation.Hash(mix.Session.Capture());mix.RestoreCheckpoint(mixCheckpoint.Id);Validation.Until(mix,mixDay+100);Validation.Require(mixExpected==Validation.Hash(mix.Session.Capture()),"Mixed queues restore");
File.WriteAllText(Path.Combine(folder,"results.json"),JsonSerializer.Serialize(new {Configurations=new{A=Validation.Request("A"),B=Validation.Request("B"),C=Validation.Request("C"),D=Validation.Request("D")},Reports=reports,Checks=checks,CheckpointC=new{Queues=checkpointQueues,ExactCheckpoint=true,ExactJson=true},MixedQueueCheckpoint=mixQueues,Supplemental=extras},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS: A–D all-day invariants, repeated exact determinism, Run/Live, projections, checkpoint/JSON, mixed queues and supplemental probes.");
