# Relative Delivery Cost v1

Relative Delivery Cost measures observed, consumed system capacity per delivered Work Item. One developer capacity unit and one tester capacity unit have equal weight. It is not monetary cost; no salaries, rates, scores, recommendations or optimization rules are introduced.

## Item measurement

```text
Item Delivery Cost = Development + Code Review + Rework + Testing consumed capacity
```

`WorkItem.ApplyWork` already receives the actual raw capacity of each contribution. It now adds `consumedCapacity ?? work` to an immutable `DeliveryCost` record according to the item's active stage, immediately after allocation validation and before the existing work processing. The fallback is the existing 1× case where effective work equals capacity. The record contains four cumulative components, derived Total and an IsComplete provenance flag. Costs never drive any allocation or transition.

Development primary and collaborator contributions each pass through this attribution point. A primary contribution consuming 1 and a collaborator consuming 1 adds 2 Development cost, even though their effective work at 1× is 1.5. Stage productivity changes work per capacity in the existing allocator, so less actual consumption naturally means lower cost. Cost is never multiplied by productivity. Rework and repeated Review/Testing attempts keep adding to the same lifetime totals; they do not reset them.

Debt overhead and shortcuts affect the already-calculated Development effort and thus its eventual consumption. Neither debt ratio nor overhead is injected directly into cost. Debt repayment is performed by the separate system ledger, never by `WorkItem.ApplyWork`, so it is **excluded from item cost**. Repayment still consumes developer capacity and counts toward utilization exactly as before. No System Cost metric is implemented.

Daily item snapshots and final item results expose the accumulated record. Snapshot records are immutable, so later work cannot rewrite historical observations. Hovering a Flow Board item ID shows Development, Code Review, Rework, Testing and Total consumed so far, with the repayment exclusion stated explicitly.

## Rolling periods, trend and Before/After

For inclusive display days A–B, select every item with `DoneDay >= A && DoneDay <= B`. Average their **full lifecycle** cost, including capacity consumed before A. The same completed-item cohort supplies each component average, and their sum is Total Cost/Item, subject to display rounding. Items unfinished or completed outside the period do not participate.

An item starting on Day 70 and completing on Day 90 contributes its entire measured cost to a Day 81–100 period. There is no truncation at the window start. This is the existing Cycle Time completion-selection convention.

No completions means unavailable (`null`), displayed as an em dash in compact status and “Unavailable” in detailed metrics. A cohort containing any item with incomplete lifetime tracking is also unavailable, rather than silently averaging only its known subset.

Performance Trend adds **Delivery Cost / Done Item**, unit **capacity units / item**, using the selected rolling window at each historical day. Prefix sums retain lookback before the visible range and count unknown-cost completions, with O(days + items) projection. Missing values stay missing. Prefix and component summation can differ by floating-point rounding; tests use absolute numerical tolerance rather than rounding each value independently.

Before/After includes the metric using its existing completion windows. An intervention on Day 100 with window 20 still compares Days 81–100 and 101–120. No causal interpretation or scoring is added.

## Compact Live presentation

**Cost/Item** joins the existing primary status row. Its tooltip shows the average component breakdown for the same selected completion cohort. The existing Team Performance delivery metrics also include **Relative Delivery Cost / Done Item**. Its details are not permanently expanded above the chart. Trend uses the existing metric selector and chart; no additional card or permanent panel is added.

Native layout checks at widths 1280 and 960 found identical chart position with the Cost/Item group visible or hidden: Y=632 in the verified debt-enabled scenario, chart height 210 in both cases. The chart was not shrunk. Screenshots show the cost fitting in the existing status row.

## Persistence and older sessions

`WorkItemState.DeliveryCost` persists the four counters and completeness flag through Live capture, pause/resume, checkpoints and JSON. Daily snapshots also carry their observed cumulative costs. Existing delta history encoding handles the added immutable record. Checkpoint and saved-session replay reproduce the exact accumulated values and all other state.

These are optional observational fields. JSON schema remains 1 and Simulation Model remains 0.5 because allocation/execution semantics are unchanged. All previously supported model versions remain loadable.

For an old saved item with no cost record:

- An untouched Backlog item with no Development start or capacity event begins at known zero, with IsComplete=true.
- Any already-started or completed item begins with zero **newly observed** counters and IsComplete=false. Its historical lifetime cost is unavailable, even if it later completes. Subsequent consumed capacity is still accumulated accurately, but those partial observations are never reported as a full lifecycle cost.
- Newly generated items are fully tracked from zero.
- Old daily snapshots remain unchanged with absent cost fields. No historical cost is fabricated or reconstructed from configured effort.

Once a rolling completion cohort consists entirely of fully tracked items, the metric becomes available naturally. Missing cost metadata does not alter effort, debt, random state or continuation. Persisted cost components must be finite and nonnegative.

## Verification — 2026-10-03

**498 tests pass**: 176 Core, 179 Application, 143 UI. Release solution build succeeds with **0 warnings and 0 errors**. This feature adds 19 cases (11 Core, six Application, two UI). Existing trend enumeration coverage and Before/After row count were updated; legacy-result comparisons exclude only newly introduced observational metadata.

Coverage includes all four components and their sum, independent productivity factors, raw collaboration consumption, repeated Rework/Review, debt overhead and shortcuts through actual work, repayment exclusion and unchanged utilization, immutable snapshots, missing historical fields, full-lifecycle completion windows, empty/mixed-unknown cohorts, reconciling breakdowns, trend/period equivalence, Before/After, JSON/checkpoint continuation, compact status and unavailable text.

A genuine pre-measurement executable captured `pre-cost-state.json` and `pre-cost-result.json` **before** this implementation was built. Its seeded 80-day scenario includes Always Available supply, variable effort, collaboration, non-unit availability/productivity, defects, shortcuts and debt repayment. The new execution matches every pre-existing state/result field exactly after removing only the new DeliveryCost properties. This includes random streams, events, transitions, efforts, debt, daily observations, completion days and every prior result metric. Older 0.2–0.4 fixture regressions also remain green.

The [native Avalonia verification host](verification/delivery-cost/Program.cs) runs the real MainWindow and scenario controls, checks bound tooltips and compares layout with/without the compact metric. It uses software rendering after the native display-linked timer failed to start in this environment; production rendering configuration was not changed. Screenshots were visually reviewed. This is automated native interaction and visual inspection, not physical mouse/keyboard testing.

| Native scenario | Observed rolling mean cost |
| --- | --- |
| One developer, effort 5/1/2, productivity 1×, no defects/debt | Development 5 + Review 1 + Rework 0 + Testing 2 = **8** |
| Same scenario, Development Productivity 2× | 2.5 + 1 + 0 + 2 = **5.5** |
| Restore 1×, enable defects/rework | 5 + 1.33 + 0.33 + 2 = **8.67** |
| Two developers, collaboration, WIP 1 | 6 + 1 + 0 + 2 = **9**, with all raw contributions counted |
| Shortcuts and debt repayment | Total developer/tester consumption 253.675; attributed item cost 237.093; repayment 16.583 (rounded). Exact unrounded difference reconciles. |

Several baseline completed items were individually checked. The cost trend and Before/After row were inspected in the running UI. See [native log](verification/delivery-cost/result.txt) and [screenshots](verification/delivery-cost/screenshots).

```sh
dotnet test SoftwareDevelopmentSimulation.sln -c Release
dotnet run --project docs/verification/delivery-cost/NativeVerification.csproj -c Release
```

## Files and semantic boundary

Core: new `DeliveryCost.cs`; attribution/persistence in `WorkItem.cs`, `SimulationSession.cs`; observations/results in `Models.cs`, `SimulationEngine.cs`, `WorkItemResult.cs`, `SimulationResultBuilder.cs`. Application: `LivePerformance.cs` and `LivePerformanceTrend.cs`. UI: `LiveViewModel.cs`, `LivePerformancePresentation.cs`, new `DeliveryCostDetails.cs`, and the existing item-ID tooltip in `LiveView.axaml`. Tests: `DeliveryCostTests.cs`, `DeliveryCostIntegrationTests.cs`, `DeliveryCostPresentationTests.cs`, regression fixtures and the existing projection/presentation coverage. Documentation and native verification evidence accompany these changes.

Delivery Cost tracking does not change simulation behavior. Capacity allocation, collaboration, productivity, WIP/queues, Work Supply, defects, debt creation/repayment, random sequence, item movement, completion days, throughput, cycle time, utilization, intervention timing and historical calculations retain their existing semantics.
