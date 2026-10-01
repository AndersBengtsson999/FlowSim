# Development Collaboration Model v1

Introduced in Simulation Model **0.2**, after Step 13. Technical Debt is not started. Code Review, Rework and Testing retain their previous allocation rules.

The numerical examples and legacy ledger identities below use default productivity 1x. Model 0.3 additionally scales nominal pools by Capacity Availability; model 0.4 multiplies primary and collaboration work by Development Productivity without changing the two-pass allocation rules. See [current stage formulas](STAGE_PRODUCTIVITY.md).

## Concepts and assumptions

- **Developer Capacity** is the available pool per day: developer count × configured capacity per person. Units are abstract capacity, not hours.
- **Development Effort** is work required to complete Development. Remaining effort persists between days.
- **Development WIP** limits simultaneously active Work Items. It does not limit contributor count or directly cap consumed capacity.
- **Primary capacity** is the first contribution to an active item's Development that day, with efficiency 1.0.
- **Collaboration capacity** is a second contribution, with efficiency 0.5. At most two contributors per item/day are represented; there are no named people or individual schedules.
- **Consumed capacity** is what leaves the developer pool. **Effective Development work** is what is subtracted from remaining effort. They differ during collaboration.
- **Developer Utilization** is total consumed Development, Review and Rework capacity divided by total available developer capacity over the observed days. It measures consumption, not output efficiency. Zero available capacity gives zero lifetime/recent utilization; the existing performance-period undefined-value handling is unchanged.

The simulator does not claim that two real developers always produce 1.5 times the output of one developer. The value is an explicit simulation assumption used to explore collaboration and WIP effects.

## Allocation algorithm

The shared `SimulationEngine.AdvanceOneDay` implementation serves normal Run and incremental Live through `SimulationSession`. There is no allocation logic in presentation code.

1. Admit work using existing WIP/dependency rules at day start. A completion does not admit replacement work during that same day.
2. Serve Code Review, then Rework using the existing FIFO allocator and one-unit per-item cap.
3. Materialize active Development items in FIFO order: admission timestamp, then original input order for ties. Give every item an opportunity for primary capacity before starting collaboration.
4. Sort still-active Development items by remaining Development effort **after primary work**, ascending. Stable sorting preserves FIFO/input order for ties: Closest to Done first.
5. Allocate one collaboration contribution to each eligible item while capacity remains. This implements “Finish before starting more work” within the existing daily admission model. It does not alter backlog admission ordering or introduce fractional-day stage transitions.
6. Testing continues on its independent pool. Record effective effort and consumed capacity separately.

Let `c = min(1, DeveloperCapacityPerDay)`:

```text
primary consumed = min(remaining effort, remaining developer pool, c)
primary effective = primary consumed
collaboration consumed = min(remaining effort / 0.5, remaining developer pool, c)
collaboration effective = collaboration consumed × 0.5
```

Collaboration requires at least two developers. Per-person capacity below one limits each contribution: two developers at 0.4 can contribute 0.4 primary and 0.4 collaboration, consuming 0.8 for 0.6 effective work. Capacity above one increases the aggregate pool, but neither contribution exceeds one. One developer cannot supply the second contributor even with configured capacity above one.

No item exceeds 2 consumed Development units or 1.5 effective units/day. Leftovers can serve other eligible active items in the same pass/day. Unusable capacity is left unused and does not carry forward. Review/Rework/Testing remain limited to `min(remaining effort, pool, 1, per-person capacity)`.

## Worked examples

Five developers, capacity one each, no Review/Rework demand and sufficiently large remaining Development effort:

| Development WIP | Primary consumed | Collaboration consumed | Total consumed | Effective work | Unused pool |
|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 1 | 2 | 1.5 | 3 |
| 2 | 2 | 2 | 4 | 3 | 1 |
| 3 | 3 | 2 | 5 | 4 | 0 |
| 5 | 5 | 0 | 5 | 5 | 0 |
| 10 | 5 | 0 | 5 | 5 | 0 |

At WIP 3, effective allocations can be 1.5, 1.5 and 1.0. At WIP 10, five active items receive no work that day; extra active items do not create capacity. At WIP 1 utilization is 2/5 = 40%, while effective work divided by available capacity would be 30%; the latter is **not** Developer Utilization.

Fractional cases:

- Remaining effort 0.2 before primary: consume 0.2, apply 0.2, complete. With a pool of five, 4.8 remains for other eligible work.
- Remaining effort 0.2 after primary: consume 0.4 collaboration, apply 0.2, complete. Do not consume a full second unit.
- Active items with efforts 0.2, 1.2 and 10: primary consumes 0.2 + 1 + 1; collaboration consumes 0.4 + 1. Total consumption 3.6, effective work 2.9. The remaining 1.4 is unused because all eligible contributions are exhausted. A fourth backlog item is not admitted midway through that day.

## Ledgers, metrics and presentation

Daily and per-item `DevelopmentWork` remains effective effort. New persisted `CollaborationDevelopmentCapacity` records second-contribution consumption. Derived ledger fields are:

```text
PrimaryDevelopmentCapacity = DevelopmentWork − 0.5 × CollaborationDevelopmentCapacity
UsedDevelopmentCapacity = DevelopmentWork + 0.5 × CollaborationDevelopmentCapacity
UsedDeveloperCapacity = UsedDevelopmentCapacity + ReviewWork + UsedReworkDeveloperCapacity
```

Capacity-applied events retain effective `EffortApplied` and expose `CapacityConsumed` (explicit for collaboration; equal to effort for existing allocations). Item history displays both. Numerical comparisons use floating-point tolerance.

Lifetime Developer Utilization, Development Utilization, recent utilization and Before/After analysis consume these ledgers. Review and Rework measurements are unchanged. Day boundaries remain unchanged: an intervention on Day 100 with window 20 compares Before 81–100 against After 101–120.

The existing Flow Board now includes a compact Development summary with consumed capacity and effective effort. Live retains current active count/WIP limit; Run retains its WIP-limit summary. Expand **Development allocations** for remaining effort, primary capacity, collaboration capacity, consumed capacity and effective work per item. Live shows the most recently completed day; Run follows the selected day. Items that completed Development that day remain in the allocation details and are labeled accordingly. Remaining effort and occupancy are measured at day end, so occupancy can be zero even when capacity was consumed during that day.

Development WIP contextual help explains active-item limits and spare-capacity collaboration. The interface shows observations without ranking WIP settings, attributing causality or prescribing changes.

## Model version and persistence

`SimulationModel.Version` advances from **0.1 to 0.2** because daily Development behavior changes. JSON schema remains 1. Scenario, experiment and Live session envelopes use the existing version validation and reject v0.1 files. Result records also carry model version; comparison exports retain existing provenance.

There is no migration system. Keep original v0.1 files/results for use with their matching implementation. To explore v0.2, explicitly recreate the desired configuration and run a new experiment. Do not edit the model-version label of an old session: that would mix incompatible historical ledgers and future semantics. Current v0.2 save/load preserves collaboration fields, events, random state, checkpoints and subsequent deterministic continuation.

## Verification

Focused Core tests cover all five WIP examples, primary-first allocation, Closest-to-Done ordering, FIFO and input ties, fractional consumption/reuse, contribution limits, one-developer behavior, event/daily/item ledgers, consumed-capacity utilization and unchanged Review/Rework priority/caps. Existing timing tests were updated only where the new Development rule deliberately changes completion dates or work amounts.

Application tests compare complete serialized Run/Live results with variable efforts and defects, save/load and restore checkpoints, continue after identical interventions, assert consumed utilization across lifetime/rolling/BeforeAfter metrics, and reject old model files. Existing boundary, seed, persistence and UI regression suites remain enabled.

The native verification host in [verification/development-collaboration](verification/development-collaboration/Program.cs) opens the production Avalonia MainWindow, drives its commands/bindings, expands allocation details, asserts visible text, and renders screenshots. This is automated native UI verification with visual inspection, not a claim that every scenario was manually clicked through.

```sh
dotnet run --project docs/verification/development-collaboration/NativeVerification.csproj -- docs/verification/development-collaboration
```

Validation configuration: five developers at capacity one, ten testers, 200-item fixed backlog, fixed Development/Review/Testing effort 10/0.2/0.2, Review/Testing WIP 10, no defects, 100 days. First-day allocations matched every worked example above. Each scenario retained backlog at Day 100.

| WIP | 100-day developer utilization | Completed | Throughput / 5 days | Mean completed cycle time | End backlog | Max review/testing queues |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 37.76% | 14 | 0.70 | 9.000 | 185 | 1 / 1 |
| 2 | 75.52% | 28 | 1.40 | 9.000 | 170 | 2 / 2 |
| 3 | 96.30% | 36 | 1.80 | 9.944 | 161 | 2 / 2 |
| 5 | 100.00% | 45 | 2.25 | 12.289 | 150 | 5 / 5 |
| 10 | 100.00% | 45 | 2.25 | 21.311 | 145 | 5 / 5 |

These utilization values include Review consumption and fractional final Development contributions across all 100 days; they need not equal the first-day ratios. Queue counts include end-of-day stage completions waiting for next-day admission. Review/Testing require only 0.2 effort each; no growing downstream queue appears in these cases. Cycle-time means include completed items only. These are factual outputs from this configuration, with no preferred WIP inferred.

Screenshots: [WIP 1](verification/development-collaboration/wip-1.png), [WIP 2](verification/development-collaboration/wip-2.png), [WIP 3](verification/development-collaboration/wip-3.png), [WIP 5](verification/development-collaboration/wip-5.png), [WIP 10](verification/development-collaboration/wip-10.png).

Final Release verification: **313 tests pass** (110 Core, 124 Application, 79 UI), **0 warnings, 0 errors**. Native verification passes for all five WIP cases. Review unchanged; Rework unchanged; Technical Debt not started.
