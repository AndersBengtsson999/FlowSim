namespace Simulation.Core;

public enum LaunchDecisionPolicy { Interactive, Automatic }
public enum LaunchPlanUnit { WorkingDays, Sprints }
public sealed record ProductLaunchSettings(string Name = "Version 1.0", int SprintLength = 10, int? After = null, LaunchPlanUnit Unit = LaunchPlanUnit.WorkingDays)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || SprintLength < 1 || SprintLength > 3650 || After is <= 0 || !Enum.IsDefined(Unit))
            throw new ScenarioValidationException("Version name, positive launch duration and sprint length (1–3650 working days) are required.");
        if (After is { } amount) _ = TargetDay(0, amount, Unit, SprintLength);
    }
    public static int TargetDay(int currentDay, int amount, LaunchPlanUnit unit, int sprintLength)
    {
        if (currentDay < 0 || amount <= 0 || sprintLength < 1 || sprintLength > 3650 || !Enum.IsDefined(unit)) throw new ScenarioValidationException("Launch delay and sprint length must be positive whole numbers.");
        var target = (long)currentDay + (long)amount * (unit == LaunchPlanUnit.Sprints ? sprintLength : 1);
        if (target > int.MaxValue) throw new ScenarioValidationException("Launch day exceeds the supported range.");
        return (int)target;
    }
}
public sealed record LaunchPlanChange(int DecisionDay, int? PreviousDay, int TargetDay, int Amount, LaunchPlanUnit Unit, int SprintLength);
public sealed record ProductVersion(string Id, string Name, int StartDay, int? OriginalPlannedDay, int? PlannedDay, int? ActualLaunchDay, IReadOnlyList<LaunchPlanChange> Plans)
{
    public string Status => ActualLaunchDay.HasValue ? "Customer launched" : "Active";
}
public sealed record InternalRelease(string Id, string VersionId, int Day, IReadOnlyList<string> ItemIds);
public sealed record CustomerLaunch(string Id, string VersionId, int Day, IReadOnlyList<string> ItemIds);
public sealed record LaunchDecision(string VersionId, int Day);
public sealed record ProductDeliveryState(IReadOnlyList<ProductVersion> Versions, IReadOnlyList<InternalRelease> InternalReleases, IReadOnlyList<CustomerLaunch> CustomerLaunches, LaunchDecision? Pending, bool ImportedLegacy = false);

/// <summary>Day-boundary delivery ledger, independent of workflow capacity and random streams.</summary>
internal sealed class ProductDeliveryLedger
{
    private readonly List<ProductVersion> versions = [];
    private readonly List<InternalRelease> releases = [];
    private readonly List<CustomerLaunch> launches = [];
    private bool imported;
    public LaunchDecision? Pending { get; private set; }
    public ProductVersion Active => versions[^1];
    public ProductDeliveryLedger(ProductLaunchSettings settings)
    {
        settings.Validate(); versions.Add(new("V1", settings.Name.Trim(), 0, null, null, null, []));
        if (settings.After is { } amount) Plan(0, amount, settings.Unit, settings.SprintLength);
    }
    public ProductDeliveryLedger(ProductDeliveryState state, int day, IReadOnlyList<WorkItem> items)
    {
        Validate(state, day, items);
        versions.AddRange(state.Versions.Select(v => v with { Plans = Array.AsReadOnly(v.Plans.ToArray()) }));
        releases.AddRange(state.InternalReleases.Select(r => r with { ItemIds = Array.AsReadOnly(r.ItemIds.ToArray()) }));
        launches.AddRange(state.CustomerLaunches.Select(l => l with { ItemIds = Array.AsReadOnly(l.ItemIds.ToArray()) }));
        Pending = state.Pending; imported = state.ImportedLegacy;
    }
    public static ProductDeliveryLedger Import(IReadOnlyList<WorkItem> items)
    {
        var ledger = new ProductDeliveryLedger(new("Imported Version 1.0")) { imported = true };
        foreach (var group in items.Where(w => w.State.IsDelivered() && w.DoneDay.HasValue).GroupBy(w => w.DoneDay!.Value).OrderBy(g => g.Key))
            ledger.ObserveRelease(group.Key, group.ToArray());
        return ledger;
    }
    public void ObserveRelease(int day, IReadOnlyList<WorkItem> released)
    {
        if (released.Count == 0) return;
        releases.Add(new($"IR-{day}", Active.Id, day, Array.AsReadOnly(released.Select(w => w.Id).ToArray())));
    }
    public void Boundary(int day, LaunchDecisionPolicy policy)
    {
        if (Active.PlannedDay is not { } target || day < target) return;
        Pending = new(Active.Id, target);
        if (policy == LaunchDecisionPolicy.Automatic) Launch(Active.Id, day);
    }
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ScenarioValidationException("Version name must contain 1–100 characters.");
        versions[^1] = Active with { Name = name.Trim() };
    }
    public void Plan(int day, int amount, LaunchPlanUnit unit, int sprintLength)
    {
        var target = ProductLaunchSettings.TargetDay(day, amount, unit, sprintLength);
        var revision = new LaunchPlanChange(day, Active.PlannedDay, target, amount, unit, sprintLength);
        versions[^1] = Active with { OriginalPlannedDay = Active.OriginalPlannedDay ?? target, PlannedDay = target, Plans = Array.AsReadOnly(Active.Plans.Append(revision).ToArray()) };
        Pending = null;
    }
    public CustomerLaunch Launch(string versionId, int day)
    {
        var existing = launches.SingleOrDefault(l => l.VersionId == versionId);
        if (existing is not null) return existing; // Idempotent decision retry, never launches the successor.
        if (versionId != Active.Id) throw new ScenarioValidationException("The selected product version is no longer active.");
        var ids = releases.Where(r => r.VersionId == versionId && r.Day <= day).SelectMany(r => r.ItemIds).ToArray();
        if (ids.Length == 0 && Pending is null) throw new ScenarioValidationException("No internally released work is available for a manual customer launch.");
        var launch = new CustomerLaunch($"CL-{versions.Count}", versionId, day, Array.AsReadOnly(ids));
        launches.Add(launch); versions[^1] = Active with { ActualLaunchDay = day }; Pending = null;
        var number = versions.Count + 1;
        var nameNumber = number;
        while (versions.Any(v => v.Name == $"Version {nameNumber}.0")) nameNumber++;
        versions.Add(new($"V{number}", $"Version {nameNumber}.0", day, null, null, null, []));
        return launch;
    }
    public ProductDeliveryState Capture() => new(Array.AsReadOnly(versions.ToArray()), Array.AsReadOnly(releases.ToArray()), Array.AsReadOnly(launches.ToArray()), Pending, imported);
    private static void Validate(ProductDeliveryState s, int day, IReadOnlyList<WorkItem> items)
    {
        void Check(bool condition) { if (!condition) throw new ScenarioValidationException("Invalid product version / customer launch history."); }
        Check(s.Versions is { Count: > 0 } && s.InternalReleases is not null && s.CustomerLaunches is not null);
        var versions = s.Versions!; var releases = s.InternalReleases!; var launches = s.CustomerLaunches!;
        Check(versions.Select(v => v.Id).Distinct().Count() == versions.Count && versions.Count(v => v.ActualLaunchDay is null) == 1 && versions[^1].ActualLaunchDay is null);
        for (var i = 0; i < versions.Count; i++)
        {
            var v = versions[i]; Check(v.Id == $"V{i + 1}" && !string.IsNullOrWhiteSpace(v.Name) && v.Name.Length <= 100 && v.StartDay >= 0 && v.StartDay <= day && v.Plans is not null);
            Check(i == 0 ? v.StartDay == 0 : v.StartDay == versions[i - 1].ActualLaunchDay);
            int? previous = null; var priorDecision = v.StartDay;
            foreach (var plan in v.Plans!)
            {
                Check(plan.DecisionDay >= priorDecision && plan.DecisionDay <= (v.ActualLaunchDay ?? day) && plan.PreviousDay == previous && plan.TargetDay == ProductLaunchSettings.TargetDay(plan.DecisionDay, plan.Amount, plan.Unit, plan.SprintLength));
                previous = plan.TargetDay; priorDecision = plan.DecisionDay;
            }
            Check(v.PlannedDay == previous && v.OriginalPlannedDay == v.Plans.FirstOrDefault()?.TargetDay);
            Check(v.ActualLaunchDay is null || v.ActualLaunchDay >= v.StartDay && v.ActualLaunchDay <= day);
        }
        var byId = items.ToDictionary(w => w.Id); var assigned = new HashSet<string>();
        Check(releases.Select(r => r.Id).Distinct().Count() == releases.Count);
        foreach (var r in releases)
        {
            var v = versions.SingleOrDefault(v => v.Id == r.VersionId);
            Check(v is not null && r.Id == $"IR-{r.Day}" && r.Day >= v.StartDay && r.Day <= (v.ActualLaunchDay ?? day) && r.ItemIds is { Count: > 0 });
            foreach (var id in r.ItemIds!) Check(assigned.Add(id) && byId.TryGetValue(id, out var w) && w.State.IsDelivered() && w.DoneDay == r.Day);
        }
        Check(items.Where(w => w.State.IsDelivered() && w.DoneDay.HasValue).All(w => assigned.Contains(w.Id)));
        Check(launches.Select(l => l.VersionId).Distinct().Count() == launches.Count);
        foreach (var v in versions)
        {
            var l = launches.SingleOrDefault(l => l.VersionId == v.Id);
            Check(v.ActualLaunchDay is null ? l is null : l is not null && l.Day == v.ActualLaunchDay);
            if (l is null) continue;
            var expected = releases.Where(r => r.VersionId == v.Id).SelectMany(r => r.ItemIds).ToHashSet();
            Check(l.Id == $"CL-{Array.IndexOf(versions.ToArray(), v) + 1}" && l.ItemIds.Distinct().Count() == l.ItemIds.Count && expected.SetEquals(l.ItemIds));
        }
        Check(launches.All(l => versions.Any(v => v.Id == l.VersionId)));
        var active = versions[^1];
        Check(active.PlannedDay is null || active.PlannedDay >= day);
        Check(s.Pending is null ? active.PlannedDay != day : s.Pending.VersionId == active.Id && s.Pending.Day == day && active.PlannedDay == day);
    }
}
