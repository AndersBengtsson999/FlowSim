# Step 6 — measured variation results

Measured on macOS ARM64, .NET 10.0.401 Release. Both examples use 100 days, 30 independent items, 5 developers, 2 testers, unit per-person capacity and WIP 5/3/3. Single-run seed and Monte Carlo base seed are 12345.

Fixed uses effort 5/1/2. Variable uses Triangular 2/5/12, 0.5/1/3, 1/2/5. These illustrative distributions also increase the mean effort; they are not calibrated Easy-Laser data.

## Single runs

| Metric | Fixed baseline | Variable Effort Example |
|---|---:|---:|
| CompletedWorkItems | 30 | 30 |
| ThroughputPerFiveDays | 1.5 | 1.5 |
| AverageLeadTime | 23.833333 | 29.833333 |
| AverageCycleTime | 9.666667 | 12.5 |
| AverageActiveTime | 8 | 11.533333 |
| AverageWaitingTime | 0.166667 | 0.2 |
| AverageBlockedTime | 0 | 0 |
| AverageWip | 2.6 | 3.45 |
| DeveloperUtilization | 0.36 | 0.421432 |
| TesterUtilization | 0.3 | 0.370299 |
| MaximumWaitingForCodeReviewQueue | 5 | 3 |
| MaximumWaitingForTestingQueue | 3 | 3 |

Utilization is a ratio; time is simulated working days. All 30 items finish in both runs.

## Monte Carlo — 500 variable-effort runs

| Metric | P50 | P75 | P85 | P95 | Samples |
|---|---:|---:|---:|---:|---:|
| CompletedWorkItems | 30 | 30 | 30 | 30 | 500 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 | 1.5 | 500 |
| AverageLeadTime | 32.666667 | 33.666667 | 34.266667 | 35.236667 | 500 |
| AverageCycleTime | 13.7 | 14.033333 | 14.3 | 14.57 | 500 |
| AverageWip | 3.81 | 3.91 | 3.99 | 4.071 | 500 |
| DeveloperUtilization | 0.468915 | 0.486871 | 0.495245 | 0.509606 | 500 |
| TesterUtilization | 0.399354 | 0.414276 | 0.420566 | 0.437532 | 500 |
| MaximumWaitingForCodeReviewQueue | 3 | 3 | 3 | 3 | 500 |
| MaximumWaitingForTestingQueue | 3 | 3 | 3 | 3 | 500 |

Runtime: **1.311 seconds** for 500 runs after a three-run JIT warm-up, excluding build/UI rendering. Sequential execution; no parallel simulation. A second complete 500-run execution produced identical serialized aggregates and run metrics. Native UI verification independently ran two 500-run batches and observed 320 UI timer ticks during the first batch, progress updates and successful cancellation of a subsequent batch.

## Observations and verification

- Every run completes all 30 items, so throughput is exactly 1.5 items per five days. The histogram has one bar; no artificial spread is introduced.
- Lead-time averages range from 28.400000 to 37.233333 working days.
- Queue maxima are generally lower than the synchronized Fixed baseline, even with more average work. No rule assigns a bottleneck or judges the outcomes.
- The unchanged daily model allocates fractional effort within whole working intervals and prevents same-day stage transitions. Effort averages do not equal elapsed times.
- Time percentiles describe averages of completed items per run, not pooled item times; short horizons may censor unfinished work. Zero-completion runs are excluded from lead/cycle distributions with explicit sample counts. There are no such runs in this 500-run example.
- Fixed baseline fields, item state/timing histories and daily observations were compared against pre-Step-6 results: every existing value matched exactly. The engine source is unchanged.
- Same-seed variable runs match completely. Seed 54321 changes generated effort. The pinned PRNG sequence, bounds, inverse CDF, invalid inputs, percentile conventions, mixed zero/completed runs, progress/cancellation and ViewModel modes are covered by tests.

See [SIMULATION_MODEL.md](SIMULATION_MODEL.md#variation) for the generation order, algorithm, seed derivation, percentile convention and limitations.
