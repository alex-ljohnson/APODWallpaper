# APODBenchmarks

Reusable benchmark harness. 
First benchmark: `WrapPanel` vs `VirtualizingWrapPanel` (initial layout time, scroll-frame time, memory).

## Run

From the solution dir (`APODWallpaper/`):

```
dotnet run --project APODBenchmarks -c Release # full sweep {100,365,1000,3000}
dotnet run --project APODBenchmarks -c Release -- --counts 100,1000
dotnet run --project APODBenchmarks -- --smoke # verify virtualization engages
dotnet run --project APODBenchmarks -- --selftest # run in-process unit checks
```

Reports are written to `APODBenchmarks/results/` (gitignored). Use `-c Release` for timing — Debug numbers are not meaningful.

## Layout

- `Core/`       timing, statistics (Perfolizer), result model - no WPF dependency
- `Reporting/`  console + Markdown reporters
- `Wpf/`        off-screen window harness, synthetic items, image converter, templates
- `Benchmarks/` `PanelLayoutBenchmark`
- `SelfTests.cs` in-process correctness checks (run via `--selftest`)

## Notes

- Requires an interactive desktop session (off-screen window, not headless).
- `WorkingSetMB` is the memory metric to trust (captures both managed and WPF's unmanaged graphics memory); `ManagedHeapMB` only reflects the GC heap and is noisier.
- `WpfHarness.PumpDispatcher` pumps to `Background` priority after each layout/scroll —
  needed since this is a console app with no message loop, and container cleanup is
  queued at Background priority.
