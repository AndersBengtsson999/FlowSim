# Compact Live Layout

Presentation-only update following Capacity Availability, Always Available Work Supply and Live Status. No simulation semantics changed. Technical Debt has not been started.

## Before

Live had a large application title/navigation block, a second Live title and introduction, an oversized Day line, and a separate full-height Advance expander above its scrolling content. Status occupied a heading plus three rows. A fixed 380-pixel Flow Board column competed with permanently visible Team Performance beside it; the chart followed that analysis rather than the board. Flow cards repeated the Work Items label, a count bar and a separate arrow line.

At a 1280×800 native window, the seven-stage board began at y=471 and ended at y=873, outside the viewport. The board alone occupied 402 vertical pixels.

## New hierarchy

1. Compact global title and navigation while Live is selected.
2. Live Status: delivery row, a wrapping capacity/queues/supply row, and the latest intervention only when present.
3. One wrapping simulation toolbar.
4. Collapsible Configuration with current-session summary.
5. Full-width Flow Board.
6. Performance Trend immediately after the board.
7. Flow details/allocations, Team Performance, changes, Before/After, checkpoint/files and advanced settings through existing expanders.

One main Live ScrollViewer contains the page. The Flow Board's internal scrolling is disabled, so all stage rows participate in page layout. The nested allocations ScrollViewer was removed. Bounded secondary history/checkpoint lists retain their normal control behavior.

## Status and controls

All status values and their calculation paths are unchanged. The former standalone Status heading is represented by the band's automation name. Delivery retains its rolling-window label, capacity its observation day, and tooltips distinguish current configuration from last completed-day observations. Logical status groups wrap at insufficient width. Latest intervention has no reserved row when absent.

The toolbar reuses Start, Pause, Resume, Step, RunToDay, Change, Checkpoint and Reset commands without changing their behavior. Advance opens a compact flyout with the existing target-day input and command. Speed remains directly accessible. Reset is spaced apart and has a tooltip stating that it discards the timeline/checkpoints. Restore, labels and file operations remain in Checkpoints and files.

Setup is expanded initially, collapses automatically after a successful start or load, and reopens after Reset. Users can expand/collapse it during a session; the running view shows read-only configuration context and directs edits to Change. The summary identifies developer/tester counts, Development/Review/Testing WIP, supply and both availability percentages. Expanded context includes per-person nominal capacity, Rework WIP, defect enablement and seed. Initial inputs remain convenient before starting.

## Flow and analysis

Each full-width Flow row keeps the stage name, count, queue/active explanation and direction cue. Development retains both consumed capacity and effective work, without hiding them in a tooltip. Stage labels and explanations use the normal 14-point text instead of the former smaller 13/11-point combination. Repeated Work Items labels become one column heading; decorative count bars and separate arrow rows are removed. No stage or queue is removed.

Seven stages now occupy 238 pixels, or 302 pixels with both Rework stages. At 1280×800 without an intervention, the board starts at y=267 and ends at y=505: its starting position is 204 pixels higher than before. This gain comes from hierarchy/spacing and horizontal use of space, not smaller flow text.

Performance Trend retains its 210-pixel chart height and selectors. Its explanatory paragraphs move to heading/chart tooltips. Team Performance is collapsed by default and contains the existing delivery, flow/capacity/quality details and queue chart. Before/After remains its own expander; no analysis is duplicated or removed.

## Responsive and native validation

The native verification host uses the actual MainWindow, Fluent theme, bound controls, commands and native desktop lifetime. Screenshots were visually inspected. This is automated native workflow verification plus visual review, not a claim of manual clicking. Tests ran on macOS; requested desktop height 1000 was constrained by the display to 972 actual pixels.

Measurements below include a visible latest intervention. The viewport boundary is above the persistent status footer.

| Actual window | Stages | Board top–bottom | Viewport bottom | Trend heading top |
|---|---:|---|---:|---:|
| 1280×800 | 7 | 285–523 | 744 | 541 |
| 1600×972 | 7 | 285–523 | 944 | 541 |
| 960×720 | 7 | 285–523 | 664 | 541 |
| 1280×800 | 9, including Rework | 285–587 | 744 | 605 |
| 1600×972 | 9, including Rework | 285–587 | 944 | 605 |
| 960×720 | 9, including Rework | 285–587 | 664 | 605 |

**Target achieved:** Live Status, simulation controls and the complete Flow Board are visible without vertical scrolling at all three tested sizes. The beginning of Performance Trend is also visible. No horizontal page overflow was detected. Long intervention text can naturally wrap and require additional vertical space; expanded editors and secondary sections intentionally use the main scroll region.

The executable verified: launch in Live; expanded Setup; Start auto-collapse; Pause; exactly one Step; advancing to Day 100 through the bound flyout; checkpoint creation; intervention and visible latest change; advance to Day 140; all seven/nine stage rows; metric/range selection; opening Team Performance, Before/After and flow details; navigation away/back; reset returning to Setup. Serialization of the complete Live capture remained identical across presentation interactions, including checkpoints, history, configuration and random state. Status strings and chart selections also remained identical.

Run:

```sh
dotnet run --project docs/verification/compact-live/NativeVerification.csproj -c Release -- after
```

It saves screenshots under `/tmp/flowsim-compact` and exits nonzero on failure. Committed examples: [before laptop](verification/compact-live/before-laptop.png), [after laptop with Rework](verification/compact-live/after-laptop.png), [narrow with Rework](verification/compact-live/after-narrow.png), [desktop](verification/compact-live/after-desktop.png), [initial Setup](verification/compact-live/setup.png).

## Architecture and tests

Production changes for this task are limited to LiveView.axaml, MainWindow.axaml (Live-specific header styles) and LiveViewModel (SetupExpanded presentation state and configuration text projections). Core, Application calculations, persistence and existing command behavior are unchanged. Existing verification hosts were adjusted for the new Advance flyout and Status automation name.

Three new UI-state tests verify Setup lifecycle, current/loaded configuration summaries, and exact session/status/checkpoint/chart-selection preservation on disclosure. Complete suite: **364 passing tests** (110 Core, 159 Application, 95 UI), zero failures. Final Release build: **0 warnings, 0 errors**. Native workflow and geometry assertions passed. `git diff --check` passed.

No simulation semantics changed: availability, supply/arrivals, WIP, allocation priorities, Development Collaboration Model v1, testing, defects, interventions, checkpoints, rolling metrics, Before/After, persistence and determinism retain their established behavior. No health colors, targets, scores, recommendations or charts were introduced. Technical Debt has not been started.
