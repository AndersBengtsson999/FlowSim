# Live-first UX

The primary workflow is now **Live → Analyze → Advanced**. Application launch opens Live setup directly, with no Home screen or automatic simulation start. This is a navigation/presentation change on Simulation Model 0.2.

## Previous structure and dependency review

The previous primary navigation was Home, Run, Change & Compare, Explore and Live, with a secondary Advanced button. Home was the initial page. Simple Run executed an independent fixed-horizon scenario and displayed summaries, flow and item history. Live already supplied configuration, continuous/fixed-backlog execution, interventions, snapshots, metrics, trends, Before/After, checkpoints and persistence, but lacked fast advancement to a chosen day.

Compare and Explore are independent experiments, not playback controls for an existing Live timeline. Their existing application services and ViewModels are reused. Fixed-horizon runs remain useful for expert metrics, Monte Carlo, scenario experiments and validation; these capabilities remain available through Analyze/Advanced rather than competing with Live as a top-level Run entry.

## Live: the normal simulation environment

Live is selected on startup. Choose a preset, configure Team, Work and Flow, and Start. Common developer/tester counts, Development/Testing WIP and work arrival rate remain immediately visible. **Work · effort per item** and **Quality · defects and rework** expand within Live. Less-common capacities, review/rework WIP, seed, safety limit, initial backlog and arrival mode remain under More settings. Distribution editors reuse the existing effort editor ViewModels.

During simulation, Live retains Start, Pause/Resume, Step, speed, Flow Board, current state, Team Performance, Performance Trend, Change something, Before/After, checkpoints and session save/open. Stop/Reset remain secondary under Advanced Live settings. This local expander is part of Live; it is not the top-level Advanced workspace.

Switching to Analyze or Advanced pauses Live, including fast advance. Returning preserves the same session, intervention history, random state, chart selections and checkpoints. Resume is explicit. Current navigation is in-memory presentation state; no persistence schema or saved-document changes were made.

## Run to Day

Expand **Run to a day** beneath playback controls. While paused, enter an integer target greater than the current day and no greater than the session's configured safety limit, then choose **Run to Day**.

- Advance the existing timeline; never restart from Day 0.
- Every simulated interval calls the existing `LiveSimulation.Step`, which uses `SimulationSession.AdvanceOneDay`.
- No alternative allocation or metric calculation is introduced.
- Preserve prior history, interventions, checkpoints, random states and fractional arrivals.
- Pause at the target. At the safety limit, retain the existing stopped-by-limit behavior.
- Invalid targets report a message without changing simulation state; there is no silent clamping or limit increase.
- Pause, navigation, Stop, Reset, load or restore can interrupt at a completed-day boundary.
- During fast advancement, Resume, Step, another fast advance and parameter editing cannot concurrently advance/change the session.

The UI refreshes/yields every 25 completed days and once at the end. This removes wall-clock playback delays while keeping interruption responsive. It does not batch or skip domain processing. The AsyncCommand also prevents overlapping executions. Task cancellation is not a rollback: completed days remain in history, except when Reset explicitly discards the session or restore/load replaces it.

## Analyze

Analyze contains three subviews and remembers the most recently selected one while the app remains open:

- **Compare:** existing Change & Compare, baseline versus explicit alternative, changed-parameter list, neutral observations and detailed comparison. Draft settings survive navigation rather than being reset on each entry.
- **Explore:** existing parameter/sensitivity exploration, chart and data. Expanded Advanced Analysis retains additional parameters, uncertainty settings and diagnostic detail.
- **Experiments:** the existing scenario manager/CompareView, saved/predefined experiments, scenario editing, single/Monte Carlo comparisons and import/export. It uses the same `Owner.Compare` instance previously hosted in Advanced.

These workflows execute independent scenarios. They do not replay or mutate the active Live session, and the Analyze caption says so. The simple comparison uses its explicitly selected Baseline/Last simulation starting configuration; it does not silently copy Live's current configuration. Explore retains its established base-scenario controls and calculations.

Editing an experiment scenario opens the existing Advanced scenario form. Apply or discard returns to Analyze → Experiments. The simple comparison's specialist controls still use its separate existing comparison instance, preserving separation from the expert experiment collection.

## Advanced

Advanced is a smaller secondary navigation action and contains:

- **Setup & Detailed Metrics:** complete fixed-horizon scenario form, distributions, capacities, seeds/reproducibility, full result tables, flow, work-item/event histories and specialist controls.
- **Diagnostics & uncertainty:** detailed sensitivity tools, model validation and Monte Carlo.

Scenario management is rehosted in Analyze → Experiments, not duplicated in two views. Existing import/export actions stay with their underlying scenario/experiment/session tools. Existing numeric page identifiers are retained internally through an `ExpertArea` presentation mapping so callbacks and result navigation remain compatible.

## Legacy code and architecture

Home, simple Run, simple result/flow markup, their commands and orchestration remain in `MainWindow`/`SimpleWorkflowViewModel` for a later cleanup. They have **no entry in the normal primary navigation** and are not reachable from the new user journey. Specialist fixed-run functionality remains intentionally reachable in Advanced. No scenario services or numerical engines were deleted.

Major affected components:

- `SimpleWorkflowViewModel`: Live startup, Analyze grouping/subview memory, preserving comparison drafts and pausing Live on departure.
- `MainWindow`: three primary destinations; Analyze subnavigation; rehosted experiment view.
- `MainWindowViewModel` / `MainWindowPresentation`: existing scenario-edit callback routes and Advanced tab mapping.
- `LiveViewModel`: fast-advance command and interruption orchestration only.
- `LiveView`, `LiveSettingsView`, new `EffortSettingsView`: existing configuration controls reorganized with progressive disclosure.
- `AdvancedWorkspace`: two expert groups, with scenario management moved to Analyze.

No new dependencies. Core, Application and Infrastructure have no changes for this UX step. Simulation rules and document schema/version remain unchanged.

## Verification

Before changing code, the complete solution built with **0 warnings/errors** and all **327 tests passed**.

Nine new test cases cover Live startup, Compare/Explore/Experiments/Advanced reachability, remembered subviews/drafts, exact session preservation across navigation, scenario editor routing, Run-to-Day equivalence against normal stepping with variable effort/defects/arrivals/intervention/checkpoint state, four invalid target cases, Pause/navigation interruption, Reset during advancement and the safety limit. The earlier startup assertion was updated from Home to Live; fixed-run numerical regression tests remain enabled.

The [native verification host](verification/live-first/Program.cs) runs the production MainWindow and real control bindings. It verifies the exact primary buttons and absence of old primary entries, Live setup, Start/Pause, Run to Day 100, intervention and continuation to Day 140, Flow/Trend/BeforeAfter, executing Compare and Explore, running experiment comparisons, scenario-edit routing, returning to the identical Live document, Advanced access and JSON round trip. This is automated native UI validation plus visual screenshot inspection, not a claim that every step was manually clicked.

```sh
dotnet run --project docs/verification/live-first/NativeVerification.csproj -- docs/verification/live-first
```

Screenshots: [startup Live](verification/live-first/startup-live.png), [Run to Day](verification/live-first/run-to-day.png), [Live trend](verification/live-first/live-trend.png), [Before/After](verification/live-first/live-before-after.png), [Analyze Compare](verification/live-first/analyze-compare.png), [Analyze Explore](verification/live-first/analyze-explore.png), [Advanced](verification/live-first/advanced.png).

**No simulation semantics changed.** Development Collaboration Model v1, Code Review/Rework, WIP/capacity priority, defects, dependencies, continuous arrivals, intervention boundaries, rolling metrics, Before/After, checkpoints, persistence and random determinism retain their existing definitions.

**Technical Debt has not been started.**

Final Release verification: **336 tests pass** (110 Core, 136 Application, 90 UI), **0 warnings, 0 errors**. Native end-to-end validation passed with the final two-group Advanced layout. `git diff --check` is clean.
