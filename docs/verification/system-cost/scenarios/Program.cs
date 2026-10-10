using System.Globalization;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelValidation;
using Simulation.Application;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
var runs=Scenarios.Run();
var fingerprints=new[]{runs.Baseline,runs.Productivity,runs.DebtTimeline}.Select(r=>PreSkillsFingerprint(r.Session)).ToArray();
var old=JsonDocument.Parse(File.ReadAllText("docs/verification/model-validation/audit.json")).RootElement.GetProperty("Fingerprints").EnumerateArray().Select(v=>v.GetString()).ToArray();
if(!fingerprints.SequenceEqual(old))throw new Exception("Pre-feature state fingerprint mismatch");
foreach(var row in new[]{("Baseline",runs.Baseline.Session,151,200),("Development 2x",runs.Productivity.Session,151,200),("Repayment 25%",runs.DebtTimeline.Session,401,450)})
{
    var p=LivePerformance.Period(row.Item2,row.Item3,row.Item4);
    Console.WriteLine(JsonSerializer.Serialize(new{Scenario=row.Item1,p.FirstDay,p.LastDay,p.Completed,p.DeliveryCostPerDoneItem,p.SystemCostPerDoneItem,p.ConsumedSystemCapacity}));
}
Console.WriteLine("PASS: all three full simulation states exactly match archived pre-System-Cost fingerprints.");

// Archive was captured before model 0.6. Remove only Skills metadata from a default-Skills run.
static string PreSkillsFingerprint(Simulation.Core.SimulationSession session)
{
    if (session.Configuration.Skills != new Simulation.Core.SkillSettings()) throw new Exception("Expected default Skills");
    var node=JsonNode.Parse(JsonSerializer.Serialize(session.Capture()))!;
    Simulation.Verification.LegacyReleaseObservation.Normalize(node);
    Remove(node);
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node.ToJsonString())));
    static void Remove(JsonNode node)
    {
        if(node is JsonObject obj) {
            foreach(var key in new[]{"Skills","RequiresSpecialist","SkillMarker","SkillRandomState","SpecialistWorkWaiting"})obj.Remove(key);
            foreach(var child in obj.Select(p=>p.Value).OfType<JsonNode>())Remove(child);
        } else if(node is JsonArray array)foreach(var child in array.OfType<JsonNode>())Remove(child);
    }
}
