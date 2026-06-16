using System.Globalization;

namespace APODWallpaper.Utils;

/// <summary>
/// Central date handling logic. NASA uses eastern time. Simplifies formatting and parsing of dates in local tz vs release tz.
/// </summary>
public static class APODDate
{
    private static readonly TimeZoneInfo Eastern =
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    private const string IsoFormat = "yyyy-MM-dd";

    /// <summary>
    /// Earliest date with APOD image.
    /// </summary>
    public static readonly DateOnly InceptionDate = new(1995, 6, 16);

    /// <summary>
    /// Return today date in NASA's tz (Eastern).
    /// </summary>
    public static DateOnly Today()
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Eastern));
    }

    /// <summary>
    /// Formats date as "yyyy-MM-dd" with invariant culture.
    /// </summary>
    public static string ToIsoString(DateOnly date)
    {
        return date.ToString(IsoFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an "yyyy-MM-dd" string with invariant culture.
    /// </summary>
    public static DateOnly ParseIso(string s)
    {
        return DateOnly.ParseExact(s, IsoFormat, CultureInfo.InvariantCulture);
    }
}
