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
    private static readonly Regex IsoNamePattern = IsoNameRegex();

    private static readonly IReadOnlyList<Migration> Migrations =
    [
        new(1, "Image filenames to ISO", static ctx => MigrateImageFilenamesToISO(ctx.GetPath("images"))),
        new(2, "Drop v1 API metadata cache", static ctx => DeleteLegacyMetadataCache(ctx.GetPath("cache/metadata.cache"))),
        new(3, "Image file extensions", static ctx => AddImageExtensions(ctx.GetPath("images"), ctx.ReadWallpaper, ctx.WriteWallpaper)),
        new(4, "Delete saved NASA logos", static ctx => DeleteSavedLogos(ctx.GetPath("images"))),
        // Add future migrations here with the next sequential version number.
        // Steps must be safe to rerun, an IOException leaves the version so the step runs again next start
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
            try
            {
                migration.Run(ctx);
                WriteSchemaVersion(statePath, migration.Version);
            }
            // Locked files or the other app migrating, steps are safe to rerun so try again next start
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"FileMigration: migration {migration.Version} incomplete, retrying next start: {ex.Message}");
                return;
            }
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

            if (IsoNamePattern.IsMatch(filename)) continue;

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
    /// Gives extensionless {date} images the extension of their content, the sidecar and desktop wallpaper path follow
    /// Throws IOException after the rest are done if any couldn't be renamed
    /// </summary>
    public static void AddImageExtensions(string imagesDirectory, Func<string?> readWallpaper, Action<string> writeWallpaper)
    {
        if (!Directory.Exists(imagesDirectory)) return;

        string? wallpaper = null;
        bool wallpaperRead = false;
        int failed = 0;
        foreach (var imagePath in Directory.GetFiles(imagesDirectory).Where(f => IsoDatePattern.IsMatch(Path.GetFileName(f))))
        {
            string? newImagePath;
            try
            {
                newImagePath = AddImageExtension(imagePath);
            }
            // Locked, or another app migrating at the same time, carry on with the rest
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"FileMigration: couldn't rename '{Path.GetFileName(imagePath)}': {ex.Message}");
                failed++;
                continue;
            }
            if (newImagePath == null) continue;

            if (!wallpaperRead)
            {
                wallpaper = TryWallpaper(readWallpaper);
                wallpaperRead = true;
            }
            if (wallpaper != null && string.Equals(wallpaper, Path.GetFullPath(imagePath), StringComparison.OrdinalIgnoreCase))
            {
                // Registry only, the desktop is already showing it
                TryWallpaper(() => { writeWallpaper(newImagePath); return newImagePath; });
                wallpaper = null;
            }
        }
        if (failed > 0) throw new IOException($"{failed} image(s) couldn't be renamed");
    }

    // New path or null when left alone, only throws before the image is moved
    private static string? AddImageExtension(string imagePath)
    {
        var filename = Path.GetFileName(imagePath);
        if (ImageFile.Detect(imagePath) is not ImageFormat format)
        {
            Trace.WriteLine($"FileMigration: '{filename}' isn't a recognised image, leaving it");
            return null;
        }
        var newImagePath = imagePath + ImageFile.Extension(format);
        if (File.Exists(newImagePath))
        {
            Trace.WriteLine($"FileMigration: '{Path.GetFileName(newImagePath)}' already exists, skipping '{filename}'");
            return null;
        }

        File.Move(imagePath, newImagePath);
        Trace.WriteLine($"FileMigration: renamed '{filename}' -> '{Path.GetFileName(newImagePath)}'");
        try
        {
            MoveSidecar(imagePath + ".json", newImagePath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Trace.WriteLine($"FileMigration: couldn't move the sidecar for '{filename}': {ex.Message}");
        }
        return newImagePath;
    }

    private static string? TryWallpaper(Func<string?> access)
    {
        try
        {
            var path = access();
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException || ex is ArgumentException)
        {
            Trace.WriteLine($"FileMigration: couldn't use the wallpaper path: {ex.Message}");
            return null;
        }
    }

    private static void MoveSidecar(string oldJsonPath, string newImagePath)
    {
        if (!File.Exists(oldJsonPath)) return;
        var newJsonPath = newImagePath + ".json";
        if (File.Exists(newJsonPath))
        {
            Trace.WriteLine($"FileMigration: '{Path.GetFileName(newJsonPath)}' already exists, leaving '{Path.GetFileName(oldJsonPath)}'");
            return;
        }
        try
        {
            var obj = JObject.Parse(File.ReadAllText(oldJsonPath));
            obj["Source"] = newImagePath;
            File.WriteAllText(newJsonPath, obj.ToString(Formatting.Indented));
            File.Delete(oldJsonPath);
        }
        catch (JsonException ex)
        {
            Trace.WriteLine($"FileMigration: couldn't update '{Path.GetFileName(oldJsonPath)}': {ex.Message}");
            File.Move(oldJsonPath, newJsonPath);
        }
    }

    /// <summary>
    /// Deletes images saved when the broken v1 API returned NASA's site logo instead of the picture, found by the URL in their sidecar
    /// Throws IOException after the rest are done if any couldn't be deleted
    /// </summary>
    public static void DeleteSavedLogos(string imagesDirectory)
    {
        if (!Directory.Exists(imagesDirectory)) return;

        int failed = 0;
        foreach (var jsonPath in Directory.GetFiles(imagesDirectory, "*.json").Where(f => IsoNamePattern.IsMatch(Path.GetFileNameWithoutExtension(f))))
        {
            try
            {
                if (!IsLogoSidecar(jsonPath)) continue;
                // Image first so a failure leaves the sidecar to find next start
                File.Delete(jsonPath[..^".json".Length]);
                File.Delete(jsonPath);
                Trace.WriteLine($"FileMigration: deleted saved logo '{Path.GetFileNameWithoutExtension(jsonPath)}'");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"FileMigration: couldn't delete '{Path.GetFileNameWithoutExtension(jsonPath)}': {ex.Message}");
                failed++;
            }
        }
        if (failed > 0) throw new IOException($"{failed} saved logo(s) couldn't be deleted");
    }

    // v1 saved the URL it downloaded, the logo was a WordPress theme asset (science.nasa.gov/wp-content/themes/.../nasa-logo@2x.png)
    private static bool IsLogoSidecar(string jsonPath)
    {
        try
        {
            var url = (JObject.Parse(File.ReadAllText(jsonPath))["OriginalUrl"] as JValue)?.Value as string;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.Contains("/wp-content/themes/", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            Trace.WriteLine($"FileMigration: couldn't read '{Path.GetFileName(jsonPath)}': {ex.Message}");
            return false;
        }
    }

    public static void DeleteLegacyMetadataCache(string cachePath)
    {
        if (!File.Exists(cachePath)) return;
        File.Delete(cachePath);
        Trace.WriteLine("FileMigration: deleted v1 metadata cache");
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
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(\.[A-Za-z0-9]+)?$")]
    private static partial Regex IsoNameRegex();
}
