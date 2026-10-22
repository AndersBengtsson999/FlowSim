using Simulation.Core;
namespace Simulation.Application;

public static partial class KpiDashboard
{
    private static double? ObservedValue(TeamKpi id, SimulationSession session, int first, int last)
    {
        if (id == TeamKpi.UnlaunchedDevelopmentCost)
            return (double?)LaunchEconomics.Calculate(session.Days, session.ProductDelivery).Points.FirstOrDefault(p => p.Day == last)?.Unlaunched;
        if (id <= TeamKpi.TechnicalDebt) return RawValue(id, session.Days, first, last);
        var days = session.Days.Skip(first - 1).Take(last - first + 1).ToArray();
        if (days.Length == 0) return null;
        var released = session.WorkItems.Where(w => w.DoneDay >= first && w.DoneDay <= last).ToArray();
        var money = TeamEconomics.From(days, released.Length);
        double? Ratio(double? numerator, double denominator) => denominator > 0 ? numerator / denominator : null;
        switch (id)
        {
            case TeamKpi.CycleTimeP85:
                if (released.Length == 0 || released.Any(w => w.DevelopmentStartedDay is null)) return null;
                var durations = released.Select(w => (double)(w.DoneDay!.Value - w.DevelopmentStartedDay!.Value)).Order().ToArray();
                return durations[(int)Math.Ceiling(.85 * durations.Length) - 1];
            case TeamKpi.DeliveryFrequency:
                return 100.0 * released.Select(w => w.DoneDay).Distinct().Count() / days.Length;
            case TeamKpi.DeliveryVariability:
                var counts = released.GroupBy(w => w.DoneDay!.Value).ToDictionary(g => g.Key, g => g.Count());
                var mean = (double)released.Length / days.Length;
                return Math.Sqrt(days.Average(d => Math.Pow(counts.GetValueOrDefault(d.Day + 1) - mean, 2)));
            case TeamKpi.WipAge:
                var activeIds = days[^1].Items.Where(w => w.State is not (WorkItemStatus.Backlog or WorkItemStatus.Done or WorkItemStatus.Released)).Select(w => w.Id).ToHashSet();
                var active = session.WorkItems.Where(w => activeIds.Contains(w.Id)).ToArray();
                if (active.Any(w => w.DevelopmentStartedDay is null)) return null;
                return active.Length == 0 ? null : active.Average(w => (double)(last - w.DevelopmentStartedDay!.Value));
            case TeamKpi.ReviewQueueGrowth: return LivePerformance.Slope(days.Select(d => ((double)d.Day, (double)d.WaitingForCodeReviewCount)));
            case TeamKpi.ReworkCost: return (double?)money.ReworkCost;
            case TeamKpi.ReworkProportion: return Ratio(100 * days.Sum(d => d.UsedReworkDeveloperCapacity), days.Sum(d => d.UsedDeveloperCapacity));
            case TeamKpi.DebtGrowth: return days.Any(d => d.Debt is null) ? null : days.Sum(d => d.Debt!.Created - d.Debt.Repaid);
            case TeamKpi.DebtRepayment: return days.Any(d => d.Debt is null) ? null : days.Sum(d => d.Debt!.RepaymentCapacity);
            case TeamKpi.CostPerWorkSize: return Ratio((double?)money.TotalTeamCost, released.Sum(w => w.DevelopmentEffort));
            case TeamKpi.WorkVolumePerSek: return money.TotalTeamCost is > 0 ? released.Sum(w => w.DevelopmentEffort) / (double)money.TotalTeamCost : null;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
    public static string Interpretation(TeamKpi metric) => metric switch
    {
        TeamKpi.WorkVolumePerSek => "Higher means more original Development effort internally released per unit of staffing cost, not more customer value. Customer launches do not change this ratio.",
        TeamKpi.UnlaunchedDevelopmentCost => "A stock of observed allocated cost, not period expenditure. Customer launches reclassify included work; they create no expense. WIP and excluded work remain unlaunched.",
        TeamKpi.DeveloperUtilization or TeamKpi.TesterUtilization => "High utilization does not imply high delivery performance and may coincide with growing queues.",
        TeamKpi.CostPerDelivery or TeamKpi.TotalTeamCost => "Staffing cost is not value or profit. Work size, batching and window boundaries affect cost per delivery.",
        TeamKpi.CycleTime => "A mean can hide large differences between items. Only released items enter this cohort; old unfinished work is excluded.",
        TeamKpi.ReleasedThroughput => "Item counts do not measure work size, quality or customer value. Batch releases and window boundaries affect this rate.",
        TeamKpi.TotalWip or TeamKpi.DevelopmentQueue or TeamKpi.ReviewQueue or TeamKpi.TestingQueue => "A smaller queue can mean improved flow or insufficient incoming work. This snapshot is not a period average.",
        TeamKpi.ReworkEffort => "Capacity consumption is not elapsed time or a defect percentage. Work mix and productivity affect interpretation.",
        TeamKpi.TechnicalDebt => "This is the model's outstanding effort debt, not financial debt or a complete measure of software quality.",
        _ => "Read the definition and cohort carefully. A higher or lower value alone is not a performance verdict."
    };
}
