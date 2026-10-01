# Software Development Simulation

An Avalonia desktop simulator for exploring software delivery using capacity, independent stage effort, queues, dependencies and WIP. This is a simulation tool, not a project-management application.

The current implementation is **Simulation Model v0.3 — Capacity Availability and Work Supply**, following Step 13 — Live Team Performance, including Simple Mode and Live simulation. It builds on the existing solution and its four-layer architecture. Earlier random batches, sprint/release settings and generic Size-based effort were replaced to match this model.

## Build and run on macOS

Requires .NET 10 LTS SDK, pinned by `global.json` with feature-band roll-forward. The solution uses C#, Avalonia 11.3.22, MVVM and xUnit. First restore requires access to NuGet.

```sh
dotnet restore SoftwareDevelopmentSimulation.sln
dotnet build SoftwareDevelopmentSimulation.sln -c Release --no-restore
dotnet test SoftwareDevelopmentSimulation.sln -c Release --no-build
dotnet run --project src/Simulation.UI/Simulation.UI.csproj -c Release --no-build
```

This machine's verification SDK was installed temporarily at `/tmp/simulation-dotnet`. While it exists:

```sh
export DOTNET_ROOT=/tmp/simulation-dotnet
export PATH="$DOTNET_ROOT:$PATH"
```

A regular SDK installation is needed for continued use after temporary files are removed. macOS ARM64 is the verified platform; no Windows/Linux runtime verification is claimed.

## Architecture

```text
SoftwareDevelopmentSimulation.sln
src/
  Simulation.Core/             Domain, validation, WipPolicy, deterministic engine and metric builder
  Simulation.Application/      Requests, baseline, run orchestration and result reports
  Simulation.Infrastructure/   Versioned scenario/experiment JSON and comparison CSV
  Simulation.UI/               Scenario, Flow, Results, Monte Carlo, Sensitivity and Compare views
tests/
  Simulation.Core.Tests/       Workflow, capacity, FIFO, WIP, dependencies, validation and metrics
  Simulation.UI.Tests/         ViewModel reset, execution, selection and formatting
  Simulation.Application.Tests/ Baseline, deterministic orchestration and reports
docs/
  SIMULATION_MODEL.md          Authoritative current model and assumptions
  REFERENCE_EXPERIMENTS.md     Historical experiment proposal; superseded assumptions
```

Project references are `UI → Application → Core`, `UI → Infrastructure` and `Infrastructure → Application`. Core has no project references, package references, Avalonia, UI or persistence dependencies. State and remaining effort on WorkItem have private setters; domain methods control progression. Runs copy input items rather than mutating scenario definitions.

## Basic model

```text
Backlog → Development → WaitingForCodeReview → CodeReview
        → WaitingForTesting → Testing → Done
```

Code Review, Rework and Development share one developer pool, served in that order. Testing has a separate tester pool. Development gives each active item primary capacity before allocating collaboration to the closest-to-done items. Each item can consume up to 2 capacity units for up to 1.5 effective effort. Review, Rework and Testing retain their one-unit caps. All allocations are bounded by per-person capacity, remaining work and the shared pool. Capacity is not hours.

Four WIP limits count only their active state; waiting queues do not occupy active slots. FIFO ties follow scenario item order. All dependencies must be Done before development admission. Invalid and circular dependencies are rejected.

Admission occurs at day start; work completes at day end. Newly completed items wait until the next day to enter another active stage. No item receives development, review and testing work on the same day. Start timestamps mean admission, not necessarily the first positive allocation. Zero-effort stages still visit every state.

Read [SIMULATION_MODEL.md](docs/SIMULATION_MODEL.md) for precise daily order, timestamp, metric and validation definitions.

## Primary workflow: Live → Analyze → Advanced

The application opens directly in **Live**. Configure Team, Work, Flow and Quality, then Start. Pause/Resume/Step, Flow Board, Team Performance, trends, interventions, checkpoints and Before/After remain in one simulator. **Run to Day**, while paused, advances the existing timeline to a chosen day without playback delays.

**Analyze** contains Compare, Explore and Experiments. **Advanced** retains detailed fixed-horizon settings/results, distributions, Monte Carlo, validation and diagnostics. Navigating away pauses Live and preserves the session. Home and simple Run remain internal legacy code without primary navigation entries. See [Live-first UX and verification](docs/LIVE_FIRST_UX.md).

## Advanced fixed-horizon workflow

The default form and **Reset to Baseline** use `BaselineScenario.Create()`: 100 days, 5 developers, 2 testers, capacity 1 each, active WIP limits 5/3/3 and 30 independent items with effort 5/1/2. The ViewModel obtains its defaults from this factory.

1. Open **Scenario**. Configure Simulation, Team, WIP Limits and Work Item Effort. Every parameter has an explanation and tooltip. Effort is capacity consumed, not elapsed time.
2. Select **Run Simulation**. The application opens **Results → Summary** when the run finishes. Invalid inputs show a message; **Cancel** stops an ongoing run.
3. Read the twelve metrics and their explanations. Time averages concern completed items only; utilization is shown as a percentage.
4. Open **Flow** to inspect total WIP and the two waiting queues over time. Scroll down to compare daily used and available developer/tester capacity.
5. Move **Selected Day** from day 1 to the final day. The seven state counts and chart marker update from the existing daily results without rerunning. Blue cards indicate active stages; amber cards indicate waiting queues. Scroll the flow column on smaller windows to reach every state.
6. Open **Results → Work Items** to inspect all individual final states, start/finish timestamps, and lead/cycle/active/waiting/blocked times. Blank timestamps or lead/cycle values indicate unfinished stages/items. Horizontal scrolling is available for the table on smaller windows.
7. Return to **Scenario**, change one parameter and run again. Results always describe the last completed run. **Reset to Baseline** restores all parameters, clears results, and returns to Scenario.

The form supports zero headcounts/capacity and an empty workload. Triangular parameters must be finite with 0 < Minimum <= Most Likely <= Maximum; Fixed effort may still be zero. WIP limits and simulation duration must be positive; effort must be finite and nonnegative. Limits remain 2,000 items, 3,650 days and 1,000,000 item-days per run.

## Verification

On macOS ARM64 with .NET 10.0.401:

- Entire Release solution builds with 0 warnings and 0 errors.
- **183 xUnit tests pass**: 77 Core, 80 Application, 26 UI ViewModel tests.
- Native macOS verification covers the previous quality/flow results and the new sensitivity tab: parameter sweeps, chart/table bindings, extreme reports, 100-run-per-point Monte Carlo, responsiveness and cancellation.
- Existing Fixed and Variable Effort no-defect results match the previous version exactly, including daily states and timestamps. Core still has no project or package dependencies.

## Step 4 metrics

Results contain immutable per-item timestamps, active/queue/blocked times, daily state counts and resource ledgers. Average WIP includes all started unfinished items, including all three waiting queues. Throughput is displayed per five working days.

See [metric definitions](docs/SIMULATION_MODEL.md#metrics-and-interpretation) and [measured baseline and comparison results](docs/METRICS_BASELINE.md). Those reference experiments all complete 30 items within 100 days (1.5 items/five days), while lead times, queues and utilization differ. No automatic quality or bottleneck classification is made.

## Scope and limitations

Step 6 adds effort generation and Monte Carlo around the existing daily simulation. It adds no charting package. Lightweight Avalonia drawing controls read daily snapshots directly. The previous A/B UI and stacked status chart are replaced by single-scenario inspection; existing Application comparison helpers and their regression tests remain available, and Step 9 provides a separate structured Compare area.

Charts show sampled end-of-day values connected by lines; they do not imply continuous intra-day activity. WIP shows the total only; stage counts are available in the selected-day flow. The Work Items table is read-only, in scenario order, with scrolling and no sorting/filtering/export. Reset clears old results. Editing fields retains the last completed results until the next Run, which replaces them; there is no multi-run history.

The form generates independent items with fixed or triangular effort. Dependencies remain a Core capability. No capacity variation, persistence, sprint/release planning, automatic recommendations or further organizational concepts are added.

## Variation and Monte Carlo

1. Use **Reset to Baseline** and **Run Simulation** for the original Fixed-effort case.
2. Select **Variable Effort Example** to load Development 2/5/12, Code Review 0.5/1/3 and Testing 1/2/5. Team size, capacity and WIP remain the baseline values. This is an example, not calibrated Easy-Laser data.
3. Choose Fixed or Triangular independently per stage. Set **Random Seed** (default 12345). Run Simulation and inspect the generated effort columns in Results → Work Items; each item keeps that effort for the run.
4. Rerun with the same seed to reproduce results, or change it to generate another workload.
5. Set **Number of Runs** (default 500) and select **Run Monte Carlo**. The Monte Carlo tab shows base seed, run count, sample counts, P50/P75/P85/P95 for nine metrics, plus lead-time and throughput histograms. Progress and Cancel remain available; execution runs off the UI thread.
6. Flow/Results retain the most recent single run; they do not show averaged daily data from Monte Carlo. A cancelled batch has no partial result. Reset clears both result types.

Monte Carlo uses seeds baseSeed + zero-based run index, with defined 32-bit wraparound. Percentiles use sorted linear interpolation at `(n-1)*p`. Lead/cycle percentiles exclude runs without completions; other metrics include all runs. These are model outcome distributions, not real-project predictions without calibration. The variable preset also increases **mean effort**, so it does not isolate dispersion alone.

See [full definitions](docs/SIMULATION_MODEL.md#variation) and [measured Step 6 results](docs/VARIATION_RESULTS.md). The 500-run reproducibility and live macOS UI responsiveness/cancellation checks passed. No new packages were required.


## Defects and rework

Defects are disabled by default. Select **Defects & Rework Example** to load an illustrative scenario, or enable **Quality & Rework** and configure the two discovery probabilities, source-specific additional effort and active Rework WIP limit.

A failed review or test enters WaitingForRework → Rework → WaitingForCodeReview. Every repeat inspection consumes its full original effort. The shared developer pool prioritizes review, then rework, then new development. Seeded discovery and rework sampling preserve reproducibility; there is no artificial loop limit beyond the simulation horizon.

Results show quality metrics and per-item defect/attempt counts. **Results → Item History** exposes transitions, capacity applications and discoveries. Flow includes the feedback branch and rework charts; Monte Carlo adds four quality distributions to its original nine. Rework effort measures capacity actually consumed in the Rework stage, excluding repeated inspection effort.

See [model definitions](docs/SIMULATION_MODEL.md#defects) and [measured Step 7 results](docs/DEFECT_RESULTS.md), including assumptions and verification evidence. No new packages were added.


## Sensitivity and model validation (Step 8)

Open **Sensitivity**, choose a base scenario and parameter, edit the comma-separated values, select SingleRun or MonteCarlo, and set seed/warm-up. **Steady Flow Validation** supplies 1,000 items over 250 days; analysis defaults to a 50-day warm-up. Quality sweeps require a defect-enabled base. Triangular sweeps change MostLikely and reject values outside the existing bounds.

**Run Sensitivity Analysis** displays a selectable-metric chart, numerical table with P50/P85/P95 and base deltas, and expandable full-run/window diagnostics for capacity, queues and active WIP saturation. **Run Model Validation** executes the three predefined extreme comparisons and shows configurations, deltas and any weak-response investigation warning. Analysis has its own progress/cancellation; results retain their captured configuration.

The entire Core implementation remains unchanged. Analysis services live in Application and the new tab has its own ViewModel and drawing control. No third-party package was added. A warm-up window is not proof of steady state; completed-item times retain their full lifetimes and exclude unfinished items.

Read [the definitions](docs/SIMULATION_MODEL.md#model-validation) and [the detailed validation report](docs/VALIDATION_RESULTS.md) before interpreting plateaus, lead time or WIP saturation. The report includes measured sweeps, extreme comparisons, deterministic Monte Carlo verification and limitations. Technical Debt has not been implemented.


## Scenario comparison and experiments (Step 9)

Open **Analyze → Experiments**, duplicate Baseline, rename the alternative, and use **Edit Selected Scenario** to modify it in the existing Scenario form. Apply the draft, choose a comparison reference and **Run All Scenarios**. Compare metrics as rows/scenarios as columns, inspect highlighted parameter differences, and select scalar or observed daily-flow charts. Out of Date results are excluded, with their historical configuration still available in traceability.

**Run Monte Carlo Comparison** defaults to 500 runs per scenario. Common Random Numbers defaults on and uses the comparison seed sequence across scenarios. The ordinary table shows distributions and differences of P50s; a separate table shows the distribution of signed differences per paired run. Disabled common seeds use each scenario's configured seed and omit pairing.

Save/load scenarios and experiments using human-readable JSON (SchemaVersion 1, SimulationModelVersion 0.3; compatible loading of model 0.2). Export current comparison results to CSV with values, deltas, percentiles, full configuration snapshots and run provenance. JSON saves configurations only; no database or result-history archive is introduced.

See [the experiment workflow](docs/EXPERIMENTS.md), [exact comparison semantics](docs/SIMULATION_MODEL.md#scenario-comparison) and [measured comparison results](docs/COMPARISON_RESULTS.md). Core execution rules are unchanged; its only new file declares the explicit model-version constant. Technical Debt remains out of scope.

## Simplified workflow (Step 11)

Use **Simulate → Run Baseline**, inspect the six primary results, then **Duplicate & Compare**. Change Testers and choose Run Simulation to open the selected comparison pair automatically. Advanced measurements and experiment tools remain in expandable sections. Sensitivity, Model Validation and Monte Carlo are under **Analyze**. See [GUI guide and verification limits](docs/GUI_REDESIGN.md). Simulation and persistence semantics are unchanged.

## Simple Mode (Step 11B)

Historically, Step 11B started at **Home**, with **Run**, **Change & Compare** and **Explore**. The current Live-first navigation supersedes that entry path. Run shows four result cards; Change & Compare creates its before/after pair automatically; Explore exposes one parameter and one chart. Expert scenario management, Monte Carlo, validation, exports and diagnostics remain under **Advanced Tools**. This section records the historical Step 11B design; see Live-first UX above for current navigation. See [Simple Mode guide, file inventory and verification](docs/SIMPLE_MODE.md).

### Live — watch and change a running system

The application opens **Live** directly. Select **Live Flow Demo** and Start. Pause/Resume/Step and 0.5x–10x playback operate one evolving timeline. **Change something** pauses; edits apply next simulated day without resetting work. The flow board, rolling metrics and one queue-history chart show observations; intervention markers retain what changed and when.

Secondary controls provide checkpoints and JSON save/open. The default safety stop is 10,000 days. Continuous arrivals default to 0.8 items/day with decimal fractional accumulation; Fixed Backlog remains available in More settings. Compare and Explore are under Analyze; detailed fixed-horizon runs remain under Advanced.

See [Live model, instructions, validation and limitations](docs/LIVE_SIMULATION.md), including the Day 100 checkpoint experiment and the capacity-only AI proxy limitation. Build/test/run remain:

```sh
dotnet build SoftwareDevelopmentSimulation.sln -c Release
dotnet test SoftwareDevelopmentSimulation.sln -c Release
dotnet run --project src/Simulation.UI -c Release
```


### Live Team Performance (Step 13)

Live now shows Recent Throughput and Recent Cycle Time alongside the Flow Board. Expand **Flow, Capacity and Quality** for average WIP, separate current/average queues, OLS trends, aggregate utilization and relevant defect/rework measures. Choose a 10/20/50/100-day window (default 20).

After recording a change, expand **Before & After an intervention**. The selected window anchors to the intervention day: a Day-100 change with window 20 compares Days 81–100 against 101–120. Available-day counts make partial periods explicit. Queue comparisons use averages; utilization differences use percentage points. No composite score or causal recommendation is generated.

Latest verification: Release build with 0 warnings/errors and **274 tests passing**. Actual Avalonia views were rendered and visually inspected, including the Add tester experiment. Manual native macOS interaction remains unverified for this step. See [implementation, calculation semantics, measured results and verification limits](docs/LIVE_TEAM_PERFORMANCE.md).

The Step 13 [developer-capacity investigation](docs/LIVE_CAPACITY_VALIDATION.md) verifies both unchanged Testing under arrival-limited demo conditions and increased Testing under abundant work. The full suite now has **282 passing tests**; simulation semantics and production code are unchanged by that investigation.

Final [Before & After boundary verification](docs/LIVE_BOUNDARY_VERIFICATION.md) confirms Day-100/window-20 comparisons use **81–100 / 101–120** in every tested metric and in the running native Avalonia UI. Partial periods and 10/20/50/100-day windows are verified. **294 tests pass, with 0 build warnings/errors**; no production-code changes were needed.

## Development Collaboration Model v1

[Model, worked examples and verification](docs/DEVELOPMENT_COLLABORATION.md) documents primary-first allocation, Closest-to-Done collaboration, fractional reuse, consumed-capacity utilization, Flow Board details and the deliberate v0.1 persistence incompatibility. Earlier verification counts and screenshots above describe their historical steps. Technical Debt has not been started.

Historical v0.2 collaboration verification: **313 tests pass; Release build has 0 warnings and 0 errors**. Five native Avalonia WIP scenarios passed; results and screenshots are linked in the model report.

## Live Performance Trend

Live → Team Performance now includes a selectable time-series chart with 10/20/50/100-day and Full Session ranges. Daily queue and Development capacity/work series remain distinct from rolling delivery, Average WIP and utilization. Intervention markers use the recorded day and highlight the existing Before/After selection. See [semantics, architecture and verification](docs/LIVE_PERFORMANCE_TREND.md). No new chart dependency or simulation-rule change was introduced.

Trend verification: **327 tests pass; Release build has 0 warnings and 0 errors**. Baseline, Day-100 WIP intervention, collaboration, Reset/checkpoint behavior and a 10,000-day native chart were verified.

## Live-first UX verification

Current navigation is **Live | Analyze | Advanced**, with Live selected on launch. Compare/Explore/Experiments are grouped under Analyze; the new secondary **Run to Day** control continues Live deterministically. **336 tests pass; Release build has 0 warnings and 0 errors.** See [workflow, retained legacy code and native verification](docs/LIVE_FIRST_UX.md).

Capacity Availability defaults to 100% and scales each team pool before allocation. Live supports Fixed rate and lazy Always available work supply, with compact Live Status and historical available-capacity trend series. See [implementation and compatibility](docs/CAPACITY_AVAILABILITY.md) and [native scenarios A–G](docs/verification/capacity-availability/RESULTS.md). Development Collaboration Model v1 remains unchanged.

Live now uses a compact status/toolbar, collapsible configuration and full-width Flow Board followed directly by Performance Trend. Team Performance and Before/After remain available through expanders. See [layout, window-size validation and screenshots](docs/COMPACT_LIVE_LAYOUT.md).
