# Multi-Seed Validation v1

Validation-only tooling; no production simulation or UI changes.

## Reproduce

From the repository root, with .NET 10 and Python 3.13:

```sh
dotnet build docs/verification/multi-seed-v1/Validation.csproj -c Release -m:1 -nr:false
dotnet docs/verification/multi-seed-v1/bin/Release/net10.0/Validation.dll docs/verification/multi-seed-v1
python3 -m venv /tmp/flowsim-multiseed-python
/tmp/flowsim-multiseed-python/bin/pip install -r docs/verification/multi-seed-v1/requirements.txt
MPLCONFIGDIR=/tmp/flowsim-multiseed-mpl /tmp/flowsim-multiseed-python/bin/python docs/verification/multi-seed-v1/analyze.py
dotnet test SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
```

The four-way execution uses independent session objects, seed-local random streams and thread-safe result collections. JSON run order can differ; identify each record by Scenario and Seed. All metrics and fingerprints must reproduce exactly. No external random source is used by the harness.

The harness links the existing Model Validation v2 definitions and checks directly. No copied scenario parameters or altered expected outcomes are used. Model v0.8 is the source snapshot recorded in production-before.json; these hashes identify the uncommitted working-tree production code more precisely than the repository HEAD alone.

## Definitions and statistics

- Seeds: 12345–12374 inclusive. A/B/C/D each run for 300 simulated days.
- Final metric window: Days 251–300 inclusive, 50 days.
- Queue metrics in scenario/paired tables: arithmetic mean of daily end-of-day counts in that window. Final queue sizes are separate metrics. The specialist queue is a subset of Development, and the dependency queue is a subset of Backlog; they must not be summed into total WIP again.
- DebtRatio is the authoritative Day 300 amount/cumulative developed-scope ratio, matching Model Validation v2. It is not a 50-day average. Developer/Tester utilization and DebtRatio use fractions in raw/final tables; multiply by 100 for percent.
- Cumulative Released counts cover Days 1–300. They are never substituted for rolling throughput.
- Throughput and Completion Rate are items per five days. Cycle and wait times are simulated days. Costs are model raw-capacity units per item, not currency.
- Delivery cycle and release wait use the released cohort; Development cycle and Delivery Work Cost use the work-completed cohort. System Cost uses capacity consumed during the window divided by releases during that window.
- A and B contain no active random mechanism in these configurations; changing seeds may legitimately produce identical output metrics. C/D activate random specialists, residual dependencies and shortcuts.
- Sample standard deviation uses n−1. Median is the middle observation or mean of the middle two.
- Percentiles use Hyndman–Fan type 7: sort observations, h=(n−1)p with zero-based indexing, linearly interpolate floor(h) and ceil(h). These empirical P05/P95 values are not population prediction limits.
- Mean 95% CI: mean ± t(0.975,n−1) × sample_SD/√n. For n=30, t≈2.045229642. Zero sample variation yields a degenerate interval; that does not establish model correctness or absence of uncertainty outside this experiment.
- Paired comparisons subtract same-seed outcomes first and calculate statistics on the 30 differences. They do not treat the 60 scenario observations as independent. Increase/decrease/tie use absolute tolerance 1e−9 in each metric's units; CIs use unrounded differences.
- CIs describe Monte Carlo mean uncertainty under a working assumption of independent pseudorandom seed realizations. Consecutive deterministic seeds do not establish statistical independence. No normality claim is made for individual outcomes. Intervals are approximate, exploratory, unadjusted for multiple metrics, and are not real-world forecasting intervals.
- Missing metrics remain null, are excluded from summaries, and have explicit N/Missing counts. No-release cycle time is never encoded as zero.
- Time-series throughput and cycle time use the production rolling 50-day projection, with available-day windows before Day 50. Queue and debt trajectories are daily observations. Debt time series use percent, exactly as the production trend projection does.
- Bands are pointwise cross-seed medians and type-7 P05/P95 percentiles, not simultaneous confidence bands or mean confidence intervals. Overlapping rolling windows and successive days are correlated, not additional independent replicates.

## Evidence files

- `raw-results.json`: all requests, intervention records, 120 per-run metric dictionaries, state/result hashes, invariant audit counts, repeat checks and failures.
- `time-series.json`: five authoritative trajectories per run, with nulls before a cycle-time cohort exists.
- `summary.json`, `scenario-summary.md`: every metric's scenario distribution and mean interval.
- `paired.json`, `paired-comparison.md`: paired statistics and all 30 raw paired differences per metric.
- `time-series-bands.json`: per-day distribution statistics, including sample counts.
- `regimes.json`: six non-overlapping 50-day blocks across the full run; cross-seed distributions of each run's block mean and within-block daily OLS slope.
- `distributions.png/.svg`, `trajectories.png/.svg`, `trajectories-detail.png/.svg`: standalone figures.
- `validation.log`, `tests.log`, `build.log`, `harness-build.log`, `analysis.log`: completed execution evidence.
- `production-before.json`, `production-integrity.json`: production source hashes and no-change verification.

Full invariant checks cover every simulation day and all work items, capacity supply/consumption and stage priorities, active-stage WIP limits, raw-cost event reconciliation, lifecycle uniqueness/conservation, dependency resolution, FIFO release opportunities/capacity, debt scope/creation/repayment, per-item effort conservation, all rolling endpoints and every production trend metric. D also preserves exact C/D history through Day 150 and existing item/history state at the intervention. D changes specialists 1→2, dependency rate .25→.10 and scheduled release capacity 5→10 at Day 150, effective Day 151. It is one combined intervention package.

Exact repeats cover all four scenarios for seeds 12345, 12359 and 12374: 12 additional runs. Serialized complete session state, exported result and final metric dictionaries must all hash identically. Full state includes authoritative daily history and random states. These repeat runs are excluded from the 120 statistical observations.
