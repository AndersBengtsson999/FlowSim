namespace Simulation.Core;

public sealed record TechnicalDebtSettings
{
    public double ShortcutRate { get; init; }
    public double ShortcutEffortReduction { get; init; } = .3;
    public double Tolerance { get; init; } = .1;
    public double Repayment { get; init; }
    public double CreationFactor { get; init; } = 1;
    public double ImpactFactor { get; init; } = 1;
    public void Validate()
    {
        if (new[] { ShortcutRate, ShortcutEffortReduction, Tolerance, Repayment }.Any(v => !double.IsFinite(v) || v < 0 || v > 1)
            || new[] { CreationFactor, ImpactFactor }.Any(v => !double.IsFinite(v) || v < 0))
            throw new ScenarioValidationException("Debt percentages must be between 0% and 100%; debt factors must be finite and nonnegative.");
    }
}

public sealed record TechnicalDebtState(double Amount = 0, double CumulativeDevelopmentScope = 0)
{
    public double Ratio => CumulativeDevelopmentScope == 0 ? 0 : Amount / CumulativeDevelopmentScope;
    public double Overhead(TechnicalDebtSettings settings)
    {
        var value = Math.Max(0, Ratio - settings.Tolerance) * settings.ImpactFactor;
        if (!double.IsFinite(value)) throw new ScenarioValidationException("Debt overhead exceeds the finite numeric range.");
        return value;
    }
    public void Validate()
    {
        if (!double.IsFinite(Amount) || Amount < 0 || !double.IsFinite(CumulativeDevelopmentScope) || CumulativeDevelopmentScope < 0 || !double.IsFinite(Ratio))
            throw new ScenarioValidationException("Technical Debt and developed scope must be finite and nonnegative, with a finite ratio.");
    }
}

// Immutable start-time decision. Base effort stays independent of overhead, shortcuts and productivity.
public sealed record DevelopmentPlan(double BaseEffort, double DebtRatioAtStart, double Overhead,
    double EffortWithDebt, bool IsShortcut, double FinalEffort, double SavedEffort, double DebtToCreate);

public sealed record DebtObservation(TechnicalDebtState State, double Tolerance, double Overhead,
    double Created = 0, double Repaid = 0, double RepaymentCapacity = 0);

/// <summary>System-level debt only. Uses the session's existing random source, never owns a generator.</summary>
internal sealed class TechnicalDebtLedger(TechnicalDebtState state)
{
    public TechnicalDebtState State { get; private set; } = state;
    public double CreatedToday { get; private set; }
    public void BeginDay() => CreatedToday = 0;
    public DevelopmentPlan? Plan(double baseEffort, TechnicalDebtSettings settings, IRandomSource random)
    {
        var overhead = State.Overhead(settings);
        // Preserve old random consumption and item metadata when debt behavior has no effect.
        if (settings.ShortcutRate == 0 && overhead == 0) return null;
        var shortcut = settings.ShortcutRate == 1 || (settings.ShortcutRate > 0 && random.NextUnitDouble() < settings.ShortcutRate);
        var withDebt = baseEffort * (1 + overhead);
        var final = shortcut ? withDebt * (1 - settings.ShortcutEffortReduction) : withDebt;
        var saved = withDebt - final;
        var created = saved * settings.CreationFactor;
        if (new[] { overhead, withDebt, final, saved, created }.Any(v => !double.IsFinite(v)))
            throw new ScenarioValidationException("Technical Debt effort calculation exceeds the finite numeric range.");
        return new(baseEffort, State.Ratio, overhead, withDebt, shortcut, final, saved, created);
    }
    public void Complete(WorkItem item)
    {
        var created = item.DevelopmentPlan?.DebtToCreate ?? 0;
        var next = new TechnicalDebtState(State.Amount + created, State.CumulativeDevelopmentScope + item.DevelopmentEffort);
        next.Validate(); State = next; CreatedToday += created;
    }
    public (double Capacity, double Work) Repay(double remainingPool, TechnicalDebtSettings settings, double productivity)
    {
        var required = State.Amount / productivity;
        var capacity = Math.Min(remainingPool * settings.Repayment, required);
        // If the required fractional capacity fits, finish exactly rather than retaining rounding residue.
        var work = capacity > 0 && capacity == required ? State.Amount : Math.Min(State.Amount, capacity * productivity);
        State = State with { Amount = Math.Max(0, State.Amount - work) };
        return (capacity, work);
    }
}
