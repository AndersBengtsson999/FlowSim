namespace Simulation.UI.ViewModels;
public sealed partial class CompareViewModel
{
    public LiveViewModel? GuidedLive { get; private set; }
    public bool HasGuided => GuidedLive is not null;
    public RelayCommand CloseGuidedCommand => new(() => { GuidedLive = null; Changed(nameof(GuidedLive)); Changed(nameof(HasGuided)); });
    public void ShowGuided(LiveViewModel live) { GuidedLive = live; Changed(nameof(GuidedLive)); Changed(nameof(HasGuided)); }
}
