using System.Text.Json;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.Infrastructure;

/// <summary>Versioned Live document. Daily item observations are delta encoded, never simulated on load.</summary>
public static class LiveSessionJson
{
    private static readonly JsonSerializerOptions Options = ExperimentJson.CreateOptions();
    public sealed record StoredDay(DailySnapshot Summary, IReadOnlyList<WorkItemDaySnapshot> ChangedItems);
    public sealed record StoredState(SimulationSessionState Header, IReadOnlyList<StoredDay> History);
    public sealed record StoredCheckpoint(Guid Id, string Label, StoredState State);
    public sealed record StoredDocument(int SchemaVersion, string SimulationModelVersion, string DocumentKind,
        StoredState State, IReadOnlyList<StoredCheckpoint> Checkpoints, int SafetyLimit, int RollingWindow);

    public static string Save(LiveSimulation live)
    {
        var d = live.Capture();
        return JsonSerializer.Serialize(new StoredDocument(d.SchemaVersion, d.SimulationModelVersion, "LiveSession", Pack(d.State),
            d.Checkpoints.Select(c => new StoredCheckpoint(c.Id, c.Label, Pack(c.State))).ToArray(), d.SafetyLimit, d.RollingWindow), Options);
    }
    public static LiveSimulation Load(string json)
    {
        var d = JsonSerializer.Deserialize<StoredDocument>(json, Options) ?? throw new JsonException("Empty Live session.");
        if (d.DocumentKind != "LiveSession" || d.SchemaVersion != 1 || d.SimulationModelVersion != SimulationModel.Version)
            throw new JsonException("Unsupported Live session document, schema or simulation model version.");
        return LiveSimulation.Restore(new(d.SchemaVersion, d.SimulationModelVersion, Unpack(d.State),
            d.Checkpoints.Select(c => new LiveCheckpoint(c.Id, c.Label, Unpack(c.State))).ToArray(), d.SafetyLimit, d.RollingWindow));
    }
    private static StoredState Pack(SimulationSessionState state)
    {
        var last = new Dictionary<string, WorkItemDaySnapshot>(StringComparer.Ordinal);
        var history = new List<StoredDay>();
        foreach (var day in state.Days)
        {
            var changed = new List<WorkItemDaySnapshot>();
            foreach (var item in day.Items)
            {
                if (!last.TryGetValue(item.Id, out var prior) || prior != item) changed.Add(item);
                last[item.Id] = item;
            }
            history.Add(new(day with { Items = [] }, changed));
        }
        return new(state with { Days = [] }, history);
    }
    private static SimulationSessionState Unpack(StoredState state)
    {
        if (state.Header.Days.Count != 0 || state.History.Count != state.Header.CurrentDay)
            throw new JsonException("Invalid Live history length.");
        var last = new Dictionary<string, WorkItemDaySnapshot>(StringComparer.Ordinal);
        var order = new List<string>(); var history = new List<DailySnapshot>();
        var ids = state.Header.WorkItems.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var day in state.History)
        {
            if (day.Summary.Items.Count != 0) throw new JsonException("Expected compact daily history.");
            var changedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in day.ChangedItems)
            {
                if (!ids.Contains(item.Id) || !changedIds.Add(item.Id)) throw new JsonException("Invalid Work Item reference in daily history.");
                if (!last.ContainsKey(item.Id)) order.Add(item.Id);
                last[item.Id] = item;
            }
            history.Add(day.Summary with { Items = Array.AsReadOnly(order.Select(id => last[id]).ToArray()) });
        }
        return state.Header with { Days = history };
    }
    public static Task SaveAsync(string path, LiveSimulation live) => ExperimentJson.WriteAsync(path, Save(live));
    public static async Task<LiveSimulation> LoadAsync(string path) => Load(await File.ReadAllTextAsync(path));
}
