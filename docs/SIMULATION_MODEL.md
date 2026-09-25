# Simulation Model v0.1 — Steps 2–8

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

Capacity is an abstract work unit, **not hours**. There are two resource types. Code review, rework and development consume the **same** developer pool; testing consumes only the tester pool. DeveloperCapacityPolicy allocates that pool in order: CodeReview, Rework, Development. Unused capacity does not carry forward.

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

Every edge is recorded; no state is skipped. Waiting states are real domain states with visible snapshot counts, not aliases for active states. Queue-entry times are recorded on every queue entry, including repeat visits after rework.

**Active means admitted to the stage**, not guaranteed to receive work that day. For example, an admitted Development item can receive no capacity because review consumed the pool. Its daily work field will be zero. The separate waiting states represent items that have not yet been admitted to the next stage, usually because of its WIP limit or the day boundary.

There is no backlog-to-testing shortcut and no transition out of Done. A stage completes when its remaining effort reaches zero. Partial items retain their state and effort for the next day.

## Daily execution and timestamps

The smallest time unit is one working day. Day `d` is the interval `[d, d+1)`. The first day starts at 0; a 100-day scenario ends at time 100. All days have identical nominal capacity. Calendar dates and non-working days are not modeled.

The implementation deliberately uses a conservative day-boundary interpretation of the conceptual workflow:

1. At day start, count created unfinished items and dependency-blocked backlog items.
2. Admit eligible Backlog items into Development in FIFO order while its WIP policy permits. Dependencies are evaluated using the state at this boundary.
3. Admit existing WaitingForCodeReview items into CodeReview, and existing WaitingForTesting items into Testing, each in FIFO order while its WIP policy permits. Admit WaitingForRework into Rework under its own WIP limit.
4. Sample the four active occupancies and initialize that day's developer and tester pools.
5. Allocate developer capacity to active CodeReview items in FIFO order. At completion, inspect for a defect: transition to WaitingForTesting on success or WaitingForRework on failure at time `d+1`.
6. Allocate the remaining developer pool to active Rework items, then active Development items in FIFO order. Completed rework or development transitions to WaitingForCodeReview at time `d+1`.
7. Allocate tester capacity to active Testing items in FIFO order. At completion, inspect for a defect: transition to Done on success or WaitingForRework on failure at time `d+1`.
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
- Review admission/allocation: earliest current review-queue entry first.
- Testing admission/allocation: earliest current testing-queue entry first.
- Rework admission/allocation: earliest current rework-queue entry first.
- Equal timestamps: original scenario collection order, maintained by stable ordering. IDs are not used to assign priority.

CodeReview → Rework → Development precedence is the resource-order policy. FIFO continues to apply within each stage. A partially served item retains its queue position.

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
| Quality.ReworkWipLimit | Rework only |

Waiting states, Backlog and Done do not consume active WIP slots. Waiting queues have no separate limits in this version. The engine consults the policy instead of duplicating membership assumptions at each admission point. Changing membership to include a waiting state belongs in this policy in a future version.

Daily active occupancy (`DevelopmentWip`, `ReviewWip`, `TestingWip`, `ReworkWip`, and their legacy sum `Wip`) is sampled after admission, before work. These fields measure admission-policy occupancy. They are distinct from the new end-of-day `TotalWip`, which includes waiting queues and is used for `AverageWip`.

## Metrics and Interpretation

Metrics describe simulated system behaviour; they do not classify results as good or bad. Higher utilization or throughput alone is not an automatic recommendation. No bottleneck classifier is implemented.

`SimulationEngine` records observations; `SimulationResultBuilder` aggregates them in Core. Application and UI consume these results without redefining metrics. `WorkItemResult` is a detached immutable record with final state, all timestamps, initial and remaining efforts, read-only transition history and time metrics. `State` is a compatibility alias of `FinalState`. Returned result, day, observation and transition collections are read-only copies; no mutable execution WorkItems escape the engine.

Day `d` represents `[d,d+1)`. Admission is stamped `d`, completion `d+1`; subtraction needs **no added 1**. Snapshot `Day` is zero-based; the existing UI labels it as end of day `Day+1`.

| Metric | Exact definition |
|---|---|
| SimulationDays | Configured working-day horizon, including idle days after work finishes |
| TotalWorkItems / IncompleteWorkItems | All configured items / total minus Done, including future arrivals |
| CompletedWorkItems | Items Done at the horizon |
| LeadTime | DoneDay − CreatedDay; null for incomplete items |
| CycleTime | DoneDay − DevelopmentStartedDay; null for incomplete items |
| ActiveTime | Number of observed days with positive development, review, rework **or** testing allocation; each day counts at most once regardless of work amount |
| WaitingForCodeReviewTime / WaitingForTestingTime / WaitingForReworkTime | Whole working intervals spent in the corresponding waiting state **after start-of-day admissions** |
| WaitingTime | Sum of those three queue times; ordinary Backlog waiting is excluded |
| BlockedTime | Created Backlog intervals with at least one incomplete dependency at day start; WIP-only backlog delay is excluded |
| AverageLeadTime / CycleTime / ActiveTime / WaitingTime / BlockedTime | Arithmetic means over **Done items only**, or 0 when none are Done |
| Throughput / ThroughputPerDay | CompletedWorkItems / SimulationDays |
| ThroughputPerFiveDays | ThroughputPerDay × 5 working days |
| TotalWip | End-of-day Development + WaitingForCodeReview + CodeReview + WaitingForTesting + Testing + WaitingForRework + Rework; excludes Backlog and Done |
| AverageWip | Arithmetic mean of daily TotalWip across the entire horizon |
| MaximumWaitingForCodeReviewQueue / MaximumWaitingForTestingQueue | Maximum corresponding **end-of-day** queue count; not a within-day peak |
| AvailableDeveloperCapacity / AvailableTesterCapacity | Nominal team pool for that day, even if there is no work |
| UsedDeveloperCapacity / UsedTesterCapacity | Actual development + review + rework allocation / actual testing allocation for that day |
| DeveloperUtilization / TesterUtilization | Sum of corresponding used capacity divided by sum of available capacity over all simulated days; 0 when available capacity is 0 |

Every daily snapshot exposes all nine end-of-day state counts, TotalWip, available/used resource capacities, stage work and detailed item observations. Items not yet created are excluded from daily counts and elapsed-time accumulation. Incomplete items retain observed active, queue and blocked times but have no completed lead/cycle time.

A queue transition at boundary 5 followed by admission at boundary 5 has zero elapsed queue time, even though the preceding day's end snapshot shows an item in that queue. Thus summing end-of-day queue counts is **not** the waiting-time definition. Waiting measurement uses the state after admissions for the interval. Future chart consumers should preserve this distinction.

An active-state item can receive zero capacity. Such an interval counts neither as ActiveTime nor as queue WaitingTime. Zero-effort stages also consume an interval under the existing workflow but contribute no ActiveTime. Consequently ActiveTime + WaitingTime need not equal CycleTime; these measurements are not an exhaustive time partition. LeadTime can additionally contain ordinary backlog wait and dependency-blocked time. Stage StartedDay records admission, not first positive capacity allocation.

Legacy report metrics remain available: BlockedTimeFraction is dependency-blocked backlog item-days divided by created unfinished item-days at day start. DevelopmentUtilization and ReviewUtilization split the shared developer capacity denominator. AverageReviewWip samples active review occupancy after admission. AverageReviewTime averages completion minus admission across completed review attempts, including active-state stalls but excluding its preceding queue.

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

The Avalonia form supports fixed or triangular effort independently per stage, stage WIP, seeded single runs and Monte Carlo. The UI has Scenario, Flow, Results and Monte Carlo areas. The older Application-level comparison helpers remain tested, but comparison is not exposed in the UI. Core supports explicit mixed-effort items and dependencies; the form generates independent items with fixed or sampled effort. The application-level UI request retains its safety bounds of 2,000 items, 3,650 days and 1,000,000 item-days.

## Determinism and exclusions

The engine uses no wall clock, unordered allocation or parallel scheduling. Step 6 generates initial effort before execution using an explicit seed; Step 7 uses separate seeded streams for optional defect discovery and rework effort. Reusing the same distributions, ordered workload, configuration and seed produces identical generated effort, states, work allocations, timestamps and metrics. As with all floating-point software, this is not a promise of byte-identical arithmetic across hypothetical different numerical implementations.

The previous incremental model’s random ordering, generic size/complexity effort derivation, sprint/release modeling, priorities and random dependencies remain absent. Step 6 introduces explicit effort distributions and a separate Monte Carlo result model; it does not restore those older rules.

No separate bug backlog, escaped production defects, technical debt, UX/PO/requirements roles, expedite classes, interruptions, support work, meetings, sickness, individual skill/productivity profiles, specialists, pairing, multiple teams, hardware dependencies, release trains, compliance/CRA, DevOps or AI simulation is implemented. The chart UI is presentation, not a modeled UX resource.

The historical reference-experiment document predates this version and is explicitly marked as superseded. Its old numerical expectations must not be used as v0.1 acceptance tests without re-derivation.

## Step 5 — visual inspection

The Step 5 presentation changes did not modify the engine or metrics. Step 6 adds generation and Monte Carlo orchestration without changing daily flow rules. The ViewModel calls `SimulationRunner`, exposes the immutable `SimulationResult`, and formats values. It never advances WorkItems or calculates simulation metrics. Daily charts select existing `DailySnapshot` values and only calculate drawing coordinates/scales.

- **Scenario** groups simulation length/workload, team capacity, active WIP limits and effort. Text and tooltips explain every field. Construction and Reset to Baseline both read the existing `BaselineScenario.Create()` factory, whose configuration comes from SimulationRequest defaults.
- **Flow** shows all seven state counts for one end-of-day snapshot. Active and waiting states differ by text as well as colour. UI day 1 selects `Days[0]`; the last UI day selects `Days[SimulationDays-1]`. Changing this selection never executes the engine. The first day is selected after a run.
- **Charts** show TotalWip, both waiting queue counts, and used versus available developer/tester capacity. Lines connect daily observations, not intra-day estimates. Developer usage includes both Development and Code Review. There is no automatic classification, smoothing or new aggregation.
- **Results** contains twelve explained summary metrics and a read-only Work Items table. Time averages are completed-only working days; utilization uses percentage formatting. Dates in the item table remain the domain's zero-based boundary timestamps. Incomplete lead/cycle times remain blank.

Results remain the last completed run when scenario fields are edited. A new run clears/replaces them; reset clears results and returns to Scenario. No full scenario comparison/history, editing of results, sorting/filtering, export, individual stage WIP series or charting dependency is added. Monte Carlo in Step 6 has its own aggregate tab. Scroll areas keep the interface usable at smaller window sizes.

See [README — Using the application](../README.md#using-the-application) for the configure → run → inspect → change-one-parameter workflow.

The visualization preserves two potentially surprising model outcomes: active states can contain items receiving no work; an end-of-day waiting state can have zero elapsed queue time if the next boundary admits it immediately. Neither is hidden or reclassified. Metrics are descriptive and must be interpreted together: high utilization is not automatically good, and high WIP is not automatically bad.

## Variation

Step 6 introduces **effort variation only**. Developer and tester headcounts and daily per-person capacities remain fixed for each scenario. Development, review and testing effort are sampled once when a request becomes a concrete scenario. These actual values remain fixed for each WorkItem throughout the run. Initial efforts are not resampled on repeat inspections. Step 7 separately samples additional rework on each defect. Random ordering, capacity variation, correlated stages and person-level effects are not implemented.

`Simulation.Core.EffortGenerator` constructs the same ordered STORY-1 … STORY-N workload as before. `SimulationRequest` chooses each stage's distribution and seed, then passes concrete items to `SimulationEngine`. Existing Core callers supplying explicit items retain exactly those effort values; the engine never replaces them based on seed. `SimulationScenario.RandomSeed` and `SimulationResult.RandomSeed` record provenance. `WorkItemResult` additionally exposes actual DevelopmentEffort, CodeReviewEffort and TestingEffort.

## Effort Distributions

`IEffortDistribution.Sample(IRandomSource)` has two built-in immutable implementations:

- **FixedEffort** returns the configured finite, nonnegative effort exactly and consumes no random draw. Zero preserves the previous zero-effort behavior.
- **TriangularEffort** requires finite `0 < Minimum <= MostLikely <= Maximum`. MostLikely is the mode, not the mean. Equal Minimum/Maximum is valid and returns that constant without a draw. Endpoint modes are supported.

For minimum `a`, mode `m`, maximum `b`, a draw `u` in `[0,1)` and `c=(m-a)/(b-a)`, inverse-CDF sampling is:

```text
u < c : a + (b-a) × sqrt(u × c)
else  : b - (b-a) × sqrt((1-u) × (1-c))
```

The final value is clamped to `[a,b]` against numerical residue. Actual effort is not rounded to whole days. The existing daily allocator still applies at most 1 unit per item/day and at most one stage per interval. Fractional work may therefore leave unused capacity or occupy a whole working interval, exactly as before.

Each stage uses separate distribution parameters. Successive PRNG draws represent uncorrelated effort samples across items/stages; no shared latent size factor is modeled. Switching a stage from Fixed to Triangular changes random-stream consumption and can change subsequent stages' draws at the same seed. The reproducibility guarantee concerns the same complete configuration, not paired common draws after configuration changes.

Baseline remains Fixed 5/1/2. `BaselineScenario.VariableEffortExample()` changes only the name and effort distributions, preserving the baseline team, WIP, duration, item count and seed:

| Stage | Minimum | Most Likely | Maximum | Theoretical mean |
|---|---:|---:|---:|---:|
| Development | 2 | 5 | 12 | 6.3333 |
| Code Review | 0.5 | 1 | 3 | 1.5 |
| Testing | 1 | 2 | 5 | 2.6667 |

This is an illustrative preset, not a claim about Easy-Laser. Its means exceed the Fixed baseline's 5/1/2. Comparing them explores both dispersion and increased mean work; it does not isolate variance at equal mean effort. No parameters are altered to hide this distinction.

## Random Seeds

Default RandomSeed is 12345; any signed 32-bit integer is accepted. One `SeededRandom` instance is created per workload. It implements SplitMix64 explicitly with unchecked 64-bit arithmetic: increment `0x9E3779B97F4A7C15`, xor/shift/multiply mixing constants `0xBF58476D1CE4E5B9` and `0x94D049BB133111EB`, and the standard shifts 30, 27, 31. The initial state is the seed's unsigned 32-bit bit pattern, widened to 64 bits. The upper 53 output bits divided by `2^53` produce a double in `[0,1)`.

This avoids global randomness and dependence on `System.Random` implementation versions. Generation order is item order, then Development → Code Review → Testing. Fixed and degenerate Triangular stages do not consume draws. A pinned-sequence test and inverse-CDF tests cover the implementation. Different seeds normally change variable effort; Fixed effort ignores randomness. With defects disabled, identical Fixed metrics across seeds are expected, although the reported seed metadata differs.

For zero-based run index `i`, Monte Carlo derives `seed_i = unchecked(baseSeed + i)` using signed 32-bit wraparound. Displayed Run Number is `i+1`; run 1 matches a single run at the base seed. Each run gets its own PRNG state, independent of execution order. The current runner deliberately executes sequentially.

## Monte Carlo Simulation

`Simulation.Application.MonteCarloRunner` runs the same Application/Core single-run path repeatedly. `MonteCarloRequest` defaults to 500 runs and validates 1–10,000 runs, the existing single-run bounds, and at most 100,000,000 total item-days. Requests are immutable. Only compact immutable per-run metrics survive each iteration; full WorkItem/day histories are not retained for every run.

`MonteCarloResult` is separate from `SimulationResult`. It records run count, base seed, runs with completions, all ordered run numbers/seeds and the following per-run observations and distributions: completed items, throughput per five days, average lead/cycle time, average WIP, developer/tester utilization and maximum end-of-day review/testing queues.

Each run contributes one observation per metric, not one per WorkItem. Average lead/cycle times use completed items within that run, as before. **Runs with zero completions are excluded from lead/cycle percentile and histogram samples**, since they have no observed completion time; SampleCount and RunsWithCompletions expose this explicitly. Their raw single-run means remain 0 under the existing single-run convention. Other aggregate metrics include every run. If no run has completions, time percentiles are null and the histogram empty, rendered as —/no observations. Even runs with some completions omit unfinished-item times, so short horizons can introduce completion-selection bias. These are not percentiles of all individual item lead times.

The ViewModel invokes this runner through `Task.Run`, keeping generation/orchestration/statistics outside Avalonia. Progress reports completed run count; the UI displays `Running simulation N / total`. Cancellation is checked between runs and by the existing engine each day. Cancelled or failed batches publish no partial aggregate. Runtime is measured externally and not embedded in reproducible results.

Monte Carlo outputs describe the simulation model under repeated generated effort. They are **not predictions of real project outcomes** unless the model and distributions have been calibrated with real data. No outcome is automatically labeled good/bad, healthy/unhealthy or a bottleneck.

## Percentiles

`DistributionStatistics` is the authoritative calculation, using linear interpolation (Hyndman–Fan type 7). For sorted observations `x[0..n-1]` and probability `p`, use `h=(n-1)*p`, `lo=floor(h)`, `hi=ceil(h)` and `x[lo] + (h-lo)*(x[hi]-x[lo])`. Equal bracketing observations return that value exactly. An overflow-safe weighted form handles opposite-sign extreme values. Empty samples return null; a singleton returns itself. Non-finite observations or probabilities outside `[0,1]` are rejected.

P50, P75, P85 and P95 use p=.50/.75/.85/.95. For `[0,10,20,30]` they are 15, 22.5, 25.5 and 28.5. Interpolated count percentiles may be fractional even though individual run counts are integers. Values describe percentiles, not probabilities of meeting a delivery commitment or statistical confidence intervals.

Histogram counts are also computed in Application. Ten equal-width bins span the observed minimum to maximum, left-inclusive/right-exclusive except the final bin includes the maximum. Identical observations use one bar; empty samples use no bars. UI charts only draw these bins. The two histograms show per-run Average Lead Time and Throughput / 5 days.

## Step 6 UI and observed experiments

Scenario includes Random Seed, Number of Runs and independent Fixed/Triangular editors for all three stages. Only inputs for the selected distribution are parsed. Reset to Baseline restores Fixed 5/1/2, seed 12345 and 500 runs. Variable Effort Example restores all baseline team/WIP settings before applying the preset distributions.

Run Simulation updates Flow and Results, including generated effort columns (displayed to three decimals; full values on hover). Run Monte Carlo updates only its separate tab, retaining the last single-run data for inspection. Both result types identify their own seed. Editing inputs does not retroactively change either result; reset clears both. Previous Monte Carlo output is cleared when a new batch begins.

See [VARIATION_RESULTS.md](VARIATION_RESULTS.md) for measured Fixed/variable runs, all 500-run percentiles and execution time. Notably, all 500 preset runs finish 30 items within 100 days, so throughput has no spread at that horizon despite varying lead/cycle times. Waiting queues can also have smaller maxima than Fixed when stage completions are less synchronized. These are observations, not hard-coded classifications or adjustments to the model.


## Defects

Step 7 adds optional defects owned by the original WorkItem, without separate bug tickets. `SimulationScenario.Quality` holds immutable `DefectSettings`. Defaults are Enabled=false, both probabilities=0, ReworkWipLimit=3, fixed review-origin rework effort=1 and testing-origin effort=2. Ratios must be finite in [0,1], the WIP limit positive, and both distributions present. Domain validation also checks disabled configuration; the UI creates valid defaults when its quality controls are disabled.

These are scenario assumptions, not inferred quality or individual skill. Disabling defects preserves all previous state, timing, capacity and metric behavior. Enabling defects does not change generated initial efforts.

## Defect Discovery

Each completed CodeReview or Testing attempt makes one probability check, including zero-effort attempts. Incomplete attempts do not make checks. A failed check creates one defect with source and assigned rework effort at completion boundary d+1. Probability 0 always succeeds; probability 1 always finds a defect. Neither endpoint consumes a discovery draw.

The existing SplitMix64 implementation supplies two run-local streams: discovery uses `seed XOR 0xD3FEC701`, rework effort uses `seed XOR 0xA11CE702` (unchecked signed 32-bit patterns). They are separate from initial effort generation and from each other. Inspection processing follows the deterministic stage/FIFO allocation order. Both discovery sources share the discovery stream; both effort sources share the rework stream. Identical full configuration and seed reproduce all events. Changing configuration can change scheduling and subsequent random draws.

## Rework

On each defect, sample the configured source-specific FixedEffort or TriangularEffort distribution. RemainingReworkEffort starts at that sampled effort and decreases only through actual Rework allocations. No fixed relationship to the original development effort is imposed. Zero rework still visits its waiting and active states under normal day-boundary rules.

## Feedback Loops

```text
CodeReview --success--> WaitingForTesting --> Testing --success--> Done
    |                                           |
    +--defect--> WaitingForRework <--defect-------+
                         |
                       Rework
                         |
                WaitingForCodeReview --> CodeReview
```

Both defect sources return through review and testing. Each admission resets that inspection's remaining effort to its full original effort; repeat inspections consume capacity again and can find another defect. Queue-entry days reset on each visit, so returning work joins FIFO at its new arrival time. Dependencies remain blocked until the predecessor is actually Done.

There is no artificial limit on loops. The finite simulation horizon bounds execution. A 100% failure probability can leave every affected item unfinished; incomplete lead/cycle times remain null. No outcome is adjusted to force completion.

First review/testing start timestamps are retained. Their completion timestamps represent the most recent completed attempt, including failures. `InspectionAttempts` preserves every admission, completion and outcome; unfinished attempts have null completion/outcome. Ordered immutable `Events` contain transitions, positive capacity applications and defect discoveries. Capacity events are stamped at interval start d; completion/discovery events at boundary d+1. This is result history, not an event-sourcing framework.

## Rework Capacity

CodeReview → Rework → Development share one developer pool in that priority order. Rework has the same per-item/day cap as development and its own active-only WIP limit. WaitingForRework does not consume that limit. All admissions precede work, so defects cannot trigger same-day rework. Daily snapshots expose WaitingForReworkCount, ReworkCount, ReworkWip and UsedReworkDeveloperCapacity. TotalWip includes both new states; developer utilization includes consumed rework.

## Quality Metrics

| Metric | Definition |
|---|---|
| TotalDefectsFound / CodeReviewDefectsFound / TestingDefectsFound | Recorded discovery events, overall or by source, including unfinished items |
| WorkItemsWithDefects | Distinct items with at least one discovery |
| EverRequiredRework | Item has at least one discovery, even if assigned effort is zero or still queued |
| ReworkCount / TotalReworkCount | Started rework episodes (admissions), per item / summed; includes unfinished active episodes, excludes episodes still waiting |
| TotalReworkEffort | Actual capacity consumed in Rework, per item or summed; not assigned outstanding effort |
| RequiredReworkEffort / RemainingReworkEffort | Per-item cumulative assigned effort / current outstanding rework effort |
| ReworkActiveTime | Item days with positive Rework allocation |
| WaitingForReworkTime | Whole intervals still in that queue after admissions; included in WaitingTime |
| AverageReworkEffortPerCompletedItem | Mean consumed rework effort over Done items, zero if none |
| ReworkDeveloperCapacityShare | Consumed rework / all consumed developer capacity (development + review + rework), zero if denominator zero |
| CodeReviewAttempts / TestingAttempts | Admissions into that inspection, including unfinished attempts |
| AverageCodeReviewAttempts / AverageTestingAttempts | Means over items created before the horizon, including zero-attempt and incomplete items; zero for no such items |

Repeat review and testing increase their ordinary capacity measures. Their additional cost is **not** classified as Rework effort or included in its share numerator. ActiveTime counts actual positive work in any stage once per day. Existing queue and time conventions remain unchanged. These metrics describe the model, not whether quality or utilization is good or bad.

Monte Carlo retains the original nine distributions and adds TotalDefectsFound, WorkItemsWithDefects, TotalReworkEffort and ReworkDeveloperCapacityShare. All runs contribute to these four distributions, including those with no completions.

The UI adds Quality & Rework controls with percentage inputs, two source-specific effort editors, a feedback branch with selected-day counts, two rework charts, quality result rows, per-item quality columns and an Item History selector. ViewModels format immutable results; they contain no discovery or simulation rules. Reset disables defects and clears history. The Defects & Rework Example starts from Variable Effort Example with 15% review defects, 10% testing defects, WIP 3 and triangular rework 0.5/1/3 and 1/2/5 respectively. It is illustrative, not calibrated organizational data.

Measured runs and remaining interpretation caveats are in [DEFECT_RESULTS.md](DEFECT_RESULTS.md).

## Model Validation

Step 8 measures the existing engine without changing Core. A simulator is useful only if its response to parameter changes can be understood and validated. `SensitivityAnalysisRunner`, `AnalysisMetrics` and `ModelValidationRunner` live in Application. They call the real SimulationRunner/SimulationEngine; no analytical capacity shortcut replaces execution. UI code selects requests and formats immutable measurements. Core source files are unchanged from Step 7.

A weak response need not indicate an incorrect model: another constraint can dominate. Extra developers may have little throughput effect when testing capacity or Development WIP already constrains flow. Reports provide observations, not automatic bottleneck detection, optimum staffing or organizational recommendations.

## Sensitivity Analysis

`SensitivityAnalysisRequest` contains a captured immutable SimulationRequest, parameter, ordered values, mode, runs per point and WarmUpDays. The base is evaluated separately even if its value is absent from the supplied list. Each point changes only the selected field. Supplied order and duplicate values are preserved in results; the chart orders X values for presentation. No quality flag, capacity, effort bound or WIP limit is silently adjusted. Quality parameter sweeps require defects already enabled in the base. The current UI supports all existing presets, Current Scenario form, Steady Flow Validation and Defect Stress Validation.

Supported parameters are Developers, Testers, all four active WIP limits, all three initial stage efforts, and both discovery probabilities. Effort sweeps change the fixed value for Fixed distributions, or only MostLikely for Triangular distributions. Minimum and Maximum stay unchanged; invalid modes fail validation before any simulation executes. An explicit generated distribution takes precedence over the request's fallback fixed field, as in ordinary runs.

Standard ranges are editable illustrations:

- Developers/Testers: 1, 2, 3, 4, 5, 6, 8, 10.
- WIP: 1, 2, 3, 5, 8, 10, 15 for each stage.
- Development effort: 1, 2, 3, 5, 8, 13.
- Review/Testing effort: 0.5, 1, 2, 3, 5, 8, 13.
- Probabilities: 0, 0.05, 0.10, 0.20, 0.30, 0.50, 0.70, entered as ratios.

A range is not automatically clipped to triangular bounds. Counts must be integers, WIP positive, effort valid, and probabilities finite in [0,1]. Quality metrics are omitted from measurement dictionaries when defects are disabled; UI table cells then show n/a.

Single Run uses the same seed at every point and at the base. Monte Carlo defaults to 100 runs per point, with the same `unchecked(baseSeed + runIndex)` sequence for each point and base. This is reproducibility, not a guarantee that different configurations discover defects in corresponding items: scheduling and draw order may change. P50/P85/P95 (and existing P75) use the established DistributionStatistics implementation. For completed-item metrics, runs without completions in the measurement window are excluded, with explicit sample counts and null percentiles if no samples exist. Other metrics include every run.

The plotted/table value is the single observation or Monte Carlo P50. Selected-metric P85/P95 and all-metric distributions/sample counts are available. Delta is point minus base for the **measurement-window P50**, not a percentile of paired per-run differences. Percentage delta is `(point - base) / abs(base) × 100`; it is absent for a zero/missing base or missing point. Absolute delta is absent when either value is absent. Ratio differences can be multiplied by 100 for percentage points; this differs from relative percentage change.

Analysis allows 1–100 values, 1–10,000 runs per point, existing per-run limits, and 500,000,000 total item-days including the extra base evaluation. All points are validated before execution. Work happens off the UI thread, with progress and cancellation; cancellation publishes no partial result and keeps the preceding completed result. The configuration shown with results is the captured configuration, not subsequently edited form fields. Closing the window cancels both simulation and analysis work.

## Steady-State Analysis

Steady Flow Validation is a separate preset: 250 days, 1,000 items, 5 developers, 2 testers, per-person capacity 1/1, Development/Review/Testing WIP 10/5/10, Fixed effort 5/1/2 and defects disabled. The original 30-item baseline remains unchanged. The default analysis warm-up is 50 days. The preset maintains a backlog during the measured cases; it does not prove stochastic stationarity or stable queues for every parameter choice.

Full Simulation and Measurement Window distributions are explicitly separated. Full Simulation uses [0, SimulationDays); Measurement Window uses [WarmUpDays, SimulationDays). The word steady-state describes the intended experiment, not a certified equilibrium. In an overloaded configuration, waiting queues can continue growing throughout the window.

## Warm-Up Period

Require `0 <= WarmUpDays < SimulationDays`. The entire simulation still executes from day 0 with its original full daily snapshots. Analysis filters observations only after the run. It retains compact per-run measurements rather than keeping every run's item/day histories in the aggregate result. The ordinary single-run engine still returns all daily snapshots.

| Measurement | Exact window convention |
|---|---|
| CompletedWorkItems | DoneDay > WarmUpDays and DoneDay <= SimulationDays |
| ThroughputPerFiveDays | Window completions / (SimulationDays − WarmUpDays) × 5 |
| AverageLeadTime / CycleTime / ActiveTime / WaitingTime | Mean **whole-item lifetime** measurements for items completed in the window, including work/wait before warm-up; null when no window completions |
| AverageWip | Mean existing end-of-day TotalWip on snapshot days >= WarmUpDays |
| Utilization | Window used capacity / window available capacity; 0 if denominator is 0 |
| Average available/used capacity | Mean corresponding daily ledger values in the window |
| WIP saturation | Fraction of window days whose **after-admission, before-work occupancy** reaches the configured active limit |
| Average/max waiting queues | Mean/max end-of-day waiting count in the window, including each rework queue when applicable |
| AverageBacklog | Mean end-of-day Backlog count in the window |
| TotalDefectsFound | Discoveries at completion boundaries > WarmUpDays and <= SimulationDays |
| TotalReworkEffort | Actual consumed Rework capacity in window daily ledgers |
| ReworkDeveloperCapacityShare | Window consumed Rework / window total used developer capacity, 0 for zero denominator |

Completions exactly at the warm-up boundary belong to the excluded interval. Work during day WarmUpDays belongs to the included interval. There is no reset of remaining effort, queues, WIP or random streams. Completed-item time metrics are a completion cohort, not clipped exposure within the window. This avoids redefining lead/cycle time while making completion-selection bias explicit. Full analysis time means are null with no completions, while the existing ordinary SimulationResult retains its historical zero convention; the underlying result is unchanged.

Saturation ratios internally use 0–1 (1 = 100% of measured days). Admission occupancy can be saturated even though an end-of-day active count is smaller after completions. Saturation alone does not establish a binding limit. Similarly, summing end-of-day queue counts is not item WaitingTime, which uses interval states after admissions.

## Extreme Tests

Run Model Validation executes these **paired experiments**, separately from one-parameter sensitivity. Default horizon/items/warm-up are 250/1000/50 and seed 12345:

1. Testing Constraint: A has 5 developers, 1 tester, effort 5/1/10 and WIP 10/5/10; B changes tester count to 10 and testing effort to 1. Both changes are explicit; the combined effect cannot be attributed to one parameter.
2. Development Capacity: A has 1 developer, B has 10; both have 20 testers, Testing WIP 20 and testing effort 1, with all remaining Steady Flow values retained.
3. Defect/Rework Stress: both use Steady Flow with defects enabled, Rework WIP 3 and triangular rework effort 1/3/6 (review) and 2/5/10 (testing). A uses 0/0 discovery probability and B uses 0.7/0.7. The stress comparison changes both probabilities explicitly.

Reports show complete A/B configurations, all window metrics/diagnostics, absolute and relative deltas, and an investigation signal confined to these predefined tests. An "Unexpectedly weak model response" warning is emitted only when **all applicable** throughput, cycle time, waiting time, WIP, maximum testing queue, defect count and rework-effort measurements change by less than 5% relative to A. Zero-to-positive changes are distinguishable; zero-to-zero or missing-to-missing are unchanged, and present-to-missing is distinguishable. Equality at 5% does not warn. This deliberately simple threshold is a prompt to review capacity allocation, WIP rules, transitions and calculations, not an automatic correctness verdict. No signal is attached to ordinary sensitivity points or regular runs. The engine is never automatically repaired from this comparison.

## Parameter Sensitivity

See [VALIDATION_RESULTS.md](VALIDATION_RESULTS.md) for actual sweeps, extreme results, diagnostic evidence and the model-readiness recommendation. Observed plateaus are described over tested ranges only; no interpolation implies an optimal value.

Two interpretation limitations are particularly visible: bulk day-0 arrivals make mean completion-cohort lead time gravitate toward the measurement window's middle, and synchronized Fixed-effort completions can shift just across warm-up/horizon boundaries. In addition, an item admitted to an active stage can stall without accumulating queue WaitingTime. These existing assumptions are reported rather than altered. No technical debt or other new simulation mechanism is introduced in Step 8.
