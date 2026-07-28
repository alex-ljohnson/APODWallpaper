namespace APODBenchmarks;

internal enum RunMode { SelfTest, Smoke, Full }

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var (mode, itemCounts) = ParseArgs(args);
        switch (mode)
        {
            case RunMode.SelfTest:
                return SelfTests.Run();
            case RunMode.Smoke:
                return Smoke();
            default:
            {
                var ctx = itemCounts is { Length: > 0 }
                    ? new Core.BenchmarkContext(itemCounts, iterations: 15, warmup: 3)
                    : Core.BenchmarkContext.Default;

                var benchmark = new Benchmarks.PanelLayoutBenchmark();
                var result = benchmark.Run(ctx);

                Reporting.ConsoleReporter.Write(result);
                double dpi = result.Rows.Count > 0 ? result.Rows[0].Dpi : 1.0;
                var meta = Reporting.RunMetadata.Capture(
                    dpi, $"iters={ctx.Iterations}, warmup={ctx.Warmup}");
                string path = Reporting.MarkdownReporter.WriteToFile(result, meta, "results");
                Console.WriteLine($"report written: {path}");
                return 0;
            }
        }
    }

    private static int Smoke()
    {
        var items = Wpf.SyntheticItem.Generate(1000);

        using var virt = new Wpf.WpfHarness(Wpf.PanelKind.VirtualizingWrap, Wpf.TemplateKind.Synthetic);
        virt.SetItems(items); virt.Layout();
        virt.ScrollTo(virt.ScrollMax / 2);
        int virtCount = virt.RealizedContainerCount();

        using var wrap = new Wpf.WpfHarness(Wpf.PanelKind.Wrap, Wpf.TemplateKind.Synthetic);
        wrap.SetItems(items); wrap.Layout();
        int wrapCount = wrap.RealizedContainerCount();

        Console.WriteLine($"virtualizing realized: {virtCount}");
        Console.WriteLine($"wrap realized: {wrapCount}");

        bool ok = virtCount > 0 && virtCount < 100 && wrapCount >= 900;
        Console.WriteLine(ok ? "SMOKE OK" : "SMOKE FAIL: virtualization not behaving as expected");
        return ok ? 0 : 1;
    }

    // parse --selftest / --smoke / --counts a,b,c
    internal static (RunMode mode, int[]? itemCounts) ParseArgs(string[] args)
    {
        RunMode mode = RunMode.Full;
        int[]? counts = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--selftest": mode = RunMode.SelfTest; break;
                case "--smoke": mode = RunMode.Smoke; break;
                case "--counts" when i + 1 < args.Length:
                    counts = Array.ConvertAll(args[++i].Split(','), int.Parse);
                    break;
            }
        }
        return (mode, counts);
    }
}
