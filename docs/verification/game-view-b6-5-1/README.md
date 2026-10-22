# B6.5.1 — Workshop interaction and visual clarity

## Changes

- Automatic framing reserves the compact KPI heading rather than the entire card strip. Short windows scroll cards below the workshop. A 12-DIP camera margin keeps the complete scene comfortably framed. Manual zoom and position survive ordinary refreshes and resizing.
- Fixed missing ShowFlowProduct notification: Game View no longer retains a duplicate product-version header after switching views.
- Larger queue counts and status plates, scaled for readability and kept inside scene edges. Growing, constrained and blocked queues gain a restrained static translucent highlight and screen-scaled outline. Text cache resets when label size changes.
- Existing queue classification, observation windows, capacity evidence, inspection list expansion and selection handling remain unchanged. Sustained growth is potential pressure, not proof of a root cause.
- Vehicle hit testing follows its actual sprite bounds, including travel. Release inspection exposes awaiting count, estimated allocated cost, current version, launch plan/mode, launch count and customer-launched allocation.
- Vehicle label now shows items awaiting customer launch rather than cumulative internal releases. Cumulative release history remains in station inspection.
- Larger KPI values and supporting text, responsive widths, wrapping monetary values, unchanged /1,000 SEK display and WIP/awaiting components.

## Data and scope

ReleaseInspection consumes CustomerDeliveryMetrics and existing cached LaunchCostHistory. Awaiting cost excludes WIP and unallocated overhead. Launch reclassifies allocations without new expense or revenue. No station-level overhead allocation was added. Core, Application and Infrastructure production code are unchanged.

## Verification

Final build: zero warnings and errors. Full suite: 918 passed (Core 218, Application 295, UI 405), zero failures or skips. Native macOS harness passed.

Build and test results: build.log and tests.log. Native macOS verification: native.log. Program.cs is a standalone production-view harness using the Avalonia native software renderer, with optional --headless.

The manual-mode Day 42 scenario has active development, growing testing queue, waiting review items, 19 internally released items awaiting launch, nonzero WIP and 304,000 SEK awaiting allocation. The harness checks allocation against Launch Economics, vehicle hit target and camera preservation. Additional native checks cover three recurring customer launches, save/load, selection, window sizes 980/1440/1920, contextual KPI navigation, detached Explorer, card bounds and later launch planning.

Focused unit tests cover padded fit at two viewport sizes, manual resize preservation, moving vehicle bounds, product-header notifications, zero awaiting items, launch reclassification, restoration and no simulation mutation. Existing queue classification, station mapping, keyboard/scroll policy, KPI formatting and deterministic tests remain in the complete suite.

Representative screenshots: manual-day42.png, testing-day42.png, release-day42.png, release-inspection.png, kpi-cards.png, layout-980.png and layout-1920.png in screenshots/.

## Limitations

Physical two-finger scrolling and Cmd-scroll feel still need human acceptance on a Mac; automated input-policy tests do not substitute for physical gesture testing. The harness verifies production vehicle hit testing, rather than driving a physical mouse. Cards can wrap to a second row and require scrolling in short windows. Very small manually zoomed-out scenes necessarily reduce text readability. No new rates, station cost attribution or simulation rules were inferred.
