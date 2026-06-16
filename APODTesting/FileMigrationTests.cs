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
