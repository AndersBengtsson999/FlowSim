# Specialist Capacity Experiment v1

Analysis-only harness. Production simulation and UI remain unchanged.

## Reproduce

Run from the repository root with .NET 10 and Python 3.13:

```sh
dotnet build docs/verification/specialist-capacity-v1/Validation.csproj -c Release -m:1 -nr:false
dotnet docs/verification/specialist-capacity-v1/bin/Release/net10.0/Validation.dll docs/verification/specialist-capacity-v1
python3 -m venv /tmp/flowsim-specialist-python
/tmp/flowsim-specialist-python/bin/pip install -r docs/verification/specialist-capacity-v1/requirements.txt
MPLCONFIGDIR=/tmp/flowsim-specialist-mpl /tmp/flowsim-specialist-python/bin/python docs/verification/specialist-capacity-v1/analyze.py
dotnet test SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
```

The analysis reads archived Multi-Seed Validation v1 raw results to assert exact S1/Scenario-C equivalence for every seed. All other statistics are calculated from this experiment's own 210 runs.

## Design

S0–S5 reuse `ModelValidationV2.Validation.Complex`, changing only `Skills.Specialists` to 0–5. Total developers stay 5. G6 changes total developers to 6 and specialists to 2. Every variant preserves specialist demand .30, dependency rate .25/mean 4, productivity 1.5, WIP limits, two testers, debt settings and scheduled release capacity 5 every 5 days. There are no live interventions.

Seeds 12345–12374 are paired across all seven variants. Each run covers 300 days; measurement window is Days 251–300. Three seeds (12345, 12359, 12374) repeat all seven variants, yielding 21 extra runs excluded from statistics. Complete session-state, result and metric hashes must match exactly. Four seed-local sessions run concurrently; output array ordering may differ, so key by Scenario and Seed.

## Metrics and statistics

- Rates: items per five days. Times: simulated days. Cost: raw capacity units/item, not money.
- Queue averages and AverageWip use the final 50-day window. Final queues are separate. Derived specialist waiting is a subset of Development; dependency waiting is a subset of Backlog.
- Utilization and Day-300 DebtRatio use fractions. DebtRatio is an end-state ratio, not a window average. Cumulative counts cover all 300 days.
- DevelopmentCompletedWindow also equals incoming Code Review demand, since defects are disabled. ReviewCompletedWindow measures incoming Testing demand. Both are counts over Days 251–300 at completion boundaries. Capacity and collaboration totals provide additional allocation diagnostics.
- Development cycle and Delivery Work Cost use work-completed items. Delivery cycle, release wait and throughput use released items. System Cost divides period consumed capacity by period releases. No-release/no-completion ratios remain null, never zero.
- Statistics include mean, median, sample SD (n−1), min/max, type-7 P05/P95 and Student-t 95% mean intervals. Type 7 linearly interpolates sorted observations at zero-based h=(n−1)p. Mean CI = mean ± t(.975,n−1)·SD/√n; t(.975,29)=2.045229642.
- Paired differences subtract same-seed outcomes before statistical summaries. Direction tie tolerance is 1e−9; CIs use unrounded values. Incomplete pairs are excluded and counted as Unavailable. N=0 has no mean, SD, percentile or CI; it is not evidence of equality or an improvement. This is especially important for S0 cycle time and cost.
- These are exploratory, unadjusted intervals with an approximate independent pseudorandom-seed working assumption; consecutive seeds do not prove independence. They quantify Monte Carlo mean uncertainty, not the range of runs or real-world uncertainty. No hypothesis of universal optimal staffing is tested.
- Time-series bands use per-day cross-seed median and empirical type-7 P05/P95. They are pointwise outcome bands, not mean confidence intervals. Throughput/cycle use authoritative rolling 50-day history (available days before Day 50). Queue counts are daily end states. Debt trajectories, additionally exported, use percent. Consecutive days/overlapping windows are correlated.
- Regime data split all 300 days into six 50-day blocks, reporting distributions of each run's block mean and daily OLS slope. A block average of rolling endpoints is distinct from the final Day-300 rolling metric.

## Artifacts

- `REPORT.md`: interpretation, findings, recommendations and evidence links.
- `raw-results.json`: 210 requests/metric records, invariant audit counts, hashes, 21 repeat checks, failures.
- `summary.json`, `scenario-summary.md`: every requested metric and additional bottleneck diagnostics.
- `paired.json`, `paired-comparison.md`: S0/S2/S3/S4/S5/G6 against S1, G6−S2, S5−S2, and adjacent S2−S1 through S5−S4; per-seed differences preserved.
- `time-series.json`, `time-series-bands.json`, `regimes.json`: complete histories, bands and accumulation diagnostics.
- `distributions.png/.svg`, `trajectories-1.png/.svg`, `trajectories-2.png/.svg`: standalone figures; trajectory axes shared across both figures.
- `validation.log`, `analysis.log`, `tests.log`, `build.log`, `harness-build.log`: completed execution evidence.
- `production-before.json`, `production-integrity.json`: working-tree source SHA-256 hashes before/after; relevant because earlier feature work is uncommitted.

The linked Model Validation v2 checker audits all days and items: capacity, eligible specialist capacity, allocation priorities, active WIP, conservation, raw cost events, release/dependency rules, debt creation/repayment, effort conservation, rolling metrics and all production trend definitions. Extra experiment checks assert daily total developer capacity is exactly 5 (S0–S5) or 6 (G6), specialists are a subset, and S0 specialist items never consume Development capacity. Existing Core Skills tests explicitly verify that general work can use specialist capacity and that mixed-work fallback preserves allocation order.
