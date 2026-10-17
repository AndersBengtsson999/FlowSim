namespace Simulation.Core;

/// <summary>Illustrative fully loaded monthly employer costs in SEK, allocated to available staffing.</summary>
public sealed record RoleCostSettings(decimal DeveloperMonthly = 85000m, decimal TesterMonthly = 75000m,
    decimal SpecialistMonthly = 100000m, int WorkingDaysPerMonth = 20)
{
    public void Validate()
    {
        if (DeveloperMonthly < 0 || TesterMonthly < 0 || SpecialistMonthly < 0 ||
            DeveloperMonthly > 1_000_000_000m || TesterMonthly > 1_000_000_000m || SpecialistMonthly > 1_000_000_000m)
            throw new ScenarioValidationException("Monthly role costs must be between 0 and 1,000,000,000 SEK.");
        if (WorkingDaysPerMonth is < 1 or > 31)
            throw new ScenarioValidationException("Working days per month must be between 1 and 31.");
    }
    public DailyTeamCost Staffing(Team team, SkillSettings skills)
    {
        var developers = (team.DeveloperCount - skills.Specialists) * (decimal)team.DeveloperAvailability;
        var specialists = skills.Specialists * (decimal)team.DeveloperAvailability;
        var testers = team.TesterCount * (decimal)team.TesterAvailability;
        return new(this, developers * DeveloperMonthly / WorkingDaysPerMonth,
            specialists * SpecialistMonthly / WorkingDaysPerMonth, testers * TesterMonthly / WorkingDaysPerMonth, 0m);
    }
}

/// <summary>Immutable end-of-day economic observation. Rework is an allocation of the developer pool's
/// actual daily staffing cost using observed raw capacity, not an extra charge or employee assignment.</summary>
public sealed record DailyTeamCost(RoleCostSettings Rates, decimal Developers, decimal Specialists, decimal Testers, decimal Rework)
{
    public decimal Total => Developers + Specialists + Testers;
    public static DailyTeamCost Observe(SessionConfiguration configuration, DailySnapshot day)
    {
        var cost = configuration.RoleCosts.Staffing(configuration.Team, configuration.Skills);
        var fraction = day.AvailableDeveloperCapacity > 0 ? Math.Clamp(day.UsedReworkDeveloperCapacity / day.AvailableDeveloperCapacity, 0, 1) : 0;
        return cost with { Rework = (cost.Developers + cost.Specialists) * (decimal)fraction };
    }
    public void Validate()
    {
        if (Rates is null) throw new ScenarioValidationException("Economic history needs cost rates.");
        Rates.Validate();
        if (Developers < 0 || Specialists < 0 || Testers < 0 || Rework < 0 || Rework > Developers + Specialists)
            throw new ScenarioValidationException("Invalid economic history.");
    }
}
