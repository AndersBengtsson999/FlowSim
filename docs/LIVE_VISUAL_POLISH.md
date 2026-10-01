# Live Visual Polish

A focused presentation refinement of the existing Fluent-based Live UI. The navigation, information architecture, commands and simulation remain unchanged.

## Before

The previous screen was compact and functional, but Status read as long sentences with little label/value distinction. Toolbar actions had nearly equal weight, simulation state was disconnected at the bottom, Configuration was visually flat, and Flow rows mixed WIP, capacity and explanations into one text field. Expanded items had individual card outlines. Chart controls looked like a separate form. Flow categories used relatively prominent waiting-state yellow.

The original application was built and its full 369-test suite passed before editing. The native application was inspected in setup, running, paused and stopped states, with expanded Configuration/items, both work-supply modes, a large queue, chart/interventions and secondary analysis at laptop through wide-desktop sizes.

## Small shared visual system

`Styles/LiveVisualStyles.axaml` adds a restrained shared palette: dark data text, muted labels, light separators/surfaces, existing teal interaction emphasis, and subtle active/waiting/completed category tints. Typography stays at 12 for secondary labels, 14 for interface values and stage names, and 16 for section headings and the quieter application title. Weight and tone carry hierarchy. Compact panel/control padding and a four-pixel corner radius are shared resources.

The existing Fluent theme and control templates remain in use. Default keyboard interaction, focus adorners, hover/pressed states and disabled behavior are retained; disclosure checked surfaces retain their category colors rather than becoming bright selection blocks. Navigation remains Live / Analyze / Advanced with a consistent selected treatment. No performance health colors, targets or decorative animations were added.

## Status and controls

Status remains two compact wrapping groups of metrics plus the optional latest-change line. Each group distinguishes a muted label from its value. Day, Done, WIP, recent Throughput/Cycle Time, Dev/Test used/available/utilization, queues and work supply remain present. The selected delivery window is visible; tooltips identify the completed day used for capacity and describe neutral trend arrows. These are formatting projections over the existing LiveStatus and LivePerformance observations; the old status semantics are unchanged.

The toolbar separates execution, experiment and session actions using small gaps and light separators. Change remains the principal accent. Reset is quiet but visible. A neutral dot and Running/Paused/Advancing/Stopped/Ready text now sit with the controls. The old bottom status placement is removed. Detailed feedback/errors remain next to the toolbar when useful, with the full Status text also available as a tooltip. Commands and their eligibility are unchanged.

Configuration retains the existing expander and state preservation. Its heading now emphasizes “Configuration” while keeping the current team/WIP/supply/availability values readable. Expanded context remains secondary and read-only during a session; interventions still use Change. Setup remains expanded before Start and after Reset.

## Flow Board

Rows use stable stage, active-WIP, supporting-data, item-count and disclosure columns. Stage names are strongest; explanations are secondary. Development displays Capacity used and Effective work as separate labeled values copied from the existing daily ledger. No new capacity denominator is invented: Review and Rework share the developer pool, so the row continues reporting Development consumption without implying it owns the entire pool.

Active Development, Code Review, Testing and Rework rows receive a neutral thin WIP bar with an explicit current active count / configured limit. It depicts occupancy only. The numeric label is not clamped when an intervention lowers the limit below existing WIP; the progress track visually saturates. Waiting, Backlog and Done have no WIP indicator. A Fluent default minimum width was overridden so the small indicator stays inside its column.

Waiting rows use a light warm-neutral category surface, not a warning. Done uses a quiet completed-state tint, not a judgment of performance. Conventional `› / ⌄` disclosure remains distinct from `↑ ↓ →` trend arrows. Entire headers remain toggle targets.

Expanded Work Items keep the existing snapshots, membership and disclosure behavior, but replace card outlines with indentation and subtle bottom separators. Compact entries wrap horizontally, with no new inner scrolling or truncation. The visual host inspected a 44-item waiting queue and multiple open rows; the dedicated native disclosure regression verified all 105 items in a larger queue, including the final item, plus live updates, collapse and navigation.

## Performance Trend and secondary analysis

The existing title, metric selector and range selector share one compact wrapping header. Chart height remains 210. Axis labels increase from 11 to 12, horizontal guides become subtle, the existing line uses a restrained blue, and intervention markers retain dashed styling with a slightly lighter selected width. Hover descriptions still use exact underlying points and existing marker explanations. No data, interpolation/bucketing, scale computation, metric or marker timing changed.

Flow Board and Trend share section typography. Team Performance and Before/After remain in their existing expanders. No feature was relocated or duplicated.

## Actual window validation and density

Native verification uses the real MainWindow, native Avalonia desktop lifetime, current bindings and viewmodels. Window screenshots were visually inspected. This is automated native workflow verification plus visual inspection, not a claim of physical manual clicking.

All measured positions below are native window coordinates with Configuration collapsed and a visible latest intervention. The wide window's requested height of 1000 was constrained by macOS to approximately 970 pixels.

| Actual window | Flow stages | Flow Board top–bottom | Trend heading top | Complete board and Trend heading without scroll |
|---|---:|---|---:|---|
| 1280×800 laptop | 7 | 286–531 | 555 | Yes |
| 1280×800 laptop | 9 including Rework | 286–601 | 625 | Yes |
| 1600×960 desktop | 7 / 9 | 286–531 / 601 | 555 / 625 | Yes |
| 1920×approximately 970 wide desktop | 7 / 9 | 286–531 / 601 | 555 / 625 | Yes |
| 960×720 narrower desktop | 7 / 9 | 286–531 / 601 | 555 / 625 | Yes |

**Live Status, controls, Configuration summary and the complete Flow Board remain visible without vertical scrolling at every tested size. The beginning of Performance Trend also remains visible.** Horizontal page overflow was checked and absent. Seven-stage board height decreased from 268 to 245 pixels, despite adding the neutral occupancy indicators. Expanded item lists and analysis panels intentionally use the normal page scroll.

The native host covered initial Setup; Running, Paused and Stopped; Fixed rate and Always available; expanded/collapsed Configuration; Development and waiting-queue disclosure; multiple open rows; large queue; Performance Trend and interventions; Team Performance and Before/After; navigation; and exact serialized session preservation through presentation interactions. A transient macOS RenderTimer startup failure was resolved by rerunning the host without changing application rendering.

Reproduce:

```sh
dotnet run --project docs/verification/live-visual-polish/NativeVerification.csproj -c Release -- after
dotnet run --project docs/verification/flow-disclosure/NativeVerification.csproj -c Release
```

Screenshots: [before](verification/live-visual-polish/before-laptop.png), [after](verification/live-visual-polish/after-laptop.png), [nine-stage laptop](verification/live-visual-polish/after-laptop-rework.png), [wide desktop](verification/live-visual-polish/after-wide.png), [narrow](verification/live-visual-polish/after-narrow.png), [expanded items](verification/live-visual-polish/expanded-items.png), [Configuration](verification/live-visual-polish/configuration.png), [stopped state](verification/live-visual-polish/stopped.png), [Before/After](verification/live-visual-polish/before-after.png).

## Architecture, tests and scope

Changed production files: LiveVisualStyles.axaml, LiveView.axaml, MainWindow.axaml, LivePerformanceTrendChart drawing styles, FlowStateRow/FlowPresentation display fields, and LiveViewModel formatting/state-label properties. No Core, Application or Infrastructure files changed.

Three additional UI tests cover WIP labels/limits and unchanged capacity values, read-only grouped status/configuration, and toolbar state/message presentation. All existing disclosure, Live state/status, commands, configuration, trend, intervention, checkpoint and navigation tests remain green.

**372 tests pass**: 110 Core, 159 Application, 103 UI. Final Release build: **0 warnings, 0 errors**. Native visual and disclosure regressions pass. `git diff --check` passes.

**No simulation semantics changed.**

**No new simulation features were introduced.**

**Technical Debt has not been started.**
