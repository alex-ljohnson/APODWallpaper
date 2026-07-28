using APODBenchmarks.Core;
using APODBenchmarks.Wpf;

namespace APODBenchmarks.Benchmarks;

public sealed class PanelLayoutBenchmark : IBenchmark
{
    private const double ScrollStep = 250;

    public string Name => "PanelLayout";

    public BenchmarkResult Run(BenchmarkContext context)
    {
        var rows = new List<BenchmarkRow>();
        foreach (var template in new[] { TemplateKind.Synthetic, TemplateKind.Image })
            foreach (var panel in new[] { PanelKind.Wrap, PanelKind.VirtualizingWrap })
                foreach (int count in context.ItemCounts)
                    rows.Add(Measure(panel, template, count, context));
        return new BenchmarkResult(Name, rows);
    }

    private BenchmarkRow Measure(PanelKind panel, TemplateKind template, int count, BenchmarkContext ctx)
    {
        var items = SyntheticItem.Generate(count);
        using var h = new WpfHarness(panel, template);
        double dpi = h.Dpi;

        // initial layout: reset clears source, action assigns + lays out N items
        double[] layout = Core.Measure.Time(
            action: () => { h.SetItems(items); h.Layout(); },
            iterations: ctx.Iterations, warmup: ctx.Warmup,
            reset: () => { h.SetItems(null); h.Layout(); });

        // scroll frames: load once, step top->bottom timing each layout
        h.SetItems(items); h.Layout();
        double[] scroll = ScrollFrameSamples(h, ctx.Warmup);

        // memory: after a scroll pass, snapshot
        var (heap, ws) = Core.Measure.MemorySnapshot();

        // realized containers: sample mid-list
        h.ScrollTo(h.ScrollMax / 2);
        int realized = h.RealizedContainerCount();

        var metrics = new Dictionary<string, MetricStats>
        {
            ["InitialLayoutMs"] = MetricStats.FromSamples("ms", layout),
            ["ScrollFrameMs"] = MetricStats.FromSamples("ms", scroll),
            ["ManagedHeapMB"] = MetricStats.FromSamples("MB", [heap]),
            ["WorkingSetMB"] = MetricStats.FromSamples("MB", [ws]),
        };
        return new BenchmarkRow(panel.ToString(), template.ToString(), count, dpi, metrics, realized);
    }

    // one pass t-b in viewport-height steps w/ first warm up pass
    private static double[] ScrollFrameSamples(WpfHarness h, int warmupPasses)
    {
        double step = h.ViewportHeight > 0 ? h.ViewportHeight : ScrollStep;
        var samples = new List<double>();
        for (int pass = 0; pass <= warmupPasses; pass++)
        {
            h.ScrollTo(0);
            for (double y = 0; y <= h.ScrollMax; y += step)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                h.ScrollTo(y);
                sw.Stop();
                if (pass == warmupPasses)
                    samples.Add(sw.Elapsed.TotalMilliseconds);
            }
        }
        return samples.Count > 0 ? [.. samples] : [0.0];
    }
}
