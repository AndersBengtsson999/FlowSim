# B5.4 — Game View usability and polish

## Issues found and changes

- A tall four-card sidebar left unused space next to the workshop and reduced its width. The compact dashboard now uses a horizontal row below the workshop. Its measured height is reserved when fitting the world, with a minimum usable workshop height. The inline Explorer uses a sidebar only at sufficient width; station inspection retains its own responsive placement.
- Small graphs and nested scrolling made investigation difficult. A yellow **Expand Explorer** action opens one owned, non-modal window using the same `KpiDashboardView`, existing charts and LIVE view model. It does not copy simulation state or calculate a second set of KPIs. The inline Explorer gives its space back to the workshop while expanded.
- Expanded graphs are 300px high. Two KPI graphs appear side by side in wide Explorer windows and stack at narrow widths. Inline graphs are 160px high. The expanded window can be moved/resized while LIVE remains usable; Escape or **Return to compact view** returns to the inline Explorer without changing selections or comparison mode.
- Rolling evaluation days and displayed history are explicitly distinguished. Scenario comparison continues to use the existing matched experiment period. Original units, numeric scales, actual day ranges, tooltips and unavailable values are unchanged.
- Selected KPI cards have an accent border. Expand/focus controls, button sizing, text contrast and spacing are consistent with FLOW's yellow, dark workshop and light information surfaces. The expanded information window explicitly uses the existing light surface theme.
- A quiet workshop status line shows actual releases on the current simulated day, the effective day of the latest recorded intervention, and completion of a valid prepared experiment. Existing queue/bottleneck observations remain visible. No new timers, alerts, animations, rules or inferred bottlenecks were introduced.

## Exit Application

The main header has an **Application** menu with **Exit Application** and its platform shortcut. On macOS, a native FLOW menu also exposes the command and Cmd+Q is registered. Windows retains its standard Alt+F4 window behavior and the menu displays Alt+F4. Expanded Explorer also wires macOS Cmd+Q to the same main-window command.

The command calls Avalonia `IClassicDesktopStyleApplicationLifetime.TryShutdown`, which respects cancellable window closing, rather than killing the process. Closing the main application cancels existing background work and disposes LIVE's timer/baseline work. It does not reset or automatically save the simulation.

No reliable dirty-state/unsaved-change mechanism was present, so none was invented. There is no new unsaved-change prompt. The standard framework close behavior remains in force.

## Preserved behavior

No files in Simulation.Core or Simulation.Application are changed by B5.4. KPI formulas, histories, experiment branching, deterministic RNG, costs, staffing allocation, sprites and animation scheduling are unchanged. Expanded/compact state is presentation-only and is not serialized into LIVE files. Existing card/window preferences keep their prior persistence behavior.

Opening/closing/resizing Explorer preserves station selection, manual camera zoom, KPI choices, evaluation window and both comparison modes. The existing camera handles resize while preserving the manual view and Fit to View still resets it. The expanded view observes normal LIVE notifications and checkpoint changes, with subscriptions removed on detachment.

## Verification

`GameViewPolishTests` verifies presentation-only state changes against serialized simulation state and KPI values, status text against authoritative release/change observations, checkpoint/reset behavior and valid experiment completion. The full suite retains all prior camera, station, determinism and KPI tests.

The native macOS harness in `Program.cs` uses real MainWindow/GameView/Explorer controls and checks ownership/non-modal expansion, wide/narrow resizing, LIVE refresh, comparison-mode and station preservation, manual zoom/pan/Fit, unchanged simulation state and exit behavior. It verifies the Cmd+Q gesture registration/match and executes its bound command; this is not an OS-level synthetic keyboard injection. A cancellable Closing handler confirms `TryShutdown` is respected; after removing that handler, the harness invokes the final Exit command. Rendered Avalonia screenshots are saved below.

## Remaining limits

- Small screens may require scrolling. Expanded Explorer is an ordinary owned desktop window and may overlap the workshop; move it or use a second screen when simultaneous large views are needed. It is not modal and does not block playback or station inspection.
- The workshop retains its isometric aspect ratio, so some letterboxing is unavoidable. It has a minimum usable height; short windows may still scroll.
- Windows runtime behavior was not executed on this macOS host; it uses Avalonia's standard close/lifecycle APIs and Alt+F4 behavior.
- No new save warning, autosave, notification history or experiment persistence was added.

### Results

Release build passed with zero warnings and errors. **820 tests passed**: Core 218, Application 258, UI 344; no failures or skips. Native macOS verification passed and exited using the application command. The final layout preserves at least 420 logical pixels of workshop height when width permits; short windows scroll rather than making the sprites unreadably small.

- [Compact workshop and dashboard](screenshots/compact-workshop.png)
- [Expanded side-by-side KPI graphs](screenshots/expanded-kpis.png)
- [Narrow expanded Explorer](screenshots/expanded-narrow.png)
