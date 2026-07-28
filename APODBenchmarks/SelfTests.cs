using APODBenchmarks.Core;

namespace APODBenchmarks;

// lightweight in-process assertions, used instead of a separate test project per the spec
internal static class SelfTests
{
    private static int passed, failed;

    public static int Run()
    {
        passed = failed = 0;
        BenchmarkContextValidation();
        MetricStatsBasics();
        MeasureHelpers();
        ReportingChecks();
        WpfSupport();
        Console.WriteLine($"self-tests: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string label)
    {
        if (condition) { passed++; }
        else { failed++; Console.WriteLine($"  FAIL: {label}"); }
    }

    private static void Throws<T>(Action a, string label) where T : Exception
    {
        try { a(); failed++; Console.WriteLine($"  FAIL: {label} (no throw)"); }
        catch (T) { passed++; }
        catch (Exception e) { failed++; Console.WriteLine($"  FAIL: {label} (wrong type {e.GetType().Name})"); }
    }

    private static void BenchmarkContextValidation()
    {
        Throws<ArgumentException>(() => new BenchmarkContext(Array.Empty<int>(), 10, 3), "empty counts");
        Throws<ArgumentException>(() => new BenchmarkContext(new[] { 100 }, 0, 0), "zero iterations");
        Throws<ArgumentException>(() => new BenchmarkContext(new[] { 100 }, 3, 3), "warmup >= iterations");
        var ok = new BenchmarkContext(new[] { 100 }, 10, 3);
        Check(ok.Iterations == 10 && ok.Warmup == 3, "valid context constructs");
    }

    private static void MetricStatsBasics()
    {
        var s = MetricStats.FromSamples("ms", new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        Check(Math.Abs(s.Mean - 5.0) < 1e-9, "mean of known sample");
        Check(Math.Abs(s.StdDev - 2.138089935299395) < 1e-9, "sample stddev of known sample");
        Check(s.Min == 2 && s.Max == 9, "min/max");
        Check(Math.Abs(s.Median - 4.5) < 1e-9, "median");
        Check(s.Ci95.Lower <= s.Mean && s.Ci95.Upper >= s.Mean, "ci brackets mean");
    }

    private static void MeasureHelpers()
    {
        int calls = 0, resets = 0;
        var samples = Core.Measure.Time(
            action: () => { calls++; },
            iterations: 5, warmup: 2,
            reset: () => resets++);
        Check(samples.Length == 3, "Time returns iterations minus warmup");
        Check(calls == 5, "Time runs all iterations including warmup");
        Check(resets == 5, "Time calls reset before each iteration");

        var (heap, ws) = Core.Measure.MemorySnapshot();
        Check(heap > 0 && ws > 0, "memory snapshot returns positive values");
    }

    private static void ReportingChecks()
    {
        var metrics = new Dictionary<string, MetricStats>
        {
            ["InitialLayoutMs"] = MetricStats.FromSamples("ms", new double[] { 10, 12, 11 }),
        };
        var rows = new List<BenchmarkRow>
        {
            new("WrapPanel", "Synthetic", 1000, dpi: 1.0, metrics, realizedContainers: 1000),
            new("VirtualizingWrapPanel", "Synthetic", 1000, dpi: 1.0, metrics, realizedContainers: 24),
        };
        var result = new BenchmarkResult("PanelLayout", rows);
        var meta = new Reporting.RunMetadata("M", DateTime.UtcNow, "10.0", 1.0, "test");
        string md = Reporting.MarkdownReporter.Render(result, meta);
        Check(md.Contains("PanelLayout"), "markdown contains benchmark name");
        Check(md.Contains("WrapPanel") && md.Contains("VirtualizingWrapPanel"), "markdown lists panels");
        Check(md.Contains("InitialLayoutMs"), "markdown lists metric");
    }

    private static void WpfSupport()
    {
        var items = Wpf.SyntheticItem.Generate(50);
        Check(items.Count == 50, "Generate produces requested count");
        Check(items[0].Seed != items[1].Seed, "items have distinct seeds");

        var conv = new Wpf.SyntheticImageConverter();
        var a1 = conv.Convert(0, typeof(object), null!, System.Globalization.CultureInfo.InvariantCulture)
            as System.Windows.Media.Imaging.BitmapSource;
        var a2 = conv.Convert(0, typeof(object), null!, System.Globalization.CultureInfo.InvariantCulture)
            as System.Windows.Media.Imaging.BitmapSource;
        Check(a1 is { } && a1.IsFrozen, "converter returns frozen bitmap");
        Check(a1!.PixelWidth == 220 && a1.PixelHeight == 160, "converter bitmap is 220x160");
        Check(a2 is { } && Math.Abs(a1.PixelWidth - a2!.PixelWidth) < 1, "converter deterministic size");

        var t = Wpf.Templates.BuildItemTemplate(Wpf.TemplateKind.Image);
        Check(t is { }, "image template builds");
        var p = Wpf.Templates.BuildPanelTemplate(Wpf.PanelKind.VirtualizingWrap);
        Check(p is { }, "virtualizing panel template builds");
    }
}
