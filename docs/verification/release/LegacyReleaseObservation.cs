using System.Text.Json.Nodes;
using Simulation.Core;
namespace Simulation.Verification;

// Verification-only normalization for unconstrained release. Never used to load or rewrite user history.
public static class LegacyReleaseObservation
{
    public static void Normalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj["ResidualDependency"] is not null || obj["ResidualDependencies"] is JsonObject settings && settings["Rate"]!.GetValue<double>() != 0 || obj["WaitingForDependencyCount"] is JsonValue count && count.GetValue<int>() != 0)
                throw new InvalidOperationException("Cannot normalize active residual dependencies.");
            foreach (var key in new[]{"ResidualDependency","ResidualDependencies","DependencyRandomState","WaitingForDependencyCount"}) obj.Remove(key);
            if (State(obj["State"]) == WorkItemStatus.ReadyForRelease)
                throw new InvalidOperationException("Cannot normalize a queued release to old Done semantics.");
            if (obj["ReadyForReleaseDay"] is JsonValue ready && obj["ReleasedDay"] is JsonValue released
                && ready.GetValue<int>() != released.GetValue<int>())
                throw new InvalidOperationException("Cannot normalize nonzero release waiting to old Done semantics.");
            foreach (var key in new[]{"Release","ReadyForReleaseDay","ReleasedDay","WorkCompletedDay","DevelopmentCycleTime","ReleaseWaitTime","ReadyForReleaseCount"})obj.Remove(key);
            foreach (var key in new[]{"State","FinalState","StateDuringDay","From","To","FromState","ToState"})
                if(State(obj[key]) is WorkItemStatus.ReadyForRelease or WorkItemStatus.Released)
                    obj[key] = obj[key] is JsonValue value && value.TryGetValue<string>(out _) ? JsonValue.Create("Done") : JsonValue.Create((int)WorkItemStatus.Done);
            foreach(var child in obj.Select(p=>p.Value).OfType<JsonNode>())Normalize(child);
        }
        else if(node is JsonArray array)
        {
            for(var i=array.Count-1;i>=0;i--)
            {
                if(array[i] is JsonObject item && (IsRelease(item,"From","To") || IsRelease(item,"FromState","ToState")))array.RemoveAt(i);
                else if(array[i] is { } child)Normalize(child);
            }
        }
    }
    static WorkItemStatus? State(JsonNode? node)
    {
        if(node is not JsonValue value)return null;
        if(value.TryGetValue<int>(out var number))return (WorkItemStatus)number;
        return value.TryGetValue<string>(out var text) && Enum.TryParse<WorkItemStatus>(text,true,out var state) ? state : null;
    }
    static bool IsRelease(JsonObject obj,string from,string to) => State(obj[from]) == WorkItemStatus.ReadyForRelease && State(obj[to]) == WorkItemStatus.Released;
}
