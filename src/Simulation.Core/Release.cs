namespace Simulation.Core;

public enum ReleaseMode { FlowBased, Scheduled }
public sealed record ReleaseSettings(ReleaseMode Mode = ReleaseMode.FlowBased, int Capacity = int.MaxValue, int Interval = 5)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || Capacity < 0 || Interval < 1)
            throw new ScenarioValidationException("Release mode must be valid, capacity a nonnegative integer, and interval a positive integer.");
    }
    public bool IsOpportunity(int day) => Mode == ReleaseMode.FlowBased || day % Interval == 0;
    public static string FormatCapacity(int capacity) => capacity == int.MaxValue ? "Unlimited" : capacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Description => Mode == ReleaseMode.FlowBased ? $"Flow-based · {FormatCapacity(Capacity)} items/day" : $"Scheduled · every {Interval} days · {FormatCapacity(Capacity)} items/release";
}

public static class DeliveryState
{
    public static bool IsDelivered(this WorkItemStatus state) => state is WorkItemStatus.Done or WorkItemStatus.Released;
    public static bool IsWorkComplete(this WorkItemStatus state) => state.IsDelivered() || state == WorkItemStatus.ReadyForRelease;
}
