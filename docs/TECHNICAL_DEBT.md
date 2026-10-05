# Technical Debt v1 — model 0.5

Technical Debt is system-level accumulated deferred work created only by explicit shortcut implementations. This is a simulation abstraction, not a claim that real technical debt can be measured precisely from Development effort. Shortcut, tolerance and impact settings are scenario assumptions, not recommendations. High utilization, WIP, productivity, team size, queues, Always Available supply and AI labels do not create debt.

## Configuration and defaults

| Setting | Default | Meaning |
| --- | --- | --- |
| Shortcut Rate | 0% | Once-only probability at Development admission |
| Shortcut Effort Reduction | 30% | Fraction of effort including overhead avoided by a shortcut |
| Debt Tolerance | 10% | Ratio at or below which overhead is zero |
| Debt Repayment | 0% | Maximum fraction of the developer pool remaining after Review/Rework devoted to debt |
| Debt Creation Factor | 1 | Debt per saved effort unit; Advanced configuration |
| Debt Impact Factor | 1 | Overhead per unit of excess ratio; internal configuration |

The four normal percentage controls accept finite values from 0% to 100%. Factors accept finite nonnegative values and are preserved on load/edit/save. The construction defaults of 30% and 10% do not imply recommended shortcut or tolerance levels. No master switch is required. With Shortcut Rate zero and no existing debt, the feature has no effect on work. With no debt, even a nonzero repayment percentage reserves no capacity.

## State, formulas and scope

State contains absolute debt in effort-like units and cumulative original Development scope. Scope increases exactly once, when an item completes its original Development stage, by its original base Development effort. It excludes overhead, shortcut savings, productivity, collaboration and consumed capacity. Rework returns to Review and never adds scope again.

```text
DebtRatio = Debt / CumulativeDevelopmentScope
DebtRatio = 0 when CumulativeDevelopmentScope = 0
EffectiveDebtRatio = max(0, DebtRatio - DebtTolerance)
DebtOverhead = EffectiveDebtRatio × DebtImpactFactor
EffortWithDebt = BaseDevelopmentEffort × (1 + DebtOverhead)
FinalShortcutEffort = EffortWithDebt × (1 - ShortcutEffortReduction)
ShortcutEffortSaved = EffortWithDebt - FinalShortcutEffort
DebtCreated = ShortcutEffortSaved × DebtCreationFactor
```

A normal implementation uses EffortWithDebt, saves zero effort and creates zero debt. Original base effort stays available independently of its immutable Development plan. The plan records the start-time ratio, overhead, effort before/after shortcut, selection, savings and debt to create. A null plan represents unchanged normal effort when no shortcut or overhead applies.

For debt 20 and scope 200, ratio is 10%; with scope 500 it is 4%. At tolerance 10%, ratios 5%, 10%, 15% and 25% produce overhead 0%, 0%, 5% and 15% when impact is 1. Base effort 5 with ratio 20% and tolerance 10% becomes 5.5; a 30% shortcut makes final effort 3.85 and saves 1.65. Completion adds 1.65 debt and **5** scope, not 3.85 or 5.5.

Zero scope defines ratio zero even for a legitimate persisted positive debt amount. Debt, scope, ratio, overhead and planned efforts must remain finite; invalid persisted states/settings and arithmetic overflow are rejected. Debt never becomes negative. Absolute debt changes only through shortcut completion or active repayment. Growing scope can lower the ratio without removing absolute debt. There is no additional interest or automatic decay.

## Lifecycle, timing and determinism

1. At Development admission, read the current debt state and calculate overhead.
2. Make the normal/shortcut decision once; calculate and freeze final effort, savings and debt to create.
3. Process that effort using existing capacity, productivity and Development collaboration.
4. At original Development completion, add original base scope once and add the stored shortcut debt.
5. Later Development admissions see the updated state.

All daily admissions still occur before any work. Thus all starts on one day see the opening debt state. That day's repayment and completions affect subsequent days' admissions, never another same-day admission. An item never receives overhead from its own future debt. Rework, Review and Testing defects do not create debt or change the original plan.

The decision uses the session's **existing arrival random stream**, after that day's arrival processing, in Development FIFO admission order. No new generator is introduced. Rates 0% and 100% do not consume a draw; intermediate rates consume one draw per start. Active choices are never re-rolled. Enabling probabilistic shortcuts can therefore change later draws for variable arrivals/effort on this shared stream; it does not modify the arrival rules. Separate defect/rework streams remain unchanged. The seed, complete random state and plans persist, so identical continuation reproduces exactly.

A Live change recorded on Day N affects processing on Day N+1. Active plans, remaining effort and historical observations are unchanged. This includes frozen creation-factor results. The current bar immediately reflects the newly configured tolerance/overhead applicable to future starts; Day N's historical debt observation retains its original settings. Before/After remains Before Days 81–100 and After Days 101–120 for a Day 100 intervention and window 20.

## Repayment and capacity

Code Review retains first priority and Rework second priority. Let R be the actual remaining developer capacity after both, f the repayment fraction, D the current debt, and p Development Productivity:

```text
ConsumedRepaymentCapacity = min(R × f, D / p)
RepaymentEffectiveWork = min(D, ConsumedRepaymentCapacity × p)
DevelopmentPool = R - ConsumedRepaymentCapacity
```

Repayment finishing the debt sets the remaining amount exactly to zero, avoiding floating-point residue. With R = 5 and f = 20%, at most 1 capacity goes to debt and at least 4 remains for Development. With debt 0.3 and productivity 1.5, only 0.2 capacity is consumed; all other capacity remains available that same day. With zero debt, no capacity is reserved. Debt created later at Development completion becomes available for repayment on subsequent days.

Repayment is system-level work: it has no item, WIP slot, individual developer, per-item contribution cap or collaboration bonus/penalty. Development Productivity applies; Review and Testing productivity do not. Availability still determines the original shared pool. Developer used capacity is the sum of Review, Rework, Debt Work and Development. Utilization remains consumed capacity divided by available capacity; repayment is counted as real consumption, not effective work or free work. Details exposes the four-way breakdown. Testing's separate pool and utilization remain unchanged.

## Live presentation

The compact bar above Flow Board shows ratio, configured tolerance, future-start overhead and repayment percentage. Details includes absolute debt, cumulative scope, latest daily capacity breakdown and debt removed. Flow item details show normal/shortcut assignment, original base, start overhead, final effort and savings when a plan exists. Normal Configuration/Change exposes the four percentages; Debt Creation Factor is separately available under Advanced · Technical Debt, while Debt Impact Factor stays internal. See [the intervention editor](INTERVENTION_EDITOR.md). Latest-intervention descriptions remain factual.

Color is relative to configured tolerance T: green below 70% of T, yellow from 70% through T, red strictly above T. Both green and yellow have zero overhead. Equality at T has zero overhead. These colors are presentation only, without management judgments. With T = 0, zero ratio still means zero overhead and any positive ratio is above tolerance.

The deterministic display maximum is `max(0.01, 2 × T, 1.2 × DebtRatio)` (ratios are fractions), with a finite-range guard. It does not cap ratios at 100%. Zero, tolerance and the current marker remain represented, and textual values accompany colors. The bar is hidden when unused; configured shortcuts/repayment, existing debt or prior debt creation make it relevant. It remains visible after repayment to zero when history contains debt.

The existing selectable Performance Trend supports Technical Debt Ratio, absolute Technical Debt and Debt Overhead. These are actual daily state observations, never rolling averages. Optional Before/After rows explicitly report period-end ratio and overhead when debt is relevant. Existing throughput, cycle time, WIP, queues and utilization aggregation and intervention boundaries remain unchanged. No causal conclusion is generated. Analyze recognizes all six configuration inputs; debt state is not presented as an input parameter.

## Persistence and compatibility

Simulation model version is **0.5**; JSON envelope schema remains 1. Scenario, experiment and Live persistence use existing infrastructure. Session/checkpoint state includes settings, absolute debt, cumulative scope, immutable plans, remaining effort, daily observations, intervention history and random continuation.

Compatible models 0.2, 0.3 and 0.4 load with default debt settings and zero debt. Current cumulative scope is recovered from items whose original Development has completed. Historical debt fields stay absent rather than inventing debt events or rewriting old snapshots; their debt trend value is zero. Legacy active items keep their existing effort. Models 0.2/0.3 also retain their existing productivity-default migration. Model 0.1 remains incompatible because of its earlier allocation model. Original session provenance is retained.

## Verification — 2026-10-02

The solution has **452 passing tests**: 160 Core, 172 Application and 120 UI. This adds 44 cases to the previous 408 (29 Core, 6 Application, 9 UI). Existing enum coverage and version assertions were updated. Release build: zero warnings and zero errors.

A genuine pre-edit model 0.4 executable captured fixed-seed fixtures for Continuous Arrival and Always Available with variable effort, quality, non-unit productivity and availability. Fresh zero-debt runs and continuation from day-30 legacy sessions exactly match the original day-60 results and random states. The comparison excludes only newly introduced daily debt metadata; all previous observations and metrics are compared. Existing model 0.2/0.3 regression fixtures also pass.

New tests cover shortcut endpoints/probability/repeatability, no re-roll, calculation order, completion timing, original scope, no Rework double counting, zero scope, tolerance boundaries, frozen active plans, same-day admission ordering, repayment priority/fractions/productivity/availability/reuse, no collaboration on repayment, utilization, invalid state, scenario persistence/comparison, Live Day N/N+1 changes, checkpoint/save replay, actual daily trends, Before/After, editor round-trips and bar thresholds/scaling.

### Running Avalonia verification A–G

The [native verification host](verification/technical-debt/Program.cs) runs the real Avalonia MainWindow and controls through automation peers and bound TextBoxes. It asserts model/UI values and renders screenshots. Screenshots were visually reviewed. This is automated native interaction plus visual inspection, not a claim of physical mouse/keyboard testing. Run on a graphical macOS session:

```sh
dotnet run --project docs/verification/technical-debt/NativeVerification.csproj -c Release
```

| Scenario | Observed result |
| --- | --- |
| A: unchanged baseline | Day 100: debt 0, scope 410, overhead 0; unused bar hidden |
| B: shortcuts | Day 220: debt 49.504, scope 940, ratio 5.27%; an expanded shortcut item shows reduced effort and saved effort |
| C: within tolerance | Day 240: ratio 5.80%, tolerance 50%, overhead 0; new starts have no overhead |
| D: above tolerance | Day 260: ratio 6.33%, tolerance 1%, overhead 5.33%; future plans include overhead, existing plans remain fixed |
| E: repayment | With shortcuts stopped and repayment 30%, debt falls to zero by Day 360; unused reservation returns to Development |
| F: productivity | At Day 261, productivity 1.5 consumes 1.2 repayment capacity and removes 1.8 debt; Review uses 1, Development 2.8, total developer use 5/5 |
| G: persistence and display | Save/load and checkpoint replay exactly reproduce state/history/random continuation; bar/details, actual daily trend, Before/After and compact layouts verified |

At 1280×800 and 960×720, seven- and nine-row Flow Boards remain entirely within the viewport when collapsed, with no horizontal overflow. At 960×720 the nine-row board ends at y=661. The [native result log](verification/technical-debt/result.txt) and [screenshots](verification/technical-debt/screenshots) preserve the evidence. Before/After shows the agreed Day 100 boundaries.

## Semantic and scope confirmation

Review/Rework priority, stage-specific productivity, Capacity Availability, consumed-capacity utilization, Development Collaboration, Work Supply rules, Rework/defects, Testing, Live intervention timing and existing metric definitions are preserved. The explicit additions are start-time effort adjustment, completion-time scope/debt, and optional consumed repayment capacity. Shared-stream random consumption is described above; default behavior consumes no extra draws.

No debt-driven quality, Review or Testing effort model, AI-specific debt rule, debt ownership/category system, automatic repayment/recommendation, separate interest mechanism, individual developer model or multiple-team model was introduced.
