# Game View B3.2 — Clean UI & Station Interaction

## Result

The sprite workshop keeps station titles, primary counts, important warnings and release feedback. Secondary metrics move to a read-only panel; explanations and motion controls remain available through Help. A summary shows at most three observed conditions in workflow order, with a count of additional conditions; it does not rank root bottlenecks.

Click any of the seven stations or its designated queue floor area. Click another to switch, empty floor to clear, or Close. The station dropdown offers a standard keyboard-accessible alternative. With the world focused, arrows cycle, Home/End select endpoints, Enter/Space open, and Escape closes. Selection remains stable during LIVE updates and restoration; reset clears it.

## Architecture

- `StationMap` owns disjoint world-coordinate polygons and keyboard order. The same geometry draws restrained hover/selection outlines and performs hit testing.
- `GameWorldControl.Selection` owns only presentation selection. Snapshot changes refresh immutable `StationDetails`; input handlers do not call simulation commands or reset the shared animation clock.
- `StationDetails` maps existing snapshots/configuration into rows. Active WIP and waiting are separate. Configured staff are distinguished from capacity observed on the last completed day. Code Review explicitly shares developer capacity; no independent available Review capacity is invented. Day zero omits unavailable observed capacity.
- Queue trend compares completed daily counts separately from queue condition. Scheduled release batching remains batching even with a large queue. The presentation classifier now includes the first effective snapshot after an intervention: a change recorded on session Day N affects zero-based snapshot Day N (displayed Day N+1). It previously excluded that first effective snapshot.
- The existing Avalonia `Viewbox` uniformly transforms the 1080 × 620 sprite world and its input coordinates. The legacy canvas remains 680 high. Fit height uses the actual LIVE scroll viewport, header and panel allowance. At Game View widths of 1250 or more the panel sits beside the scene; otherwise it sits below with two metric columns. Closing it returns its space to the world. No manual zoom or panning.
- Existing sprite assets, caches, queue stacks and B2/B3.1 animation controllers remain in use. No new artwork, dependency or runtime network access. No simulation-engine changes.

## Verification

Run from the repository root:

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b3-2/AnimationPreview.csproj -c Release
```

The native macOS harness opens the actual MainWindow using Avalonia's native software renderer. Screenshots are RenderTargetBitmap captures of those live controls, not mockups. Pointer tests first check the actual window input hit target, then dispatch routed pointer events; keyboard tests dispatch routed keys. This is automated native-control verification, not an OS-level mouse automation or screen-reader audit.

Verified: Testing waiting 35, active WIP 3/3, zero testers and observed 0/0 capacity; configured tester recovery and next-day observed capacity; Code Review shared-capacity constraint; scheduled release batching; normal operation; open/switch/close; queue hit alignment after resizing; uniform scaling and complete world visibility; selection across LIVE updates, checkpoint restore, animation toggles, fast advance and legacy rendering; selection does not modify full captured state/history/checkpoints or pause the running clock.

Requested window sizes: 960 × 820, 1440 × 1060 and 1840 × 1120. macOS constrains tall windows to the available display (973 logical pixels in this run). The harness checks the actual viewport rather than assuming the requested size was granted.

Focused unit tests cover polygon hit/overlap, keyboard order, responsive dimensions, detail mapping, queue trend versus condition, intervention-day boundaries, restored snapshots and three seeded 100-day runs whose full captures/results are unchanged by inspecting all station details.

Final result: build succeeded with 0 warnings and 0 errors. All 733 tests passed (218 Core, 216 Application, 299 UI), none skipped. Native verification passed. See `build.log`, `tests.log`, and `native.log`.

## Screenshots

- [Testing selected](screenshots/testing-selected.png)
- [Development selected](screenshots/development-selected.png)
- [Large window with side panel](screenshots/window-1840.png)
- [Small window](screenshots/window-960.png)
- [Code Review](screenshots/review-selected.png)
- [Scheduled release](screenshots/release-selected.png)
- [Clean balanced workshop](screenshots/clean-balanced.png)

## Limitations

Long details scroll inside the compact panel, particularly Development. At very small window heights the surrounding LIVE page can scroll; the world has a minimum usable height of 240. Legacy vectors remain functional/selectable but retain their existing in-world labels. Polygon selection refers to designated station floor regions, not individual animated workers or packages. Worker poses and trips remain illustrative. Reduced-motion behavior continues through the existing Animate activity control; no new OS motion-preference integration was added. Native screen-reader behavior was not audited.

Stopped at B3.2: no new mechanics, worker management, animation system or sprite redesign.
