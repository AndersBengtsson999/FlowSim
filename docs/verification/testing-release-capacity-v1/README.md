# Testing & Release Capacity Experiment v1

Analysis only, Model 0.8. No production changes.

## Reproduction

Run from the repository root with .NET 10 and Python 3.13:

```sh
dotnet build docs/verification/testing-release-capacity-v1/Validation.csproj -c Release -m:1 -nr:false
dotnet docs/verification/testing-release-capacity-v1/bin/Release/net10.0/Validation.dll docs/verification/testing-release-capacity-v1
python3 docs/verification/testing-release-capacity-v1/archive.py
python3 -m venv /tmp/flowsim-testing-release-python
/tmp/flowsim-testing-release-python/bin/pip install -r docs/verification/testing-release-capacity-v1/requirements.txt
MPLCONFIGDIR=/tmp/flowsim-testing-release-mpl /tmp/flowsim-testing-release-python/bin/python docs/verification/testing-release-capacity-v1/analyze.py
/tmp/flowsim-testing-release-python/bin/python docs/verification/testing-release-capacity-v1/tables.py
/tmp/flowsim-testing-release-python/bin/python docs/verification/testing-release-capacity-v1/write_report.py
dotnet test SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
```

The harness constructs S2 through the existing validated `Validation.Complex` definition with two specialists. Before running, it asserts JSON structural equality with the archived S2 request for seed 12345. Only tester count, release capacity, seed and horizon change. Scheduled Unlimited is explicitly validated as supported while preserving five-day opportunities. `U` represents `int.MaxValue`, the existing Unlimited sentinel, not flow-based release.

Seeds 12345–12374 run all 12 configurations for 500 days. Days 451–500 inclusive are the primary measurement window. Three seeds (12345, 12359, 12374) repeat all configurations: 36 additional runs, excluded from statistical summaries, with exact session-state/result/metric hash checks. Four seed-local sessions execute concurrently; output array order may differ, so identify records by Scenario and Seed.

## Metrics and statistical conventions

Rates are items per five days; times are simulated days; costs are consumed raw capacity units per item, not money or payroll. Utilization and final Day-500 debt ratio are fractions. Trend debt ratio and utilization use the production trend percent scale. Final queue sizes and cumulative released counts are separate from final-window means.

Development cycle and Delivery Work Cost use the work-completed cohort. Delivery cycle and release wait use the released cohort. These cohorts can differ; their averaged cycles need not add up. System Cost divides all raw capacity consumed during the window, including unfinished work and debt repayment, by releases during that window. Unused staff capacity is not charged. No-release/no-completion metrics remain null rather than zero. Specialist waiting is a subset of Development; dependency waiting is a subset of Backlog, which is excluded from total WIP.

Statistics: mean, median, sample SD (n−1), min/max, type-7 P05/P95, and Student-t 95% mean intervals. Type 7 interpolates at sorted zero-based index (n−1)p. Mean interval is mean ± t(.975,n−1)·SD/√n; t(.975,29)=2.045229642. Missing observations are counted and omitted from ratios; no data is not equality. Paired differences use the same seed and a 1e−9 direction tie tolerance. All 30 isolated-factor pairs are reported: 12 tester comparisons at fixed release capacity and 18 release comparisons at fixed tester count.

Intervals are exploratory and unadjusted for multiple comparisons. They quantify Monte Carlo variation under the pseudorandom-seed independence working assumption, not real-world forecasting uncertainty. Same seeds do not guarantee identical item paths after timing diverges. Zero empirical variance is not proof of universal determinism across untested seeds.

Daily throughput and cycle series use authoritative rolling 50-day calculations, with available history before Day 50. Queue counts and `TotalWip` are unsmoothed end-of-day states. `AverageWip` remains the rolling mean for comparison. Graph bands are pointwise cross-seed median/P05/P95, not confidence intervals. `regimes.json` contains each metric's ten consecutive 50-day block means and within-block OLS slopes, summarized across seeds. Means of rolling endpoints differ from non-overlapping cohort rates. Correlated days/windows are not independent samples.

## Artifacts

- `REPORT.md`: findings, isolated effects, accumulation evidence and limitations.
- `configurations.json`: all 12 complete requests; matrix also in report.
- `raw-results.json` and `runs/*.json`: every run's metrics, request, hashes and audit result; aggregate includes repeats/failures.
- `histories/*.json.xz`: complete 500-day DailySnapshot records, including all item snapshots, retained losslessly.
- `daily-flow.json.gz`: compact full daily snapshots without item arrays.
- `time-series.json.gz`, `time-series-bands.json.gz`, `regimes.json`: daily production projections, raw WIP, statistical bands and block diagnostics.
- `factor-checks.json`, `flow-balance.json`, `capacity-evidence.json`, `boundary-effects.json`: fixed-factor checks, inventory reconciliation, capacity peaks and release-window carryover effects.
- `block-flow.json`, `block-rates.md`: actual non-overlapping block release/completion rates, distinct from averages of rolling endpoints.
- `report-tables.md`, `block-comparison.md`: compact comparison tables and all ten blocks/slopes.
- `summary.json`, `scenario-summary.md`, `paired.json`, `paired-comparison.md`: complete statistics and per-seed paired differences.
- `distributions.*`, `trajectories-1.*` through `trajectories-3.*`: standalone PNG/SVG figures.
- Logs and production SHA-256 manifests: build/test/invariant evidence and proof production source was unchanged.

The reused Model Validation v2 checker audits all days and item observations for lifecycle conservation, raw capacity accounting, specialist allocation, active WIP limits, dependency resolution, debt accounting, release FIFO/timing/capacity, item cost events, effort conservation, rolling metrics and production trend definitions. Additional experiment checks assert fixed developer capacity, requested tester capacity, specialist count and release cadence on every day. No application behavior is changed by this harness.

`archive.py` losslessly converts the harness gzip histories to smaller XZ archives (preset 1), checks uncompressed SHA-256 equality, and records sizes and hashes in `history-archive.json`. This changes storage only. Run it before `analyze.py`.

`write_report.py` assembles the reviewed interpretation for this fixed Model 0.8 experiment. Revalidate its prose if the model or experimental settings change; numerical tables are generated from the current artifacts.
