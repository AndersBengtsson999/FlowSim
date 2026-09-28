using Simulation.Core;

namespace Simulation.Application;

/// <summary>In-memory editing and result lifecycle. Configuration records and execution snapshots stay separate.</summary>
public sealed class ExperimentSession
{
    private readonly Dictionary<Guid, ScenarioRun> results = [];
    private readonly HashSet<Guid> drafts = [];
    public Experiment Experiment { get; private set; }
    public IReadOnlyList<ScenarioRun> Results => Array.AsReadOnly(results.Values.ToArray());
    public ExperimentSession(Experiment? experiment = null)
    {
        var initial = experiment ?? Experiment.Baseline(); initial.Validate();
        Experiment = initial with { Scenarios = Array.AsReadOnly(initial.Scenarios.ToArray()) };
    }
    public ScenarioDefinition Add(SimulationRequest? configuration = null)
    {
        var scenario = ScenarioDefinition.Create(configuration ?? BaselineScenario.CreateRequest() with { Name = "New Scenario" });
        Replace(Experiment with { Scenarios = Array.AsReadOnly(Experiment.Scenarios.Append(scenario).ToArray()) }); return scenario;
    }
    public ScenarioDefinition Duplicate(Guid id) => Add(Find(id).Configuration with { Name = Find(id).Name + " Copy" });
    public ScenarioDefinition Find(Guid id) => Experiment.Scenarios.Single(s => s.Id == id);
    public void Update(Guid id, SimulationRequest configuration)
    {
        Find(id);
        Replace(Experiment with { Scenarios = Array.AsReadOnly(Experiment.Scenarios.Select(s => s.Id == id ? s with { Configuration = configuration } : s).ToArray()) });
        drafts.Remove(id);
    }
    public void Rename(Guid id, string name) => Update(id, Find(id).Configuration with { Name = name });
    public void Reset(Guid id) => Update(id, BaselineScenario.CreateRequest() with { Name = Find(id).Name });
    public void Delete(Guid id)
    {
        Find(id); var remaining = Experiment.Scenarios.Where(s => s.Id != id).ToArray();
        Replace(Experiment with { Scenarios = Array.AsReadOnly(remaining), BaselineId = Experiment.BaselineId == id && remaining.Length > 0 ? remaining[0].Id : Experiment.BaselineId });
        results.Remove(id); drafts.Remove(id);
    }
    public void SetBaseline(Guid id) { Find(id); Replace(Experiment with { BaselineId = id }); }
    public void SetOptions(ComparisonOptions options) => Replace(Experiment with { Options = options });
    public void SetDetails(string name, string description) => Replace(Experiment with { Name = name, Description = description });
    public void BeginEdit(Guid id) { Find(id); drafts.Add(id); }
    public void CancelEdit(Guid id) => drafts.Remove(id);
    public string Status(Guid id) => !results.TryGetValue(id, out var run) ? (drafts.Contains(id) ? "Editing draft" : "Not Run")
        : drafts.Contains(id) || run.IsOutOfDate(Find(id), Experiment.Options) ? "Out of Date" : "Current";
    public void Store(ScenarioRun run) { Find(run.ScenarioSnapshot.Id); results[run.ScenarioSnapshot.Id] = run; }
    public void StoreAll(IReadOnlyList<ScenarioRun> runs) { foreach (var r in runs) Find(r.ScenarioSnapshot.Id); foreach (var r in runs) Store(r); }
    public ScenarioComparisonResult Compare(IEnumerable<Guid> included)
    {
        var ids = included.Append(Experiment.BaselineId).ToHashSet();
        var scenarios = Experiment.Scenarios.Where(s => ids.Contains(s.Id)).ToArray();
        if (scenarios.Any(s => Status(s.Id) != "Current")) throw new ScenarioValidationException("Run all included scenarios and the comparison baseline. Out of Date or draft results cannot be compared.");
        return new ScenarioComparisonRunner().Compare(Experiment with { Scenarios = Array.AsReadOnly(scenarios) }, scenarios.Select(s => results[s.Id]).ToArray());
    }
    private void Replace(Experiment experiment) { experiment.Validate(); Experiment = experiment; }
}
