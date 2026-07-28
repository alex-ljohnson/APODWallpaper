using System.Diagnostics;

namespace APODBenchmarks.Core;

public static class Measure
{
    /// <summary>
    /// Run the given action a specified number of times, with an optional warmup period and reset action, and return the execution time for each iteration in milliseconds.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="iterations"></param>
    /// <param name="warmup"></param>
    /// <param name="reset"></param>
    /// <returns></returns>
    public static double[] Time(Action action, int iterations, int warmup, Action? reset = null)
    {
        var samples = new double[iterations - warmup];
        var sw = new Stopwatch();
        for (int i = 0; i < iterations; i++)
        {
            reset?.Invoke();
            sw.Restart();
            action();
            sw.Stop();
            if (i >= warmup)
                samples[i - warmup] = sw.Elapsed.TotalMilliseconds;
        }
        return samples;
    }

    public static (double managedHeapMB, double workingSetMB) MemorySnapshot()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        double heap = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
        using var proc = Process.GetCurrentProcess();
        double ws = proc.WorkingSet64 / (1024.0 * 1024.0);
        return (heap, ws);
    }
}
