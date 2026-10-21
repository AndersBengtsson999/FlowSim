using Simulation.Core;
namespace Simulation.UI.ViewModels;
public static class LaunchEconomicsText
{
    public static string Describe(LaunchCostHistory history)
    {
        var c = history.Current;
        string Sek(decimal n) => $"{n:N0} SEK";
        return $"Estimated allocated cost · WIP: {Sek(c.WorkInProgress)} · Internally released, awaiting launch: {Sek(c.AwaitingLaunch)}\n" +
            $"Total unlaunched: {Sek(c.Unlaunched)} · Customer-launched: {Sek(c.CustomerLaunched)}\n" +
            $"Unallocated: {Sek(c.Unallocated)} · Recorded incurred staffing cost: {Sek(c.Incurred)}\n" +
            "Allocation, not additional expenditure. Raw item capacity × daily blended developer/specialist or tester rate. Idle time, debt repayment and missing capacity remain unallocated. Postponing itself costs nothing.\n" +
            $"History: {history.MissingCostDays} days without recorded monetary cost; {history.MissingCapacityDays} days with missing item capacity. Missing costs are excluded, never invented.";
    }
}
