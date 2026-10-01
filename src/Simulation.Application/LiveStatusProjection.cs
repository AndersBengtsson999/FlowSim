using Simulation.Core;

namespace Simulation.Application;

public sealed record LiveStatusSnapshot(int Day, int Done, int Wip, double? DeveloperUsed, double? DeveloperAvailable,
    double? DeveloperUtilization, double? TesterUsed, double? TesterAvailable, double? TesterUtilization,
    int ReviewQueue, int TestingQueue, int ReworkQueue, string WorkSupply);

/// <summary>Latest completed-day observations; delivery/trends are supplied by existing LivePerformance.</summary>
public static class LiveStatusProjection
{
    public static string Supply(SessionConfiguration c) => c.ArrivalMode switch {
        WorkArrivalMode.AlwaysAvailable => "Always available", WorkArrivalMode.FixedBacklog => "Fixed backlog",
        _ => $"Fixed rate · {c.WorkItemsPerDay:0.###} items/day"
    };
    public static LiveStatusSnapshot From(SimulationSession session)
    {
        var d = session.Days.LastOrDefault();
        static double? Ratio(double? used, double? available) => available > 0 ? used / available : null;
        return new(session.CurrentDay, d?.DoneCount ?? 0, d?.TotalWip ?? 0,
            d?.UsedDeveloperCapacity, d?.AvailableDeveloperCapacity, Ratio(d?.UsedDeveloperCapacity, d?.AvailableDeveloperCapacity),
            d?.UsedTesterCapacity, d?.AvailableTesterCapacity, Ratio(d?.UsedTesterCapacity, d?.AvailableTesterCapacity),
            d?.WaitingForCodeReviewCount ?? 0, d?.WaitingForTestingCount ?? 0, d?.WaitingForReworkCount ?? 0, Supply(session.Configuration));
    }
}
