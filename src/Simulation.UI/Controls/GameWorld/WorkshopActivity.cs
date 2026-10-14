using Simulation.Sprites;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls.GameWorld;

/// <summary>Illustrative, bounded capacity signals. These slots are scenery, not tracked employees.</summary>
public sealed record WorkshopActivity(IReadOnlyList<SpritePose> Developers, IReadOnlyList<SpritePose> Testers,
    bool DevelopmentObserved, bool ReviewObserved, bool TestingObserved, bool RepaymentObserved)
{
    public bool UnpicturedMaintenance { get; init; }
    public int BusyDevelopers => Developers.Count(p => p != SpritePose.Idle) + (UnpicturedMaintenance ? 1 : 0);
    public int BusyTesters => Testers.Count(p => p != SpritePose.Idle);
    public static WorkshopActivity From(GameSceneState state)
    {
        var developers = Enumerable.Repeat(SpritePose.Idle, Math.Min(SpriteWorkshop.MaximumDevelopers, state.Developers)).ToArray();
        var testers = Enumerable.Repeat(SpritePose.Idle, Math.Min(SpriteWorkshop.MaximumTesters, state.Testers)).ToArray();
        var observed = state.HasSession && state.Day > 0;
        var development = observed && state.ActiveDevelopment + state.ActiveRework > 0 && state.UsedDevelopmentCapacity + state.UsedReworkCapacity > 0;
        var review = observed && state.Areas[2].Active > 0 && state.UsedReviewCapacity > 0;
        var testing = observed && state.Areas[3].Active > 0 && state.UsedTesterCapacity > 0;
        var budget = Math.Min(developers.Length, Math.Max(0, state.DeveloperPoseCeiling));
        // Reserve observed maintenance in the SAME visual budget. Small teams use the diagram
        // as a worker-equivalent rather than adding a fifth/fictional person or moving a seat.
        var repayment = observed && state.UsedDebtRepaymentCapacity > 0 && state.DeveloperPoseCeiling > 0;
        var unpicturedMaintenance = repayment && budget > 0 && developers.Length < 4;
        if (repayment && budget > 0)
        {
            if (developers.Length >= 4) developers[3] = SpritePose.Maintaining;
            budget--;
        }
        // Remaining slots illustrate primary work, review, then an optional partner.
        if (development && budget > 0) { developers[0] = SpritePose.Working; budget--; }
        if (review && budget > 0)
        {
            if (developers.Length >= 3) { developers[2] = SpritePose.Reviewing; budget--; }
            else if (developers[0] == SpritePose.Idle) { developers[0] = SpritePose.Reviewing; budget--; }
        }
        if (budget > 0 && developers.Length >= 2 && developers[0] == SpritePose.Working && state.CollaborationCapacity > 0)
        {
            developers[0] = developers[1] = SpritePose.Collaborating;
        }
        if (testing && testers.Length > 0 && state.TesterPoseCeiling > 0) testers[0] = SpritePose.Testing;
        return new(developers, testers, development, review, testing,
            repayment) { UnpicturedMaintenance = unpicturedMaintenance };
    }
}
