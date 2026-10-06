namespace Simulation.Core;

/// <summary>One Development capability; Specialists are a subset of the total developer count.</summary>
public sealed record SkillSettings(int Specialists = 0, double SpecialistWorkRate = 0)
{
    public void Validate(Team team)
    {
        if (Specialists < 0 || Specialists > team.DeveloperCount)
            throw new ScenarioValidationException("Specialists must be between 0 and Developers.");
        if (!double.IsFinite(SpecialistWorkRate) || SpecialistWorkRate < 0 || SpecialistWorkRate > 1)
            throw new ScenarioValidationException("Specialist Work Rate must be between 0% and 100%.");
    }
}
