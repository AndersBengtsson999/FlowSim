# Software Development Simulation

An Avalonia desktop simulator for exploring software delivery using capacity, independent stage effort, queues, dependencies and WIP. This is a simulation tool, not a project-management application.

The current implementation is **Simulation Model v0.1, Steps 2–8**. It builds on the existing solution and its four-layer architecture. Earlier random batches, sprint/release settings and generic Size-based effort were replaced to match this model.

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
  Simulation.Infrastructure/   Reserved for future persistence
  Simulation.UI/               Scenario, Flow, Results and Monte Carlo views and charts
tests/
  Simulation.Core.Tests/       Workflow, capacity, FIFO, WIP, dependencies, validation and metrics
  Simulation.UI.Tests/         ViewModel reset, execution, selection and formatting
  Simulation.Application.Tests/ Baseline, deterministic orchestration and reports
docs/
  SIMULATION_MODEL.md          Authoritative current model and assumptions
  REFERENCE_EXPERIMENTS.md     Historical experiment proposal; superseded assumptions
```

Project references remain `UI → Application → Core` and `Infrastructure → Application`. Core has no project references, package references, Avalonia, UI or persistence dependencies. State and remaining effort on WorkItem have private setters; domain methods control progression. Runs copy input items rather than mutating scenario definitions.

## Basic model

```text
Backlog → Development → WaitingForCodeReview → CodeReview
        → WaitingForTesting → Testing → Done
```

Code Review, Rework and Development share one developer pool, served in that order. Testing has a separate tester pool. Each active item consumes at most 1 unit per day, also limited by one person's configured capacity and the remaining pool. Capacity is not hours.

Four WIP limits count only their active state; waiting queues do not occupy active slots. FIFO ties follow scenario item order. All dependencies must be Done before development admission. Invalid and circular dependencies are rejected.

Admission occurs at day start; work completes at day end. Newly completed items wait until the next day to enter another active stage. No item receives development, review and testing work on the same day. Start timestamps mean admission, not necessarily the first positive allocation. Zero-effort stages still visit every state.

Read [SIMULATION_MODEL.md](docs/SIMULATION_MODEL.md) for precise daily order, timestamp, metric and validation definitions.

## Using the application

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
- **159 xUnit tests pass**: 77 Core, 60 Application, 22 UI ViewModel tests.
- Native macOS verification covers the previous quality/flow results and the new sensitivity tab: parameter sweeps, chart/table bindings, extreme reports, 100-run-per-point Monte Carlo, responsiveness and cancellation.
- Existing Fixed and Variable Effort no-defect results match the previous version exactly, including daily states and timestamps. Core still has no project or package dependencies.

## Step 4 metrics

Results contain immutable per-item timestamps, active/queue/blocked times, daily state counts and resource ledgers. Average WIP includes all started unfinished items, including all three waiting queues. Throughput is displayed per five working days.

See [metric definitions](docs/SIMULATION_MODEL.md#metrics-and-interpretation) and [measured baseline and comparison results](docs/METRICS_BASELINE.md). Those reference experiments all complete 30 items within 100 days (1.5 items/five days), while lead times, queues and utilization differ. No automatic quality or bottleneck classification is made.

## Scope and limitations

Step 6 adds effort generation and Monte Carlo around the existing daily simulation. It adds no charting package. Lightweight Avalonia drawing controls read daily snapshots directly. The previous A/B UI and stacked status chart are replaced by single-scenario inspection; existing Application comparison helpers and their regression tests remain available, but are not exposed in the UI.

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
