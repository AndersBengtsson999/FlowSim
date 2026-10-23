# B6.5.3 verification

The implementation and audit are described in ../../user-guide/verification-report.md.

## Final results

Release build: zero warnings/errors. Full suite: **933 passed**, zero failures/skips (Core 218, Application 295, UI 420). Native macOS acceptance passed. Portable publish succeeded; isolated smoke validation loaded all **19 embedded guide resources** including mandatory illustrations.

## Files and delivered functionality

Added: eight English chapters, README and verification report in docs/user-guide; centralized registry.json; three real screenshot copies, separate annotation metadata, four generated diagrams and their reproducible source; GuideCatalog.cs, GuideMarkdown.cs, GuideNavigation (inside GuideCatalog.cs), GuideWindow.cs and GuideService.cs under src/Simulation.UI/Help; GuideTests.cs; native/publish verification harnesses and evidence.

Modified: Simulation.UI.csproj for pinned Markdig and embedded resources; MainWindow, GameView, KpiDashboardView and ProductLaunchView XAML/code-behind only where help entry points are integrated. Core, Application, Infrastructure, formulas, save format, charts, camera geometry and animation code are unchanged.

Completed: eight chapters, 73 stable topics, all 24 KPI mappings, ranked full-text search, back/forward, chapter/section/on-page/related navigation, single non-modal window, wide/medium/narrow layouts, native Markdown and offline images. Global entry is Getting Started; Game View entry is LIVE & Game View; actual stations, all KPIs and product/version/launch/economics links resolve to registered sections. No missing illustration placeholders remain.

Significant verified distinctions: active-stage WIP versus total WIP; internal release versus customer launch; raw capacity versus effective work; period expenditure versus accumulated allocation; recurrence from actual launch day; Before includes the intervention day and After begins the next day. Default full-availability 5-developer/2-tester cost is 28,750 SEK/day, not the different settings shown in the screenshots.

## Evidence

- build.log — full Release solution build.
- tests.log — complete Core, Application and UI test suite.
- native.log — native Avalonia production-view harness; an exception makes the process fail.
- publish.log and publish-smoke.log — published application and isolated resource validation outside the repository.
- screenshots/help-wide.png — three-column reader with real screenshot and separate numbered overlay.
- screenshots/help-1000.png — two-column reader.
- screenshots/help-650.png — narrow reader with chapter selector.
- screenshots/help-search.png — ranked full-text results.
- screenshots/help-economics.png — tables and verified cost-allocation documentation.

The native harness verifies Help window reuse and close; global, Game View, station context, selected KPI and product links; all chapter renders; search result and internal-link activation; Back/Forward; resizing; no model/history/intervention/customer-launch mutation; preserved selection and continuing LIVE playback. Existing compact KPI, station, camera, launch and Flow Board checks also execute.

## Reproduce

From repository root:

```sh
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet test SoftwareDevelopmentSimulation.sln -c Release --no-build --no-restore -m:1 -nr:false
dotnet run --project docs/verification/game-view-b6-5-3/Verification.csproj -c Release
dotnet publish src/Simulation.UI/Simulation.UI.csproj -c Release -o /tmp/flow-b653-publish
dotnet build docs/verification/game-view-b6-5-3/PublishSmoke/PublishSmoke.csproj -c Release
```

Then run PublishSmoke.dll with the absolute publish directory as its argument, from a directory outside the repo. It loads the published assembly and validates all mandatory chapters, IDs, internal/related references, KPI mappings and illustrations from embedded resources. It does not read guide files from disk. The smoke project has its own output/intermediate directory to avoid sharing NuGet assets with the Avalonia harness.

All station mapping IDs are additionally checked in GuideTests. Physical keyboard/screen-reader/trackpad acceptance remains manual. macOS may constrain requested window height to the current display; the native harness logs actual layout dimensions.
