# B5.2 — Configurable staffing cost in SEK

## Findings and reused components

The pre-existing `DeliveryCost` and `SystemCost` measure consumed capacity, not money. No developer/tester salary rates existed. Those metrics remain unchanged and separately labelled. Specialists are already a subset of developer headcount, so their pricing replaces ordinary developer pricing rather than adding staff.

Reused components: session configuration/intervention history; immutable daily snapshots; checkpoint capture/restore with all random streams; LivePerformance period boundaries and released cohort; LivePerformanceTrend prefix sums and existing trend chart; B5.1 isolated baseline and matched-prefix comparison; existing analysis/scenario comparison metrics and charts; current scenario, experiment and LIVE JSON persistence.

The addition is observation and reporting only. Workflow allocation, WIP, release, defects, debt and random generation are unchanged. Simulation model version remains 0.8 because execution semantics are unchanged.

## Settings and assumptions

| Setting | Illustrative default |
|---|---:|
| Developer fully loaded monthly cost | 85,000 SEK |
| Tester fully loaded monthly cost | 75,000 SEK |
| Specialist fully loaded monthly cost | 100,000 SEK |
| Working days per month | 20 |

All four settings are editable before running (LIVE setup or scenario More settings → Role costs) and during LIVE through Change team / work → Role costs. Rates allow zero and decimals, are bounded at 1 billion SEK, and days must be a whole number from 1 to 31. These are illustrative employer costs including overhead, not actual company or individual salaries. Currency is SEK; there is no FX, revenue or value model.

Availability allocates the role's monthly employer cost to this simulation's team. It is not an estimate of company payroll savings when someone is unavailable. Idle available staff still incur their full allocated cost. Productivity and nominal capacity/person do not change staffing price.

For developer count D, specialist subset S, tester count T, availability Ad/At, monthly rates Md/Ms/Mt and working days W:

```
daily regular developer cost = (D − S) × Ad × Md / W
daily specialist cost        = S × Ad × Ms / W
daily tester cost            = T × At × Mt / W
daily team cost              = sum of the three roles
period team cost             = sum of recorded daily team cost
cost / delivered item        = period team cost / actually released items
incremental cost / additional delivery = (intervention cost − baseline cost)
                                        / (intervention releases − baseline releases)
```

Incremental cost is N/A unless the release delta is strictly positive and both costs are known. Negative values mean lower total cost with more deliveries; positive values mean additional cost per additional delivery. This is not an automatic benefit ranking. Zero deliveries yields N/A cost per delivery, not zero.

## Rework allocation

The engine records raw developer capacity consumed by Rework but does not assign that effort to individual ordinary developers versus specialists. The model therefore allocates the daily developer pool cost:

```
allocated rework cost = (daily regular developer cost + daily specialist cost)
                       × observed rework capacity / available developer capacity
```

The share is zero with no available developer capacity and bounded to [0,1] for numerical safety. This is explicitly labelled **allocated rework cost**. It is included in total staffing cost, never added on top. It includes only the authoritative Rework stage, not guessed reinspection, retesting or an arbitrary defect percentage. It is not an exact per-person payroll attribution.

## History and compatibility

`RoleCostSettings` is part of scenario/request/session configuration. Each new daily snapshot stores an immutable `DailyTeamCost` observation containing the applied rates, separate role costs and allocated rework cost. Decimal arithmetic is used for money; existing capacity fractions remain engine doubles. Formatting rounds only for display; charts convert monetary values to double.

A change recorded on Day N takes effect on N+1. Historical observations are never recomputed using new prices, staffing or availability. Checkpoints capture both rates and monetary history. Existing JSON handling serializes the additive fields for scenarios, experiments, LIVE saves and checkpoints; load validates rates and observation values.

Older files without rate settings get illustrative defaults for future days. Older daily snapshots have no monetary observation, and remain unchanged. A selected window containing any such day reports monetary totals/rework/per-delivery as N/A. An entirely new observed window becomes available normally. Existing capacity costs retain their original meaning. The new application reads old files; older binaries may reject new additive fields under their existing strict JSON rules.

## UI and comparisons

- A collapsed **Economics** panel in LIVE (including Game View) shows the configured next-day team cost in its header. Expand for the selected rolling window, total SEK, released items, cost/delivery, allocated rework, assumptions and cost/delivery trend. The window shares the existing rolling-window selector.
- Next-day wording makes a paused Day N intervention unambiguous: the header reflects the configuration for N+1; observed totals still end at N.
- The existing Performance Trend selector also includes **Team cost / delivered item**.
- Scenario results/Analyze and existing single-run Compare include total team cost, cost/delivered item and allocated rework in SEK. Existing operational metrics remain present.
- B5.1 retains its capacity cost and adds monetary rows plus incremental cost. The detailed view uses the existing bar chart for total SEK. Baseline and intervention always use the same checkpoint and matched observed days. Additional rate/staffing changes invalidate attribution under B5.1's existing lifecycle rule.

## Verification and captured example

The native harness creates the actual Avalonia MainWindow on macOS. It verifies the economic panel/trend, edits the actual rate TextBox, checks N+1 pricing, runs a 20-day B5.1 comparison and verifies monetary save/load. Images are actual rendered controls.

The captured fixture starts with 10 developers and 2 testers. Tester monthly cost changes from 75,000 to 90,000 at Day 100, effective Day 101. A checkpoint experiment changes testers 2 → 3 at Day 101, evaluating Days 102–121:

| Metric | Baseline | Intervention |
|---|---:|---:|
| Released | 8 | 12 |
| Team cost | 1,030,000 SEK | 1,120,000 SEK |
| Cost / delivery | 128,750 SEK | 93,333.33 SEK |
| Incremental cost / additional delivery | — | 22,500 SEK |

These are fixture outputs, not promises for other scenarios.

- [LIVE economic summary and trend](screenshots/live-economics.png)
- [Existing intervention editor](screenshots/role-cost-intervention.png)
- [Matched experiment comparison](screenshots/economic-comparison.png)
- [Reloaded economic history](screenshots/loaded-economics.png)

Automated coverage includes daily/idle cost, availability, specialist replacement, custom rates, correct effective day, released-only denominators, zero deliveries, incremental sign and nonpositive delivery delta, observed rework allocation, matched experiment branches, scenario/LIVE save-load, checkpoint replay, missing legacy monetary data, invalid input, UI bindings, and unchanged operational observations/random state under different prices. Existing trend and legacy operational compatibility checks now explicitly account for additive economic metadata.

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/cost-model-b5-2/Verification.csproj -c Release
```

## Remaining limits

Only shared role costs and core metrics are included. There are no individual salaries, seniority levels, payroll calendars, financial forecasts or optimization. Rework attribution is pooled as described above. Legacy monetary history is unavailable. Existing Monte Carlo summaries are not extended with new economic simulations; this iteration covers LIVE, deterministic scenario results and matched B5.1 branches. B5.1 panel results remain in memory only, while their authoritative checkpoints, configurations and daily monetary observations persist in LIVE saves.

### Final verification — 2026-10-16

- Release solution build: passed, **0 warnings, 0 errors**.
- Full suite: **789 passed, 0 failed, 0 skipped** (Core 218, Application 239, UI 332), including 21 new test cases.
- Native macOS verification: passed for cost editing, effective day, preserved price history, matching experiment periods, economic summary/trend and save/load.
- `git diff --check`: passed.
