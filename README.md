# Software Development Simulation

An Avalonia desktop simulator for exploring software delivery using capacity, independent stage effort, queues, dependencies and WIP. This is a simulation tool, not a project-management application.

The current implementation is **Simulation Model v0.1, Steps 2–4**. It builds on the existing solution and its four-layer architecture. Earlier random batches, sprint/release settings and generic Size-based effort were replaced to match this model.

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
  Simulation.UI/               Existing Avalonia views, ViewModels and status chart
tests/
  Simulation.Core.Tests/       Workflow, capacity, FIFO, WIP, dependencies, validation and metrics
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

Development and review share one developer pool; review is served first. Testing has a separate tester pool. Each active item consumes at most 1 unit per day, also limited by one person's configured capacity and the remaining pool. Capacity is not hours.

Three WIP limits count only their active state; waiting queues do not occupy active slots. FIFO ties follow scenario item order. All dependencies must be Done before development admission. Invalid and circular dependencies are rejected.

Admission occurs at day start; work completes at day end. Newly completed items wait until the next day to enter another active stage. No item receives development, review and testing work on the same day. Start timestamps mean admission, not necessarily the first positive allocation. Zero-effort stages still visit every state.

Read [SIMULATION_MODEL.md](docs/SIMULATION_MODEL.md) for precise daily order, timestamp, metric and validation definitions.

## Baseline and existing UI

The default form and `BaselineScenario.Create()` use 100 days, 5 developers, 2 testers, capacity 1 each, active WIP limits 5/3/3 and 30 independent items requiring 5/1/2 development/review/testing units.

- **Run Simulation (A)** executes a deterministic scenario.
- **Compare A / B** varies resource capacities and the three WIP limits with the same work items.
- **Overview** shows metrics and B − A differences.
- **Status over time** separates all seven states and allows inspecting a day.
- **Unfinished work** includes both waiting queues as well as active items.
- **Cancel** stops an ongoing run; invalid inputs produce a message and clear prior results.

The form generates independent equal-effort items. Explicit dependencies, differing item efforts and future creation days can be supplied through Core. There is no random variation or 100-run interface in v0.1. Resource counts/capacity may be zero; WIP limits and simulation length must be positive. Efforts must be finite and nonnegative.

## Verification

On macOS ARM64 with .NET 10.0.401:

- Entire Release solution builds with **0 warnings and 0 errors**.
- **73 xUnit tests pass**: 56 Core, 17 Application.
- Native macOS smoke verification passed for the 19 input bindings, baseline, deterministic A/B, seven-state charts, day slider, queue reports, invalid-input handling and cancellation. The temporary UI test host is not part of the xUnit suite; rendered Overview and chart screens were visually checked.
- Coverage includes effort initialization, per-item/day caps, parallel work, shared pool, review precedence, test isolation, all seven states and timestamps, dependency eligibility, FIFO, three WIP limits, determinism, input isolation, validation/cycles, zero/fractional capacity/effort, cancellation and baseline configuration.

The baseline is tested for repeatable full state/timing histories and capacity/WIP invariants, without hard-coded business conclusions.

## Scope

This increment implements the simulation world, basic workflow and observed metrics. There is no random variation, Monte Carlo, sprints/releases, priorities, rework, individual skill model, extra organizational roles or other advanced simulation. Named people and code authors are not tracked. Waiting for resources inside an admitted active state is possible and visible through zero daily work. No persistence/export is implemented.

## Step 4 metrics

Results contain immutable per-item timestamps, active/queue/blocked times, daily state counts and resource ledgers. Average WIP includes all started unfinished items, including both waiting queues. The Overview displays throughput per five working days. Allocation and admission rules are unchanged.

See [metric definitions](docs/SIMULATION_MODEL.md#metrics-and-interpretation) and [measured baseline and comparison results](docs/METRICS_BASELINE.md). The baseline and comparisons all complete 30 items within 100 days (1.5 items/five days), while lead times, queues and utilization differ. No automatic quality or bottleneck classification is made.
