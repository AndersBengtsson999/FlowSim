using Simulation.Core;

namespace Simulation.Application;

/// <summary>Raw capacity consumed inside a period, including unfinished work and system debt repayment.</summary>
public sealed record SystemCost(double Development, double CodeReview, double Rework, double Testing, double DebtRepayment)
{
    public double Total => Development + CodeReview + Rework + Testing + DebtRepayment;

    internal static bool HasExactHistory(DailySnapshot day, SessionConfiguration configuration) =>
        // Missing debt observations cannot establish whether system work occurred.
        day.Debt is not null && (day.ConsumedCapacity is not null || configuration.Productivity == new StageProductivity());

    internal static SystemCost? Sum(IReadOnlyList<DailySnapshot> days, SimulationSession session)
    {
        if (days.Count == 0 || days.Any(d => !HasExactHistory(d,
            session.Changes.LastOrDefault(c => c.Day <= d.Day)?.After ?? session.InitialConfiguration))) return null;
        return new(days.Sum(d => d.UsedDevelopmentCapacity), days.Sum(d => d.UsedReviewCapacity),
            days.Sum(d => d.UsedReworkDeveloperCapacity), days.Sum(d => d.UsedTesterCapacity),
            days.Sum(d => d.UsedDebtRepaymentCapacity));
    }
}
