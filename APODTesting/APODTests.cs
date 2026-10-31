namespace APODTesting;

[TestClass]
public sealed class APODTests
{
    private string statePath = null!;

    [TestInitialize]
    public void Setup()
    {
        statePath = Path.Combine(Path.GetTempPath(), "APODTests_" + Guid.NewGuid().ToString("N"), "last-update.state");
    }

    [TestCleanup]
    public void Cleanup()
    {
        var dir = Path.GetDirectoryName(statePath)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [TestMethod]
    public void NoStateMeansNeverUpdated()
    {
        Assert.IsNull(APODWallpaper.APODWallpaper.ReadLastUpdate(statePath));
    }

    [TestMethod]
    public void LastUpdateRoundTrips()
    {
        var date = new DateOnly(2026, 10, 29);
        APODWallpaper.APODWallpaper.WriteLastUpdate(statePath, date);
        Assert.AreEqual(date, APODWallpaper.APODWallpaper.ReadLastUpdate(statePath));
    }

    [TestMethod]
    // Today's out
    [DataRow(29, 28, false)]
    // Yesterday's already set
    [DataRow(28, 28, true)]
    // Yesterday's never set
    [DataRow(28, 27, false)]
    [DataRow(28, null, false)]
    public void WaitsForTodayOnlyOnceYesterdayIsHandled(int latestDay, int? lastDay, bool waiting)
    {
        DateOnly Day(int d) => new(2026, 10, d);
        DateOnly? last = lastDay is int l ? Day(l) : null;
        Assert.AreEqual(waiting, APODWallpaper.APODWallpaper.WaitingForToday(Day(latestDay), last, Day(29)));
    }

    [TestMethod]
    [DataRow("image", true, true, true)]
    [DataRow("image", true, false, true)]
    [DataRow("video", true, true, true)]
    [DataRow("video", true, false, false)]
    // Old videos only have the placeholder, mapped to no image
    [DataRow("video", false, true, false)]
    [DataRow("other", false, true, false)]
    public void WallpaperUsesStillsOnlyWhenAllowed(string mediaType, bool hasImage, bool useStills, bool expected)
    {
        var info = new APODWallpaper.Utils.APODInfo(new DateOnly(2026, 10, 28), "T", "E", null, mediaType,
            hasImage ? new Uri("https://example.test/a.jpg") : null, new Uri("https://example.test/post/"));
        Assert.AreEqual(expected, APODWallpaper.APODWallpaper.ShouldSetWallpaper(info, useStills));
    }

    [TestMethod]
    [DataRow("video", "a video")]
    [DataRow("iframe", "interactive")]
    [DataRow("other", "not an image")]
    public void DescribesMediaByType(string mediaType, string expected)
    {
        Assert.AreEqual(expected, APODWallpaper.APODWallpaper.DescribeMedia(mediaType));
    }

    [TestMethod]
    public void UnreadableStateMeansNeverUpdated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, "not a date");
        Assert.IsNull(APODWallpaper.APODWallpaper.ReadLastUpdate(statePath));
    }
}
