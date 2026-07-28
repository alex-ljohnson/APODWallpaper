namespace APODBenchmarks.Core;

public sealed class BenchmarkContext
{
    public IReadOnlyList<int> ItemCounts { get; }
    public int Iterations { get; }
    public int Warmup { get; }

    public BenchmarkContext(IReadOnlyList<int> itemCounts, int iterations, int warmup)
    {
        if (itemCounts is null || itemCounts.Count == 0)
            throw new ArgumentException("itemCounts must be non-empty", nameof(itemCounts));
        if (iterations <= 0)
            throw new ArgumentException("iterations must be positive", nameof(iterations));
        if (warmup < 0 || warmup >= iterations)
            throw new ArgumentException("warmup must be >= 0 and < iterations", nameof(warmup));
        foreach (int n in itemCounts)
            if (n <= 0) throw new ArgumentException("item counts must be positive", nameof(itemCounts));

        ItemCounts = itemCounts;
        Iterations = iterations;
        Warmup = warmup;
    }

    public static BenchmarkContext Default =>
        new([100, 365, 1000, 3000], iterations: 15, warmup: 3);
}
