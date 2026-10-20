# B6.1 — Interactive Workflow Inspection

Reuses GameWorldControl selection, StationMap world-space geometry, WorkshopViewport camera transforms, the existing station panel, GameSceneState, QueuePressure and authoritative WorkItem transitions and daily observations. No Core, Application, persistence or KPI calculation changes.

## Interaction

All seven existing stations remain selectable. Sprite queue pads for residual dependencies, code review, testing and rework have smaller priority hit regions. They are also available in the station picker. Queue targets are disjoint from one another and take priority over their owning station. Existing station keyboard navigation remains unchanged. Vector mode retains its existing station targets; queues remain accessible through the picker.

The panel shows purpose, observation day, exact counts, WIP limits, existing capacity observations/warnings, queue trend and utilization when a positive observed denominator exists. Developer utilization is explicitly shared across stages; no per-stage available developer allocation is invented. Missing utilization is N/A. Existing pressure classifications are observations, not proof of a root cause or recommendations.

Expandable work-item rows show ID, state, configured base effort by stage, elapsed state time, elapsed time to delivery or now, queue waiting, rework entries, unfinished prerequisites/residual dependency status, and work received during the last completed day. These are detached presentation values. Expanded rows persist by ID within the same selected queue/session while membership is refreshed. Leaving a queue removes the row; reset clears selection; restoration/load discard old item presentation identities.

Durations use differences between simulated-day boundaries, not inclusive day counts. Queue waiting includes review, testing, rework and ready-for-release intervals, excluding backlog and admitted active time. Missing/discontinuous transition history displays N/A for history-dependent fields. Zero observed work does not by itself prove why an admitted item was not processed. Technical debt remains aggregate maintenance with no invented individual tasks.

## Verification

Focused tests cover queue geometry after zoom/pan, membership and counts, elapsed state boundaries, residual dependency subset, missing history/capacity, save/load through LiveSessionJson, reset and read-only state. Existing tests cover deterministic replay, capacity rules, station interactions, camera and KPI calculations.

The reproducible Avalonia harness exercises queue selection, item expansion, LIVE run/pause/resume/step, resize, zoom/pan, KPI Explorer, checkpoint restoration and reset. The initial native macOS run passed. A subsequent native run could not start Avalonia's render timer (native error -6661); final layout and contrast are verified using Avalonia headless/Skia rendering. Final screenshots are in `headless/screenshots`; `screenshots` contains the earlier native layout. Headless evidence does not establish final OS pointer-event dispatch.

Final Release build: zero warnings and errors. Full solution tests: 825 passed (Core 218, Application 258, UI 349); the final UI-only rerun after presentation adjustments also passed all 349 tests. Headless UI verification passed. Build, tests and render-timer diagnostic logs are included.

## Limits

- Exact total counts; at most 50 item rows per selection, in model order, without pagination.
- Stack artwork remains an aggregate representation; it is not a target for a specific work-item identity.
- The dependency pad corresponds to the existing residual-dependency subset; individual rows also report unfinished named prerequisites.
- Work size is base stage effort, not a completion percentage or an assignment. Rework counts are entries into the rework state.
- A short window may require panel/page scrolling; workshop camera and KPI Explorer remain independent.
- No animation, simulation mechanics, capacity rules, KPIs or experiments were added or changed.
