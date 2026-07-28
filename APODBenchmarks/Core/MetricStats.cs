namespace APODBenchmarks.Core;

public sealed class MetricStats
{
    public string Unit { get; }
    public double Mean { get; }
    public double StdDev { get; }
    public double Min { get; }
    public double Max { get; }
    public double Median { get; }
    public (double Lower, double Upper) Ci95 { get; }
    public int SampleCount { get; }

    private MetricStats(string unit, double mean, double stdDev, double min, double max,
        double median, (double, double) ci95, int count)
    {
        Unit = unit; Mean = mean; StdDev = stdDev; Min = min; Max = max;
        Median = median; Ci95 = ci95; SampleCount = count;
    }

    public static MetricStats FromSamples(string unit, IReadOnlyList<double> samples)
    {
        if (samples is null || samples.Count == 0)
            throw new ArgumentException("samples must be non-empty", nameof(samples));

        var sorted = samples.OrderBy(x => x).ToArray();
        int n = sorted.Length;
        double mean = sorted.Average();
        double variance = n > 1
            ? sorted.Sum(x => (x - mean) * (x - mean)) / (n - 1) // sample variance
            : 0;
        double stdDev = Math.Sqrt(variance);
        double median = ComputeMedian(sorted);
        var ci = n > 1 ? ConfidenceInterval95(sorted, mean, stdDev) : (mean, mean);

        return new MetricStats(unit, mean, stdDev, sorted[0], sorted[^1], median, ci, n);
    }

    private static double ComputeMedian(double[] sorted)
    {
        int n = sorted.Length;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    // w/ Perfolizer CI estimator
    private static (double Lower, double Upper) ConfidenceInterval95(
        double[] sorted, double mean, double stdDev)
    {
        int n = sorted.Length;
        double sem = stdDev / Math.Sqrt(n);
        var ci = new Perfolizer.Mathematics.Common.ConfidenceIntervalEstimator(n, mean, sem)
            .ConfidenceInterval(Perfolizer.Mathematics.Common.ConfidenceLevel.L95);
        return (ci.Lower, ci.Upper);
    }

    public override string ToString() => $"{Mean:F2} {Unit} (±{StdDev:F2})";
}
