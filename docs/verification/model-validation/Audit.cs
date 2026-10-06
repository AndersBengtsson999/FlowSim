using System.Security.Cryptography;
using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
namespace ModelValidation;

public sealed record AuditResult(int DaysChecked,int ItemObservationsChecked,int RepaymentDays,int FractionalPayoffDays,int ZeroDebtDaysAfterRepayment);
public static class Audit
{
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Near(double actual,double expected,string message) => Require(double.IsFinite(actual)&&double.IsFinite(expected)&&Math.Abs(actual-expected)<=1e-8*Math.Max(1,Math.Abs(expected)),$"{message}: actual={actual:R}, expected={expected:R}");
    public static string Fingerprint(SimulationSession session) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(session.Capture())));
    public static AuditResult Check(SimulationSession session)
    {
        var items=session.WorkItems.ToDictionary(w=>w.Id);
        IReadOnlyDictionary<string,WorkItemDaySnapshot> previous=new Dictionary<string,WorkItemDaySnapshot>();
        double openingDebt=0;int count=0,repaymentDays=0,fractional=0,zeroDays=0;
        foreach(var day in session.Days)
        {
            var c=session.Changes.LastOrDefault(x=>x.Day<=day.Day)?.After??session.InitialConfiguration;
            var tag=$"Day {day.Day+1}";
            Near(day.AvailableDeveloperCapacity,c.Team.AvailableDeveloperCapacity,tag+" developer supply");
            Near(day.AvailableTesterCapacity,c.Team.AvailableTesterCapacity,tag+" tester supply");
            Require(day.DevelopmentWip<=c.DevelopmentWipLimit&&day.ReviewWip<=c.CodeReviewWipLimit&&day.TestingWip<=c.TestingWipLimit&&day.ReworkWip<=c.Quality.ReworkWipLimit,tag+" active WIP limits");
            foreach(var (stage,occupancy) in new[]{(WorkItemStatus.Development,day.DevelopmentWip),(WorkItemStatus.CodeReview,day.ReviewWip),(WorkItemStatus.Testing,day.TestingWip),(WorkItemStatus.Rework,day.ReworkWip)})
                Require(day.Items.Count(w=>w.StateDuringDay==stage&&w.CreatedDay<=day.Day)==occupancy,tag+" waiting states excluded from active WIP");
            double Demand(WorkItemStatus stage,double productivity,double perPerson) => day.Items.Where(w=>w.StateDuringDay==stage).Sum(w=>{
                previous.TryGetValue(w.Id,out var old);
                var remaining=stage switch{
                    WorkItemStatus.CodeReview=>old?.State==stage?old.RemainingCodeReviewEffort:items[w.Id].CodeReviewEffort,
                    WorkItemStatus.Testing=>old?.State==stage?old.RemainingTestingEffort:items[w.Id].TestingEffort,
                    _=>old?.RemainingReworkEffort??0
                };
                return Math.Min(remaining/productivity,Math.Min(1,perPerson));
            });
            Near(day.UsedReviewCapacity,Math.Min(day.AvailableDeveloperCapacity,Demand(WorkItemStatus.CodeReview,c.Productivity.CodeReview,c.Team.DeveloperCapacityPerDay)),tag+" Review first");
            Near(day.UsedReworkDeveloperCapacity,Math.Min(day.AvailableDeveloperCapacity-day.UsedReviewCapacity,Demand(WorkItemStatus.Rework,1,c.Team.DeveloperCapacityPerDay)),tag+" Rework second");
            Near(day.UsedTesterCapacity,Math.Min(day.AvailableTesterCapacity,Demand(WorkItemStatus.Testing,c.Productivity.Testing,c.Team.TesterCapacityPerDay)),tag+" independent tester pool");
            var remaining=day.AvailableDeveloperCapacity-day.UsedReviewCapacity-day.UsedReworkDeveloperCapacity;
            var allocation=remaining*c.Debt.Repayment;var needed=openingDebt/c.Productivity.Development;
            Near(day.UsedDebtRepaymentCapacity,Math.Min(allocation,needed),tag+" repayment only from post-Review/Rework pool");
            if(day.UsedDebtRepaymentCapacity>0)repaymentDays++;
            if(needed>0&&needed<allocation-1e-9){fractional++;Near(day.Debt!.Repaid,openingDebt,tag+" exact fractional payoff");}
            if(c.Debt.Repayment>0&&openingDebt==0){zeroDays++;Near(day.UsedDebtRepaymentCapacity,0,tag+" no zero-debt reservation");}
            Require(day.UsedDevelopmentCapacity<=remaining-day.UsedDebtRepaymentCapacity+1e-8,tag+" delivery pool bound");
            Near(day.Debt!.Repaid,day.UsedDebtRepaymentCapacity*c.Productivity.Development,tag+" repayment has no collaboration");
            Near(day.Debt.State.Amount,openingDebt-day.Debt.Repaid+day.Debt.Created,tag+" debt conservation");
            Near(day.Debt.Overhead,Math.Max(0,day.Debt.State.Ratio-c.Debt.Tolerance)*c.Debt.ImpactFactor,tag+" overhead");
            Near(day.DevelopmentWork,c.Productivity.Development*(day.UsedDevelopmentCapacity-.5*day.CollaborationDevelopmentCapacity),tag+" development effective/consumed accounting");
            Near(day.ReviewWork,c.Productivity.CodeReview*day.UsedReviewCapacity,tag+" review productivity");
            Near(day.TestingWork,c.Productivity.Testing*day.UsedTesterCapacity,tag+" testing productivity");
            Require(day.UsedDeveloperCapacity<=day.AvailableDeveloperCapacity+1e-8&&day.UsedTesterCapacity<=day.AvailableTesterCapacity+1e-8,tag+" bounded consumption");
            double itemConsumption=0;
            foreach(var w in day.Items)
            {
                count++;previous.TryGetValue(w.Id,out var prior);
                var old=prior?.DeliveryCost??new();var cost=w.DeliveryCost!;
                Require(cost is {IsComplete:true},tag+" complete measurement");
                var values=new[]{w.RemainingDevelopmentEffort,w.RemainingCodeReviewEffort,w.RemainingTestingEffort,w.RemainingReworkEffort,w.UsedDevelopmentCapacity,w.CodeReviewWork,w.TestingWork,w.ReworkWork,w.CollaborationDevelopmentCapacity,cost!.Development,cost.CodeReview,cost.Rework,cost.Testing,cost.Total,day.Debt.State.Amount,day.Debt.State.Ratio};
                Require(values.All(v=>double.IsFinite(v)&&v>=0),tag+" finite nonnegative effort, capacity, debt and costs");
                var review=w.ConsumedCapacity?.CodeReview??w.CodeReviewWork;var testing=w.ConsumedCapacity?.Testing??w.TestingWork;
                Near(cost.Development-old.Development,w.UsedDevelopmentCapacity,tag+" raw Development attribution "+w.Id);
                Near(cost.CodeReview-old.CodeReview,review,tag+" Review attribution "+w.Id);
                Near(cost.Rework-old.Rework,w.ReworkWork,tag+" Rework attribution "+w.Id);
                Near(cost.Testing-old.Testing,testing,tag+" Testing attribution "+w.Id);
                Near(cost.Total,cost.Development+cost.CodeReview+cost.Rework+cost.Testing,tag+" cost components");
                Require(new[]{w.UsedDevelopmentCapacity,review,testing,w.ReworkWork}.Count(v=>v>0)<=1,tag+" one item stage per day");
                Require(w.CollaborationDevelopmentCapacity==0||w.StateDuringDay==WorkItemStatus.Development,tag+" collaboration only in Development");
                itemConsumption+=cost.Total-old.Total;
            }
            Near(itemConsumption,day.UsedDeveloperCapacity-day.UsedDebtRepaymentCapacity+day.UsedTesterCapacity,tag+" no debt capacity in item costs");
            var period=LivePerformance.Period(session,Math.Max(1,day.Day+2-Scenarios.Window),day.Day+1);
            var window=session.Days.Skip(Math.Max(0,day.Day+1-Scenarios.Window)).Take(Math.Min(Scenarios.Window,day.Day+1)).ToArray();
            Near(period.DeveloperUtilization!.Value,window.Sum(d=>d.UsedDeveloperCapacity)/window.Sum(d=>d.AvailableDeveloperCapacity),tag+" consumed developer utilization");
            Near(period.TesterUtilization!.Value,window.Sum(d=>d.UsedTesterCapacity)/window.Sum(d=>d.AvailableTesterCapacity),tag+" consumed tester utilization");
            if(period.AverageDeliveryCost is { } average)Near(period.DeliveryCostPerDoneItem!.Value,average.Development+average.CodeReview+average.Rework+average.Testing,tag+" cohort cost breakdown");
            openingDebt=day.Debt.State.Amount;previous=day.Items.ToDictionary(w=>w.Id);
        }
        return new(session.CurrentDay,count,repaymentDays,fractional,zeroDays);
    }
}
