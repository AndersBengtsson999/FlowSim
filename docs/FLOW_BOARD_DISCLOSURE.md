# Flow Board disclosure fix

## Root cause

The Live row's arrow was a TextBlock bound to FlowStateRow.Arrow. It was decorative direction text, not a disclosure control: no expansion state, toggle binding or inline Work Item content existed. Selecting the ListBox row updated SelectedFlow/QueueDetail, whose summary was shown separately under Flow details and allocations. That made clicking an apparent disclosure arrow look ineffective.

## Fix

The existing Live FlowRow template now has a full-width ToggleButton header. Its two-way IsChecked binding controls FlowStateRow.IsExpanded, with property notifications for the disclosure chevron and visible items. The header is the click target; Work Item content is outside that button and does not toggle the row. Standard keyboard/accessibility toggle behavior is provided by Avalonia. Checked headers retain their state color and readable foreground rather than adopting a bright checked-button fill.

Collapsed uses `›`; expanded uses `⌄`. Live Status continues using `↑ ↓ →` only for trends. All nine states support disclosure, including Backlog and Done. Empty states display “No Work Items in this state.”

The existing FlowStateRow presentation record references existing immutable WorkItemDaySnapshot objects. No new Work Item model or simulation calculation was introduced. Rows match exact end-of-day State, excluding future-created observations consistently with the existing count definitions. Active and waiting states are not mixed; StateDuringDay is not used for membership. Each compact item shows its ID and remaining Development, Review, Testing and Rework effort, using the existing snapshot fields.

Ordinary refreshes transfer expansion state by WorkItemStatus to refreshed rows. Contents and counts therefore follow the latest completed day while open. Navigation with the same Live ViewModel preserves disclosure; Reset clears the rows and restores collapsed defaults. Nothing is added to persistence.

Compact cards wrap across the available width and use the existing main Live scroll region. No inner queue ScrollViewer, item cap, pagination or truncation was added. A 105-item waiting queue was verified through its last item. Expanding an empty Always available backlog does not generate work.

## Verification

Five new UI-state tests cover default collapsed/toggled state and chevrons; exact membership for every state across 120 days; empty backlog and large queues; navigation/reset behavior; and identical deterministic continuation with and without repeated disclosure interactions.

Complete suite: **369 passing tests** (110 Core, 159 Application, 100 UI). Release build: **0 warnings, 0 errors**. `git diff --check` passes.

The actual Avalonia MainWindow was opened at 1280×800. Its native ToggleProvider was exercised to expand/collapse Development and expand Waiting for Testing. IDs were checked in the rendered visual tree. Live playback updates preserved the open queue and matched the current snapshot. Testing availability was changed using the existing intervention mechanism to construct a large waiting queue; all 105 items, including the last, remained accessible via the main page scroll. Empty Backlog and navigation were verified, along with exact serialized session equality before/after disclosure interactions. Screenshots were visually inspected. This is automated native UI validation and visual inspection, not a claim of physical manual clicking.

A first native launch hit a transient macOS RenderTimer failure. A subsequent launch succeeded without any production rendering change. The harness waits for layout after refresh/scroll before querying realized controls.

Reproduce:

```sh
dotnet run --project docs/verification/flow-disclosure/NativeVerification.csproj -c Release
```

It exits nonzero on failure and saves images under `/tmp/flowsim-disclosure`.

Reviewed images: [Development expanded](verification/flow-disclosure/development-expanded.png), [large waiting queue](verification/flow-disclosure/large-queue.png), [last queue item](verification/flow-disclosure/large-queue-last-item.png), [empty Backlog](verification/flow-disclosure/empty-backlog.png).

Production changes are restricted to FlowStateRow, FlowPresentation, LiveViewModel refresh presentation and LiveView.axaml. Existing detailed summaries and Development allocation inspection remain available.

**No simulation semantics changed.** Work Supply, Capacity Availability, utilization, WIP, collaboration, queues, interventions, Performance Trend and Technical Debt are unchanged.
