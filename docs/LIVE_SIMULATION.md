# Step 12 — Live simulation

Live is the default application entry and primary evolving simulation timeline. Compare, Explore and Experiments are under Analyze; Advanced remains secondary. See [Live-first UX](LIVE_FIRST_UX.md). The model is an illustrative laboratory, not a calibrated representation of a real organization and not a management recommendation engine.

## Using Live

The application opens **Live**. Choose **Live Flow Demo**, then **Start**. The compact setup exposes developers, testers, Development/Testing WIP and new work per day. Work effort and Quality have their own collapsed sections. More settings contains initial backlog, arrival mode, capacities, review/rework WIP, seed, safety limit and rolling window. Baseline, Variable Effort Example and Defects & Rework Example are also available starting points. Choosing one copies its existing configuration into the Live setup; it does not modify the normal Run setup.

Live Flow Demo starts with **zero initial items**, 5 developers, 2 testers, capacities 1/1, WIP 5/3/3/3, fixed development/review/testing effort 5/1/2, defects off, 0.8 new items/day, seed 12345. Baseline instead retains its 30 initial items; arrivals are additional when Continuous is selected. Fixed Backlog ignores the arrival rate.

The running page emphasizes the current day, the existing flow-board colors and stage labels, waiting queues and playback. Select a stage or queue for count, age of the oldest item in that state, and up to eight item IDs. Rework appears if enabled or relevant to this timeline. Active counts can validly exceed a newly reduced WIP limit.

- **Start** creates a new timeline at Day 0 and starts automatic playback.
- **Pause** stops wall-clock playback without advancing or changing domain state.
- **Resume** continues; **Step**, while paused, processes exactly one day.
- **Run to Day**, while paused, advances the same session to a greater integer day within its safety limit. It yields every 25 days for UI responsiveness, stops at the target and can be interrupted by Pause/navigation. No days are replayed or skipped.
- Speeds 0.5x/1x/2x/5x/10x mean nominal intervals of 2/1/0.5/0.2/0.1 real seconds per day. A slow render can delay playback; it never adds work or skips simulated days.
- Opening **Change something**, creating/restoring a checkpoint, opening/saving a file, requesting all-time details, or navigating to another workflow pauses playback.
- Applying or cancelling an edit leaves playback paused. Resume is explicit.
- **Stop** keeps the timeline for inspection/saving but prevents further advancement. **Reset** discards the current timeline and checkpoints and returns to setup. Save first to retain them. Stop/Reset are secondary controls under Advanced Live settings.
- The default 10,000-day safety limit pauses with **“Live simulation safety limit reached.”** It can be raised under Advanced Live settings. A loaded session resumes paused and is subject to its saved limit.

## Incremental architecture and unchanged daily rules

`Simulation.Core/SimulationSession.cs` owns the continuing timeline. `SimulationEngine.Run` now creates a session and repeatedly calls `AdvanceOneDay`; Live uses that same method. There is one implementation of daily admissions, WIP, dependencies, developer/reviewer/tester allocation, defects and rework: `SimulationEngine.AdvanceOneDay`. It was extracted from the previous run loop, preserving operation order and stable FIFO ties. No day is replayed from Day 0 to advance Live.

The daily order remains:

1. Generate this day's continuous arrivals, if selected.
2. Observe dependency blocking; admit eligible Backlog, review queue, test queue and rework queue items under the corresponding active WIP policy.
3. Allocate the shared developer pool in **Code Review → Rework → Development** order.
4. Allocate tester capacity to Testing.
5. Record end-of-day states, actual work and capacities; increment the completed-day count.

All admissions precede work. A completion cannot receive another stage's capacity in the same day. Generated items have no dependencies; existing dependencies remain intact. Development now uses the shared model v0.2 primary/collaboration passes; Review, Rework and Testing retain `min(1, per-person capacity)` per item/day. Each Development contribution has that cap, with the second 50% effective. Team pool remains count × per-person capacity. See [Development Collaboration Model v1](DEVELOPMENT_COLLABORATION.md).

Core has no Avalonia, clock, file, JSON or persistence dependency. Application owns Live lifecycle, checkpoints and rolling analysis. Infrastructure owns the versioned JSON representation and atomic file replacement. UI owns playback scheduling, forms, chart rendering and file pickers; no work allocation or rolling-metric formulas live in ViewModels.

## Session contents

A session retains its name and seed, initial/current configuration, every Work Item's original and remaining efforts, dependencies, state/timestamps, all queue entry days, current rework amount, transitions, inspection attempts, actual capacity/defect events, daily observations, parameter-change history, fractional arrival balance, next ID sequence and three current random-stream states. `WorkItem.Capture/Restore` copies execution state directly; it does not infer events from timestamps.

`GetResult` uses the existing authoritative result builder over the current history. Zero-day results are now explicitly supported and yield zero aggregate metrics. Core callers still receive immutable observations and read-only collection views. Captured transport records contain detached collections; restore copies them back into private session collections.

## Work arrival and day convention

`CurrentDay` is the number of completed working-day intervals. At displayed **Day 100**, intervals `[0,1)` through `[99,100)` have completed; the next Step processes `[100,101)` and the UI shows **Day 101**. Existing domain event and daily snapshot conventions are unchanged: snapshot `Day` is the zero-based start of the interval; completion timestamps are its right boundary.

Continuous arrival uses a **decimal** accumulator, initially zero. At the beginning of each interval, add `WorkItemsPerDay`, generate `floor(accumulator)` items, then retain the fractional balance. Thus 0.5 produces 0,1,0,1 items in the first four displayed days; 0.8 produces exactly 80 arrivals in 100 days; 1.5 produces exactly 150. Fractional credit survives rate changes, checkpoints and saving. Fixed Backlog neither adds arrivals nor accumulates credit. Core supports changing arrival mode; the normal UI selects it before starting.

New items use the current configured Fixed/Triangular development, review and testing distributions in that order. Their `CreatedDay` is the beginning of their arrival interval; they can be admitted that day. IDs use a retained `LIVE-n` sequence, skipping any IDs already present. IDs are never reused **within a timeline**. Restoring a checkpoint intentionally reproduces that checkpoint's ID sequence in the alternate continuation.

The rate must be between 0 and 2,000 items/day. This input guard does not promise every large-rate/long-duration combination will fit memory.

## Intervention semantics and history

At displayed Day 100, Apply Changes records `{Day: 100, Before, After, Label}` and updates the configuration used for interval `[100,101)`. It does not advance time. Existing efforts, states, assignments, events and past snapshots are unchanged. Capacity uses the new team configuration on the next step. New probabilities apply only to future inspection completions; they do not revisit past checks or existing rework amounts.

Reducing an active WIP limit never ejects items. A board can show **5 / 3**; admissions wait until occupancy is below 3. Reducing developer/tester counts similarly leaves states intact. These decisions remain in the existing WIP and capacity policies.

The edit panel exposes counts, capacities, all four active WIP limits and arrival rate. More things to change exposes defect enablement/probabilities and rework distributions. Initial effort distributions are configured before starting; this UI does not edit already-created work. Quality values remain configurable while defects are off. Optional labels have no simulation effect. A no-op edit creates no intervention or chart marker and says that no parameters changed.

The Changes section records exact configurations and presents readable differences. Queue-history markers are placed at intervention boundary days; pointing at a marker shows day, optional label and differences (including multiple changes on the same day). Their presence makes no causal claim.

## Rolling metrics and history

The default window is the **last 20 completed days**; advanced choices are 10/20/50/100. During the first fewer-than-window days, use only observed days, never pad with zero-capacity days.

For a timeline at boundary `D` and window `W`, consider intervals starting at `max(0,D-W)` through `D-1`:

- Recent completed = cumulative Done at `D` minus cumulative Done at the window's starting boundary. A completion exactly at the starting boundary is excluded; a completion at `D` is included.
- Recent throughput / 5 days = recent completed / observed intervals × 5.
- Rolling average WIP = mean of the existing end-of-day TotalWip observations.
- Rolling developer/tester utilization = sum used / sum available capacity in those intervals, including changes in capacity. A zero denominator yields 0.
- Current WIP and Completed cards are respectively the latest end-of-day WIP and **all-time** completed count; they are not rolling averages.

`RollingMetrics` in Application implements these definitions once. The API exposes rolling average WIP; the compact main display shows current WIP. Show details requests the existing all-time lead/cycle/waiting times, throughput, average WIP, utilization, defects and rework effort. It pauses and explicitly labels the observed duration. There are no automatic good/bad judgments or bottleneck classifications.

The single primary chart shows review/testing waiting queues and rework when relevant. It uses cached daily count points and a single drawing surface. When there are more days than horizontal pixels, per-bucket min/max envelopes retain brief queue peaks; rendering does not discard stored history. Dense repeated oscillations can appear as a band at long horizons. Changes are drawn as vertical lines, not permanent visual controls per historical day. Long Changes lists use a height-bounded ListBox.

## Checkpoints and persistence

Create Checkpoint takes a detached capture of the complete state, including random continuation and fractional balance. Restore replaces the current timeline with that capture, truncating the currently displayed future and its changes. Other named checkpoints remain available, including alternate futures. Repeating the same future interventions after restore reproduces items, metrics, event histories and IDs. Delete removes only the selected checkpoint.

There are three independent **SplitMix64** streams:

- Arrival efforts: seed XOR `0xA771A150`.
- Defect discovery: seed XOR `0xD3FEC701` (existing stream).
- Rework effort: seed XOR `0xA11CE702` (existing stream).

Checkpoints/files save each stream's current **64-bit state**, not only the initial seed. This preserves exact continuation without replaying random draws. Normal initial-backlog effort generation retains its existing original-seed stream and ordering.

Infrastructure stores JSON with `SchemaVersion = 1`, `SimulationModelVersion = 0.1`, `DocumentKind = LiveSession`, initial/current configuration, complete current items/events, continuation state, change history, safety/window settings and named checkpoints. Unsupported schema/model/kind is rejected. The model version stays 0.1 because fixed-mode numerical rules are unchanged.

Daily history is losslessly delta encoded: each day saves its capacity/occupancy summary and only item observations that changed from the previous day. Loading reconstructs observations directly from these deltas; it does **not** rerun simulation logic. The reconstructed counts/results match the in-memory timeline. Saves use the existing temporary-file-and-replace mechanism. No database or UI object is serialized. Timer speed, an unfinished edit draft and wall-clock running/stopped status are intentionally not persisted; an opened timeline is paused.

## Manual experiment

1. Start Live Flow Demo; run toward Day 50, pause and select queues.
2. Resume to Day 100 and create **Day 100 baseline**.
3. Open Change something; verify automatic pause. Change Testers 2 → 3, label **Add tester**, Apply, then Resume.
4. Observe through Day 200. Inspect the marker at Day 100 and the recent metrics.
5. Restore Day 100 baseline. Change Developer Capacity 1.0 → 1.4, label **AI-assisted development**, Apply and continue to Day 200.
6. Observe without assuming either alternative is better. This proxy represents only increased effective developer capacity; it does not model AI quality, adoption, cost or other AI behavior, and retains the one-unit-per-item cap.
7. Change arrivals 0.8 → 1.2, continue, save, reopen and resume. Existing queues and fractional credit remain.

## Validation and performance

The final Release solution build passed with **0 warnings and 0 errors**. All **252 tests** passed: **95 Core, 91 Application, 66 UI**, including **40 new test cases**; all 212 existing tests remain green. Native validation also checked intervention-marker tooltips and that the day/playback header remains fixed while scrolling.

The Step 12 test suites cover incremental/fixed equivalence (including defects), arrivals 0.5/0.8/1/1.5, distribution bounds and sampling variation, ID collisions/continuation, day-100 capacity changes, remaining-effort preservation, future-only quality changes, lower WIP and resources, dependency restoration, checkpoints/random continuation, rolling window boundaries/zero denominators/changing capacity, persisted checkpoints/changes, unsupported documents, safety limits, pause/step/speeds/editing, no-op edits, hidden workflow isolation and long runs.

Native macOS validation runs the actual Avalonia window and its commands, checks normal Run/Compare/Explore afterward, and renders screenshots. It exercised Day 100 intervention → Day 200 → exact restore → capacity proxy → increased demand → save/load → identical continuation. A fresh Live Flow Demo was then advanced for 1,000 days with the ViewModel and chart data updating. The final validated run took **1.65 seconds**, including periodic dispatcher yields; the longest measured step was **9.8 ms** and process working set was about **283 MiB**. Earlier runs of the workflow measured 2.7–3.5 seconds. These are measurements of this machine/run, not general performance guarantees. Playback at 1x intentionally takes one real second per simulated day.

Unchanged immutable item observations are shared between in-memory days. Full histories and per-day reference arrays are retained, so memory still grows with timeline length and item population; checkpoints and loading an additional copy increase memory further. JSON files are smaller through delta encoding, but saving/loading/checkpoint capture can pause the UI for large sessions. No history needed for continuation or metrics is discarded. The default day limit is a safety stop, not a guarantee for extreme arrival rates. There is no asynchronous background simulation or autosave in this increment.

Historical Step 12 verification under model v0.1: no hidden inconsistency requiring changes to fixed simulation behavior was found. Two existing model characteristics matter when interpreting Live: queues include newly completed stages at day end even if admitted immediately next morning, and capacity above 1 per person cannot increase one item's daily work beyond 1. Neither was altered to make an experiment look different.

## Files

Created:

- Core: `SimulationSession.cs` (configuration, state transport records, incremental session).
- Application: `LiveSimulation.cs` (lifecycle, checkpoints, rolling metrics).
- Infrastructure: `LiveSessionJson.cs` (versioned lossless session persistence).
- UI: `LiveViewModel.cs`, `FlowPresentation.cs`, `LiveQueueChart.cs`; `LiveView`, `LiveSettingsView`, `LiveQualityView` XAML and code-behind.
- Tests: `SimulationSessionTests.cs`, `LiveSimulationTests.cs`, `LiveViewModelTests.cs`.
- Documentation: this document and Step 12 screenshots.

Modified:

- Core: `SimulationEngine.cs`, `WorkItem.cs`, `EffortDistributions.cs`, `Defects.cs`, `SimulationResultBuilder.cs`.
- Infrastructure: `ExperimentJson.cs` exposes its existing serializer-option factory internally for the Live schema.
- UI: `MainWindowViewModel.cs` delegates the same flow rows to shared presentation; `SimpleWorkflowViewModel.cs` adds Live navigation and pauses when leaving; `MainWindow.axaml` adds the Live page/Home entry.
- `README.md`, `docs/SIMULATION_MODEL.md` link/document the new mode.

No project dependencies or solution architecture were replaced. There is no explicit AI model, technical debt, multiple-team model, Live Monte Carlo or other later-step feature.

## Screenshots from native macOS validation

- [Simple Live setup](screenshots/step12-setup.png)
- [Paused intervention at Day 100](screenshots/step12-change.png)
- [Day 200 after adding a tester](screenshots/step12-day200.png)
- [Day 1000](screenshots/step12-day1000.png)

## Live Performance Trend

The Team Performance area now includes a single-metric trend chart with independent visible-range and existing rolling-window controls. Daily queues and consumed/effective Development work use authoritative snapshots; rolling throughput, cycle time, Average WIP and utilization match Step 13. Interventions remain at recorded Day N, effective N+1, with the Before/After selection highlighted. See [Live Performance Trend](LIVE_PERFORMANCE_TREND.md) for full definitions, lifecycle behavior, rendering and native validation.
