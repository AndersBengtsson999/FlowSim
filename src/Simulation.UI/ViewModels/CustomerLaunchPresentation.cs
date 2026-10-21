using System.ComponentModel;
using Simulation.Core;
namespace Simulation.UI.ViewModels;

public sealed record LaunchNotice(string Id, string Version, int Day, int Releases, int Items)
{
    public string Description => $"{Version} · Launched on Day {Day}\n{Releases} internal releases · {Items} work items delivered to customers";
}
/// <summary>Session-local acknowledgement queue. Persistent simulation history is never consumed or modified.</summary>
public sealed class CustomerLaunchPresentation : INotifyPropertyChanged
{
    private readonly HashSet<string> seen = [];
    private readonly Queue<LaunchNotice> pending = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public LaunchNotice? Current => pending.TryPeek(out var value) ? value : null;
    public bool HasNotification => Current is not null;
    public string Description => Current?.Description ?? "";
    public string PendingLabel => pending.Count > 1 ? $"Dismiss · {pending.Count - 1} more launches" : "Dismiss";
    public RelayCommand DismissCommand { get; }
    public CustomerLaunchPresentation() => DismissCommand = new(() => { if (pending.Count > 0) pending.Dequeue(); Notify(); });
    public void Observe(ProductDeliveryState? state, bool historical = false)
    {
        if (historical) pending.Clear();
        if (state is not null) foreach (var launch in state.CustomerLaunches)
        {
            // IDs are stable within a simulation. Rewinding never unacknowledges an ID.
            if (!seen.Add(launch.Id) || historical) continue;
            pending.Enqueue(new(launch.Id, state.Versions.Single(v => v.Id == launch.VersionId).Name, launch.Day,
                state.InternalReleases.Count(r => r.VersionId == launch.VersionId), launch.ItemIds.Count));
        }
        Notify();
    }
    public void Reset() { seen.Clear(); pending.Clear(); Notify(); }
    private void Notify() => PropertyChanged?.Invoke(this, new(null));
}
