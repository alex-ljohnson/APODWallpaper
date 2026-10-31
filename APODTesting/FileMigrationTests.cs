using APODWallpaper.Utils;
using System.Globalization;

namespace APODTesting;

[TestClass]
public sealed class FileMigrationTests
{
    private string testDir = null!;

    [TestInitialize]
    public void Setup()
    {
        testDir = Path.Combine(Path.GetTempPath(), "APODMigrationTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);
    }

    [TestMethod]
    public void MigrateImageFilenamesRenamesLongDateToIso()
    {
        var oldName = new DateOnly(2025, 4, 5).ToString("D", CultureInfo.GetCultureInfo("en-US"));
        var oldImagePath = Path.Combine(testDir, oldName);
        var oldJsonPath = oldImagePath + ".json";
        File.WriteAllText(oldImagePath, "fake-image-data");
        File.WriteAllText(oldJsonPath, "{\"Name\":\"Test\",\"Description\":\"d\",\"Source\":\"" + oldImagePath.Replace("\\", "\\\\") + "\",\"Date\":\"2025-04-05\"}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        var expectedImage = Path.Combine(testDir, "2025-04-05");
        var expectedJson = expectedImage + ".json";
        Assert.IsTrue(File.Exists(expectedImage));
        Assert.IsTrue(File.Exists(expectedJson));
        Assert.IsFalse(File.Exists(oldImagePath));
        Assert.IsFalse(File.Exists(oldJsonPath));
    }

    [TestMethod]
    public void MigrateImageFilenamesSkipsAlreadyIsoFiles()
    {
        var isoPath = Path.Combine(testDir, "2025-04-05");
        var isoJsonPath = isoPath + ".json";
        File.WriteAllText(isoPath, "fake-image-data");
        File.WriteAllText(isoJsonPath, "{}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(isoPath));
        Assert.IsTrue(File.Exists(isoJsonPath));
    }

    [TestMethod]
    public void MigrateImageFilenamesUpdatesSourceInJson()
    {
        var oldName = new DateOnly(2025, 4, 5).ToString("D", CultureInfo.GetCultureInfo("en-US"));
        var oldImagePath = Path.Combine(testDir, oldName);
        var oldJsonPath = oldImagePath + ".json";
        File.WriteAllText(oldImagePath, "fake-image-data");
        File.WriteAllText(oldJsonPath, "{\"Name\":\"Test\",\"Description\":\"d\",\"Source\":\"" + oldImagePath.Replace("\\", "\\\\") + "\",\"Date\":\"2025-04-05\"}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        var newJsonPath = Path.Combine(testDir, "2025-04-05.json");
        var json = File.ReadAllText(newJsonPath);
        Assert.IsTrue(json.Contains("2025-04-05"));
    }

    [TestMethod]
    public void MigrateImageFilenamesUsesJsonDateWhenFilenameNotParseable()
    {
        // Unparseable filename; the ISO date comes from the sidecar JSON.
        var oldImagePath = Path.Combine(testDir, "thors_helmet_image");
        var oldJsonPath = oldImagePath + ".json";
        File.WriteAllText(oldImagePath, "fake-image-data");
        File.WriteAllText(oldJsonPath, "{\"Name\":\"Test\",\"Description\":\"d\",\"Source\":\"" + oldImagePath.Replace("\\", "\\\\") + "\",\"Date\":\"2025-04-05\"}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2025-04-05")));
        Assert.IsFalse(File.Exists(oldImagePath));
    }

    [TestMethod]
    public void MigrateImageFilenamesFallsBackToCurrentCultureFilename()
    {
        // No sidecar JSON, fall back to parsing localised filename.
        var oldName = new DateOnly(2025, 4, 5).ToString("D", CultureInfo.CurrentCulture);
        var oldImagePath = Path.Combine(testDir, oldName);
        File.WriteAllText(oldImagePath, "fake-image-data");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2025-04-05")));
        Assert.IsFalse(File.Exists(oldImagePath));
    }

    [TestMethod]
    public void MigrateImageFilenamesFallsBackToFilenameWhenJsonHasNoDate()
    {
        // Sidecar exists but carries no usable Date, so the ISO date must come from the filename.
        var oldName = new DateOnly(2025, 4, 5).ToString("D", CultureInfo.CurrentCulture);
        var oldImagePath = Path.Combine(testDir, oldName);
        var oldJsonPath = oldImagePath + ".json";
        File.WriteAllText(oldImagePath, "fake-image-data");
        File.WriteAllText(oldJsonPath, "{\"Name\":\"Test\",\"Description\":\"d\"}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2025-04-05")));
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2025-04-05.json")));
        Assert.IsFalse(File.Exists(oldImagePath));
    }

    [TestMethod]
    public void MigrateImageFilenamesPrefersJsonDateOverParseableFilename()
    {
        // Both sources are valid but disagree; the JSON Date must win over the filename.
        var oldName = new DateOnly(2025, 4, 6).ToString("D", CultureInfo.CurrentCulture);
        var oldImagePath = Path.Combine(testDir, oldName);
        var oldJsonPath = oldImagePath + ".json";
        File.WriteAllText(oldImagePath, "fake-image-data");
        File.WriteAllText(oldJsonPath, "{\"Name\":\"Test\",\"Description\":\"d\",\"Source\":\"" + oldImagePath.Replace("\\", "\\\\") + "\",\"Date\":\"2025-04-05\"}");

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2025-04-05")), "image should be renamed using the JSON Date");
        Assert.IsFalse(File.Exists(Path.Combine(testDir, "2025-04-06")), "filename date must not be used when JSON Date is present");
        Assert.IsFalse(File.Exists(oldImagePath));
    }

    [TestMethod]
    public void MigrateImageFilenamesHandlesEmptyDirectory()
    {
        FileMigration.MigrateImageFilenamesToISO(testDir);
    }

    [TestMethod]
    public void ReadSchemaVersionMissingFileReturnsZero()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        Assert.AreEqual(0, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void WriteThenReadSchemaVersionRoundTrips()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        FileMigration.WriteSchemaVersion(statePath, 3);
        Assert.AreEqual(3, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void ReadSchemaVersionGarbageFileReturnsZero()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        File.WriteAllText(statePath, "not-a-number");
        Assert.AreEqual(0, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void RunOnlyPendingMigrationsInOrder()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        FileMigration.WriteSchemaVersion(statePath, 1);
        var order = new List<int>();
        var migrations = new List<Migration>
        {
            new(1, "one", _ => order.Add(1)),
            new(2, "two", _ => order.Add(2)),
            new(3, "three", _ => order.Add(3)),
        };

        FileMigration.Run(new MigrationContext(testDir), migrations, statePath);

        CollectionAssert.AreEqual(new[] { 2, 3 }, order);
        Assert.AreEqual(3, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void RunNoStateRunsAllAndRecordsLatest()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        var ran = 0;
        var migrations = new List<Migration>
        {
            new(1, "one", _ => ran++),
            new(2, "two", _ => ran++),
        };

        FileMigration.Run(new MigrationContext(testDir), migrations, statePath);

        Assert.AreEqual(2, ran);
        Assert.AreEqual(2, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void RunDeletesLegacyMetadataCacheOnly()
    {
        var cacheDir = Path.Combine(testDir, "cache");
        Directory.CreateDirectory(cacheDir);
        var legacy = Path.Combine(cacheDir, "metadata.cache");
        var current = Path.Combine(cacheDir, "metadata.v2.cache");
        File.WriteAllText(legacy, "[]");
        File.WriteAllText(current, "[]");

        FileMigration.Run(new MigrationContext(testDir));

        Assert.IsFalse(File.Exists(legacy));
        Assert.IsTrue(File.Exists(current));
    }

    [TestMethod]
    public void DeleteLegacyMetadataCacheIgnoresMissingFile()
    {
        FileMigration.DeleteLegacyMetadataCache(Path.Combine(testDir, "cache", "metadata.cache"));
    }

    [TestMethod]
    public void RunAdvancesStateAfterEachMigration()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        var seen = new List<int>();
        var migrations = new List<Migration>
        {
            new(1, "one", _ => seen.Add(FileMigration.ReadSchemaVersion(statePath))),
            new(2, "two", _ => seen.Add(FileMigration.ReadSchemaVersion(statePath))),
        };

        FileMigration.Run(new MigrationContext(testDir), migrations, statePath);

        CollectionAssert.AreEqual(new[] { 0, 1 }, seen);
    }

    [TestMethod]
    public void RunFailedMigrationDoesNotAdvanceOrRunLater()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        var ranThird = false;
        var migrations = new List<Migration>
        {
            new(1, "ok", _ => { }),
            new(2, "boom", _ => throw new InvalidOperationException("boom")),
            new(3, "later", _ => ranThird = true),
        };

        Assert.ThrowsException<InvalidOperationException>(() =>
            FileMigration.Run(new MigrationContext(testDir), migrations, statePath));

        Assert.IsFalse(ranThird, "migrations after a failure must not run");
        Assert.AreEqual(1, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void RunFileErrorStopsQuietlyWithoutAdvancing()
    {
        var statePath = Path.Combine(testDir, "migrations.state");
        var ranThird = false;
        var migrations = new List<Migration>
        {
            new(1, "ok", _ => { }),
            new(2, "locked", _ => throw new IOException("locked")),
            new(3, "later", _ => ranThird = true),
        };

        FileMigration.Run(new MigrationContext(testDir), migrations, statePath);

        Assert.IsFalse(ranThird);
        Assert.AreEqual(1, FileMigration.ReadSchemaVersion(statePath));
    }

    private string? wallpaper;

    private void AddExtensions() => FileMigration.AddImageExtensions(testDir, () => wallpaper, path => wallpaper = path);

    private string WriteSidecar(string imagePath)
    {
        File.WriteAllText(imagePath + ".json", "{\"Name\":\"Test\",\"Description\":\"d\",\"Source\":\"" + imagePath.Replace("\\", "\\\\") + "\",\"Date\":\"2026-08-05\",\"OriginalUrl\":\"https://example.test/a.gif\"}");
        return imagePath + ".json";
    }

    [TestMethod]
    public void AddImageExtensionsUsesTheContentsFormat()
    {
        var gif = Path.Combine(testDir, "2026-08-05");
        var jpeg = Path.Combine(testDir, "2026-08-06");
        File.WriteAllBytes(gif, TestImages.Gif(10, 10));
        File.WriteAllBytes(jpeg, TestImages.Jpeg(10, 10));

        AddExtensions();

        Assert.IsTrue(File.Exists(gif + ".gif"));
        Assert.IsTrue(File.Exists(jpeg + ".jpg"));
        Assert.IsFalse(File.Exists(gif));
        Assert.IsFalse(File.Exists(jpeg));
    }

    [TestMethod]
    public void AddImageExtensionsMovesAndRewritesTheSidecar()
    {
        var image = Path.Combine(testDir, "2026-08-05");
        File.WriteAllBytes(image, TestImages.Gif(10, 10));
        var oldJson = WriteSidecar(image);

        AddExtensions();

        Assert.IsFalse(File.Exists(oldJson));
        var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(image + ".gif.json"));
        Assert.AreEqual(image + ".gif", (string?)json["Source"]);
        Assert.AreEqual("https://example.test/a.gif", (string?)json["OriginalUrl"]);
    }

    [TestMethod]
    public void AddImageExtensionsLeavesUnrecognisedFiles()
    {
        var image = Path.Combine(testDir, "2026-08-05");
        File.WriteAllText(image, "<html>not an image</html>");
        var json = WriteSidecar(image);

        AddExtensions();

        Assert.IsTrue(File.Exists(image));
        Assert.IsTrue(File.Exists(json));
        Assert.AreEqual(2, Directory.GetFiles(testDir).Length);
    }

    [TestMethod]
    public void AddImageExtensionsSkipsWhenTheTargetExists()
    {
        var image = Path.Combine(testDir, "2026-08-05");
        File.WriteAllBytes(image, TestImages.Gif(10, 10));
        File.WriteAllBytes(image + ".gif", TestImages.Gif(20, 20));

        AddExtensions();

        Assert.IsTrue(File.Exists(image));
        using var kept = File.OpenRead(image + ".gif");
        Assert.AreEqual(new ImageSize(20, 20), ImageFile.ReadSize(kept));
    }

    [TestMethod]
    public void AddImageExtensionsLeavesOrphanSidecarsAndNamedFiles()
    {
        var orphan = WriteSidecar(Path.Combine(testDir, "2026-08-05"));
        var named = Path.Combine(testDir, "2026-08-06.jpg");
        File.WriteAllBytes(named, TestImages.Jpeg(10, 10));

        AddExtensions();

        Assert.IsTrue(File.Exists(orphan));
        Assert.IsTrue(File.Exists(named));
        Assert.AreEqual(2, Directory.GetFiles(testDir).Length);
    }

    [TestMethod]
    public void AddImageExtensionsMovesTheWallpaperPath()
    {
        var image = Path.Combine(testDir, "2026-08-05");
        File.WriteAllBytes(image, TestImages.Gif(10, 10));
        wallpaper = image.ToUpperInvariant();

        AddExtensions();

        Assert.AreEqual(image + ".gif", wallpaper);
    }

    [TestMethod]
    public void AddImageExtensionsLeavesOtherWallpapers()
    {
        File.WriteAllBytes(Path.Combine(testDir, "2026-08-05"), TestImages.Gif(10, 10));
        wallpaper = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";

        AddExtensions();

        Assert.AreEqual(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", wallpaper);
    }

    [TestMethod]
    public void AddImageExtensionsIgnoresAnEmptyWallpaper()
    {
        File.WriteAllBytes(Path.Combine(testDir, "2026-08-05"), TestImages.Gif(10, 10));
        File.WriteAllBytes(Path.Combine(testDir, "2026-08-06"), TestImages.Gif(10, 10));
        int reads = 0, writes = 0;

        // Solid colour backgrounds store an empty path
        FileMigration.AddImageExtensions(testDir, () => { reads++; return ""; }, _ => writes++);

        Assert.AreEqual(1, reads);
        Assert.AreEqual(0, writes);
        Assert.IsTrue(File.Exists(Path.Combine(testDir, "2026-08-06.gif")));
    }

    [TestMethod]
    public void AddImageExtensionsSkipsLockedFiles()
    {
        var locked = Path.Combine(testDir, "2026-08-05");
        var free = Path.Combine(testDir, "2026-08-06");
        File.WriteAllBytes(locked, TestImages.Gif(10, 10));
        File.WriteAllBytes(free, TestImages.Gif(10, 10));

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsException<IOException>(AddExtensions);

        Assert.IsTrue(File.Exists(locked));
        Assert.IsTrue(File.Exists(free + ".gif"));
    }

    [TestMethod]
    public void RunRetriesALockedImageNextStart()
    {
        var images = Path.Combine(testDir, "images");
        Directory.CreateDirectory(images);
        var locked = Path.Combine(images, "2026-08-05");
        File.WriteAllBytes(locked, TestImages.Gif(10, 10));
        var statePath = Path.Combine(testDir, "migrations.state");
        FileMigration.WriteSchemaVersion(statePath, 2);
        var ctx = new MigrationContext(testDir) { ReadWallpaper = () => null, WriteWallpaper = _ => { } };

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            FileMigration.Run(ctx);
        Assert.AreEqual(2, FileMigration.ReadSchemaVersion(statePath));

        FileMigration.Run(ctx);
        Assert.IsTrue(File.Exists(locked + ".gif"));
        Assert.AreEqual(3, FileMigration.ReadSchemaVersion(statePath));
    }

    [TestMethod]
    public void AddImageExtensionsSurvivesRegistryErrors()
    {
        var image = Path.Combine(testDir, "2026-08-05");
        File.WriteAllBytes(image, TestImages.Gif(10, 10));

        FileMigration.AddImageExtensions(testDir, () => throw new UnauthorizedAccessException(), _ => { });

        Assert.IsTrue(File.Exists(image + ".gif"));
    }

    [TestMethod]
    public void MigrateImageFilenamesSkipsIsoFilesWithExtensions()
    {
        var image = Path.Combine(testDir, "2025-04-05.jpg");
        File.WriteAllBytes(image, TestImages.Jpeg(10, 10));

        FileMigration.MigrateImageFilenamesToISO(testDir);

        Assert.IsTrue(File.Exists(image));
    }

    [TestMethod]
    public void RunProductionIsoFilesNoStateRecordsLatestVersion()
    {
        var imagesDir = Path.Combine(testDir, "images");
        Directory.CreateDirectory(imagesDir);
        var isoPath = Path.Combine(imagesDir, "2025-04-05");
        File.WriteAllText(isoPath, "fake-image-data");
        File.WriteAllText(isoPath + ".json", "{}");

        FileMigration.Run(new MigrationContext(testDir));

        Assert.IsTrue(File.Exists(isoPath));
        Assert.IsTrue(FileMigration.ReadSchemaVersion(Path.Combine(testDir, "migrations.state")) >= 1);
    }
}
