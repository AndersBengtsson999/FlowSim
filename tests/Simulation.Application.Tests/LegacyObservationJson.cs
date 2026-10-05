using System.Text.Json;
using Simulation.Core;
namespace Simulation.Application.Tests;
// New cost and debt history is independently tested; every previously existing result field is compared exactly.
internal static class LegacyObservationJson
{
    public static string Serialize(SimulationResult result) => JsonSerializer.Serialize(result with
        { WorkItems = result.WorkItems.Select(w => w with { DeliveryCost = null }).ToArray(),
          Days = result.Days.Select(d => d with { Debt = null, Items = d.Items.Select(w => w with { DeliveryCost = null }).ToArray() }).ToArray() });
}
