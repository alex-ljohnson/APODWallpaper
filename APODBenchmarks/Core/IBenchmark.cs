namespace APODBenchmarks.Core;

public interface IBenchmark
{
    string Name { get; }
    BenchmarkResult Run(BenchmarkContext context);
}
