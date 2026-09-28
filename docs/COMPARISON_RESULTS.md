# Step 9 — scenario comparison verification

Verified on macOS ARM64 with .NET 10.0.401, Release configuration, model semantics version **0.1**. No engine capacity, timing, WIP, priority, defect or rework rule changed. The sole new Core file identifies that version explicitly. Comparison executes the existing SimulationRunner/MonteCarloRunner paths.

## Single-run experiment

The original Baseline was duplicated into three alternatives, each changing one parameter. All configurations use 30 independent day-0 items, 100 working days, Fixed efforts 5/1/2, capacities 1/1, review/testing WIP 3/3, defects disabled, common effective seed 12345.

| Scenario | Developers | Testers | Development WIP |
|---|---:|---:|---:|
| Baseline (reference) | 5 | 2 | 5 |
| More Developers | 8 | 2 | 5 |
| More Testers | 5 | 4 | 5 |
| Lower WIP | 5 | 2 | 3 |

The following are full-horizon values, without Step 8 warm-up exclusion. Utilization columns/rows are ratios (1 = 100%).

| Metric | Baseline | More Developers | More Testers | Lower WIP |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 30 | 30 | 30 | 30 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 | 1.5 |
| AverageLeadTime | 23.8333 | 22.4333 | 23.1333 | 31.1667 |
| AverageCycleTime | 9.6667 | 9.9333 | 8.9667 | 8.4 |
| AverageActiveTime | 8 | 8 | 8 | 8 |
| AverageWaitingTime | 0.1667 | 1.1333 | 0.1333 | 0 |
| AverageWip | 2.6 | 2.68 | 2.39 | 2.22 |
| DeveloperUtilization | 0.36 | 0.225 | 0.36 | 0.36 |
| TesterUtilization | 0.3 | 0.3 | 0.15 | 0.3 |
| MaximumWaitingForCodeReviewQueue | 5 | 5 | 5 | 3 |
| MaximumWaitingForTestingQueue | 3 | 3 | 3 | 3 |
| AverageBlockedTime | 0 | 0 | 0 | 0 |

All four finish 30 items within 100 days, so throughput is identical. This finite-backlog result is not evidence of identical flow. More Testers reduces tester utilization from 30% to 15% (−15 percentage points) and mean cycle time by 0.7 days. Lower WIP reduces cycle time but increases lead time: time spent waiting before Development is excluded from cycle time. These are different measurements, not a contradiction or a ranking.

More Developers has a slightly higher single-run cycle time (9.9333 versus 9.6667) and queue waiting (1.1333 versus 0.1667), despite lower lead time. Development WIP and the per-item cap stay unchanged; the changed shared-pool allocation changes stage timing and queue accumulation. No monotonicity was imposed on cycle time, and no engine correction was made. This is an existing scheduling/model response, not evidence of a preferable staffing level.

## Paired Monte Carlo experiment

The same four scenarios were run with triangular Development 2/5/12, Code Review 0.5/1/3 and Testing 1/2/5; defects remain disabled. Each uses 500 matched seeds, 12345–12844. Two complete 2,000-run comparisons produced identical ordered per-run metrics and comparison distributions. A measured first batch took **2.175 seconds**, an environment-specific observation. Execution timestamps and generated run IDs are intentionally not part of the numerical reproducibility comparison.

Values below are per-scenario P50s, not the median of individual item times.

| Metric | Baseline P50 | More Developers P50 | More Testers P50 | Lower WIP P50 |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 30 | 30 | 30 | 30 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 | 1.5 |
| AverageLeadTime | 32.6667 | 30.4167 | 32.0167 | 42.85 |
| AverageCycleTime | 13.7 | 13.4667 | 13.1333 | 12.15 |
| AverageWip | 3.81 | 3.74 | 3.64 | 3.345 |
| DeveloperUtilization | 0.4689 | 0.2931 | 0.4689 | 0.4689 |
| TesterUtilization | 0.3994 | 0.3994 | 0.1997 | 0.3994 |
| MaximumWaitingForCodeReviewQueue | 3 | 3 | 3 | 2 |
| MaximumWaitingForTestingQueue | 3 | 3 | 3 | 2 |

### Signed paired deltas

For each seed, compute scenario minus reference, then calculate percentiles. All listed completed-time distributions have 500 valid pairs. Utilization/share differences here are ratio differences; multiply by 100 for percentage points. The ordinary comparison table/CSV additionally exposes percentage-point deltas.

| Scenario | Metric | Δ P50 | Δ P75 | Δ P85 | Δ P95 | Pairs |
|---|---:|---:|---:|---:|---:|---:|
| More Developers | AverageLeadTime | -2.2333 | -1.7667 | -1.4283 | -0.8 | 500 |
| More Testers | AverageLeadTime | -0.5 | -0.3667 | -0.3 | -0.2 | 500 |
| Lower WIP | AverageLeadTime | 10.2167 | 10.9 | 11.305 | 11.9017 | 500 |
| More Developers | AverageCycleTime | -0.2667 | 0.2333 | 0.6 | 1.2 | 500 |
| More Testers | AverageCycleTime | -0.5 | -0.3667 | -0.3 | -0.2 | 500 |
| Lower WIP | AverageCycleTime | -1.5 | -1.3333 | -1.2333 | -1.1 | 500 |
| More Developers | AverageWip | -0.08 | 0.07 | 0.18 | 0.36 | 500 |
| More Testers | AverageWip | -0.15 | -0.11 | -0.09 | -0.06 | 500 |
| Lower WIP | AverageWip | -0.45 | -0.4 | -0.37 | -0.33 | 500 |
| More Developers | TesterUtilization | 0 | 0 | 0 | 0 | 500 |
| More Testers | TesterUtilization | -0.1997 | -0.1931 | -0.1885 | -0.1816 | 500 |
| Lower WIP | TesterUtilization | 0 | 0 | 0 | 0 | 500 |

For More Developers, paired cycle-time P50 is −0.2667 days while P95 is +1.2 days. The upper signed tail includes increases; it is not presented as an improvement. For More Testers, the paired median lead/cycle delta is −0.5 days. Lower WIP raises median lead time by 10.2167 days while reducing median cycle time by 1.5 days.

The maximum-testing-queue P50 is 3 in both Baseline and More Developers, but their **paired delta P50 is 1**. This illustrates why a distribution of differences cannot be substituted by a difference of percentiles. Both are exposed with distinct labels.

All scenarios again complete 30 items in every run, so throughput and paired-throughput differences have no spread. This experiment validates comparison and pairing; it is not a sustained-flow capacity forecast. Use the larger demonstration experiments or Step 8 validation preset when backlog depletion masks capacity effects.

## Persistence, export and traceability

SchemaVersion 1 JSON was saved and loaded for the complete experiment and each scenario. All configuration values, distribution parameters, source seeds, identities, reference selection and run options survived the round trip. The UI imports standalone scenarios with fresh identities to avoid collisions; loading the whole experiment retains identities. Unknown schema/model/kind values are rejected. The saved experiment contains configurations, not mutable UI objects or executed histories.

Single-run and Monte Carlo CSV exports were produced through the Infrastructure exporter, then parsed as CSV to check measured values, paired percentile values, seeds and complete configuration snapshots. Tests cover embedded commas, quotes, newlines and Swedish current culture. Numeric output remains invariant-culture and round-trip precise. CSV includes both original and effective configurations, run IDs, run mode/count, model/schema version and UTC execution timestamp; editing the current configuration cannot reinterpret a stored result.

Only the latest result per scenario is held in memory. Out of Date results retain their original snapshot for inspection but are excluded from current comparison and export. Cancelled batches do not publish partial results. Changing the reference recalculates scalar/paired deltas without changing any run identity or executing simulations again.

## Verification

- Entire solution Release build succeeds with no warnings/errors.
- **183 xUnit tests pass:** 77 Core, 80 Application (including Infrastructure persistence/export verification), 26 UI ViewModel. All earlier tests remain green.
- Tests added: independent duplication/editing; baseline switching; absolute/percentage/percentage-point deltas; Run All; draft/config/option staleness; identical generated effort under common seeds; own seeds when disabled; seed-by-seed Monte Carlo pairing and signed percentiles; missing-completion pair filtering; applicable metrics; lifecycle/delete/reset; all four demonstrations; cancellation; schema/model validation; configuration round trips; real file I/O; CSV parsing/escaping/provenance; shared editor integration and current-result display.
- Native macOS Avalonia workflow created Baseline and three alternatives, edited each through the actual Scenario text fields, ran all scenarios, inspected the comparison matrix, highlighted parameters, scalar chart and all four daily-flow choices, verified stale/draft exclusion and discard, saved/reloaded the experiment, and exported single/Monte Carlo CSV.
- Native UI ran a 4-scenario × 500-run paired Monte Carlo comparison while remaining responsive (334 UI timer ticks in the recorded run). Changing the reference recomputed paired deltas without changing run IDs. Screenshots of the collection, tables, parameter differences, flow and paired percentiles were inspected.
- Core remains independent of Avalonia, JSON and filesystem concerns. Its existing source files compare unchanged with the Step 8 copy; only SimulationModel.cs was added.

## Files and architecture

Created in Application: Experiments.cs (identity/container/presets), ExperimentSession.cs (editing/result lifecycle), ScenarioComparisonRunner.cs (execution/comparison/paired deltas), ScenarioParameters.cs (configuration description). AnalysisMetrics.cs additionally exposes the already-defined AverageBlockedTime.

Created in Infrastructure: ExperimentJson.cs and ComparisonCsv.cs; the project README now describes their responsibilities. Created in Core: SimulationModel.cs, an explicit semantics-version constant. No simulation rules were added.

Created in UI: CompareViewModel.cs, ComparisonCharts.cs, CompareView.axaml and its code-behind. MainWindowViewModel loads comparison configurations into the existing editor; MainWindow adds draft apply/discard and Compare navigation. App cancels comparison work on close. UI references Infrastructure for persistence; no dependency points from Core to these layers.

Created tests: ScenarioComparisonTests.cs and CompareViewModelTests.cs. Application test project references Infrastructure to test its boundaries without introducing dependencies into Application itself. README, SIMULATION_MODEL, EXPERIMENTS and this report describe the workflow and semantics.

## Assumptions and limitations

- Comparisons use generated independent workloads represented by the existing SimulationRequest. Explicit heterogeneous/dependent WorkItems remain supported by the Core scenario API but do not have a new persistence/editor format in this step.
- Common Random Numbers aligns base/derived seeds, not arbitrary event identities. Equal effort settings share sampled workloads, while changed scheduling can assign defect draws differently. No guarantee of variance reduction or identical defects is made.
- Single runs expose all requested time/queue metrics. Monte Carlo uses its existing compact records: active/waiting/blocked times and maximum rework queue are not synthesized for Monte Carlo comparisons.
- The ordinary MC table shows a difference of P50s; the paired table shows percentiles of per-run differences. These are not confidence intervals or management advice.
- Configuration files exclude results; CSV retains a reproducible result snapshot. There is no database, autosave, version migration, permanent run archive or undo history. Demonstration/load operations replace the experiment and are labeled accordingly.
- Flow charts show the first three included scenarios' original samples, without interpolation, padding or averaging. Scalar bars show one metric per scenario; tables scroll for additional columns.
- No automatic ranking, causal attribution, technical debt or new organizational behavior was introduced. Surprising flow responses above remain visible rather than being corrected to meet an assumed business conclusion.
