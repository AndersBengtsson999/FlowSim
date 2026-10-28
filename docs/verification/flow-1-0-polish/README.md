# FLOW final product polish

Scope: accessible LIVE files, confirmed Reset, targeted guide corrections and build-time product identity. No simulation, KPI, pressure/trend analysis, scenario format or packaging changes.

## Implementation

- LIVE Open and Save buttons are at the start of the existing wrapping toolbar. Open is available before Start; Save is disabled without a session. The previous checkpoint/file controls are retained. Both entry points reuse the existing picker callbacks and `LiveSessionJson` persistence. File operations pause playback. Cancel or read/validation failure leaves the current session object, timeline and checkpoints intact; successful load remains paused.
- Reset is an asynchronous presentation command with a modal Cancel/Reset question. Cancel is initially focused; Escape and closing the window cancel. No destructive default action. Playback's timer is suspended while asking, without changing the status or captured session; Cancel restores playback. Confirm calls the existing Reset implementation. Reset is unavailable during Run to Day; Pause first. No dirty-state tracking, autosave or saved-file deletion was added.
- The guide describes the 10-day/minimum-four stable trend, entry/retention confirmation thresholds, separate four-observation queue pressure and 20-day capacity evidence, current Insights headings and stable viewing order. All topic IDs, registry entries and KPI mappings are unchanged. Current controls and Insights images are actual Avalonia renders; the older overview is explicitly labelled historical.
- `ProductVersion.props` is the single public version source. The UI assembly's version/product and embedded `FlowPublicVersion` metadata use it; mismatched `Version` overrides fail the UI build. `FlowCommit` and `FlowSourceState` are captured during compilation. About FLOW uses the compiled assembly, never a runtime Git query. Source archives without Git provenance are explicitly unverified. Model/save-format versions are untouched. No current TEST DMG was changed or produced.
- Main window/product name is FLOW. About is available in Application and the macOS native FLOW menu. Its version/commit text is selectable for support reports.

## Verification

```sh
dotnet build SoftwareDevelopmentSimulation.sln -c Release --no-restore -m:1 -nr:false
dotnet test SoftwareDevelopmentSimulation.sln -c Release --no-restore
dotnet run --project docs/verification/flow-1-0-polish/Verification.csproj -c Release
```

Full Release solution build: successful, **0 warnings and 0 errors**.

Full automated suite: **1,016 passed**, no failures/skips (218 Core, 295 Application, 503 UI). Seven new test cases cover running/paused Reset cancellation, exact session and checkpoint preservation, deterministic next-day continuation, confirmed Reset, no-session/unwired confirmation, confirmation failure, embedded identity and guide content/link integrity.

The headless/Skia harness passed 16 checks using the actual controls and dialog. It verifies Open before Start, cancelled opening with no session, complete load, cancelled/invalid loading with an active session, save while running and exact round-trip, toolbar visibility, suspended simulation during the real Reset dialog, Cancel/close/confirm, saved-file retention and the actual About text. Temporary test sessions are deleted. Images are in `screenshots/`; these are rendered Avalonia controls, not native desktop screenshots.

Existing numerical/deterministic regression tests pass. `src/Simulation.Core`, `src/Simulation.Application`, `src/Simulation.Infrastructure` and `docs/user-guide/registry.json` are unchanged. The only timing intervention is suspension of the UI playback timer while the user answers Reset; no simulated day, capacity budget or random state is changed by cancellation.

## Review and remaining limits

Changes are uncommitted for review, based on `39df09e86e8df51fb554f185204414eaabf9fc23`; screenshots therefore correctly say `local changes`. A clean release-candidate build must be rebuilt from its approved commit. Screenshots are verification evidence, not a claim that the source revision already contains this uncommitted patch.

Native macOS file-picker interaction and native menu/keyboard acceptance remain manual RC checks. The harness exercises the picker-result seam and production read/write operations, not an automated OS file picker. Cross-Mac installation, signing, notarization and distribution are intentionally out of scope. Source-archive provenance must be supplied/verified in the future release process before such a build can be treated as an identified candidate; unknown provenance is never disguised.

Ready for RC review after approval of this focused diff; not a public-distribution approval.

## Changed files

- `Directory.Build.props`
- `ProductVersion.props`
- `README.md`
- `docs/user-guide/01-getting-started.md`
- `docs/user-guide/03-live-and-game-view.md`
- `docs/user-guide/07-experiments-and-decisions.md`
- `docs/user-guide/README.md`
- `docs/user-guide/assets/images/live-session-controls.png`
- `docs/user-guide/assets/images/simulation-insights.png`
- `docs/verification/flow-1-0-polish/Program.cs`
- `docs/verification/flow-1-0-polish/README.md`
- `docs/verification/flow-1-0-polish/Verification.csproj`
- `docs/verification/flow-1-0-polish/screenshots/about.png`
- `docs/verification/flow-1-0-polish/screenshots/before-start.png`
- `docs/verification/flow-1-0-polish/screenshots/live-session-controls.png`
- `docs/verification/flow-1-0-polish/screenshots/live.png`
- `docs/verification/flow-1-0-polish/screenshots/reset-confirmation.png`
- `docs/verification/flow-1-0-polish/screenshots/simulation-insights.png`
- `src/Simulation.UI/ProductIdentity.cs`
- `src/Simulation.UI/Simulation.UI.csproj`
- `src/Simulation.UI/ViewModels/LiveViewModel.cs`
- `src/Simulation.UI/Views/LiveView.axaml`
- `src/Simulation.UI/Views/LiveView.axaml.cs`
- `src/Simulation.UI/Views/MainWindow.axaml`
- `src/Simulation.UI/Views/MainWindow.axaml.cs`
- `src/Simulation.UI/Views/ProductDialogs.cs`
- `tests/Simulation.UI.Tests/ProductPolishTests.cs`
