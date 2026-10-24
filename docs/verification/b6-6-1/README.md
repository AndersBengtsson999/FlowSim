# B6.6.1 — Simulation Insights

Implemented read-only explanatory analysis, a compact LIVE entry point, one reusable modeless analysis window per LIVE view, and integrated offline documentation. No engine, KPI, cost, save-format, geometry or animation changes.

## Inspected and reused

- `ViewModels/QueuePressure.cs`: same station classification, exact four-observation/three-saturation rules, zero-capacity distinction and intervention reset. Called directly, unchanged. Development uses eligible Backlog and deliberately never receives a saturation-based bottleneck diagnosis.
- `Core/Models.cs`: recorded snapshots, raw used/available pool capacity, and existing legacy capacity fallback properties. Review is part of developer usage alongside Development, Rework and debt repayment.
- `Core/SimulationEngine.cs` / `WorkItem.cs`: end-of-day stage-completion transitions use `snapshot.Day + 1`; release is an internal-delivery boundary. Counts include repeat processing attempts where applicable.
- `Application/LiveSimulation`, `KpiDashboard` and intervention comparisons: simulated-day conventions, 20-day presentation precedent, nullable KPI semantics, next-day intervention effects. KPI links open the existing Explorer; no KPI formulas are reimplemented.
- `LiveViewModel`, station controls, `KpiDashboardView`, guided staffing workflow: non-invasive toolbar and navigation integration. Non-staffing suggestions use scenario comparisons rather than the staffing-only guided workflow.
- B6.5.3 `GuideCatalog`, registry, native markdown reader and embedded resources: one documentation system.
- B6.5.3 native verification harness and existing UI/Core/Application test projects.

## Evidence rules

1. Three queue observations always present: eligible Development Backlog, review queue, testing queue. Station classification is reused exactly. Recent growth is not asserted to be temporary in duration or a proven bottleneck. Four equal observations are labelled stable. Early history is explicitly insufficient for sustained pressure. Queue size alone never diagnoses a bottleneck.
2. Capacity relationships: positive recorded upstream exits and positive downstream queue change between first/last snapshots. Show transition counts for Development→review, review→testing, testing→release queue, internal releases, plus raw available/used pools. Endpoint growth is explicitly distinct from continuous growth and causal attribution.
3. Unused Testing capacity with zero active/waiting items at day end: describe only those jointly observed days; include dependency waiting and testing-WIP occupancy context without claiming either caused underutilization. No staffing-cut prescription.

Capacity evidence takes at most 20 completed days after the latest intervention. Four comparable observations are an explicit new minimum for capacity findings, chosen to align with the station evidence floor and avoid a one-day imbalance claim; it is not a statistical confidence threshold. Queue classification keeps its existing four-day window. Every row carries actual first/last day and count. Empty capacity evidence is unavailable, not a measured zero. Used/available raw totals are measurements, not alternative KPI calculations. Existing full-usage epsilon is reused for unused-capacity comparisons. No arbitrary high-utilization percentage, queue-size threshold or probability is introduced.

No before/after intervention finding is emitted in v1, so the service never attributes an observed change to an intervention or compares incomplete counterfactuals. Day-100 tests establish that new evidence starts on Day 101 and reaches Days 101–120 after 20 observations.

## Lifecycle and UI

Stable IDs are category + underlying condition + station; no dates in identity. New relevant conditions are **New**. Selecting a row reviews its current evidence. Repeated counts/dates within the same classification remain reviewed. Classification changes or recurrence are **Updated**, counted separately. Resolved conditions become **No longer relevant** and retain dated evidence; neutral early/stable conditions are **Informational**. The bounded tracker retains only the small known set of condition IDs.

Acknowledgements are in-memory UI state. Window close/reopen preserves them; new session, reset, load or checkpoint replacement clears them. No persistence format or simulation state is modified. A changed selected row remains Updated until selected again or explicitly marked reviewed. List selection and metrics disclosure survive ordinary updates.

The dark window shows current day, actual periods, active/new/updated summary, master list, observation, evidence strength, possible explanation, optional experiment, expandable recorded metrics, existing station/KPI navigation and contextual Help. Narrow layouts move the list above details. Mac Cmd+Q follows the main window's existing command. No automatic pause, navigation, intervention, notification or sound.

## Files

Added:
- `src/Simulation.UI/Insights/SimulationInsights.cs` — immutable contracts and read-only analysis.
- `src/Simulation.UI/Insights/InsightTracker.cs` — session-local acknowledgement lifecycle.
- `src/Simulation.UI/Insights/InsightsWindow.cs` — modeless reader.
- `src/Simulation.UI/ViewModels/LiveViewModel.Insights.cs` — refresh bridge and indicator.
- `tests/Simulation.UI.Tests/SimulationInsightsTests.cs` — 12 focused cases including three seeded replay cases.
- `docs/user-guide/assets/images/simulation-insights.png` — verified rendered UI.
- This verification directory — reproducible harness, logs and images.

Modified:
- `LiveViewModel.cs` — read-only refresh/reset hooks.
- `LiveView.axaml` and `.axaml.cs` — one toolbar button, window lifetime and existing navigation callbacks.
- `docs/user-guide/07-experiments-and-decisions.md`, `08-reference-and-glossary.md`, `registry.json`, `verification-report.md` — searchable help, glossary, periods, lifecycle and limitations.

## Verification

- Release solution build: zero warnings/errors (`build.log`).
- Full test suite: 945 passed (Core 218, Application 295, UI 432), zero failures/skips (`tests.log`).
- Focused tests include deterministic repeated reports, complete state equality, random-stream preservation across three seeded continuations, identical KPI projections, station consistency before/after interventions, no-history N/A values, Day100 boundaries, stable identities, new/reviewed/updated/resolved/recurrence, shared raw developer capacity, exact release/transition boundaries, idle-work uncertainty, LIVE indicator, load/reset/checkpoint restoration, guide mappings/search/embedded illustrations.
- Headless Avalonia with real Skia on macOS: window reuse/open/close/reopen, detail selection and acknowledgement, LIVE advancing while open, updated lifecycle, narrow composition, station navigation, existing Explorer, correct contextual Help, unchanged simulation capture and packaged guide resources (`headless.log`). Screenshots are actual control renders, not mockups.
- Published application offline smoke check from `/tmp`: all 20 embedded guide resources, IDs, links and illustrations load from the published assembly (`publish.log`, `publish-smoke.log`).
- Ordinary native macOS attempt failed before UI creation with Avalonia.Native RenderTimer error **−6661** (`native-attempt.log`). Therefore native display acceptance is **not complete**. No production renderer workaround was introduced.

Reproduce the UI checks:

```sh
dotnet run --project docs/verification/b6-6-1/Verification.csproj -c Release -- --headless
# Omit --headless to retry ordinary native macOS acceptance on an available display.
```

## Images

- `headless/screenshots/insights-wide.png`: complete analysis layout, selected observation and experiment.
- `headless/screenshots/queue-details.png`: queue classification and evidence.
- `headless/screenshots/capacity-metrics.png`: expanded recorded measurements.
- `headless/screenshots/insights-narrow.png`: responsive analysis layout.
- `headless/screenshots/live-indicator.png` and `live-980.png`: compact LIVE entry and retained workshop/inspection.

## Limitations

Native display, physical keyboard/trackpad and screen-reader acceptance remain manual. Evidence-strength states are descriptive, not causal confidence. Four/20-day bounded evidence may miss longer-term behavior. No intervention-effect inference, automatic optimization, worker-level diagnosis or acknowledgement persistence. Raw count changes alone are not material updates. Headless verification cannot establish OS window activation behavior. Existing historical records remain unchanged.
