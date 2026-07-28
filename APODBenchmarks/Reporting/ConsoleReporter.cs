using APODBenchmarks.Core;

namespace APODBenchmarks.Reporting;

public static class ConsoleReporter
{
    public static void Write(BenchmarkResult result)
    {
        Console.WriteLine($"== {result.BenchmarkName} ==");
        var metricNames = result.Rows.SelectMany(r => r.Metrics.Keys).Distinct().ToArray();

        Console.WriteLine($"{"Panel",-24} {"Tmpl",-10} {"Items",6} {"Real.",6}  " +
            string.Join("  ", metricNames.Select(m => m.PadLeft(16))));

        foreach (var row in result.Rows)
        {
            string metrics = string.Join("  ", metricNames.Select(m =>
                (row.Metrics.TryGetValue(m, out var s) ? $"{s.Mean,8:F2}±{s.StdDev,-6:F2}" : "-").PadLeft(16)));
            Console.WriteLine($"{row.Panel,-24} {row.Template,-10} {row.ItemCount,6} " +
                $"{row.RealizedContainers,6}  {metrics}");
        }
    }
}
