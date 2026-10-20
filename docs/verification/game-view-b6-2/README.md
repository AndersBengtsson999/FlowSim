# B6.2 — Stable Workflow Inspection & Trackpad Navigation

## Confirmed causes

B6.1 created a new GameSceneState inspection projection and a new StationDetails.Items array on every observed day. GameWorldControl copied `Expanded` booleans by ID, but then replaced the ItemsSource and all item objects. Avalonia therefore discarded/rebound the item controls: copying a boolean did not preserve control identity, focus or measured scroll content. The panel also placed summary, metrics, explanations and item details in one content-sized ScrollViewer; changes to those elements changed the list position. Default bring-into-view requests could reach the enclosing application ScrollViewer.

GameWorldControl.OnPointerWheelChanged explicitly zoomed on every nonzero vertical delta inside the workshop, then marked the event handled. It made no modifier distinction, so macOS two-finger scrolling was interpreted as zoom and could not reach the page.

## Changes

- `WorkflowInspectionPresentation` owns one ObservableCollection and retains InspectionItemViewModel instances by work-item ID. Ordinary refreshes update data properties; only actual membership changes add/remove/move rows. Expansion belongs to these UI objects, not to simulation snapshots or persisted documents.
- Station switches deliberately start a fresh inspection. Checkpoint restoration/save-load retain the station choice but reset old item interaction objects for the reconstructed session. Reset clears the inspection. A departing item is removed and a last-membership-change notice explains its disappearance.
- One virtualized ListBox provides the item scroll region. Its height is constrained within a stable panel; the summary, Info and Close controls stay outside that region. No inner scroll viewer exists inside item details. Bring-into-view requests stop at the panel; list scroll chaining is disabled. Refresh anchoring compensates for layout changes above surviving visible rows, when a visible anchor is available.
- The summary shows exact totals and disjoint group counts plus the existing queue trend. Group headings also show counts. Full existing metrics and explanatory distinctions are in Info's tooltip. The previous 50-item presentation cap is removed; only visible controls are realized by the list.
- Active work means admitted development/review/testing/rework WIP, not necessarily processing on the last day. Waiting capacity contains backlog without unresolved dependencies and explicit review/testing/rework waits. Blocked work requires authoritative unresolved residual/named prerequisites while in Backlog. Ready for release and Delivered have separate groups; they are not incorrectly classified as capacity waiting. No age heuristic infers blockage.
- Labeled, wrapping item fields show existing stage effort, state age, elapsed time, queue waiting, rework entries, last-day work and dependency information. WorkflowStateNames centralizes presentation-only status names. No enum or saved format changes.
- Ordinary vertical/horizontal wheel input returns without marking handled or changing the camera. Cmd+vertical scroll on macOS, Ctrl+vertical scroll on Windows/Linux, intentionally zooms around the pointer using the existing camera limits. Existing buttons, Fit, pan and inverse hit testing remain unchanged. Help now documents this behavior. Native pinch is deliberately not added without hardware verification.

## Verification

Release solution build: zero warnings/errors. Full suite: **838 passed** (Core 218, Application 258, UI 362), zero failures/skips. Focused tests cover ID and collection identity, expansion across property/group changes, removal, selection/session reset, disjoint groups, readable names, modifier policy, 30 real LIVE days, checkpoint and no simulation mutation. Existing viewport and persistence tests remain green.

The reproducible Avalonia harness passed in both a native macOS window and headless/Skia. It verifies 30 daily updates retaining the row object, Expander control, expansion and scroll offset; page offset when expanding/collapsing; accelerated playback/run/pause; intervention; checkpoint, save/load and reset; resize; KPI Explorer and camera. Headless input additionally sends real Avalonia horizontal/vertical wheel events and modifier-wheel events, checking handled state and zoom, plus list-boundary scroll ownership. Native execution is not physical trackpad input.

Screenshots are in `screenshots` (native) and `headless/screenshots` (headless). Build/test/harness logs are included. The long-label screenshot uses a narrow station sidebar with wrapping; a second screenshot covers a smaller desktop window. Virtualized rows may legitimately be recreated after scrolling out of view; their ID-owned expansion state remains in the presentation model.

## Physical macOS checklist (manual verification still required)

1. Open Game View, select Testing and expand an item.
2. Run 20–30 days, including accelerated playback; confirm expansion persists while the item remains in Testing and the list stays readable.
3. Move two fingers vertically over the workshop: the application should scroll without zoom. Horizontal movement should reach the appropriate horizontal scroller where one exists.
4. Hold Cmd and scroll over the workshop: only this explicit input should zoom.
5. Use +/− and Fit; pan and select stations/queues again.
6. Scroll within the item list to both ends: scrolling should remain owned by the list, with no page jump or zoom.
7. Expand and collapse long details; Close and Info should remain reachable.
8. Open KPI Explorer and verify its independent scrolling, including expanded view.

Physical trackpad inertia, OS gesture synthesis and native pinch are not established by simulated input. Windows/Linux were not runtime-tested here. If all visible anchors leave the collection, scrolling falls back to the list's valid clamped position. Actual departure removes the item; it is not kept as stale current work. Re-entering items start collapsed. No simulation mechanics, capacity allocation, KPI values, experiment logic or worker scheduling were changed.
