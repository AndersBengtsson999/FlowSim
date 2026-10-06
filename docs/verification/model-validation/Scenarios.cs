using Simulation.Application;
using Simulation.Core;

namespace ModelValidation;

public sealed record Observation(string Scenario, int Day, int FirstDay, int LastDay, int Done, int CompletedInWindow,
    double ThroughputPerFiveDays, double? CycleTime, double AverageWip, int CurrentWip,
    double AverageReviewQueue, int CurrentReviewQueue, double AverageTestingQueue, int CurrentTestingQueue,
    double? DeveloperUtilization, double? TesterUtilization, double Debt, double DebtRatio, double DebtOverhead,
    double? CostPerItem, DeliveryCost? CostBreakdown, double DevelopmentCapacity, double DevelopmentWork,
    double ReviewCapacity, double ReworkCapacity, double TestingCapacity, double RepaymentCapacity, double DebtRepaid,
    double AvailableDeveloperCapacity, double AvailableTesterCapacity);

public sealed record Runs(LiveSimulation Baseline, LiveSimulation Productivity, LiveSimulation DebtTimeline,
    SimulationSessionState AtStop, SimulationSessionState AtRepayment,
    IReadOnlyList<Observation> Observations, int DebtZeroDay);

public static class Scenarios
{
    public const int Seed = 12345, Window = 50, StopDay = 200, RepaymentDay = 400;
    public static SimulationRequest BaselineRequest => new() {
        Name="Cross-metric validation", RandomSeed=Seed, DeveloperCount=5, TesterCount=2,
        DeveloperCapacityPerDay=1,TesterCapacityPerDay=1,DeveloperAvailability=1,TesterAvailability=1,
        DevelopmentWipLimit=5,CodeReviewWipLimit=3,TestingWipLimit=3,NumberOfWorkItems=0,
        DevelopmentEffort=5,CodeReviewEffort=1,TestingEffort=2,Productivity=new(1,1,1),
        Debt=new(){ShortcutRate=0,ShortcutEffortReduction=.5,CreationFactor=2,Tolerance=.1,Repayment=0},
        Quality=new(){Enabled=false,ReworkWipLimit=3},ArrivalMode=WorkArrivalMode.AlwaysAvailable
    };
    public static LiveSimulation Start(SimulationRequest request) => LiveSimulation.Start(request,WorkArrivalMode.AlwaysAvailable);
    public static void Until(LiveSimulation live,int day) { while(live.Session.CurrentDay<day) if(!live.Step()) throw new InvalidOperationException("Safety limit reached."); }
    public static Runs Run()
    {
        var rows=new List<Observation>();
        var baseline=Start(BaselineRequest);Until(baseline,StopDay);rows.Add(Observe(baseline,"1 Baseline"));
        var productivity=Start(BaselineRequest with{Productivity=new(2,1,1)});Until(productivity,StopDay);rows.Add(Observe(productivity,"2 Development 2x"));
        var debt=Start(BaselineRequest with{Debt=BaselineRequest.Debt with{ShortcutRate=.5}});
        Until(debt,StopDay);rows.Add(Observe(debt,"3a Shortcuts 50%"));var stop=debt.Session.Capture();
        debt.Session.ApplyChanges(debt.Session.Configuration with{Debt=debt.Session.Configuration.Debt with{ShortcutRate=0}},"Stop shortcuts");
        Until(debt,StopDay+Window);rows.Add(Observe(debt,"3b First window after stopping"));
        Until(debt,RepaymentDay);rows.Add(Observe(debt,"3c Continued without shortcuts"));var repayment=debt.Session.Capture();
        debt.Session.ApplyChanges(debt.Session.Configuration with{Debt=debt.Session.Configuration.Debt with{Repayment=.25}},"Repay debt");
        Until(debt,RepaymentDay+Window);rows.Add(Observe(debt,"4a Repayment 25%"));
        while(debt.Session.DebtState.Amount>0 && debt.Session.CurrentDay<2000)debt.Step();
        if(debt.Session.DebtState.Amount!=0)throw new InvalidOperationException("Debt not exhausted by validation bound.");
        var zeroDay=debt.Session.CurrentDay;
        Until(debt,zeroDay+Window);rows.Add(Observe(debt,"4b After debt is exhausted"));
        return new(baseline,productivity,debt,stop,repayment,rows,zeroDay);
    }
    public static Observation Observe(LiveSimulation live,string name)
    {
        var s=live.Session;var p=LivePerformance.Rolling(s,Window);var d=s.Days.Last();var days=s.Days.Where(x=>x.Day+1>=p.FirstDay).ToArray();
        return new(name,s.CurrentDay,p.FirstDay,p.LastDay,s.WorkItems.Count(w=>w.State==WorkItemStatus.Done),p.Completed,
            p.Throughput,p.CycleTime,p.AverageWip,d.TotalWip,p.Review.Average,p.Review.Current,p.Testing.Average,p.Testing.Current,
            p.DeveloperUtilization,p.TesterUtilization,s.DebtState.Amount,s.DebtState.Ratio,s.DebtState.Overhead(s.Configuration.Debt),
            p.DeliveryCostPerDoneItem,p.AverageDeliveryCost,days.Sum(x=>x.UsedDevelopmentCapacity),days.Sum(x=>x.DevelopmentWork),
            days.Sum(x=>x.UsedReviewCapacity),days.Sum(x=>x.UsedReworkDeveloperCapacity),days.Sum(x=>x.UsedTesterCapacity),
            days.Sum(x=>x.UsedDebtRepaymentCapacity),days.Sum(x=>x.Debt!.Repaid),days.Sum(x=>x.AvailableDeveloperCapacity),days.Sum(x=>x.AvailableTesterCapacity));
    }
}
