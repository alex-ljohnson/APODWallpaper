using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace APODWallpaper.Utils;

public static partial class FileMigration
{
    private static readonly Regex IsoDatePattern = IsoDateRegex();

    /// <summary>
    /// Renames image files from old long-date format to ISO format. Also renames .json and updates path inside it.
    /// </summary>
    public static void MigrateImageFilenamesToISO(string imagesDirectory)
    {
        if (!Directory.Exists(imagesDirectory)) return;

        var files = Directory.GetFiles(imagesDirectory);
        var imageFiles = files
            .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var imagePath in imageFiles)
        {
            var filename = Path.GetFileName(imagePath);

            if (IsoDatePattern.IsMatch(filename)) continue;

            var oldJsonPath = imagePath + ".json";
            if (!TryResolveDate(filename, oldJsonPath, out var date))
            {
                Trace.WriteLine($"FileMigration: skipping unrecognised file '{filename}'");
                continue;
            }

            var isoName = APODDate.ToIsoString(date);
            var newImagePath = Path.Combine(imagesDirectory, isoName);
            var newJsonPath = newImagePath + ".json";

            if (File.Exists(newImagePath))
            {
                Trace.WriteLine($"FileMigration: target '{isoName}' already exists, skipping '{filename}'");
                continue;
            }

            File.Move(imagePath, newImagePath);
            Trace.WriteLine($"FileMigration: renamed '{filename}' -> '{isoName}'");

            if (File.Exists(oldJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(oldJsonPath);
                    var obj = JObject.Parse(json);
                    if (obj["Source"] != null)
                    {
                        obj["Source"] = newImagePath;
                        json = obj.ToString(Formatting.Indented);
                    }
                    File.WriteAllText(newJsonPath, json);
                    File.Delete(oldJsonPath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"FileMigration: failed to update JSON for '{filename}': {ex.Message}");
                    if (!File.Exists(newJsonPath))
                        File.Move(oldJsonPath, newJsonPath);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the image's date. Prefer date field in json, fallback to filename parsing. Returns true if a date was successfully resolved.
    /// </summary>
    private static bool TryResolveDate(string filename, string jsonPath, out DateOnly date)
    {
        if (File.Exists(jsonPath))
        {
            try
            {
                var dateToken = JObject.Parse(File.ReadAllText(jsonPath))["Date"];
                if (dateToken != null &&
                    DateOnly.TryParse(dateToken.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"FileMigration: could not read date from '{jsonPath}': {ex.Message}");
            }
        }

        // Old names came from DateOnly.ToString("D") under the machine's current culture.
        if (DateTime.TryParse(filename, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        date = default;
        return false;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDateRegex();
}
