using System.IO;
using System.Text;
using APODBenchmarks.Core;

namespace APODBenchmarks.Reporting;

public static class MarkdownReporter
{
    public static string Render(BenchmarkResult result, RunMetadata meta)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Benchmark: {result.BenchmarkName}");
        sb.AppendLine();
        sb.AppendLine($"- Machine: {meta.Machine}");
        sb.AppendLine($"- UTC: {meta.UtcTimestamp:u}");
        sb.AppendLine($"- Runtime: {meta.RuntimeVersion}");
        sb.AppendLine($"- DPI scale: {meta.Dpi:F2}");
        sb.AppendLine($"- Config: {meta.Config}");
        sb.AppendLine();

        // union of metric names across rows, stable order
        var metricNames = result.Rows.SelectMany(r => r.Metrics.Keys).Distinct().ToArray();

        sb.Append("| Panel | Template | Items | Realized |");
        foreach (var m in metricNames) sb.Append($" {m} |");
        sb.AppendLine();
        sb.Append("|---|---|---:|---:|");
        foreach (var _ in metricNames) sb.Append("---:|");
        sb.AppendLine();

        foreach (var row in result.Rows)
        {
            sb.Append($"| {row.Panel} | {row.Template} | {row.ItemCount} | {row.RealizedContainers} |");
            foreach (var m in metricNames)
            {
                string cell = row.Metrics.TryGetValue(m, out var s)
                    ? $"{s.Mean:F2} ±{s.StdDev:F2}"
                    : "-";
                sb.Append($" {cell} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string WriteToFile(BenchmarkResult result, RunMetadata meta, string resultsDir)
    {
        Directory.CreateDirectory(resultsDir);
        string path = Path.Combine(resultsDir,
            $"{meta.UtcTimestamp:yyyyMMdd-HHmmss}-{result.BenchmarkName}.md");
        File.WriteAllText(path, Render(result, meta));
        return path;
    }
}
