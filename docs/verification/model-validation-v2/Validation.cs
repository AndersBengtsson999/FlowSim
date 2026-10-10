using System.Security.Cryptography;
using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
namespace ModelValidationV2;

public static class Validation
{
    public const int LastDay=300, Window=50;
    public static SimulationRequest Baseline => new() {Name="Validation v2",NumberOfWorkItems=0,SimulationDays=LastDay,RandomSeed=12345,ArrivalMode=WorkArrivalMode.AlwaysAvailable,
        DeveloperCount=5,TesterCount=2,DeveloperAvailability=1,TesterAvailability=1,DevelopmentWipLimit=5,CodeReviewWipLimit=3,TestingWipLimit=3,
        DevelopmentEffort=5,CodeReviewEffort=1,TestingEffort=2,Productivity=new(),Skills=new(),ResidualDependencies=new(),Release=new(),Quality=new(){Enabled=false},Debt=new(){ShortcutRate=0,Repayment=0}};
    public static SimulationRequest Complex => Baseline with {Skills=new(1,.3),ResidualDependencies=new(.25,4),Productivity=new(1.5,1,1),Debt=new(){ShortcutRate=.3,ShortcutEffortReduction=.5,CreationFactor=2,Tolerance=.1,Repayment=.2,ImpactFactor=1},Release=new(ReleaseMode.Scheduled,5,5)};
    public static SimulationRequest Request(string scenario) => scenario switch {"A"=>Baseline,"B"=>Baseline with {Productivity=new(2,1,1)},"C" or "D"=>Complex,_=>throw new ArgumentException(scenario)};
    public static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    public static void Near(double actual,double expected,string message){Require(double.IsFinite(actual)&&double.IsFinite(expected)&&Math.Abs(actual-expected)<=1e-8*Math.Max(1,Math.Abs(expected)),$"{message}: {actual:R} != {expected:R}");}
    public static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static void Until(LiveSimulation live,int day){while(live.Session.CurrentDay<day)Require(live.Step(),"Safety bound");}
    public static LiveSimulation Run(string name)
    {
        var live=LiveSimulation.Start(Request(name),WorkArrivalMode.AlwaysAvailable);
        if(name=="D")
        {
            Until(live,150);var items=Hash(live.Session.Capture().WorkItems);var history=Hash(live.Session.Days);var debt=live.Session.DebtState;
            var c=live.Session.Configuration;
            live.Session.ApplyChanges(c with {Skills=c.Skills with {Specialists=2},ResidualDependencies=c.ResidualDependencies with {Rate=.1},Release=c.Release with{Capacity=10}},"Recovery assumptions");
            Require(items==Hash(live.Session.Capture().WorkItems)&&history==Hash(live.Session.Days)&&debt==live.Session.DebtState,"Intervention rewrote existing items, assignments, debt or history");
            var comparison=LivePerformance.Compare(live.Session,live.Session.Changes.Single(),50);
            Require(comparison.Before.FirstDay==101&&comparison.Before.LastDay==150&&comparison.After.FirstDay==151&&comparison.After.LastDay==200&&comparison.After.AvailableDays==0,"Intervention period boundaries");
        }
        Until(live,LastDay);return live;
    }
    public static SessionConfiguration Config(SimulationSession s,int day)=>s.Changes.LastOrDefault(c=>c.Day<=day)?.After??s.InitialConfiguration;
    public static object Check(LiveSimulation live)
    {
        var s=live.Session;
        var previousAudit=ModelValidation.Audit.Check(s);
        var counts=0;double scope=0;
        var itemResults=s.GetResult().WorkItems.ToDictionary(w=>w.Id);
        foreach(var day in s.Days)
        {
            var c=Config(s,day.Day);var tag=$"day {day.Day+1}";
            Require(day.Items.Select(w=>w.Id).Distinct().Count()==day.Items.Count,tag+" duplicate item");
            Require(day.Items.Select(w=>w.Id).Order().SequenceEqual(s.WorkItems.Where(w=>w.CreatedDay<=day.Day).Select(w=>w.Id).Order()),tag+" materialized item conservation");
            Require(day.Items.All(w=>Enum.IsDefined(w.State)),tag+" valid authoritative state");
            Require(day.Items.Count==day.BacklogCount+day.DevelopmentCount+day.WaitingForCodeReviewCount+day.CodeReviewCount+day.WaitingForReworkCount+day.ReworkCount+day.WaitingForTestingCount+day.TestingCount+day.ReadyForReleaseCount+day.DoneCount,tag+" lifecycle partition");
            Require(day.TotalWip==day.Items.Count-day.BacklogCount-day.DoneCount,tag+" total WIP excludes Backlog");
            Require(day.WaitingForDependencyCount==day.Items.Count(w=>w.State==WorkItemStatus.Backlog&&w.ResidualDependency is {} dep&&dep.ResolutionDay>day.Day+1),tag+" dependency queue");
            Require(day.SpecialistWorkWaiting==day.Items.Count(w=>w.State==WorkItemStatus.Development&&w.RequiresSpecialist&&w.RemainingDevelopmentEffort>0&&w.UsedDevelopmentCapacity==0),tag+" specialist derived queue");
            var pool=Math.Max(0,day.AvailableDeveloperCapacity-day.UsedReviewCapacity-day.UsedReworkDeveloperCapacity-day.UsedDebtRepaymentCapacity);
            var spec=day.Items.Where(w=>w.RequiresSpecialist).Sum(w=>w.UsedDevelopmentCapacity);
            Require(spec<=pool*c.Skills.Specialists/Math.Max(1,c.Team.DeveloperCount)+1e-8,tag+" specialist eligible pool");
            foreach(var w in day.Items)
            {
                counts++;
                if(w.StateDuringDay==WorkItemStatus.Backlog || w.StateDuringDay==WorkItemStatus.ReadyForRelease)
                    Near(w.UsedDevelopmentCapacity+(w.ConsumedCapacity?.CodeReview??w.CodeReviewWork)+(w.ConsumedCapacity?.Testing??w.TestingWork)+w.ReworkWork,0,tag+" waiting consumes no item capacity");
                if(w.RequiresSpecialist&&c.Skills.Specialists<2)Near(w.CollaborationDevelopmentCapacity,0,tag+" specialist collaboration eligibility");
                Near(w.DevelopmentWork,c.Productivity.Development*(w.UsedDevelopmentCapacity-.5*w.CollaborationDevelopmentCapacity),tag+" per-item work/capacity");
                Require(w.UsedDevelopmentCapacity<=2*Math.Min(1,c.Team.DeveloperCapacityPerDay)+1e-8,tag+" two contributions maximum");
                Require(w.CollaborationDevelopmentCapacity<=Math.Min(1,c.Team.DeveloperCapacityPerDay)+1e-8,tag+" collaboration maximum");
            }
            var completed=s.WorkItems.Where(w=>w.DevelopmentCompletedDay==day.Day+1).ToArray();
            scope+=completed.Sum(w=>w.DevelopmentEffort);Near(day.Debt!.State.CumulativeDevelopmentScope,scope,tag+" debt scope");
            Near(day.Debt.Created,completed.Sum(w=>w.DevelopmentPlan?.DebtToCreate??0),tag+" debt created at Development completion");
            var opening=day.Day==0?new TechnicalDebtState():s.Days[day.Day-1].Debt!.State;
            foreach(var w in s.WorkItems.Where(w=>w.DevelopmentStartedDay==day.Day))
            {
                Require(w.ResidualDependency is null||w.DevelopmentStartedDay>=w.ResidualDependency.ResolutionDay,tag+" dependency start boundary");
                if(w.DevelopmentPlan is {} p)
                {
                    Near(p.DebtRatioAtStart,opening.Ratio,tag+" opening debt ratio");Near(p.Overhead,Math.Max(0,opening.Ratio-c.Debt.Tolerance)*c.Debt.ImpactFactor,tag+" start overhead");
                    Near(p.EffortWithDebt,w.DevelopmentEffort*(1+p.Overhead),tag+" overhead effort");Near(p.FinalEffort,p.EffortWithDebt*(p.IsShortcut?1-c.Debt.ShortcutEffortReduction:1),tag+" shortcut effort");
                    Near(p.DebtToCreate,(p.EffortWithDebt-p.FinalEffort)*c.Debt.CreationFactor,tag+" frozen debt creation");
                }
            }
            var released=s.WorkItems.Count(w=>w.ReleasedDay==day.Day+1);
            Require(released<=(c.Release.IsOpportunity(day.Day+1)?c.Release.Capacity:0),tag+" release opportunity/capacity");
            var eligibleRelease=s.WorkItems.Where(w=>w.ReadyForReleaseDay<=day.Day+1 && (w.ReleasedDay is null||w.ReleasedDay>=day.Day+1)).OrderBy(w=>w.ReadyForReleaseDay).Take(c.Release.IsOpportunity(day.Day+1)?c.Release.Capacity:0).Select(w=>w.Id).ToHashSet();
            Require(eligibleRelease.SetEquals(s.WorkItems.Where(w=>w.ReleasedDay==day.Day+1).Select(w=>w.Id)),tag+" release FIFO and same-day Testing completion");
            CheckPeriod(s,Math.Max(1,day.Day+2-Window),day.Day+1);
        }
        foreach(var w in s.WorkItems)
        {
            var events=w.Events.Where(e=>e.EventType==WorkItemEventType.CapacityApplied).ToArray();
            Near(w.DeliveryCost.Development,events.Where(e=>e.FromState==WorkItemStatus.Development).Sum(e=>e.CapacityConsumed),"Development cost versus raw events");
            Near(w.DeliveryCost.CodeReview,events.Where(e=>e.FromState==WorkItemStatus.CodeReview).Sum(e=>e.CapacityConsumed),"Review cost versus raw events");
            Near(w.DeliveryCost.Rework,events.Where(e=>e.FromState==WorkItemStatus.Rework).Sum(e=>e.CapacityConsumed),"Rework cost versus raw events");
            Near(w.DeliveryCost.Testing,events.Where(e=>e.FromState==WorkItemStatus.Testing).Sum(e=>e.CapacityConsumed),"Testing cost versus raw events");
            if(w.ResidualDependency is {} dep)Require(dep.ResolutionDay==(long)w.CreatedDay+dep.WaitingDays,"Arrival-based dependency");
            var work=s.Days.SelectMany(d=>d.Items.Where(x=>x.Id==w.Id)).Sum(x=>x.DevelopmentWork);
            Near(work+w.RemainingDevelopmentEffort,w.DevelopmentPlan?.FinalEffort??w.DevelopmentEffort,"Development effort conservation "+w.Id);
            if(w.ReleasedDay is {} released)
            {
                Require(w.ReadyForReleaseDay<=released&&w.DevelopmentStartedDay<=w.ReadyForReleaseDay,"Lifecycle ordering");
                var result=itemResults[w.Id];
                Near(result.CycleTime!.Value,result.DevelopmentCycleTime!.Value+result.ReleaseWaitTime!.Value,"Same-item exported cycle identity");
                Near(result.DevelopmentCycleTime.Value,w.ReadyForReleaseDay!.Value-w.DevelopmentStartedDay!.Value,"Exported Development cycle population");
                Near(result.LeadTime!.Value-result.CycleTime.Value,w.DevelopmentStartedDay.Value-w.CreatedDay,"Pre-Development waiting excluded from cycle");
            }
        }
        foreach(var metric in LivePerformanceTrend.Metrics)
        {
            var series=LivePerformanceTrend.Project(s,metric.Metric,Window,null);
            foreach(var point in series.Points)
            {
                Require(point.Value is null||double.IsFinite(point.Value.Value),"Finite trend "+metric.Name);
                var d=s.Days[point.Day-1];var p=LivePerformance.Period(s,Math.Max(1,point.Day-Window+1),point.Day);
                double? expected=metric.Metric switch {
                    LiveTrendMetric.Throughput=>p.Throughput,LiveTrendMetric.CycleTime=>p.CycleTime,LiveTrendMetric.DevelopmentCycleTime=>p.DevelopmentCycleTime,LiveTrendMetric.ReleaseWaitTime=>p.ReleaseWaitTime,
                    LiveTrendMetric.CompletionRate=>p.CompletionRate,LiveTrendMetric.AverageWip=>p.AverageWip,LiveTrendMetric.DeliveryCost=>p.DeliveryWorkCostPerItem,LiveTrendMetric.SystemCost=>p.SystemCostPerReleasedItem,
                    LiveTrendMetric.DeveloperUtilization=>p.DeveloperUtilization*100,LiveTrendMetric.TesterUtilization=>p.TesterUtilization*100,
                    LiveTrendMetric.ReviewQueue=>d.WaitingForCodeReviewCount,LiveTrendMetric.TestingQueue=>d.WaitingForTestingCount,LiveTrendMetric.ReworkQueue=>d.WaitingForReworkCount,LiveTrendMetric.WaitingForDependency=>d.WaitingForDependencyCount,
                    LiveTrendMetric.ReadyForRelease=>d.ReadyForReleaseCount,LiveTrendMetric.SpecialistWorkWaiting=>d.SpecialistWorkWaiting,
                    LiveTrendMetric.DevelopmentCapacity=>d.UsedDevelopmentCapacity,LiveTrendMetric.DevelopmentWork=>d.DevelopmentWork,LiveTrendMetric.AvailableDevelopers=>d.AvailableDeveloperCapacity,LiveTrendMetric.AvailableTesters=>d.AvailableTesterCapacity,
                    LiveTrendMetric.TechnicalDebtRatio=>d.Debt!.State.Ratio*100,LiveTrendMetric.TechnicalDebt=>d.Debt!.State.Amount,LiveTrendMetric.DebtOverhead=>d.Debt!.Overhead*100,_=>throw new Exception("Unvalidated trend")};
                if(expected is null)Require(point.Value is null,"No-data trend "+metric.Name);else {Require(point.Value.HasValue,"Missing trend");Near(point.Value!.Value,expected.Value,"Trend "+metric.Name);}
            }
        }
        return new {previousAudit,ItemObservations=counts};
    }
    public static void CheckPeriod(SimulationSession s,int first,int last)
    {
        var p=LivePerformance.Period(s,first,last);var days=s.Days.Where(d=>d.Day+1>=first&&d.Day+1<=last).ToArray();
        var released=s.WorkItems.Where(w=>w.ReleasedDay>=first&&w.ReleasedDay<=last).ToArray();
        var ready=s.WorkItems.Where(w=>w.ReadyForReleaseDay>=first&&w.ReadyForReleaseDay<=last).ToArray();
        Near(p.Throughput,5.0*released.Length/days.Length,"Released throughput denominator");Near(p.CompletionRate,5.0*ready.Length/days.Length,"Completion Rate denominator");
        var raw=days.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity);Near(p.ConsumedSystemCapacity!.Total,raw,"System capacity sum including repayment");
        if(released.Length==0){Require(p.SystemCostPerReleasedItem is null&&p.CycleTime is null&&p.ReleaseWaitTime is null,"No releases => unavailable ratios");}
        else {Near(p.SystemCostPerReleasedItem!.Value,raw/released.Length,"System Cost denominator");Near(p.CycleTime!.Value,released.Average(w=>(double)(w.ReleasedDay!.Value-w.DevelopmentStartedDay!.Value)),"Delivery cohort cycle");Near(p.ReleaseWaitTime!.Value,released.Average(w=>(double)(w.ReleasedDay!.Value-w.ReadyForReleaseDay!.Value)),"Release cohort wait");}
        if(ready.Length==0)Require(p.DeliveryWorkCostPerItem is null&&p.DevelopmentCycleTime is null,"No work completions => unavailable");
        else {Near(p.DeliveryWorkCostPerItem!.Value,ready.Average(w=>w.DeliveryCost.Total),"Item cost cohort");Near(p.DevelopmentCycleTime!.Value,ready.Average(w=>(double)(w.ReadyForReleaseDay!.Value-w.DevelopmentStartedDay!.Value)),"Development cycle excludes pre-start wait");}
        var queues=new (QueuePerformance Q,Func<DailySnapshot,double> Value)[]{(p.Review,d=>d.WaitingForCodeReviewCount),(p.Testing,d=>d.WaitingForTestingCount),(p.Rework,d=>d.WaitingForReworkCount),(p.ReadyForRelease,d=>d.ReadyForReleaseCount),(p.WaitingForDependency,d=>d.WaitingForDependencyCount)};
        foreach(var (q,value) in queues){Near(q.Current,value(days[^1]),"Queue current");Near(q.Average,days.Average(value),"Queue average");var slope=LivePerformance.Slope(days.Select(d=>((double)d.Day,value(d))));if(slope.HasValue)Near(q.Trend!.Value,slope.Value,"OLS trend");else Require(q.Trend is null,"Insufficient OLS history");}
    }
}
