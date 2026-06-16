namespace APODWallpaper.Utils;

/// <summary>
/// Helpers for presenting and parsing assembly informational versions of the
/// form "1.2.3.4+&lt;commit-sha&gt;" produced by the .NET SDK.
/// </summary>
public static class VersionInfo
{
    private const int ShortShaLength = 7;

    /// <summary>
    /// Display form with a shortened commit SHA, e.g. "2026.06.16.1 (a1b2c3d)".
    /// Falls back to the bare version when no SHA is present.
    /// </summary>
    public static string ForDisplay(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion)) return "Unknown";
        int plus = informationalVersion.IndexOf('+');
        if (plus < 0) return informationalVersion;
        string version = informationalVersion[..plus];
        string sha = informationalVersion[(plus + 1)..];
        if (sha.Length == 0) return version;
        if (sha.Length > ShortShaLength) sha = sha[..ShortShaLength];
        return $"{version} ({sha})";
    }

    /// <summary>
    /// Numeric form with any "+sha" suffix stripped, safe for <see cref="System.Version"/>
    /// parsing and HTTP product-version headers.
    /// </summary>
    public static string Numeric(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion)) return "0.0.0.0";
        int plus = informationalVersion.IndexOf('+');
        return plus < 0 ? informationalVersion : informationalVersion[..plus];
    }
}
