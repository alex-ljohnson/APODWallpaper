namespace APODBenchmarks.Core;

public sealed class BenchmarkResult
{
    public string BenchmarkName { get; }
    public IReadOnlyList<BenchmarkRow> Rows { get; }

    public BenchmarkResult(string benchmarkName, IReadOnlyList<BenchmarkRow> rows)
    {
        BenchmarkName = benchmarkName; Rows = rows;
    }
}
