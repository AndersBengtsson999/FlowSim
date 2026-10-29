# RC1 release verification

Release engineering only: existing model, persistence and KPI behavior remain unchanged. Run from a temporary working directory to keep generated measurements and screenshots outside tracked source:

```sh
dotnet run --project /absolute/repo/docs/verification/rc1/Verification.csproj -c Release -- --headless
```

This extends the existing workshop verification with three cycles through 1440×900, 1280×800, 980×800 and 960×680. It logs requested/client/world/viewport sizes and the original minimum-height threshold before asserting. It does not relax the original threshold, discard failed attempts or rerun until success. Existing checks cover manual zoom after disclosure/resize, Fit centering, KPI accessibility, unchanged session capture and paused-only customer launch. A separately retained original harness run supplies a baseline.

The limited soak uses the authoritative LIVE implementation, continuous arrival of one item per day and a 2,000-day target. It measures Run to Day wall time, UI dispatcher post latency at 100 ms nominal cadence, representative process working set, JSON size, save/load time, full-session round-trip and deterministic next-day continuation. Dispatcher latencies are a headless responsiveness proxy, not native input latency. Process samples are not a guaranteed absolute peak; save/load can allocate between samples. The temporary JSON is explicitly identified in the result and is not committed.

Use the existing product-polish, KPI Explorer and Insights verification harnesses as separate checks. Native packaged-app startup is checked by `packaging/macos/verify.py`. A source harness using native Avalonia is not evidence that every interactive action was tested in the shipped executable. Manual native file-picker, Cmd+Q, Retina/trackpad and clean-Mac acceptance remain explicit gates.

Final architecture-specific manifests, checksums, release notes and the measured verification report are stored with the ignored distribution outputs under `dist/macos/`. No binary or generated soak session belongs in Git.

The initial soak used DispatcherTimer and produced no responsiveness samples under this headless run. Its timing/memory/serialization results are retained, but zero samples are not interpreted as zero latency. The corrected instrument posts from a wall-clock background task and requires nonzero sample count; no product code was changed.

## Direct release correction: fast-advance responsiveness

Both headless and native-Avalonia runs initially queued ordinary dispatcher work until the entire approximately 24-second Run to Day operation completed. `Task.Delay(1)` alone did not ensure lower-priority UI work ran between batches. The existing 25-day batch boundary now also yields at Avalonia Background priority when running in a AvaloniaSynchronizationContext. Non-UI callers retain the previous behavior. This is a UI-pump correction, not a change to simulated scheduling, day order, effort or random state.

The RC harness now queues Pause from a wall-clock background task and requires it to stop before Day 2000. It also requires nonzero dispatcher samples and a maximum measured post latency below two seconds during the controlled soak. Original measurements/failures are retained in release evidence; successful reruns do not erase them. This threshold is a release-test criterion for this bounded fixture, not a guarantee for arbitrary scenarios or machines.
