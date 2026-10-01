# Simulation Model v0.4 — Stage-specific Productivity Multipliers v1

Current semantics are v0.4, adding independent stage productivity while retaining Development Collaboration Model v1 allocation. See [Stage-specific Productivity Multipliers v1](STAGE_PRODUCTIVITY.md) for formulas and verification. See [availability, supply and status](CAPACITY_AVAILABILITY.md) for the new semantics and compatibility. Historical Step 2–13 measured examples and linked validation reports describe their original v0.1 runs; current collaboration measurements are in [Development Collaboration Model v1](DEVELOPMENT_COLLABORATION.md).

## Purpose and scope

This version defines the simulation world and basic workflow for a software development system. It is an explicit, simplified model, not a project-management system or a calibrated prediction of an organization. Delivery and queues emerge from effort, available capacity, dependencies, FIFO ordering and WIP limits. There are no rules that declare a resource a bottleneck based on headcount ratios.

This document describes the current implementation. It supersedes the earlier incremental model with Size/Complexity, seeded random ordering, a single WIP limit and a fixed/derived review stage.

## Entities and boundaries

- **Team**: DeveloperCount, TesterCount, DeveloperCapacityPerDay and TesterCapacityPerDay. Both per-person capacity defaults are 1.0. The computed totals are nominal capacity per working day.
- **WorkItem**: string Id and Name; independent DevelopmentEffort, CodeReviewEffort and TestingEffort; corresponding remaining efforts; dependency IDs; State; CreatedDay; DevelopmentStartedDay, DevelopmentCompletedDay, CodeReviewStartedDay, CodeReviewCompletedDay, TestingStartedDay, TestingCompletedDay and DoneDay.
- **SimulationScenario**: Name, SimulationDays, one Team, DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit and an ordered collection of WorkItems.
- **SimulationResult**: aggregate measures, immutable WorkItemResult records and daily snapshots. Every result retains its ordered transition history. Daily item snapshots include state, remaining efforts and effective work and consumed capacity in each stage.

Configuration properties on WorkItem are read-only. Remaining efforts equal initial efforts at construction. Execution state and timestamps have private setters, and internal domain methods enforce the next legal transition. UI code cannot assign an arbitrary state. Dependencies are defensively copied into a read-only collection.

Each engine run creates fresh execution copies. Inputs remain unchanged and can be reused. Completed or partially executed items are rejected as new scenario inputs. IDs are case-sensitive and must be unique. The order of the input collection is meaningful for simultaneous FIFO arrivals.

Simulation.Core uses only .NET libraries. Simulation.Application generates requests, exposes the baseline, orchestrates runs and projects reports. The existing UI configures requests and displays reports. Infrastructure implements versioned scenario/experiment JSON and comparison CSV persistence from Step 9.

## Resources and capacity

Normal Live uses nominal capacity 1.0 per person/day, so **people × availability → available capacity**. Consumed capacity × stage productivity produces effective work (with Development collaboration efficiency where applicable). Per-person scaling remains an Advanced parameter for legacy/custom scenarios and is preserved on load. See [Capacity UX Simplification](CAPACITY_UX_SIMPLIFICATION.md).

```text
Developer pool/day = DeveloperCount × DeveloperCapacityPerDay × DeveloperAvailability
Tester pool/day    = TesterCount × TesterCapacityPerDay × TesterAvailability
```

Capacity is an abstract work unit, **not hours**. There are two resource types. Code review, rework and development consume the **same** developer pool; testing consumes only the tester pool. DeveloperCapacityPolicy allocates that pool in order: CodeReview, Rework, Development. Unused capacity does not carry forward.

Code Review and Testing use their own productivity factor `p`; Rework always uses `p = 1`. Their per-item daily capacity allocation is:

```text
consumed = min(remaining stage effort / p, remaining resource pool, 1.0, capacity per person)
effective work = consumed × p
```

Development uses two passes over the items admitted at day start. First each gets a primary allocation in FIFO order, limited to `min(remaining effort / DevelopmentProductivity, remaining pool, 1, DeveloperCapacityPerDay)`. Primary work equals capacity × DevelopmentProductivity. Then remaining active items are ordered by remaining Development effort **after primary work**, ascending, with stable FIFO/input order for ties. Each may consume up to `min(remaining effort / (0.5 × DevelopmentProductivity), remaining pool, 1, DeveloperCapacityPerDay)` collaboration capacity, producing capacity × 0.5 × DevelopmentProductivity effective work. Collaboration requires at least two developers in the team.

Thus a Development item can consume at most 2 capacity units and receive at most 1.5 × DevelopmentProductivity effective effort per day. Every active item has a primary opportunity before any collaboration. Fractional leftovers remain available to other eligible active items during that day. All admissions still precede all work; completion does not admit replacement backlog work midway through a day.

This is a pooled model, without named individuals or tracking authors/reviewers. Development WIP counts active items, not contributors. Per-person capacity below 1 limits each contribution; values above 1 increase the pool but not either contribution cap. At default productivity 1x, two developers at capacity 0.4 allow one active item to consume 0.8 and receive 0.6 effective work. With only one developer, no second contribution is available.

The 50% second-contribution efficiency is an explicit simulation assumption, not a claim about real-world pair-programming productivity. See [Development collaboration](DEVELOPMENT_COLLABORATION.md) for concepts, worked examples, compatibility and verification.

Nominal Team capacity is kept separate from available capacity. DeveloperAvailability and TesterAvailability default to 1 (100%) and must be finite in [0,1]. They scale the pool, not contribution caps. Meetings, support, absence and individual calendars are not simulated.

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
6. Allocate the remaining developer pool to active Rework items, then Development primary work in FIFO order and collaboration in stable Closest-to-Done order. Completed rework or development transitions to WaitingForCodeReview at time `d+1`.
7. Allocate tester capacity to active Testing items in FIFO order. At completion, inspect for a defect: transition to Done on success or WaitingForRework on failure at time `d+1`.
8. Record end-of-day states, remaining efforts, work consumption and transitions.

**All admissions happen before all work.** A completion never frees a slot for another admission later in the same day. New waiting items enter their next active stage no earlier than the following day's start. A dependent item can start on the day whose start equals its predecessor's DoneDay.

This choice differs from admitting newly completed work downstream during the same day's later processing steps. It guarantees that an item receives capacity from at most one stage on a day and leaves queues visible at day end. No fractional-day or within-day scheduling is implemented.

Start timestamps record **admission** to an active state at `d`; completion timestamps record reaching zero effort at `d+1`. A waiting transition and subsequent admission can have the same numerical boundary timestamp, while remaining distinct ordered transitions. Waiting duration can therefore be zero when a slot is available at the next boundary. No artificial extra waiting day is added.

Example with at least two developers, sufficient available capacity and one item requiring 5/1/2 units:

| Working interval | Outcome |
|---|---|
| `[0,4)` | Development work 1.5, 1.5, 1.5, 0.5; DevelopmentStartedDay 0, DevelopmentCompletedDay 4 |
| `[4,5)` | Review admitted at 4; CodeReviewCompletedDay 5 |
| `[5,7)` | Testing admitted at 5; TestingCompletedDay and DoneDay 7 |

At the end of engine day 3 the item is WaitingForCodeReview; at the end of day 4 it is WaitingForTesting. An active stage can start and finish between two end-of-day chart samples. Admission occupancy, transition history and work consumption still record that activity.

## FIFO

There are no priority classes and no random tie-breaking.

- Eligible backlog items: oldest CreatedDay first. Blocked and future items are skipped without preventing eligible items from starting.
- Primary development allocation: earliest DevelopmentStartedDay first. An older, previously blocked backlog item does not displace an already admitted item.
- Review admission/allocation: earliest current review-queue entry first.
- Testing admission/allocation: earliest current testing-queue entry first.
- Rework admission/allocation: earliest current rework-queue entry first.
- Equal timestamps: original scenario collection order, maintained by stable ordering. IDs are not used to assign priority.

CodeReview → Rework → Development precedence is the resource-order policy. FIFO applies to primary Development, Review, Rework and Testing. Development collaboration uses lowest remaining effort first after the primary pass, with FIFO ties. A partially served item retains its queue position.

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

| Limit | States counted in v0.4 |
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
| UsedDeveloperCapacity / UsedTesterCapacity | Consumed Development capacity + review + rework capacity / testing capacity for that day |
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

The Avalonia form supports fixed or triangular effort independently per stage, stage WIP, seeded single runs and Monte Carlo. The UI has Scenario, Flow, Results and Monte Carlo areas. The older Application-level comparison helpers remain tested, and Step 9 adds a separate Compare area. Core supports explicit mixed-effort items and dependencies; the form generates independent items with fixed or sampled effort. The application-level UI request retains its safety bounds of 2,000 items, 3,650 days and 1,000,000 item-days.

## Determinism and exclusions

The engine uses no wall clock, unordered allocation or parallel scheduling. Step 6 generates initial effort before execution using an explicit seed; Step 7 uses separate seeded streams for optional defect discovery and rework effort. Reusing the same distributions, ordered workload, configuration and seed produces identical generated effort, states, work allocations, timestamps and metrics. As with all floating-point software, this is not a promise of byte-identical arithmetic across hypothetical different numerical implementations.

The previous incremental model’s random ordering, generic size/complexity effort derivation, sprint/release modeling, priorities and random dependencies remain absent. Step 6 introduces explicit effort distributions and a separate Monte Carlo result model; it does not restore those older rules.

No separate bug backlog, escaped production defects, technical debt, UX/PO/requirements roles, expedite classes, interruptions, support work, meetings, sickness, individual skill/productivity profiles, specialists, pairing, multiple teams, hardware dependencies, release trains, compliance/CRA, DevOps or AI simulation is implemented. The chart UI is presentation, not a modeled UX resource.

The historical reference-experiment document predates this version and is explicitly marked as superseded. Its old numerical expectations must not be used as v0.1 acceptance tests without re-derivation.

## Step 5 — visual inspection

The Step 5 presentation changes did not modify the engine or metrics. Step 6 adds generation and Monte Carlo orchestration without changing daily flow rules. The ViewModel calls `SimulationRunner`, exposes the immutable `SimulationResult`, and formats values. It never advances WorkItems or calculates simulation metrics. Daily charts select existing `DailySnapshot` values and only calculate drawing coordinates/scales.

- **Scenario** groups simulation length/workload, team capacity, active WIP limits and effort. Text and tooltips explain every field. Construction and Reset to Baseline both read the existing `BaselineScenario.Create()` factory, whose configuration comes from SimulationRequest defaults.
- **Flow** shows all seven state counts for one end-of-day snapshot. Active and waiting states differ by text as well as colour. UI day 1 selects `Days[0]`; the last UI day selects `Days[SimulationDays-1]`. Changing this selection never executes the engine. The first day is selected after a run.
- **Charts** show TotalWip, both waiting queue counts, and used versus available developer/tester capacity. Lines connect daily observations, not intra-day estimates. Developer usage includes consumed Development, Code Review and Rework capacity. There is no automatic classification, smoothing or new aggregation.
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

The final value is clamped to `[a,b]` against numerical residue. Actual effort is not rounded to whole days. The daily allocator caps each contribution at 1 consumed capacity unit (Development may receive two contributions with collaboration efficiency) and at most one stage per interval. Fractional work may therefore leave unused capacity or occupy a whole working interval, exactly as before.

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

CodeReview → Rework → Development share one developer pool in that priority order. Rework retains its one-unit per-item/day cap (also limited by per-person capacity) and its own active-only WIP limit. WaitingForRework does not consume that limit. All admissions precede work, so defects cannot trigger same-day rework. Daily snapshots expose WaitingForReworkCount, ReworkCount, ReworkWip and UsedReworkDeveloperCapacity. TotalWip includes both new states; developer utilization includes consumed rework.

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

## Scenario Comparison

Step 9 adds structured comparison around the existing engine. `SimulationRequest` remains the single generated-workload configuration model. `ScenarioDefinition` wraps that immutable configuration with a Guid identity; its name is the configuration name. No per-scenario configuration copy is stored in a second editor ViewModel. Compare uses the existing Scenario form through load/apply callbacks. Core's execution rules, timing, distributions and capacity allocation are unchanged.

`ExperimentSession` maintains an in-memory collection and latest immutable execution result per identity. Add creates a baseline-configured scenario. Duplicate creates a new identity and appends “Copy” to the name, sharing only immutable values. Rename preserves identity. Reset restores the original baseline configuration while retaining identity/name. Delete removes the scenario and its result; deleting the reference selects the first remaining scenario. The last scenario cannot be deleted. Collections support 1–20 scenarios, with horizontal scrolling in comparison matrices. Scenarios can be excluded using checkboxes, but the designated reference is always included.

An editor draft is explicitly marked; its previous result is Out of Date and cannot enter a comparison. Apply replaces the scenario configuration after validation. Discard keeps the previous saved configuration/result. Any changed configuration field, including a rename, or comparison option marks the result outdated; this is deliberately conservative. Returning exactly to the stored record/options makes it current again. Merely choosing another comparison reference recomputes deltas without rerunning because executions do not depend on which result is the reference.

`ScenarioComparisonRunner.Run` calls SimulationRunner for single runs and the existing MonteCarloRunner for Monte Carlo. RunAll validates the complete experiment and runs every configured scenario, including those not selected for display. Progress is scenario number/name and run count. A selected scenario can be run independently. Runs execute off the UI thread; cancellation keeps previous results and publishes no partial batch. Single-run comparisons retain original daily histories and are limited to 5,000,000 total item-days. Monte Carlo retains compact existing per-run results, with the existing 100,000,000 item-day limit per scenario and 500,000,000 per comparison. Other existing per-run limits still apply.

All comparison metrics use the **full configured horizon**, without sensitivity warm-up. Single-run measurements reuse AnalysisMetrics at warm-up 0 and include completed items, throughput, lead/cycle/active/waiting/blocked time, WIP, utilization, maximum review/testing/rework queues and defect/rework metrics. AverageBlockedTime is additionally exposed by the shared analysis projection from its existing per-item measurements. No existing Core metric changes. Incomplete time means are absent when there are no completions; quality metrics are absent for defect-disabled scenarios. When only one side has an applicable metric, its value can be displayed but the delta is undefined. For comparison against zero defects, enable defects with zero probabilities in the reference, as in the quality demonstration.

For any applicable metric, absolute delta = scenario value − reference value. Relative percentage delta = absolute delta / abs(reference) × 100; absent when the reference is zero or either side is absent. Developer/tester utilization and rework share use percentage-point delta = ratio difference × 100 in the UI/CSV. No color, rank or direction marker indicates preference.

The comparison table has metrics as rows and included scenarios as columns. Parameter differences use bold/underline against the reference, solely to identify changed configuration values. Scalar bars use the chosen single-run value or Monte Carlo P50. Flow displays **observed end-of-day points**, with no interpolation or synthesized data, for the first three included scenarios in collection order. Available series are TotalWip, WaitingForCodeReview, WaitingForTesting and WaitingForRework. Different horizons share their actual day coordinates; absent days are not filled. Monte Carlo results have no aggregate time-series chart.

## Experiments

`Experiment` is an organizational container with identity, name, description, immutable scenario list, reference scenario ID, run mode, common-seed flag, base seed, Monte Carlo run count and explicit model version. It introduces no simulation rules. ExperimentSession and ScenarioComparisonRunner implement editing and execution; the earlier ExperimentRunner reporting API remains intact for existing consumers.

Demonstration Experiments use 250 days and 500 independent items, with other Steady Flow settings (5 developers, 2 testers, capacities 1, WIP 10/5/10 and Fixed effort 5/1/2):

- Developer Capacity: 1, 3, 5, 8, 10 developers; reference 5.
- Testing Capacity: 1, 2, 3, 5, 8 testers; reference 2.
- Development WIP: 2, 3, 5, 8, 12; reference 5.
- Quality / Rework: both discovery probabilities 0%, 10%, 20%, 40%; reference 0%. Defects are enabled in all four, Rework WIP 3, triangular review-origin effort 1/3/6 and testing-origin 2/5/10. This example deliberately changes **two** probabilities at once.

These are illustrative experiments, not calibrated recommendations. The 500-item choice keeps these demonstrations within the existing Monte Carlo workload bounds at 500 runs while supplying substantial backlog. Backlog depletion still depends on the chosen configuration and should be inspected.

Changing one parameter at a time helps identify sensitivity. Changing several represents an alternative system design but weakens causal attribution. Comparison shows simulated consequences; it does not determine which organizational choice is preferable.

## Common Random Numbers

Common Random Numbers defaults to enabled. A separately configured comparison BaseSeed (default 12345) overrides each scenario's configured RandomSeed for execution only. Both the original configured seed and the effective execution configuration are retained. Disabled common seeds use each scenario's configured seed, and no paired inference is presented.

For Monte Carlo run index i (zero-based), every scenario uses `unchecked(effectiveBaseSeed + i)` through the existing MonteCarloRunner strategy. With equal item count/distribution settings and only capacity or WIP changed, the generated underlying effort workload is identical for each matched run. There is no outcome-forcing or reordering. Changing distribution kinds/bounds, item count or scheduling may change how draws correspond; the independent discovery/rework streams still follow their existing Step 7 policy. Common seeds are a variance-reduction strategy where applicable, not a promise of identical defects or a guaranteed reduction in variance. The comparison reference can change without changing these effective seeds.

## Paired Monte Carlo Comparison

Monte Carlo comparison defaults to 500 runs per scenario and uses the existing runner's seed derivation and simulation path. It shows P50/P75/P85/P95 and sample counts for completed items, throughput, mean lead/cycle time, WIP, developer/tester utilization, maximum review/testing queues, and applicable total defects, consumed rework effort and rework share. Active/waiting/blocked time and maximum rework queue are available for single runs but are not retrospectively inferred from the existing compact Monte Carlo records.

The main table's delta is **difference between scenario and reference P50s**. A separate paired table calculates each difference first, then summarizes those differences:

```text
delta_i = metric(scenario, seed_i) − metric(reference, seed_i)
paired percentiles = DistributionStatistics(delta_0, delta_1, ...)
```

This is generally different from subtracting percentile values. Pairing checks equal run number and seed. For lead/cycle metrics, a pair contributes only if **both** runs completed at least one item; missing means are never substituted by zero. Sample counts expose exclusions. If no valid pairs exist, percentiles are absent. Other applicable metrics retain all pairs. With common seeds disabled there is no paired delta distribution, even if configured seeds happen to coincide.

Percentiles use the existing linearly interpolated type-7 implementation. Signed deltas are sorted numerically: P95 is the upper, more positive tail, not a maximum improvement or an uncertainty interval. Ratio-based paired deltas are displayed/exported in ratio units (0.01 = one percentage point), explicitly labeled; the ordinary table uses percentage points for its scalar ratio deltas. No statistical significance, confidence interval, optimality or recommendation is implied.

## Result Traceability

Each `ScenarioRun` retains RunId, immutable original ScenarioDefinition, immutable effective SimulationRequest, comparison options, model version, execution-start UTC timestamp, and the actual single-run/Monte Carlo result. Monte Carlo results retain ordered run numbers and seeds. Run count is 1 for single runs or the actual Monte Carlo count. Editing a scenario never mutates its stored execution snapshot. The UI shows stored provenance even for Out of Date results, while excluding those results from tables/charts.

Execution date and identity intentionally differ between reruns; reproducibility applies to observations, derived seeds, metrics and paired distributions, not wall-clock metadata. The session keeps the **latest** result per scenario, not an unbounded result archive. Saving configuration does not save result histories. CSV exports preserve the configurations and provenance of the exported result so they can be reproduced after later edits.

## Simulation Model Version

`Simulation.Core.SimulationModel.Version` is **"0.4"**, independently of the assembly/application version. It adds independent Development, Code Review and Testing productivity while retaining Capacity Availability, Work Supply and Development Collaboration Model v1. Scenario/experiment files, Live sessions, result records and CSV provenance carry the version. Model 0.2 and 0.3 documents load with productivity 1x/1x/1x; original Live version provenance and all historical ledgers are retained. Model 0.1 remains rejected because its Development allocation differs. Editing a version label cannot migrate a saved timeline. JSON schema remains 1. See [compatibility verification](CAPACITY_AVAILABILITY.md).

## Scenario and experiment persistence

Persistence belongs in Simulation.Infrastructure, which now implements ExperimentJson and ComparisonCsv. UI references Infrastructure for file operations; Core remains free of UI, JSON and filesystem dependencies. The configuration JSON envelope has SchemaVersion=1, SimulationModelVersion="0.4", DocumentKind="Scenario" or "Experiment", and the corresponding payload. All existing SimulationRequest settings, including fixed fallbacks, distribution parameters, dormant defect configuration and configured seeds, are retained. Effort objects have explicit Kind="Fixed"/"Triangular" and their numerical fields. There is no CLR type-name activation.

Loading validates schema/model/kind, scenario settings, unique IDs, reference membership and supported distributions. Unknown properties and distribution kinds are rejected. Experiment loading preserves identities and creates a read-only scenario collection. Importing a standalone scenario into the current collection assigns a fresh identity to avoid collisions. Files are human-readable UTF-8 JSON, limited to 5 MB on read. Writes use a sibling temporary file followed by replacement. Native file pickers handle location selection and overwrite prompting; no database or automatic background save is introduced.

## Comparison CSV export

Infrastructure exports one row per scenario/metric. Columns include scenario ID/name, metric, raw Value (single value or MC P50), absolute/relative/percentage-point deltas, P50/P75/P85/P95 and sample count, paired-delta percentiles/sample count, reference ID, experiment name, run ID/mode/count, common-seed flag, effective seed, execution timestamp, schema/model versions, and original/effective configuration JSON snapshots.

Numbers use invariant culture and round-trip precision. Empty cells represent missing/undefined data. Every field is CSV-quoted, with embedded quotes doubled, preserving commas and embedded newlines. Ratio Values and paired deltas are raw ratios; PercentagePointDelta is a separate scaled column. This is a text export, with no Excel-specific dependency. Configuration JSON in the CSV is a snapshot, not a reference to mutable UI settings.

See [EXPERIMENTS.md](EXPERIMENTS.md) for the user workflow and [COMPARISON_RESULTS.md](COMPARISON_RESULTS.md) for measured examples, paired-distribution interpretation and verification.

## Step 11 presentation

The GUI groups simulation, comparison and analysis without changing this model. Primary summaries and deltas consume the existing result/calculation layer. Compare flow shows actual same-day observations, never a synthetic Monte Carlo flow. See [GUI definitions, workflow and verification](GUI_REDESIGN.md) for queue selection, day conventions, progressive disclosure and current limitations.

## Step 11B — Simple Mode presentation

Home/Run/Change & Compare/Explore orchestrate the existing model and services. No numerical rule changes were made. The simple comparison selects an observed same-day flow snapshot with the largest waiting-queue difference to help inspection; this is a presentation choice, not a new metric. [Simple Mode documentation](SIMPLE_MODE.md) records defaults, hidden settings, advanced access and verification.

## Step 12 — Live, incremental continuation and arrivals

The fixed Run loop and Live now call the same incremental `SimulationSession.AdvanceOneDay` and daily engine. Existing allocation, admissions, FIFO, dependency, inspection/rework and per-item capacity rules are unchanged. See [Live simulation](LIVE_SIMULATION.md) for the complete day convention, arrival algorithm, intervention/checkpoint semantics, rolling metrics, random-state strategy and persistence schema.

Live adds optional continuous work at the **start** of each interval using a decimal fractional accumulator. Existing fixed runs continue with Fixed Backlog. A configuration intervention at displayed Day N takes effect in interval `[N,N+1)`, displayed as Day N+1 when complete; it never rewrites prior events or remaining efforts. Reduced limits/counts preserve existing work. New arrival efforts use their own SplitMix64 stream, and all current random states are retained in checkpoints/files.

Lifetime metrics keep their existing definitions. Live's recent metrics use the last 20 completed intervals by default, or the number actually observed if fewer: completions divided by observed days × 5; mean end-of-day WIP; summed used divided by summed available developer/tester capacity. Zero-day aggregates and zero-capacity utilization are defined as zero. Current WIP and Completed remain latest occupancy and cumulative completion counts. Charts and metrics report observations without automatic recommendations or causal interpretations.

## Work supply in model 0.3

Fixed backlog remains supported. Fixed rate retains the existing decimal arrival accumulator. Always available generates only enough items at day start to fill Development admission slots after counting eligible existing backlog. It uses the same seeded arrival generator and does not change admission, WIP, priority or collaboration rules. Availability and supply interventions recorded on Day N apply on Day N+1. See [full semantics and verification](CAPACITY_AVAILABILITY.md).
