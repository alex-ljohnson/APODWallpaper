using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("APODTesting")]

namespace APODWallpaper.Utils;

public static partial class FileMigration
{
    private static readonly Regex IsoDatePattern = IsoDateRegex();

    private static readonly IReadOnlyList<Migration> Migrations =
    [
        new(1, "Image filenames to ISO", static ctx => MigrateImageFilenamesToISO(ctx.GetPath("images"))),
        // Add future migrations here with the next sequential version number.
    ];

    /// <summary>Runs all pending migrations for the given context, advancing the persisted schema version.</summary>
    public static void Run(MigrationContext ctx)
        => Run(ctx, Migrations, ctx.GetPath("migrations.state"));

    internal static void Run(MigrationContext ctx, IReadOnlyList<Migration> migrations, string statePath)
    {
        var current = ReadSchemaVersion(statePath);
        foreach (var migration in migrations.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            Trace.WriteLine($"FileMigration: running migration {migration.Version} '{migration.Name}'");
            migration.Run(ctx);
            WriteSchemaVersion(statePath, migration.Version);
        }
    }

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

    internal static int ReadSchemaVersion(string statePath)
    {
        try
        {
            if (!File.Exists(statePath)) return 0;
            return int.TryParse(File.ReadAllText(statePath).Trim(), out var v) ? v : 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"FileMigration: failed to read state '{statePath}': {ex.Message}");
            return 0;
        }
    }

    internal static void WriteSchemaVersion(string statePath, int version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, version.ToString());
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDateRegex();
}
