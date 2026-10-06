# End-to-end model validation — 2026-10-05

The current model is internally consistent across the controlled scenarios below and is suitable as the baseline for the next simulation features, within its documented abstractions. No model defect was found and **no production code was changed**. Results were investigated without tuning configuration to produce preferred outcomes.

Validation target: commit `6199fd0`, model 0.5, plus only this verification harness/tests/documentation. This validates internal accounting and interactions, not empirical calibration against real teams.

## Reproducible configuration

Seed **12345**, Always Available supply, no initial backlog, five developers and two testers, nominal capacity 1/person/day, availability 100% for both groups. Active-stage WIP limits: Development **5**, Review **3**, Testing **3**, Rework **3**. Fixed original efforts: Development **5**, Review **1**, Testing **2**. Defects/Rework disabled. No dependencies. Existing Development Collaboration Model remains enabled. Performance window **50 days**. All periods are inclusive display days.

Baseline productivity is Development/Review/Testing **1×/1×/1×**. Debt initially zero, Shortcut Rate zero, repayment zero. Dormant debt settings are held constant throughout: Shortcut Effort Reduction **50%**, Debt Creation Factor **2**, Debt Impact Factor **1**, tolerance **10%**. These are explicit experiment assumptions, not recommended values. Review/Testing productivity, people, availability, WIP, supply and original efforts never change.

1. Baseline runs to Day 200.
2. A fresh run with the same seed changes **only Development Productivity to 2×**, also to Day 200.
3. A fresh run changes **only Shortcut Rate to 50%**. At Day 200, record the state, then set Shortcut Rate to zero, effective Day 201. Inspect the first 50-day after period and continue to Day 400 without a reset.
4. Continue that same debt-bearing timeline. At Day 400, set repayment to **25%**, effective Day 401, retaining zero shortcuts and 1× productivity. Inspect Days 401–450, continue until debt is exhausted on **Day 845**, then observe 50 further days to Day 895.

Baseline/Scenario 2 each have 200 simulated days. Scenarios 3 and 4 share one continuous 895-day timeline. Live advancement uses explicit target days; the request's batch-duration default is not the run horizon. Full serialized inputs, phase boundaries and unrounded values are in [observations.json](verification/model-validation/observations.json).

## Measured outcomes

| Scenario / phase | Window | Done¹ | TP / 5d | Cycle days | Avg WIP | Dev util. | Test util. | Debt ratio² | Cost/item | Dev | Review | Rework | Test |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 Baseline | 151–200 | 162 (42) | 4.20 | 9.57 | 7.20 | 100.00% | 84.00% | 0.00% | 8.000 | 5.000 | 1.000 | 0.000 | 2.000 |
| 2 Development 2x | 151–200 | 196 (50) | 5.00 | 56.30 | 77.84 | 99.58% | 100.00% | 0.00% | 5.531 | 2.531 | 1.000 | 0.000 | 2.000 |
| 3a Shortcuts 50% | 151–200 | 144 (37) | 3.70 | 10.05 | 6.70 | 100.00% | 74.00% | 65.20% | 8.672 | 5.672 | 1.000 | 0.000 | 2.000 |
| 3b First window after stopping | 201–250 | 173 (29) | 2.90 | 11.45 | 6.18 | 100.00% | 58.00% | 55.34% | 10.453 | 7.453 | 1.000 | 0.000 | 2.000 |
| 3c Continued without shortcuts | 351–400 | 268 (32) | 3.20 | 10.59 | 6.34 | 100.00% | 66.00% | 36.07% | 9.624 | 6.624 | 1.000 | 0.000 | 2.000 |
| 4a Repayment 25% | 401–450 | 296 (28) | 2.80 | 11.89 | 6.10 | 100.00% | 55.00% | 28.96% | 9.224 | 6.224 | 1.000 | 0.000 | 2.000 |
| 4b After debt is exhausted | 846–895 | 586 (41) | 4.10 | 9.37 | 6.80 | 100.00% | 82.00% | 0.00% | 8.011 | 5.011 | 1.000 | 0.000 | 2.000 |

¹ Done is cumulative at the observation day; parentheses give completions within the window. Do not compare cumulative Done across different observation horizons as a throughput measure. TP is completed items per five simulated days. Cycle Time and Cost use the full lifecycle of the window's completed items. WIP is the average end-of-day started unfinished population. Utilization is period consumed capacity / period available capacity. Cost components are capacity units per completed item.

² Debt ratio is the end-of-window state, not a rolling average.

| Scenario / phase | Review queue avg / end | Testing queue avg / end | WIP at end | Debt amount at end | Overhead at end | Dev capacity / effective work³ | Repayment capacity³ |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 Baseline | 0.80 / 0 | 0.84 / 0 | 8 | 0.000 | 0.00% | 208.000 / 208.000 | 0.000 |
| 2 Development 2x | 1.46 / 0 | 70.80 / 82 | 88 | 0.000 | 0.00% | 177.938 / 351.062 | 0.000 |
| 3a Shortcuts 50% | 0.74 / 1 | 0.74 / 1 | 7 | 479.225 | 55.20% | 214.000 / 209.945 | 0.000 |
| 3b First window after stopping | 0.58 / 1 | 0.58 / 1 | 7 | 486.955 | 45.34% | 221.000 / 214.609 | 0.000 |
| 3c Continued without shortcuts | 0.64 / 0 | 0.66 / 0 | 7 | 486.955 | 26.07% | 217.000 / 211.662 | 0.000 |
| 4a Repayment 25% | 0.56 / 0 | 0.56 / 1 | 7 | 431.455 | 18.96% | 166.500 / 166.442 | 55.500 |
| 4b After debt is exhausted | 0.82 / 1 | 0.82 / 0 | 6 | 0.000 | 0.00% | 209.000 / 208.567 | 0.000 |

³ Capacity/work columns are sums over the 50-day period. Each period provides 250 developer and 100 tester capacity units. Waiting queues are reported separately from active WIP occupancy.

## Scenario 1 — baseline

The developer pool is the limiting resource: each item requires 5 Development + 1 Review developer capacity, while Testing requires 2 tester capacity. Approximate long-run ceilings are 5/6 items/day from developers versus 2/2=1 item/day from testers. The observed 4.2 items/5 days is consistent with the developer ceiling (4.167/5 days), with integer completions and a finite 50-day observation window.

Developers consume 100% of capacity, testers 84%. Review and Testing waiting averages are small (0.80 and 0.84); a waiting snapshot is expected under the existing all-admissions-before-work rule. Cost is exactly 8, split 5/1/0/2, with zero debt and zero repayment. WIP averages 7.20, higher than Development's limit of 5 because it includes the other stages and queues. No WIP policy is violated.

## Scenario 2 — Development Productivity 2×

Development produces 351.0625 effective effort from 177.9375 consumed units in the period: about 1.973 effective units per consumed unit, versus 1.000 in baseline. The ratio is slightly below 2 because the window includes 4.8125 raw collaboration capacity, whose second contribution remains 50% efficient.

Development cost falls from 5 to 2.531 per completed item. It is slightly above the no-collaboration 2.5 reference because costs count both raw contributions. Review and Testing stay exactly 1 and 2 per item. Total becomes 5.531.

Throughput reaches 5 items/5 days, **not twice baseline**. Testing is saturated at 100%; its queue averages 70.80 and ends at 82. Review is not the sustained constraint (average queue 1.46). The model allows waiting states outside active WIP limits, so faster Development continues supplying downstream work. Overall average WIP rises to 77.84 and completion-cohort Cycle Time to 56.30 days.

This is a coherent, non-stationary system: cheaper Development and higher throughput coexist with much longer waiting. Unlimited demand plus no end-to-end WIP cap allows the Testing queue to grow. The period's cycle time is not an equilibrium service-time estimate, and Little's Law should not be forced onto these transient completion-cohort/window statistics. Utilization stays based on consumed capacity (99.58% Dev), not the larger effective-work number.

## Scenario 3 — build debt, stop shortcuts

At Day 200, absolute debt is 479.2254 and developed scope is 735, giving ratio 65.20% and overhead 55.20%. Despite shortcut savings, the accumulated overhead makes cost 8.672 versus baseline 8; throughput is 3.7/5 days. This is allowed by the explicitly assumed 2× debt creation factor and growing feedback, without changing productivity or injecting debt directly into cost.

Stopping shortcuts does not rewrite active effort, saved effort or history. New Development admissions from Day 201 have no shortcut reduction. Already-started shortcut plans still complete using their locked choices and debt-to-create values: they add **7.7294838887352615** debt through **Day 204**. This small post-stop increase is expected, not evidence that shortcuts are still assigned to new items.

The first after window (201–250) has cost 10.453, with Development 7.453, throughput 2.9/5 days and Cycle Time 11.45. Removing the immediate saving leaves new work facing the existing debt overhead. Some completions also belong to pre-intervention starts; period selection deliberately uses full lifecycle measurements. No immediate clean separation of all work is assumed.

Absolute debt then stays exactly **486.95491838882043** through Day 400 because neither new shortcut completions nor repayment occurs. Scope grows to 1350 and the ratio falls to 36.07%, overhead to 26.07%. The later cost is 9.624 and throughput 3.2/5 days. **A falling debt ratio here does not mean debt was repaid.** Lower overhead on later starts and retained old plans explain the gradual, cohort-dependent metrics. Review/Testing queues stay small and testers are underutilized because Development is the limiting resource.

## Scenario 4 — repay debt

With 25% repayment from Day 401, the first 50-day period allocates Review 28, Rework 0, repayment **55.5** and Development **166.5** developer capacity units, totaling 250. Repayment uses 25% of the 222 remaining after Review. Developer utilization is still 100%; Development gets less capacity than the 217 consumed in the preceding window.

Debt falls from 486.955 to 431.455. Throughput falls from 3.2 to 2.8/5 days, Cycle Time rises from 10.59 to 11.89, and tester utilization falls from 66% to 55%. At the same time, Cost/Item falls from 9.624 to 9.224 as newer cohorts require less Development work and the collaboration pattern changes. The metrics do not contradict each other: repayment competes with delivery capacity but is intentionally excluded from item cost. Period-to-period numbers also contain evolving scope and completion-cohort effects, so these deltas are not presented as isolated universal causal coefficients.

On Day 845, opening debt is **0.7049183888204311**. Review consumes 1 capacity, leaving 4. The maximum repayment allocation would be 1, but only **0.7049183888204311** is consumed. The remaining **3.295081611179569** capacity is used for Development that same day, exceeding the nominal 3 if the entire reservation were consumed. Debt becomes exactly zero. All 50 following days use zero repayment capacity despite repayment still being configured at 25%.

After exhaustion, cost is 8.0105 and throughput 4.1/5 days, close to baseline. All 41 cohort items have zero start overhead; their aggregate Development consumption exceeds base scope by 0.43142391607704234 because of collaboration, not residual debt. Different completion-phase alignment and collaboration mean a later finite window need not exactly reproduce the original baseline window.

## Cross-metric and invariant audit

Audited **1,295 simulated days** and **314,194 item-day observations**, covering all three independent timelines (the debt timeline includes both Scenarios 3 and 4). Daily checks verify:

- Finite, nonnegative remaining effort, debt and consumed capacity/cost values; bounded developer and tester use.
- Active occupancy equals the corresponding active-state population and stays within each stage's WIP limit. Waiting populations remain outside these limits.
- Review receives its capped demand first, then Rework; repayment draws only on their remainder; Development stays within the residual pool. Tester allocation is independently bounded by its own pool and demand.
- Effective Development work equals productivity times primary capacity plus half the collaborating capacity; Review/Testing use their own multipliers. Collaboration is confined to Development. Repayment uses Development Productivity without collaboration.
- Debt closing balance equals opening debt minus repayment plus completion-created debt. Overhead follows excess ratio above tolerance.
- Every item's four accumulated-cost deltas reconcile to that day's actual stage consumption. Sum of item-cost increments equals developer use minus repayment plus tester use. Thus repayment never enters item cost, while utilization includes it.
- Period utilization is consumed / available capacity. Component cost means reconcile using the same completion cohort, and the cost trend matches period calculations.

Defects are disabled in these four controlled scenarios, so their Rework consumption is zero. The existing full-suite quality/repeated-Rework and nonzero Review/Rework-priority tests remain green; this report does not pretend the four scenarios alone exercise nonzero Rework competition.

Do not equate `Cost/Item × period Done` with total capacity consumed inside that period: the former uses full lifecycle completion cohorts, whereas the latter includes work on still-unfinished items and excludes work consumed before the window. This distinction is especially material when Scenario 2's downstream queue is growing.

## Determinism and measurement neutrality

All four scenarios were repeated with identical seed/configuration/interventions. SHA-256 fingerprints of complete captured states match for baseline, productivity and the entire debt/repayment timeline. The capture includes items, completion dates, all daily history, debt, per-item cost, random streams and interventions. Fingerprints and audit counts are recorded in [audit.json](verification/model-validation/audit.json).

Projecting every selectable metric leaves the complete state fingerprint unchanged. Existing genuine pre-Delivery-Cost regression fixtures also pass, preserving every previous observation and random-continuation value. This supports both repeatability and the claim that measurement does not influence execution.

## Surprises, concerns and defects

**Surprising but correct:** doubling Development productivity greatly increases Cycle Time here; Development cost is slightly above 2.5 because of collaboration; debt briefly grows after shortcuts stop because active plans complete; debt ratio falls without repayment because scope grows; cost can fall while throughput falls during repayment; after payoff, finite-window values need not exactly equal the earlier baseline.

**Potentially misleading unless explained:** Scenario 2 is overloaded downstream and not in steady state. Its high utilization is not a measure of balanced flow. The WIP parameter limits active stages rather than all started work. Item Delivery Cost intentionally omits system maintenance/repayment. None of these observations is evidence of an allocation/accounting defect, and no unexplained suspicious discrepancy remains in these runs. These scope limits should stay visible in future feature discussions.

**Actual model defects found: none. Production changes: none.** No tuning or semantic adjustment was made.

## Tests, build and files

Six interaction tests were added in `tests/Simulation.Application.Tests/ModelInteractionValidationTests.cs`, reusing the same verification scenario/audit source via the test project. They cover daily conservation, downstream bottleneck behavior, locked shortcut plans/history, competing repayment and fractional payoff, complete replay determinism, and read-only metric projection. They complement rather than duplicate individual arithmetic unit tests.

Full suite: **504 passed** (176 Core, 185 Application, 143 UI), no failures. Release solution build: **0 warnings, 0 errors**. The standalone validation runner also completed successfully.

Changed files: the Application test project (links verification source), the new interaction test, this report, README link, and `docs/verification/model-validation/` (reproducible runner, scenario definitions, audit, exact observations, diagnostics, hashes and result log). No `src/` file changed.

```sh
dotnet run --project docs/verification/model-validation/Validation.csproj -c Release
dotnet test SoftwareDevelopmentSimulation.sln -c Release
dotnet build SoftwareDevelopmentSimulation.sln -c Release --no-restore
```

## Assessment

**Yes: the current model is internally consistent enough to serve as the baseline for the next simulation features.** The checked metrics describe the same underlying capacity allocation and state history. Retain these tests and the explicit distinctions between active WIP and queues, debt amount and ratio, effective work and consumed capacity, and item cost versus system repayment. This is evidence for the tested deterministic configurations and existing suite, not proof of every possible parameter combination or a claim of empirical realism.
