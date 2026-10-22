# B6.5 — Game View and LIVE UX refinement

## Scope and layout

This change is confined to UI presentation. Simulation, capacity allocation, economic calculations, release scheduling, product version lifecycle and persisted state are unchanged. Artwork and animation scheduling are unchanged.

Workshop header spacing is tighter, and day feedback plus workflow observations share a wrapping line. The compact version/next-launch/awaiting summary, launch decisions, notices, economics details, Flow Board selector and existing KPI dashboard remain available. Inspection moves alongside the workshop from 1,120 logical pixels of available workshop width (previously 1,250); below that it remains beneath the workshop. The panel uses a responsive height with a 420-pixel minimum so contextual information cannot collapse the work-item list. The short context section has a bounded scroll area; the existing item-list scroll anchoring remains unchanged. No wheel handlers were added.

## Workflow and selection

Existing floor routes, arrows and open-corner waiting pads are retained. Waiting Review/Testing labels are explicit; the shipping station reads **Internal Release** to distinguish it from customer launch. No fictional transitions or employee assignments were introduced.

Selected stations and queues have a blue outline at two logical screen pixels independent of camera scale. Its overlay has no fill over sprites. Existing hit regions, queue precedence, camera transforms, keyboard clearing and mouse/trackpad handlers are unchanged.

## Queue indicator rules

These are observations, not proof of root cause or new simulation mechanics. Existing pressure thresholds are preserved; shrinking adds a presentation category.

| Condition | Evidence / meaning |
|---|---|
| Neutral | Count alone, including a large queue, is insufficient evidence of a bottleneck. |
| Growing ↑ | Latest completed-day count exceeds the preceding count. |
| Shrinking ↓ | Positive latest count is below the preceding count. Green, static; not a claim that throughput rose. Empty queues remain neutral. |
| Sustained pressure Ⅱ | Four nondecreasing daily counts with net growth, and full observed capacity-pool use on each of the last three days. Review/rework also require positive stage work on those days. Presented as a **potential** bottleneck. |
| Zero configured capacity ! | Waiting work exists while the configured capacity is zero. The explanation states that newly configured changes take effect next day. |
| Scheduled batch B | Ready work under scheduled release policy; accumulation alone is not a capacity diagnosis. |
| Dependencies D | Explicit unresolved dependencies; not a capacity diagnosis. |

The capacity comparison uses the existing tolerance of `1e-8`. The evidence window is at most four completed snapshots after the latest intervention; it never mixes pre/post-change evidence. Inspection shows the exact day range and explains the threshold. Last-day utilization/WIP are separately labelled; related period KPIs explicitly use the dashboard evaluation window.

## Context and KPI integration

Station/queue inspection displays condition, evidence period, concise reason and existing active-WIP/utilization rows. Detailed work-item expansion remains available with its previous identity and scroll-anchor behavior. Neutral conditions use neutral text; shrinking uses success color; warnings retain their semantics.

The expandable **Related KPIs · open Explorer** section projects existing KpiDashboard definitions:

- Backlog/dependencies: Development queue and shared developer utilization.
- Development/rework: shared developer utilization, Development queue and Rework effort. Exact active Development WIP remains in station details; it is not mislabeled Total WIP.
- Review: Review queue and shared developer utilization, not an invented dedicated review utilization.
- Testing: Testing queue and tester utilization.
- Ready/Internal Release: internally released throughput and Unlaunched Development Cost; existing version/next-launch/awaiting summary and a link to launch details.
- Technical Debt: outstanding debt, repayment effort and shared developer utilization.

Links open the existing detached KPI Explorer without clearing queue selection. Values reuse existing observations, cost cache, unit scaling and percentage presentation. Context refresh tracks session, day, launches, selected window and station; it does not change the user's dashboard selection. No analytics subsystem or KPI formula was added.

## Verification

Focused tests cover transient/sustained/shrinking classification, insufficient evidence, exact post-intervention observation range, station-to-KPI mapping, authoritative value equality, no simulation mutation and constant-width selection outlines. Existing selection, expansion, scrolling, zoom/pan, KPI, replay, save/load, release and cost tests remain in the full suite.

The Avalonia acceptance harness uses active workflow queues, scheduled internal releases and recurring customer launches at days 60/120/180. It checks queue selection across resize and Explorer navigation, context-window changes, accessible work-item list height, KPI cards without horizontal clipping, zoom/Fit, launch reclassification and unchanged expense. It renders 980×720, 1440×1000 and 1920×1080 layouts using native macOS and headless Avalonia.

Screenshots:

- [Desktop inspection](screenshots/layout-1440.png)
- [Wide workshop](screenshots/layout-1920.png)
- [Narrow workspace](screenshots/layout-980.png)
- [Station context](screenshots/station-context.png)
- [KPI Explorer](screenshots/kpi-explorer.png)
- [Compact workshop](screenshots/compact-game.png)

## Limits

- Queue pressure indicates observed conditions, not a proven root bottleneck. Shared developer utilization is not a dedicated stage allocation.
- Related KPIs use the chosen dashboard period; queue-pressure evidence uses its explicitly labelled four-snapshot window. These are not silently equated.
- At smaller window sizes the main page scrolls to the inspection panel and KPI cards; artwork is not shrunk indefinitely to fit everything at once.
- Physical macOS two-finger/keyboard/panning checks remain a hands-on acceptance step. Existing input code is unchanged; automated camera and selection checks do not emulate a physical trackpad.
- Windows/Linux runtime appearance was not exercised.

Stop point: B6.5 only.

## Final results

Release solution build: **0 warnings, 0 errors**. Complete suite: **913 passed, 0 failed** (Core 218, Application 295, UI 400). Final UI-only rerun after the context refresh/height correction: **400 passed**. Native macOS and headless acceptance both passed, including the three window sizes, minimum inspection-list height, context window synchronization and KPI navigation preserving queue selection. `git diff --check` passed. Core, Application and Infrastructure have no changes.
