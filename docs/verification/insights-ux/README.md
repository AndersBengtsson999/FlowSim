# Simulation Insights UX verification

The previous reader mixed observation, classification evidence, explanations and technical caveats at the same level. Queue titles repeated observations, the detail status exposed the NoLongerRelevant enum spelling, and active and previous findings shared an undifferentiated list.

The existing InsightsWindow now uses a display-only InsightPresentation helper. Active findings appear first, followed by other current observations and previous findings. No entries are removed. The first finding is displayed without automatically acknowledging it; explicit selection retains the existing review behavior. The selected reader follows “What FLOW observed”, “What it might mean”, and “What to explore next”. Technical evidence is collapsed under “Supporting evidence”; all original evidence strings, metrics, exact measurements and methodological limitations remain accessible. Existing comparison preparation, station links, KPI links and Help remain available.

Charts reuse LivePerformanceTrendChart with SamplesOnly=true, larger 220px height, chart title, units, recorded day range and sample count. No interpolation or new calculations. Expansion and chart selection survive refresh. Narrow windows stack the list above the reader.

## Validation

- Release solution build: successful, 0 warnings, 0 errors.
- Full suite: 960 passed (Core 218, Application 295, UI 447); no failures or skips.
- Two new presentation tests cover ordering, retention, human-readable status, wording and date ranges.
- Avalonia headless/Skia harness passes window reuse, dark theme, explicit review, separate explanation, collapsed evidence, samples-only charts, selectable historical findings and retained historical chart evidence.
- Serialized simulation session capture is identical before and after inspection interactions.
- No changes to SimulationInsights, InsightTracker, InsightEvidence, engine, classification thresholds, KPI calculations, history, or experiment execution.

Run the UI harness from the repository root:

```sh
dotnet run --project docs/verification/insights-ux/Verification.csproj -- --headless
```

## Visual evidence

Before/after images use the same deterministic Day 35 fixture, selected insight and 1180 × 820 window. These are actual Avalonia renders using headless Skia on macOS, not native desktop screenshots. Native window-manager interaction and the 10–15-second comprehension target have not been user-tested.

- Before: `before/headless/screenshots/insights-wide.png`
- After: `after/headless/screenshots/insights-wide.png`
- Narrow: `after/headless/screenshots/insights-narrow.png`
- Supporting evidence: `after/headless/screenshots/supporting-evidence.png`
- Historical selection: `after/headless/screenshots/historical-finding.png`

The historical example uses synthetic tracker lifecycle input only; it does not change the simulation. Baseline images were captured before this UX edit; the harness now verifies the updated interface. This task does not claim completion of the remaining B6.6.2 experiment acceptance work.
