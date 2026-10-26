# Stable Simulation Insights ordering

## Cause

InsightsWindow.Refresh called InsightPresentation.Ordered on every InsightsReport update. That helper sorts by current relevance/group, category and identifier. When relevance changed, existing entries moved between groups and list positions. Selection used an ID, but a missing ID triggered a new default selection.

## Change

Each InsightsWindow now owns an InsightViewingSession. On its first nonempty update it uses the existing priority/group rules and default-selection helper. It freezes relative entry order and each entry’s presentation group for the window lifetime. The original groups remain Active findings, Other current observations and Previous findings; the header explains that placement stays fixed while row statuses are current.

An ObservableCollection is assigned to the ListBox once. Existing row objects use property notifications and bindings to update content by stable observation ID without replacing containers. Newly discovered IDs append to their current analytical group in deterministic priority order. Existing entries retain relative order; inserting in an earlier group can shift later indices, which are never used for selection. Classifications, trend labels, evidence and explanations can change normally. A tracked historical observation stays in its original slot. If the tracker actually removes an ID (for example on session reset), the entry is marked unavailable and its selected ID remains; the detail reader shows an explicit message instead of old actionable evidence or a different selection. Reappearance restores the same slot.

Closing discards this window-local ordering. Reopening creates a fresh order and selects the highest-priority active finding, otherwise current information, otherwise the most recent historical finding. No data is persisted and no analysis acknowledgement rules change.

## Verification

- Release solution build: successful, zero warnings/errors.
- Full automated suite: 999 passed (Core 218, Application 295, UI 486), no failures/skips.
- Seven focused regression tests cover repeated priority changes and Growing→Stable→Shrinking, frozen groups, tracker-retained history, deterministic append order, selection and unavailable/reappearing observations, reopening/default selection and identical-input ordering.
- Actual Avalonia headless/Skia harness verifies the rendered list order, retained selection/history, frozen original group and explicit unavailable state after reset; serialized simulation state remains unchanged by inspection.
- The headless UI also advances eight actual simulation days with the Insights window open and checks collection identity, row identity, selected observation ID and unchanged nonzero list scroll offset after each update.
- OrdinaryUpdatesKeepCollectionAndRowIdentity asserts no collection reset/replacement on twenty content updates. ChangingPriorityAndTrendsNeverMoveEntriesOrGroups reproduces the original relevance-based reorder trigger.
- A sandbox-only test invocation could not create MSBuild IPC sockets; verification was rerun with normal host permissions. No test failure was attributed to this environmental restriction.

Run from repository root:

```sh
dotnet run --project docs/verification/insights-ordering/Verification.csproj -- --headless
```

## Scope

Production changes are limited to InsightsWindow.cs and the new InsightViewingSession.cs. Regression tests are in InsightViewingSessionTests.cs. Detection, queue trends, pressure, evidence calculations, simulation, KPIs and persistence are unchanged. Headless screenshots are supporting evidence, not native macOS desktop verification.

## Modified files for this task

- `src/Simulation.UI/Insights/InsightViewingSession.cs`: stable mutable presentation rows and group placement.
- `src/Simulation.UI/Insights/InsightsWindow.cs`: one collection binding, property-bound labels, stable selection.
- `tests/Simulation.UI.Tests/InsightViewingSessionTests.cs`: seven regression tests.
- `docs/verification/insights-ordering/Program.cs`, this report and headless screenshots: repeated-update UI verification.

QueueTrend.cs, StationDetails.cs and queue-trend diagnosis artifacts already had uncommitted changes from the preceding task; this ordering task did not change them. Stable identities come from existing semantic insight IDs (queue/station or finding kind), never generated indices. Unavailable placeholders exist only for the open window and disappear on reopening unless the authoritative tracker retains them. No synthetic historical observations are created. Native macOS manual scroll testing remains a follow-up; automated rendering used Avalonia headless/Skia.
