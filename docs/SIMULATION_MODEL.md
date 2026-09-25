# Simulation Model v0.1 — Steps 2–4

## Purpose and scope

This version defines the simulation world and basic workflow for a software development system. It is an explicit, simplified model, not a project-management system or a calibrated prediction of an organization. Delivery and queues emerge from effort, available capacity, dependencies, FIFO ordering and WIP limits. There are no rules that declare a resource a bottleneck based on headcount ratios.

This document describes the current implementation. It supersedes the earlier incremental model with Size/Complexity, seeded random ordering, a single WIP limit and a fixed/derived review stage.

## Entities and boundaries

- **Team**: DeveloperCount, TesterCount, DeveloperCapacityPerDay and TesterCapacityPerDay. Both per-person capacity defaults are 1.0. The computed totals are nominal capacity per working day.
- **WorkItem**: string Id and Name; independent DevelopmentEffort, CodeReviewEffort and TestingEffort; corresponding remaining efforts; dependency IDs; State; CreatedDay; DevelopmentStartedDay, DevelopmentCompletedDay, CodeReviewStartedDay, CodeReviewCompletedDay, TestingStartedDay, TestingCompletedDay and DoneDay.
- **SimulationScenario**: Name, SimulationDays, one Team, DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit and an ordered collection of WorkItems.
- **SimulationResult**: aggregate measures, immutable WorkItemResult records and daily snapshots. Every result retains its ordered transition history. Daily item snapshots include state, remaining efforts and actual work consumed in each stage.

Configuration properties on WorkItem are read-only. Remaining efforts equal initial efforts at construction. Execution state and timestamps have private setters, and internal domain methods enforce the next legal transition. UI code cannot assign an arbitrary state. Dependencies are defensively copied into a read-only collection.

Each engine run creates fresh execution copies. Inputs remain unchanged and can be reused. Completed or partially executed items are rejected as new scenario inputs. IDs are case-sensitive and must be unique. The order of the input collection is meaningful for simultaneous FIFO arrivals.

Simulation.Core uses only .NET libraries. Simulation.Application generates requests, exposes the baseline, orchestrates runs and projects reports. The existing UI configures requests and displays reports. Infrastructure remains reserved for future persistence.

## Resources and capacity

```text
Developer pool/day = DeveloperCount × DeveloperCapacityPerDay
Tester pool/day    = TesterCount × TesterCapacityPerDay
```

Capacity is an abstract work unit, **not hours**. There are two resource types. Code review and development consume the **same** developer pool; testing consumes only the tester pool. Review receives capacity first. Remaining developer capacity is available for development in the same day. Unused capacity does not carry forward.

For each item in an active stage, the allocator applies:

```text
min(remaining stage effort, remaining resource pool, 1.0, capacity per person)
```

Thus one item can never consume more than 1.0 capacity unit per day. Five developers with capacity 1 can instead advance five separate items by one unit each. A person with capacity 0.5 can apply at most 0.5 to one item per day. Capacity above 1 increases the aggregate pool but never the per-item limit; unused capacity is possible when too few items are active. Fractions left after a small review can be allocated to another item's development.

This is a pool model with a one-person-per-item cap, not a schedule of named individuals. Fractional allocations can represent sequential work on different items. Authors and reviewers are not tracked: a team with one developer can review its own modeled work. No individual skill or self-review constraint is implemented.

Nominal Team capacity is kept separate from the daily pool variables in the engine. Future capacity reductions can be introduced at that boundary; meetings, support, absence and similar adjustments are not implemented.

## Explicit efforts

A story can require 5 development units, 1 review unit and 2 testing units. These are independent inputs; none is computed from Size, Complexity or a review factor. Partial work persists in the relevant Remaining...Effort property. Each stage consumes only its own effort.

Efforts may be zero but cannot be negative or non-finite. Zero-effort stages still visit their active and waiting states under the normal daily rules. They require no resource units, even if that resource pool is zero. Positive effort does not advance with zero capacity.

A subtraction residue at most `initial effort × 1e-12` is rounded to zero after positive work has actually been applied. This avoids an extra day due solely to floating-point arithmetic, for example ten allocations of 0.1. Capacity ledger comparisons should use a numerical tolerance.

## States and workflow

```text
Backlog
  → Development
  → WaitingForCodeReview
  → CodeReview
  → WaitingForTesting
  → Testing
  → Done
```

Every edge is recorded; no state is skipped. Waiting states are real domain states with visible snapshot counts, not aliases for active states. Queue-entry times are captured by the preceding stage's completion timestamp.

**Active means admitted to the stage**, not guaranteed to receive work that day. For example, an admitted Development item can receive no capacity because review consumed the pool. Its daily work field will be zero. The separate waiting states represent items that have not yet been admitted to the next stage, usually because of its WIP limit or the day boundary.

There is no backlog-to-testing shortcut and no transition out of Done. A stage completes when its remaining effort reaches zero. Partial items retain their state and effort for the next day.

## Daily execution and timestamps

The smallest time unit is one working day. Day `d` is the interval `[d, d+1)`. The first day starts at 0; a 100-day scenario ends at time 100. All days have identical nominal capacity. Calendar dates and non-working days are not modeled.

The implementation deliberately uses a conservative day-boundary interpretation of the conceptual workflow:

1. At day start, count created unfinished items and dependency-blocked backlog items.
2. Admit eligible Backlog items into Development in FIFO order while its WIP policy permits. Dependencies are evaluated using the state at this boundary.
3. Admit existing WaitingForCodeReview items into CodeReview, and existing WaitingForTesting items into Testing, each in FIFO order while its WIP policy permits.
4. Sample the three active occupancies and initialize that day's developer and tester pools.
5. Allocate developer capacity to active CodeReview items in FIFO order. Completed reviews transition to WaitingForTesting at time `d+1`.
6. Allocate the remaining developer pool to active Development items in FIFO order. Completed development transitions to WaitingForCodeReview at time `d+1`.
7. Allocate tester capacity to active Testing items in FIFO order. Completed testing transitions to Done at time `d+1`.
8. Record end-of-day states, remaining efforts, work consumption and transitions.

**All admissions happen before all work.** A completion never frees a slot for another admission later in the same day. New waiting items enter their next active stage no earlier than the following day's start. A dependent item can start on the day whose start equals its predecessor's DoneDay.

This choice differs from admitting newly completed work downstream during the same day's later processing steps. It guarantees that an item receives capacity from at most one stage on a day and leaves queues visible at day end. No fractional-day or within-day scheduling is implemented.

Start timestamps record **admission** to an active state at `d`; completion timestamps record reaching zero effort at `d+1`. A waiting transition and subsequent admission can have the same numerical boundary timestamp, while remaining distinct ordered transitions. Waiting duration can therefore be zero when a slot is available at the next boundary. No artificial extra waiting day is added.

Example with sufficient available capacity and one item requiring 5/1/2 units:

| Working interval | Outcome |
|---|---|
| `[0,5)` | Five development allocations of at most 1; DevelopmentStartedDay 0, DevelopmentCompletedDay 5 |
| `[5,6)` | Review admitted at 5; CodeReviewCompletedDay 6 |
| `[6,8)` | Testing admitted at 6; TestingCompletedDay and DoneDay 8 |

At the end of engine day 4 the item is WaitingForCodeReview; at the end of day 5 it is WaitingForTesting. An active stage can start and finish between two end-of-day chart samples. Admission occupancy, transition history and work consumption still record that activity.

## FIFO

There are no priority classes and no random tie-breaking.

- Eligible backlog items: oldest CreatedDay first. Blocked and future items are skipped without preventing eligible items from starting.
- Active development allocation: earliest DevelopmentStartedDay first. An older, previously blocked backlog item does not displace an already admitted item.
- Review admission/allocation: earliest DevelopmentCompletedDay first.
- Testing admission/allocation: earliest CodeReviewCompletedDay first.
- Equal timestamps: original scenario collection order, maintained by stable ordering. IDs are not used to assign priority.

Review-stage precedence over development is the only resource-order policy. FIFO continues to apply within each stage. A partially served item retains its queue position.

## Dependencies and validation

A WorkItem leaves Backlog only when **all** referenced items are Done. Dependencies are direct, explicit IDs supplied on WorkItems. No random graph generation is present. Duplicate references to the same valid predecessor are redundant and have the same effect as one reference.

Before execution, ScenarioValidator rejects invalid configurations using ScenarioValidationException with an explanatory message:

- missing scenario/item name or ID, duplicate IDs, null team/collections/items;
- nonpositive SimulationDays or any active-stage WIP limit;
- negative headcounts, negative/non-finite capacities or overflowing aggregate capacity;
- negative/non-finite efforts or negative CreatedDay;
- execution-state items supplied as fresh backlog input;
- missing/blank dependency references, self-dependencies, and circular dependency graphs.

Cycle validation uses an iterative topological traversal, avoiding recursion on long chains. Zero personnel, zero capacity, zero efforts and an empty workload are valid experiments. Future CreatedDay values are supported and ignored for admissions until that day arrives.

## WIP policy

WipPolicy is the single admission/occupancy policy boundary:

| Limit | States counted in v0.1 |
|---|---|
| DevelopmentWipLimit | Development only |
| CodeReviewWipLimit | CodeReview only |
| TestingWipLimit | Testing only |

Waiting states, Backlog and Done do not consume active WIP slots. Waiting queues have no separate limits in this version. The engine consults the policy instead of duplicating membership assumptions at each admission point. Changing membership to include a waiting state belongs in this policy in a future version.

Daily active occupancy (`DevelopmentWip`, `ReviewWip`, `TestingWip`, and their legacy sum `Wip`) is sampled after admission, before work. These fields measure admission-policy occupancy. They are distinct from the new end-of-day `TotalWip`, which includes waiting queues and is used for `AverageWip`.

## Metrics and Interpretation

Metrics describe simulated system behaviour; they do not classify results as good or bad. Higher utilization or throughput alone is not an automatic recommendation. No bottleneck classifier is implemented.

`SimulationEngine` records observations; `SimulationResultBuilder` aggregates them in Core. Application and UI consume these results without redefining metrics. `WorkItemResult` is a detached immutable record with final state, all timestamps, remaining efforts, read-only transition history and time metrics. `State` is a compatibility alias of `FinalState`. Returned result, day, observation and transition collections are read-only copies; no mutable execution WorkItems escape the engine.

Day `d` represents `[d,d+1)`. Admission is stamped `d`, completion `d+1`; subtraction needs **no added 1**. Snapshot `Day` is zero-based; the existing UI labels it as end of day `Day+1`.

| Metric | Exact definition |
|---|---|
| SimulationDays | Configured working-day horizon, including idle days after work finishes |
| TotalWorkItems / IncompleteWorkItems | All configured items / total minus Done, including future arrivals |
| CompletedWorkItems | Items Done at the horizon |
| LeadTime | DoneDay − CreatedDay; null for incomplete items |
| CycleTime | DoneDay − DevelopmentStartedDay; null for incomplete items |
| ActiveTime | Number of observed days with positive development, review **or** testing allocation; each day counts at most once regardless of work amount |
| WaitingForCodeReviewTime / WaitingForTestingTime | Whole working intervals spent in the corresponding waiting state **after start-of-day admissions** |
| WaitingTime | Sum of those two queue times; ordinary Backlog waiting is excluded |
| BlockedTime | Created Backlog intervals with at least one incomplete dependency at day start; WIP-only backlog delay is excluded |
| AverageLeadTime / CycleTime / ActiveTime / WaitingTime / BlockedTime | Arithmetic means over **Done items only**, or 0 when none are Done |
| Throughput / ThroughputPerDay | CompletedWorkItems / SimulationDays |
| ThroughputPerFiveDays | ThroughputPerDay × 5 working days |
| TotalWip | End-of-day Development + WaitingForCodeReview + CodeReview + WaitingForTesting + Testing; excludes Backlog and Done |
| AverageWip | Arithmetic mean of daily TotalWip across the entire horizon |
| MaximumWaitingForCodeReviewQueue / MaximumWaitingForTestingQueue | Maximum corresponding **end-of-day** queue count; not a within-day peak |
| AvailableDeveloperCapacity / AvailableTesterCapacity | Nominal team pool for that day, even if there is no work |
| UsedDeveloperCapacity / UsedTesterCapacity | Actual development + review allocation / actual testing allocation for that day |
| DeveloperUtilization / TesterUtilization | Sum of corresponding used capacity divided by sum of available capacity over all simulated days; 0 when available capacity is 0 |

Every daily snapshot exposes all seven end-of-day state counts, TotalWip, available/used resource capacities, stage work and detailed item observations. Items not yet created are excluded from daily counts and elapsed-time accumulation. Incomplete items retain observed active, queue and blocked times but have no completed lead/cycle time.

A queue transition at boundary 5 followed by admission at boundary 5 has zero elapsed queue time, even though the preceding day's end snapshot shows an item in that queue. Thus summing end-of-day queue counts is **not** the waiting-time definition. Waiting measurement uses the state after admissions for the interval. Future chart consumers should preserve this distinction.

An active-state item can receive zero capacity. Such an interval counts neither as ActiveTime nor as queue WaitingTime. Zero-effort stages also consume an interval under the existing workflow but contribute no ActiveTime. Consequently ActiveTime + WaitingTime need not equal CycleTime; these measurements are not an exhaustive time partition. LeadTime can additionally contain ordinary backlog wait and dependency-blocked time. Stage StartedDay records admission, not first positive capacity allocation.

Legacy report metrics remain available: BlockedTimeFraction is dependency-blocked backlog item-days divided by created unfinished item-days at day start. DevelopmentUtilization and ReviewUtilization split the shared developer capacity denominator. AverageReviewWip samples active review occupancy after admission. AverageReviewTime averages review completion minus admission for items that exited review, including active-state stalls but excluding its preceding queue.

Application reports count only items created before the relevant snapshot/horizon. Unfinished age is horizon − CreatedDay; cycle age is horizon − DevelopmentStartedDay, or absent if never admitted. Dependency blocking at the horizon uses final predecessor states, which may differ from the last day's starting sample.

## Baseline and demonstration

`Simulation.Application.BaselineScenario.Create()` returns a fresh baseline, also represented by the default SimulationRequest and initial UI values:

| Setting | Value |
|---|---:|
| SimulationDays | 100 |
| DeveloperCount / TesterCount | 5 / 2 |
| Capacity per developer / tester per day | 1 / 1 |
| Development / CodeReview / Testing WIP | 5 / 3 / 3 |
| WorkItems | 30, IDs STORY-1 through STORY-30 in that order |
| Development / CodeReview / Testing effort per item | 5 / 1 / 2 |
| CreatedDay | 0 for all |
| Dependencies | None |

No delivery target or bottleneck classification is attached to the baseline. Its results are computed by the same engine as any other scenario.

The existing Avalonia form supports independent effort inputs, stage WIP, single runs and deterministic resource-only A/B comparisons with the same workload. Core supports explicit mixed-effort items and dependencies; the form currently generates independent, identical-effort items. The application-level UI request retains its safety bounds of 2,000 items, 3,650 days and 1,000,000 item-days.

## Determinism and exclusions

The engine uses no Random, seed, wall clock, unordered allocation or parallel scheduling. Reusing the same ordered scenario produces identical states, work allocations and timestamps. As with all floating-point software, this is not a promise of byte-identical arithmetic across hypothetical different numerical implementations.

The previous seeded batches, Monte Carlo interface, generic size/complexity effort derivation, sprint and release modeling, priorities and random dependencies were removed from the current execution path. They are not silently ignored settings.

No bugs, rework, technical debt, UX/PO/requirements roles, expedite classes, interruptions, support work, meetings, sickness, individual skill/productivity profiles, specialists, pairing, multiple teams, hardware dependencies, release trains, compliance/CRA, DevOps or AI simulation is implemented. The chart UI is presentation, not a modeled UX resource.

The historical reference-experiment document predates this version and is explicitly marked as superseded. Its old numerical expectations must not be used as v0.1 acceptance tests without re-derivation.
