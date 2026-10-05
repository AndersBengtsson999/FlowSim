# Optional intervention labels

## Investigation and fix

The existing path already captured and persisted labels correctly: the Live TextBox binds to `ChangeLabel`; Apply passes it to `SimulationSession.ApplyChanges`; that method trims it, converts whitespace-only input to null and stores it on the existing `ConfigurationChange`. Changes are part of captured session state, checkpoints and Live JSON. No parallel metadata store, schema migration or Core change was needed.

The presentation was incomplete: Before/After displayed only Day and Label, omitting actual parameter changes. Latest status and trend hover already included the label through `Describe`, but concatenated it with the factual changes. This repair separates label and facts for readability and uses the same stored intervention everywhere.

- Latest status: labeled interventions use “Last change · Day N · Label” followed by factual changes and effective Day N+1. Unlabeled interventions retain the compact single-line form.
- Performance Trend hover: Day, optional label, actual changes and effective day appear on separate lines. The existing marker hit-testing and trend calculations are untouched.
- Before/After: the selector shows the optional label followed by Day and actual changes. Selected intervention identity and comparison calculations are unchanged.
- The Changes list continues showing label and actual changes through the existing formatter.

Labels belong to the whole intervention, including multiple simultaneous changes. No label is duplicated per parameter. Blank or missing labels render no empty placeholder. A label with no configuration change still creates no intervention. Existing 100-character validation is preserved.

## Verification

Six focused cases in `tests/Simulation.UI.Tests/InterventionLabelTests.cs` cover UI draft-to-history capture, trimming, empty/whitespace labels, latest/marker/selection presentation, daily continuation and pause/resume, multiple changes under one label, label-only no-op, checkpoint restoration, JSON round-trip and historical missing-label JSON.

A paired seeded run with and without a label compares the full captured state (normalizing only the intervention label) and complete results after identical future days. Both are identical, including random continuation, debt state, items and daily history. Labels have no effect on simulation results.

The complete suite passes **479 tests** (165 Core, 173 Application, 141 UI). Release build: **0 warnings, 0 errors**.

The [native verification host](verification/intervention-label/Program.cs) runs the actual Avalonia MainWindow, invokes controls using automation peers and enters the label through its bound TextBox. It changes Shortcut Rate 50% → 0% on Day 100 with “  Stop shortcuts  ”, verifies the trimmed stored value and visible status, raises an actual Avalonia pointer-moved event at the chart marker and checks its resulting tooltip, then verifies the rendered Before/After selection. A second unlabeled intervention verifies the clean fallback. Screenshots were visually inspected. This is automated native interaction plus visual review, not a claim of physical mouse/keyboard testing.

[Result log](verification/intervention-label/result.txt) · [Screenshots](verification/intervention-label/screenshots)

```sh
dotnet run --project docs/verification/intervention-label/NativeVerification.csproj -c Release
dotnet test SoftwareDevelopmentSimulation.sln -c Release
```

Production changes are limited to `LiveViewModel.cs` (label/factual formatting separation), new `InterventionPresentation.cs` (presentation and selector converter), `LivePerformanceTrendChart.cs` (tooltip presentation) and `LiveView.axaml` (Before/After selector). Tests, this report and the native verification evidence complete the change.

No simulation calculations, intervention timing, Technical Debt semantics, capacity, productivity, WIP, random state, history calculations, Performance Trend values or Before/After calculations were changed. Persistence/checkpoint implementation remains unchanged because it already preserves the metadata correctly.

## Follow-up: reported missing labels after repeated edits

A further native reproduction runs five consecutive labeled changes after the labeled and unlabeled cases above. Each label is entered through Avalonia's TextInput event, immediately applied and checked against both stored state and the rendered Latest intervention TextBlock; Resume/Pause follows each edit. All five pass. See `verification/intervention-label/repeated-edits-result.txt` and `screenshots/repeated-labels.png`. The reported failure is not yet reproduced; exact user steps or a saved session are still needed. No production behavior was changed speculatively for this report.
