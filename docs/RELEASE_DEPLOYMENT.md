# Release / Deployment v1 — model 0.7

New simulations finish through **Testing → Ready for Release → Released**. Ready for Release is a waiting state with no WIP limit. Released is terminal. Failed inspections still follow the existing Rework loop; only successful final Testing reaches Ready. Release is a discrete flow constraint, not a staffed stage or a capacity-cost model.

## Timing, modes and interventions

Release runs after all of the day's Testing work, at the same end-of-day boundary `day + 1` used for stage completions. An item becoming Ready today is eligible for today's release. FIFO sorts by ReadyForReleaseDay and preserves original workload insertion order on ties. No work or random draw occurs during release.

- **Flow-based:** release up to Capacity whole items at each day end; capacity unit is items/day.
- **Scheduled:** release up to Capacity whole items when the one-based display day is divisible by Interval; capacity unit is items/release. Interval 5 means Days 5, 10, 15, etc. Excess stays queued; unused capacity is not banked.

Exactly these two modes exist. Capacity is a nonnegative integer; zero suspends release while upstream work continues. Interval is a positive integer (also retained while Flow-based is selected). Fractional items/capacity are rejected. The default is Flow-based with `int.MaxValue` capacity (2147483647), non-constraining for the supported workload. This avoids an arbitrary default cap and adds **no extra tick** to historical completion timing.

Changes recorded on Day N start on Day N+1. No release happens during Apply Changes. Mode, interval and capacity changes retain the queue and its FIFO order. The Scheduled calendar always uses absolute simulation days; it never resets from the intervention. For example, changing interval 5 to 10 on Day 4 suppresses the Day 5 opportunity and makes the next opportunity Day 10. Changing to Flow-based on Day 10 first releases on Day 11.

## Domain, legacy data and persistence

The original enum value Done remains a legacy terminal value; new enum values are appended without renumbering historical values. Old Done items, their timestamps, events and daily histories remain unchanged on load. They are interpreted as both work-completed and delivered on their original DoneDay, with zero historical release wait. The UI includes them in the Released total and terminal row; it does not manufacture release events in old history.

New items persist ReadyForReleaseDay and ReleasedDay. DoneDay remains a compatibility field equal to ReleasedDay for new items. `WorkCompletedDay` reads ReadyForReleaseDay, falling back to old DoneDay. Invalid new release timestamp/state combinations are rejected. New results expose DevelopmentCycleTime and ReleaseWaitTime as well as the existing CycleTime, which now ends at Released.

The schema remains 1 and the simulation model becomes 0.7. Model 0.2–0.6 documents remain supported; absent release settings default to unconstrained Flow-based. New items in resumed sessions use the new transitions without modifying old observations. Delta history, configurations, interventions, queues and both timestamps use existing JSON transport. Checkpoint/save-load tests reproduce future release order, days, queues and complete state exactly. No additional schedule clock or accumulator needs persistence.

Existing dependency eligibility remains tied to **work completion** (Ready, Released or legacy Done). A pre-existing dependency does not acquire a new release gate. This preserves the previous Testing-completion dependency timing and prevents constrained release from changing upstream mechanics. No new dependency model is introduced.

## Metrics and populations

All period boundaries remain inclusive one-based display days. Partial windows use observed days. No-data means unavailable, never NaN/Infinity.

| Metric | Population / definition |
|---|---|
| Throughput | Released in the period / observed days × 5; includes historical Done as delivered |
| Completion Rate | Items entering Ready in the period / observed days × 5; historical Done is the legacy work-completion boundary |
| Delivery Cycle Time | Released-in-period cohort; full ReleasedDay − DevelopmentStartedDay |
| Development Cycle Time | Ready-in-period cohort; full ReadyForReleaseDay − DevelopmentStartedDay |
| Release Wait Time | Released-in-period cohort; full ReleasedDay − ReadyForReleaseDay |
| Ready for Release | End-of-day count in that waiting state; period comparison also shows its daily average |
| Delivery Work Cost / Item | Ready-in-period cohort; mean full lifecycle raw Development + Review + Rework + Testing consumption |
| System Cost / Released Item | Period raw Development + Review + Rework + Testing + Debt Repayment consumption / Released-in-period count |

For each individual released item, Delivery Cycle Time = Development Cycle Time + Release Wait Time exactly. Aggregate averages need not add when the Ready and Released cohorts differ. An item Ready before the window but Released inside it contributes to Throughput, Delivery Cycle and Release Wait, not that window's Completion Rate, Development Cycle or Delivery Work Cost. The converse applies to Ready inside/Released after the window.

System Cost retains capacity spent on unfinished items in its numerator and returns unavailable with zero releases. Release Capacity contributes **no capacity-cost units**. Release waiting never increases an item's Delivery Cost. Debt repayment remains included only in System Cost and developer utilization. The existing Application property names DeliveryCostPerDoneItem and SystemCostPerDoneItem remain compatibility aliases for the explicitly named DeliveryWorkCostPerItem and SystemCostPerReleasedItem.

Rolling trend uses separate completion and release prefix counts/cycle sums, with the same selected windows and full lookback even outside the visible range. Before/After independently applies these populations to the existing Day N / Day N+1 intervention boundary.

## Explicit changes to previous metrics

Previously Done coincided with successful Testing completion and delivery. New primary Throughput, Cycle Time, lead time and delivered counts end at **Released**. System Cost's denominator likewise changes to Released. The old item-cost metric is renamed **Delivery Work Cost / Item** and preserves the work-completion population, now Ready for Release. Development Cycle Time retains the earlier start-to-work-completion meaning as a secondary metric.

Ready items now contribute to total WIP and unfinished-item counts, and released-item waiting time includes the release interval. Active Development/Review/Testing WIP limits do not change. With the default non-constraining release setting, these numerical observations retain their previous values because Ready and Released occur at the same boundary. With constrained release, these changes are intentional and visible, not silently normalized in production.

## Existing mechanics

Developer priority, Development collaboration, stage productivity, availability, debt plans/shortcuts/creation, debt repayment, Skills classification and eligible Development allocation are unchanged. Release reads none of the productivity or skill settings and uses no developer/tester capacity. Specialist Work Waiting remains Development-only. A release-stop integration test with active Skills, productivity, defects, shortcuts and repayment confirms identical upstream item costs, Ready days, classification, capacity usage, debt and all four random streams versus unconstrained release.

## Live UX

Configuration adds a collapsed Release / Deployment editor. Flow-based shows capacity in items/day; Scheduled shows capacity in items/release and interval in days. The Change panel uses Parameter / Current / Try rows with the same dynamic units; intervals are hidden when the proposed mode is Flow-based. Apply remains atomic and intervention labels persist.

Flow Board adds the Ready queue and replaces the terminal Done presentation with Released. Ready shows mode/capacity in its existing supporting text and allows item inspection; it has no WIP gauge. The compact status prioritizes Released and keeps one Cycle Time and one item-cost value. Secondary metrics live in Team Performance, tooltips, the existing trend dropdown and Before/After. No extra chart or large permanent release panel was added.

Flow rows have slightly reduced padding to accommodate the extra queue. Automated interaction with the real Avalonia MainWindow and visual review verified the release mode/capacity bindings, label, configuration, queue, trend and Before/After at 960 and 1280 pixels. The chart starts at Y=559 versus the previous Y=572, retaining height 210. It was not moved down or shrunk. [Results](verification/release/result.txt), [960px](verification/release/screenshots/layout-960.png), [1280px](verification/release/screenshots/layout-1280.png), [Configuration](verification/release/screenshots/configuration.png), [Change](verification/release/screenshots/change.png), [Before/After](verification/release/screenshots/before-after.png).

## Validation scenarios A, B and C

Seed 12345, Always Available, 5 Developers / 2 Testers, availability 100%, productivity 1/1/1, WIP 5/3/3, fixed effort 5/1/2, defects/debt/Skills off. Independent deterministic runs to Day 200; all metrics below use Days 151–200 unless marked cumulative/current.

| Metric | A: Flow 1/day | B: Scheduled 5 every 5 days | C: Flow 2/day | C: Scheduled 10 every 5 days |
|---|---:|---:|---:|---:|
| Ready queue, current | 0 | 0 | 0 | 0 |
| Ready queue, daily average | 0.34 | 1.66 | 0.00 | 1.66 |
| Released, cumulative | 162 | 162 | 162 | 162 |
| Released in period | 42 | 42 | 42 | 42 |
| Throughput / 5 days | 4.20 | 4.20 | 4.20 | 4.20 |
| Completion Rate / 5 days | 4.20 | 4.20 | 4.20 | 4.20 |
| Delivery Cycle Time | 9.976190 | 11.547619 | 9.571429 | 11.547619 |
| Development Cycle Time | 9.571429 | 9.571429 | 9.571429 | 9.571429 |
| Release Wait Time | 0.404762 | 1.976190 | 0.000000 | 1.976190 |
| Developer Utilization | 100% | 100% | 100% | 100% |
| Tester Utilization | 84% | 84% | 84% | 84% |
| Delivery Work Cost / Item | 8.000000 | 8.000000 | 8.000000 | 8.000000 |
| System Cost / Released Item | 7.952381 | 7.952381 | 7.952381 | 7.952381 |

Day 200 is a scheduled opportunity, so current queue zero does not imply no waiting between releases. The same upstream output lies below the long-run release capacities; the scheduled cases show batch waiting without a long-run capacity shortage. No mode is rated better.

A separate demand-saturating test supplies two items daily with zero upstream effort (still three stage intervals). In Days 151–200, Flow 2/day and Scheduled 10/5days both release exactly 100 items (10/5days), but mean release waits are 0 and 2 days. A constrained FIFO burst test produces 12 Ready items on Day 3: Flow capacity 2 releases exactly two daily while Scheduled capacity 10 releases ten on Day 5 and the remaining two on Day 10.

## Verification and reproducibility

19 new cases: 10 Core, 7 Application, 2 UI. Coverage includes settings validation, zero-cost/unconstrained timing, bounded FIFO, schedule/excess/batching, cycle identity, window populations, no-release no-data with ongoing consumption, intervention calendar changes, shared-mechanics independence, nominal-capacity comparison, checkpoint/save-load with a queue, legacy Done preservation and UI bindings/units/inspection.

Existing assertions which explicitly named Done as the fresh-run terminal state now name Released and include the same-boundary Ready transition. Version assertions move to 0.7; flow/comparison row counts and trend cases include the new observations. Existing numeric expectations are retained. Verification-only `LegacyReleaseObservation` collapses zero-wait Ready→Released into old Done for archived comparisons, removes new release metadata and rejects queued/nonzero-wait normalization. It supports both numeric and string enum serialization and is never used by production loading. Full archived pre-feature state fingerprints and genuine legacy result fixtures verify unchanged upstream behavior under the compatibility default.

Full suite: **556 passed (204 Core, 205 Application, 147 UI), zero failed/skipped**. Release build: **0 warnings, 0 errors**. Native verification passed. Release logic consumes no randomness.

```sh
dotnet test --nologo -m:1
dotnet build -c Release --nologo -m:1
dotnet run --project docs/verification/release/NativeVerification.csproj
```

Principal implementation files: Core `Release.cs`, `WorkItem.cs`, `Defects.cs`, `Models.cs`, `SimulationEngine.cs`, `SimulationSession.cs`, `SimulationResultBuilder.cs`, `WorkItemResult.cs`, `ScenarioValidator.cs`, `SimulationModel.cs`; Application request/Live/performance/trend/experiment projections; UI `ReleaseSettingsView`, `LiveView`, main/Live/flow/performance ViewModels and result labels. Tests, fixture normalization and native verification accompany the change.

### Unlimited capacity presentation follow-up

The editor displays the unconstrained default as `Unlimited`, accepts that text case-insensitively, and retains `int.MaxValue` in the model. Current values, change history and Flow Board captions use the same presentation. Mode, capacity and interval fields share aligned columns; helper text explains zero capacity and scheduled days. Added regression coverage for default/reset/load, parsing and Live changes, plus a native default-capacity capture. Follow-up verification: Release build succeeded, all 148 UI tests passed, and the native Avalonia verification passed. The default-capacity screenshot was visually inspected: Unlimited is visible and the fields align. An initial automatic approval error cleared on retry. The full-suite counts above predate this presentation follow-up.
