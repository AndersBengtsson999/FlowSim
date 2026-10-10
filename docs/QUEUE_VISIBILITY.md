# Queue Visibility & Accumulation Highlighting

## Deterministic semantics

This is read-only presentation, not a bottleneck prediction. Current count is authoritative. Let R = current queue count / max(1, reference scale).

| State | Rule, evaluated top to bottom |
| --- | --- |
| Neutral | Count = 0 |
| Strong | R ≥ 4, or R ≥ 2 and increasing |
| Attention | R ≥ 2, or R ≥ 1 and increasing |
| Neutral | Otherwise |

Increasing/decreasing/stable reuse `LivePerformance.Slope` (OLS) and `LivePerformancePresentation.Trend`. Stable remains absolute slope < 0.05 items/day; exactly +0.05 is increasing, exactly -0.05 decreasing. Observations are the most recent completed days in the selected 10/20/50/100-day performance window. No future data or invented zero observations. Missing or fewer than three observations yields an unavailable arrow (—), not Stable. Size alone can still warrant attention. Changing the performance window refreshes rows without advancing the simulation.

Shrinking and stable queues receive no growth uplift. For example R=2 is Attention when shrinking/stable and Strong when increasing. R≥4 remains Strong even while shrinking; count and ↓ show recovery explicitly. Zero queues are always quiet, even when the historical slope is positive. No smoothing, hysteresis or forecast is added.

| Existing queue/indicator | Reference scale |
| --- | --- |
| Waiting for Code Review | Current configured Code Review WIP limit |
| Waiting for Testing | Current configured Testing WIP limit |
| Waiting for Rework | Current configured Rework WIP limit |
| Ready for Release, finite Flow-based | Configured release items/day |
| Ready for Release, finite Scheduled | Release capacity / interval, the nominal daily equivalent |
| Ready for Release, Unlimited | Development WIP limit, a bounded existing system-scale reference |
| Specialist Work Waiting | Configured specialist count |

The minimum denominator of 1 avoids division by zero and amplification for fractional daily release capacity. The tooltip states both the raw reference and effective normalization scale. WIP limits are item slots, not daily throughput; scheduled daily equivalent is not a completion forecast. Zero specialists/release capacity is valid and handled. A configuration change can immediately alter relative size, while trend still describes actual completed-day history.

## UI and accessibility

Only actual waiting rows receive row tint. Backlog, active stages and Released do not receive queue severity. Specialist waiting retains its existing definition (active specialist Development items receiving no Development capacity) and gets a separately focusable inline tint; Development itself remains an active stage. No fake queue state or double-counted total.

Neutral retains existing appearance; Attention uses restrained amber #F6EACD; Strong uses restrained rose #F1DAD7. Shared brushes reside in LiveVisualStyles.axaml. Primary and supporting text on tinted rows uses LiveInk #203440, giving approximately 10.8:1 amber and 9.7:1 rose contrast. Hover/pressed/expanded treatments preserve tint. Count and ↑/↓/→ remain visible separately from ›/⌄ disclosure chevrons. Unavailable history uses —. Tooltips give count, trend/slope, reference, effective relative size and thresholds. Accessible names expose the same facts on keyboard-focusable controls; explicit focus outlines remain visible.

The application explicitly supports a fixed Light theme (`App.axaml`). The new brushes follow the existing shared Live palette; a forced-Dark verification also confirms legible highlighted rows and focus. This does not add full dark-theme support to the rest of the application.

No extra rows, panels, charts or legends. At both 1280×850 and 960×850 the normal eight-row board retains chart Y=559 and height=210. The pre-existing intervention summary can move the board after a change; queue highlighting adds no height.

## Architecture and regression

Shared calculation: `src/Simulation.UI/ViewModels/QueueAttention.cs`.

Other feature changes: `MainWindowViewModel.cs` (FlowStateRow presentation fields), `LiveViewModel.cs` (read-only history projection on refresh/window change), `Views/LiveView.axaml` (bindings, arrows, accessible names, inline specialist indicator), `Styles/LiveVisualStyles.axaml` (shared tints, contrast, focus).

No Core/Application production code was changed for this feature. Earlier Release implementation changes already present in the working tree are separate. Queue projection only reads snapshots/configuration. A regression test compares complete serialized session captures before/after projection/window changes, including items, history, configuration, costs, debt and random states. Capacity allocation, transitions, WIP, release, specialist eligibility, collaboration, debt, productivity, throughput, cycle time, Delivery Cost, System Cost and random sequences remain unchanged.

## Validation

12 new UI test cases in `QueueAttentionTests.cs`: empty/small/stable/growing/large/shrinking/scale/zero denominator/Stable boundary cases, missing and singleton history, release and specialist projection, exclusion of active stages, window refresh and serialized-session immutability.

Passing tests: **569** = 204 Core + 205 Application + 160 UI. Final UI build/test and verification-host build: no warnings/errors. Initial Avalonia selector compilation errors were corrected before the final runs. `git diff --check` passes.

Automated full Avalonia view rendering with Skia/headless and visual inspection, seed 12345, Always available, 20-day window:

| Scenario | Observation |
| --- | --- |
| Baseline, Day 100 | Review 2 →, Testing 0 →, Ready 0 →; all Neutral |
| Testing constrained, 1 tester, effort 10, WIP 3 | Testing queue 68 ↑; Strong |
| Same testing workload, WIP 30 | Testing queue 41 ↑; Attention (41/30) |
| Release Scheduled 1 every 5 days | Ready 60 ↑; Strong; Completion Rate exceeds release Throughput |
| 5 developers, 1 specialist, 100% specialist work | Specialist waiting 4 →; Strong inline; developer utilization 36%; unrelated waiting rows Neutral |
| Recovery, testers/WIP raised to 30 on Day 100 | Day 120 Testing queue 28 ↓, OLS -1.477/day, R=28/30; Neutral |
| Simultaneous testing constraint and release capacity 0 | Testing 68 ↑ and Ready 9 ↑ both Strong; other rows quiet |

Evidence: [verification log](verification/queue-attention/result.txt), [baseline](verification/queue-attention/screenshots/baseline.png), [testing](verification/queue-attention/screenshots/testing.png), [attention](verification/queue-attention/screenshots/attention.png), [release at 960px](verification/queue-attention/screenshots/release-960.png), [specialists at 960px](verification/queue-attention/screenshots/specialists-960.png), [recovery](verification/queue-attention/screenshots/recovery.png), [multiple queues](verification/queue-attention/screenshots/multiple.png), [keyboard focus](verification/queue-attention/screenshots/release-focus.png), [forced Dark](verification/queue-attention/screenshots/release-dark.png).

Native macOS startup was attempted twice but failed before the window opened with Avalonia.Native RenderTimer error -6661. Therefore these are automated headless Avalonia checks and visual review, not a completed manual/native desktop run. The harness supports both native (default) and `--headless` for repeating verification. Headless focus screenshots include an extraneous top-level focus-adornment rectangle; the actual focused queue outline is visible.
