# Comparing scenarios and saving experiments

Open **Compare**. The initial experiment contains **Baseline**, which is also the comparison reference. It uses the original 30-item, 100-day preset.

1. Select Baseline and click **Duplicate Scenario**.
2. Enter `More Testers` in Selected scenario name and click **Rename**.
3. Click **Edit Selected Scenario**. The application opens the existing Scenario form and identifies the active draft.
4. Change Testers from 2 to 4, then click **Apply Scenario Changes** at the top. You return to Compare. **Discard Scenario Draft** keeps the saved configuration unchanged. While a draft is open, its prior result is excluded from comparison.
5. Select Baseline again and duplicate it into `More Developers`; change Developers to 8. Duplicate Baseline again into `Lower WIP`; change Development WIP to 3. Starting each alternative from Baseline isolates one parameter per alternative.
6. Choose the reference in **Comparison Baseline**. It need not be the original Baseline. Use scenario checkboxes to choose displayed alternatives; the reference is always included.
7. Leave mode SingleRun and click **Run All Scenarios**. Every configured scenario runs, including those unchecked for display. Run Selected Scenario updates only the selected one. Progress and Cancel Comparison appear during execution.
8. Scroll to the comparison matrix. Each metric shows its value and signed delta from the reference. Percentage differences are absent for a zero/missing reference; utilization and rework-share differences use percentage points. No color indicates preference.
9. Inspect **Parameter differences**. Bold/underlined values differ from the current reference. Changing several parameters does not isolate their individual causal contributions.
10. Choose the scalar chart metric. Inspect Total WIP or a waiting queue in the flow chart; it displays original end-of-day points for the first three included scenarios. Adjust checkboxes to focus the chart. Different durations are not padded or interpolated.
11. For stochastic comparison, configure variable effort/defects in scenarios, set **Monte Carlo Runs** (default 500) and **Base Random Seed**, then click **Run Monte Carlo Comparison**. This changes run mode and runs all scenarios. Current values and percentiles appear in the matrix. Flow charts are empty for Monte Carlo; the program does not invent an average daily path.
12. Inspect **Paired Monte Carlo delta distributions**. With Common Random Numbers enabled, each matched run uses the same derived seed. A delta is scenario minus reference per run, then P50/P75/P85/P95 are calculated. P95 is the upper signed tail, not a “better” outcome. Null completed-item means are excluded pairwise and sample counts show this.
13. Use **Save Experiment** to save the entire configuration container as JSON. **Load Experiment** restores scenario identities, configuration, reference, seed strategy and run settings. It replaces the current collection and clears execution results. Save the current experiment first if you want to retain it.
14. **Save Scenario** saves just the selected configuration with its identity/version; **Load Scenario** imports it with a new collection identity. Neither operation saves execution histories.
15. Use **Export Results CSV** for the current included comparison. The export contains metrics, deltas, percentile samples and complete original/effective configuration snapshots, model version, timestamp and seed provenance. Results must be current before export.

Editing or renaming a scenario after a run marks its result **Out of Date**. Changing mode, comparison seed strategy/seed or run count also invalidates prior results. The stored snapshot is still inspectable under traceability; it is not silently mixed with current configurations. Rerun the affected scenarios to compare again. Choosing a different reference alone recalculates deltas without rerunning.

Common Random Numbers uses the comparison Base Random Seed for every scenario, overriding configured scenario seeds only for execution. Disabled common seeds use each scenario's own seed and suppress paired deltas. Equal effort settings share generated workloads when seeds correspond; changed flow can still change defect-event assignment.

**Demonstration Experiments** provide developer-count, tester-count, Development WIP and quality/rework alternatives. Loading one replaces the in-memory experiment. They use 500 items over 250 days and illustrative parameters, not calibrated Easy-Laser data. The quality example changes both review and testing probabilities simultaneously. For an explicit warm-up measurement window or parameter sweep, use the separate Sensitivity area.

Configuration files use SchemaVersion 1 and SimulationModelVersion 0.1. Unsupported versions are rejected instead of silently migrated. Results are kept only in memory until exported. The latest run replaces the previous run for that scenario; there is no permanent result archive, autosave, database or undo history.

These comparisons describe simulated consequences. They do not decide which organizational design is preferable. Read [the model definitions](SIMULATION_MODEL.md#scenario-comparison) and [the measured comparison example](COMPARISON_RESULTS.md), particularly the limits of finite backlogs and signed percentile interpretation.
