# System Cost / Done Item v1

System Cost / Done Item = (Development + Code Review + Rework + Testing + Technical Debt Repayment raw capacity consumed inside the selected period) / items entering final Done inside that same period.

One developer capacity unit and one tester capacity unit each count as one relative system cost unit. This is not money. Productivity affects how much work capacity produces; cost never multiplies capacity by productivity. Collaboration includes all raw capacity, without discount. Zero Done returns unavailable, even when capacity was consumed.

## Two different questions

- **Delivery Cost / Done Item** selects items completed in the period and averages their full lifecycle attributed consumption, including consumption before the period. Debt repayment remains excluded.
- **System Cost / Done Item** sums all consumed capacity within the period, including unfinished work and debt repayment, then divides by completions within that period. It does not allocate repayment back to items.

For example, 100 Development + 20 Review + 0 Rework + 40 Testing capacity and 20 Done gives System Cost 8.0. Adding 20 repayment capacity makes System Cost 9.0. It does not alter the completed items' Delivery Cost. Conversely, an item whose Development and Review happened before the period carries those costs into Delivery Cost when it completes, while System Cost only includes work actually consumed inside the period. Neither metric implies better or worse performance.

## History, rolling windows and compatibility

No new history fields or persistence format are needed. Daily snapshots already retain stage consumption, raw collaboration consumption, Rework capacity, and Debt.RepaymentCapacity. With non-default productivity, explicit ConsumedCapacity provides the exact values. At default productivity, the existing identity is exact: DevelopmentWork + 0.5 × CollaborationDevelopmentCapacity equals raw Development capacity; Review and Testing work equal their consumed capacity. No utilization reconstruction is used.

The period projector uses inclusive one-based days and counts final DoneDay once. Trend uses prefix sums of actual daily developer consumption (including repayment) and tester consumption, divided by completion counts over the same lookback. The visible chart range does not truncate that lookback. Existing 10/20/50/100-day windows and partial observed windows remain unchanged.

Before/After uses the same independent period calculation. An intervention recorded on Day 100 with a 20-day window has Before Days 81–100 and After Days 101–120. An unfinished After period uses only observed days and their completions; the UI retains its partial-period annotation.

History without a debt observation is conservatively unavailable for System Cost; a missing field is not asserted to be zero repayment. History without explicit stage consumption is unavailable when that day's recorded configuration had non-default productivity. Any unknown day makes the selected System Cost unavailable, without suppressing Delivery Cost or other metrics. New days after load have authoritative observations and become available when the rolling window no longer intersects unknown history. Save/load and checkpoints reuse existing history and preserve the values and deterministic continuation.

## UI

The existing Cost/Item headline still means Delivery Cost. Its tooltip now distinguishes both metrics and gives a compact period System Cost breakdown. System Cost / Done is selectable in the existing Performance Trend, with unit `capacity units / done item`. The existing collapsed Team Performance metric list and Before/After comparison also expose it. No permanent block was added above the chart and no extra selector/chart was added. Individual item tooltips remain item-level Delivery Cost.

Automated interaction with the real Avalonia MainWindow plus visual review verified widths 960 and 1280: chart Y=632 and height=210, identical to previous Delivery Cost verification. Before/After renders both costs and correct day boundaries. Evidence: [native results](verification/system-cost/result.txt), [960px](verification/system-cost/screenshots/layout-960.png), [1280px](verification/system-cost/screenshots/layout-1280.png), [Before/After](verification/system-cost/screenshots/before-after.png).

## Controlled validation

Reuse seed 12345, Always Available, 5 developers / 2 testers, availability 100%, WIP 5/3/3, effort 5/1/2, defects off and a 50-day window from the model interaction validation.

| Scenario | Period | Done | Delivery Cost | System Cost | Dev | Review | Rework | Test | Repayment | Total system capacity |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Baseline | 151–200 | 42 | 8.000000 | 7.952381 | 208 | 42 | 0 | 84 | 0 | 334 |
| Development 2× | 151–200 | 50 | 5.530625 | 6.978750 | 177.9375 | 71 | 0 | 100 | 0 | 348.9375 |
| Debt repayment 25% | 401–450 | 28 | 9.224279 | 10.892857 | 166.5 | 28 | 0 | 55 | 55.5 | 305 |

Baseline differs slightly because period boundaries cut through work in progress. Development 2× has a large Testing queue; System Cost includes capacity spent on items not yet Done. The repayment period includes 55.5 capacity units of system debt work that Delivery Cost excludes. These are descriptive comparisons, not causal or efficiency ratings.

[Reproducible scenario results](verification/system-cost/scenarios/result.txt) also confirm that all three complete simulation-state fingerprints match the archived pre-feature model validation exactly.

## Verification

- Nine new Application test cases: exact baseline/repayment arithmetic, lifecycle versus period boundaries and future completions, zero Done, raw productivity/collaboration/rework/repayment capacity and conservation, growing queues, checkpoints/save-load/read-only projection and repeated continuation, two missing-history cases, and partial Before/After boundaries.
- Existing all-rolling-metric tests now include System Cost for 10/20/50/100-day windows and every historical point.
- Existing UI tests verify System Cost in the tooltip/trend/comparison and no second headline metric; comparison row count updated.
- Full solution: **513 passed (176 Core, 194 Application, 143 UI), zero failed or skipped**.
- Release solution build: **0 warnings, 0 errors**.
- Native verification: passed; screenshots reviewed.
- No Core, allocation, randomness, model version or persistence source changes. The new metric is a read-only Application projection. Delivery Cost, completion timing, queues, debt, utilization and simulation behavior remain unchanged.

Run from repository root:

```sh
dotnet test --nologo -m:1
dotnet build -c Release --nologo -m:1
dotnet run --project docs/verification/system-cost/scenarios/Validation.csproj
dotnet run --project docs/verification/system-cost/NativeVerification.csproj
```
