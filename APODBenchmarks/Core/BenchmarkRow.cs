namespace APODBenchmarks.Core;

public sealed class BenchmarkRow
{
    public string Panel { get; }
    public string Template { get; }
    public int ItemCount { get; }
    public double Dpi { get; }
    public IReadOnlyDictionary<string, MetricStats> Metrics { get; }
    public int RealizedContainers { get; }

    public BenchmarkRow(string panel, string template, int itemCount, double dpi,
        IReadOnlyDictionary<string, MetricStats> metrics, int realizedContainers)
    {
        Panel = panel; Template = template; ItemCount = itemCount; Dpi = dpi;
        Metrics = metrics; RealizedContainers = realizedContainers;
    }
}
