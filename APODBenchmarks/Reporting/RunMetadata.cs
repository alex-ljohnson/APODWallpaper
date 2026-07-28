namespace APODBenchmarks.Reporting;

public sealed class RunMetadata
{
    public string Machine { get; }
    public DateTime UtcTimestamp { get; }
    public string RuntimeVersion { get; }
    public double Dpi { get; }
    public string Config { get; }

    public RunMetadata(string machine, DateTime utcTimestamp, string runtimeVersion, double dpi, string config)
    {
        Machine = machine; UtcTimestamp = utcTimestamp; RuntimeVersion = runtimeVersion;
        Dpi = dpi; Config = config;
    }

    public static RunMetadata Capture(double dpi, string config) => new(
        Environment.MachineName,
        DateTime.UtcNow,
        Environment.Version.ToString(),
        dpi,
        config);
}
