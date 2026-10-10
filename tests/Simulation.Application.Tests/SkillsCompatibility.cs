using System.Text.Json.Nodes;
namespace Simulation.Application.Tests;
internal static class SkillsCompatibility
{
    // Only the new v0.6 metadata is removed; every pre-existing observation remains compared.
    public static void RemoveNewFields(JsonNode node)
    {
        Simulation.Verification.LegacyReleaseObservation.Normalize(node);
        if(node is JsonObject obj)
        {
            foreach(var key in new[]{"Skills","RequiresSpecialist","SkillMarker","SkillRandomState","SpecialistWorkWaiting"}) obj.Remove(key);
            foreach(var child in obj.Select(p=>p.Value).OfType<JsonNode>())RemoveNewFields(child);
        }
        else if(node is JsonArray arr) foreach(var child in arr.OfType<JsonNode>())RemoveNewFields(child);
    }
}
