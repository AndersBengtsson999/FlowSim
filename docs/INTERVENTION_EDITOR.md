# Live intervention editor — compact rows and debt calibration

The Paused · Change something panel is a left-aligned 600-pixel table. Parameter, read-only Current and editable Try stay together rather than spanning the window. Team & Capacity, Productivity, Technical Debt and WIP use lightweight headings and spacing. Current and Try share column positions across groups. The panel scrolls vertically with Live; expanding Advanced does not change its width. The existing application minimum window width remains 960; the native verification also temporarily lowers that minimum to inspect a 720-pixel window.

Percentage and multiplier units appear beside Current and Try values. Full original parameter names remain the input automation names and tooltip targets. Current is a TextBlock backed by a getter-only property. Try writes through the existing draft setters, and existing Apply/Cancel, validation and intervention recording are retained.

A row's `:focus-within` style highlights its full background with Avalonia's theme-provided `SystemControlHighlightListLowBrush`. No focus state is stored in the simulation or ViewModel. A visible “Changed” label supplements color and disappears when Try returns to the original numeric value. `SimpleChangeField` notifies bindings when Value/IsChanged changes. Integer fields use the existing integer parsing rules; other numeric fields use finite double parsing with invariant decimal points and accepted decimal commas. Thus 2, 2.0 and 2,0 are equivalent for a multiplier, without hiding small real numeric differences. Invalid input remains indicated as different and is still rejected by the existing validation path; an invalid integer such as 5.0 is not silently treated as a valid 5.

## Advanced Technical Debt

Debt Creation Factor is available under **Technical Debt → Advanced · Technical Debt** in the shared configuration view (including Advanced workspace), and in the Change panel's separate **Advanced · Technical Debt** expander. The four primary debt controls stay unchanged. Debt Impact Factor remains internal.

The editor accepts finite, nonnegative factors, including fractional values such as 1.5 and 2.0, with no new arbitrary upper bound. The existing Core validation and overflow guards remain in force. The factor is a calibration/scenario assumption, not an empirical claim or recommendation.

```text
DebtCreated = ShortcutEffortSaved × DebtCreationFactor
```

The existing Development plan already captures `DebtToCreate` at Development start. A factor intervention on Day N therefore affects starts processed from Day N+1. Active items complete using their original captured debt amount even if they complete after the change. Existing debt, saved effort, required effort, history and random state are not rewritten. Saved effort 1.5 creates debt 1.5 at factor 1 and debt 3 at factor 2. Debt Ratio remains debt divided by original completed scope; tolerance still controls overhead only.

The same configuration property travels through existing scenario, Live, intervention and checkpoint persistence. No schema/model-version change is needed for exposing it. Old configurations without it retain the existing default of 1. Tests cover loaded editor round-trips, saved Live continuation and exact checkpoint replay, including plans established before the factor change.

## Verification

The full Release test suite passes **473 tests**: 165 Core, 173 Application, 135 UI. This task adds 21 cases: 15 UI cases, five Core cases and one Application integration case. Coverage includes all 15 original parameter bindings and four groups, separate factor binding, read-only Current, changed/reset notifications, equivalent formatting, integer validation, fractional/large/invalid factors, 1×/1.5×/2× creation, zero shortcuts, old-factor completion versus new starts, unchanged debt/history/random state and persisted/checkpoint continuation.

[Native verification](verification/intervention-editor/Program.cs) opens the real Avalonia MainWindow and invokes its controls through automation peers, changes bound TextBoxes and focuses actual inputs. Screenshots are visually inspected; this is automated native interaction and visual review, not a claim of physical mouse/keyboard testing. It verifies normal desktop, 960- and 720-pixel widths; several simultaneous changes; formatting equivalence and reverting; full-row focus; units; Advanced placement; factor application; and the theme-aware focus resource. The application remains configured for its existing Light theme. Evidence is in [screenshots](verification/intervention-editor/screenshots) and [the result log](verification/intervention-editor/result.txt).

```sh
dotnet run --project docs/verification/intervention-editor/NativeVerification.csproj -c Release
dotnet test SoftwareDevelopmentSimulation.sln -c Release
```

No Core or Application production calculations changed in this task. Day N/N+1 timing, checkpoint behavior, Before/After, debt ratio/tolerance/repayment, shortcut selection/reduction, overhead, capacity/productivity/collaboration, WIP, priority, utilization, Work Supply and defects/rework are unchanged.

The optional dark-resource probe checks the focus brush inside Advanced only; the existing application has hardcoded Light surfaces and does not offer a full dark theme. No application-wide theme redesign was made.

Release solution build completed with **0 warnings and 0 errors**.

Files changed for this task: `Views/LiveView.axaml` (grouped table and focus style), `Views/DebtSettingsView.axaml` (Advanced calibration), `ViewModels/SimpleWorkflowViewModel.cs` (observable typed change fields), `ViewModels/LiveViewModel.cs` (groups and separate advanced binding), and `ViewModels/MainWindowViewModel.cs` (factor input/round-trip). Tests are in `InterventionEditorTests.cs`, `TechnicalDebtTests.cs` and `TechnicalDebtIntegrationTests.cs`. This report, the Live/Technical Debt documentation and the native verification host/evidence complete the changes. Paths to production files are relative to `src/Simulation.UI`.
