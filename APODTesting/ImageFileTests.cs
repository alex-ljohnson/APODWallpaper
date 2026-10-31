using APODWallpaper.Utils;

namespace APODTesting;

[TestClass]
public sealed class ImageFileTests
{
    private string testDir = null!;

    [TestInitialize]
    public void Setup()
    {
        testDir = Path.Combine(Path.GetTempPath(), "ImageFileTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
    }

    private static ImageSize? SizeOf(byte[] bytes) => ImageFile.ReadSize(new MemoryStream(bytes));

    [TestMethod]
    public void DetectsEachAllowedFormat()
    {
        Assert.AreEqual(ImageFormat.Jpeg, ImageFile.Detect(TestImages.Jpeg(1, 1)));
        Assert.AreEqual(ImageFormat.Png, ImageFile.Detect(TestImages.Png(1, 1)));
        Assert.AreEqual(ImageFormat.Gif, ImageFile.Detect(TestImages.Gif(1, 1)));
        Assert.AreEqual(ImageFormat.Gif, ImageFile.Detect("GIF87a"u8));
        Assert.AreEqual(ImageFormat.Bmp, ImageFile.Detect(TestImages.Bmp(1, 1)));
        Assert.AreEqual(ImageFormat.Tiff, ImageFile.Detect(TestImages.Tiff(1, 1, little: true)));
        Assert.AreEqual(ImageFormat.Tiff, ImageFile.Detect(TestImages.Tiff(1, 1, little: false)));
    }

    [TestMethod]
    public void RejectsHtmlEmptyAndTruncatedSignatures()
    {
        Assert.IsNull(ImageFile.Detect(TestImages.Html));
        Assert.IsNull(ImageFile.Detect(ReadOnlySpan<byte>.Empty));
        Assert.IsNull(ImageFile.Detect(TestImages.Png(1, 1).AsSpan(0, 4)));
        Assert.IsNull(ImageFile.Detect("RIFF\0\0\0\0WEBP"u8));
    }

    [TestMethod]
    public void ReadsSizes()
    {
        Assert.AreEqual(new ImageSize(6048, 4030), SizeOf(TestImages.Jpeg(6048, 4030)));
        Assert.AreEqual(new ImageSize(2622, 1751), SizeOf(TestImages.Png(2622, 1751)));
        Assert.AreEqual(new ImageSize(1133, 716), SizeOf(TestImages.Gif(1133, 716)));
        Assert.AreEqual(new ImageSize(800, 600), SizeOf(TestImages.Bmp(800, 600)));
        Assert.AreEqual(new ImageSize(800, 600), SizeOf(TestImages.Bmp(800, -600)));
        Assert.AreEqual(new ImageSize(640, 480), SizeOf(TestImages.Bmp(640, 480, core: true)));
        Assert.AreEqual(new ImageSize(310, 624), SizeOf(TestImages.Tiff(310, 624, little: true)));
        Assert.AreEqual(new ImageSize(310, 624), SizeOf(TestImages.Tiff(310, 624, little: false)));
    }

    [TestMethod]
    public void UnreadableSizesAreNull()
    {
        Assert.IsNull(SizeOf(TestImages.Html));
        Assert.IsNull(SizeOf([]));
        // Signature only
        Assert.IsNull(SizeOf(TestImages.Png(1, 1)[..8]));
        Assert.IsNull(SizeOf(TestImages.Jpeg(100, 100, sosFirst: true)));
        Assert.IsNull(SizeOf(TestImages.Jpeg(100, 100)[..6]));
        Assert.IsNull(SizeOf(TestImages.Png(0, 100)));
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(testDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [TestMethod]
    public void CheckFlagsUnderHalfTheExpectedSize()
    {
        var path = Write("a", TestImages.Jpeg(594, 516));

        var small = ImageFile.Check(path, new ImageSize(6048, 4030))!;
        Assert.AreEqual(ImageFormat.Jpeg, small.Format);
        Assert.IsTrue(small.Undersized);
        // Proxy drift
        Assert.IsFalse(ImageFile.Check(path, new ImageSize(600, 516))!.Undersized);
        Assert.IsFalse(ImageFile.Check(path, null)!.Undersized);
    }

    [TestMethod]
    public void CheckRejectsNonImages()
    {
        Assert.IsNull(ImageFile.Check(Write("a", TestImages.Html), new ImageSize(100, 100)));
    }

    [TestMethod]
    public void CheckKeepsAnAllowedFormatWithAnUnreadableSize()
    {
        var check = ImageFile.Check(Write("a", TestImages.Jpeg(100, 100, sosFirst: true)), new ImageSize(100, 100))!;
        Assert.IsNull(check.Size);
        Assert.IsFalse(check.Undersized);
    }

    [TestMethod]
    public void DetectFileReadsShortFiles()
    {
        Assert.AreEqual(ImageFormat.Bmp, ImageFile.Detect(Write("a", "BM"u8.ToArray())));
        Assert.IsNull(ImageFile.Detect(Write("b", [])));
    }

    [TestMethod]
    public void FindSavedTakesOnlyThatDatesImage()
    {
        var date = new DateOnly(2026, 10, 30);
        Write("2026-10-30.png.json", []);
        Write("2026-10-30.part", []);
        Write("2026-10-30", []);
        Write("2026-10-29.jpg", []);
        Assert.IsNull(ImageFile.FindSaved(testDir, date));

        var saved = Write("2026-10-30.png", []);
        Assert.AreEqual(saved, ImageFile.FindSaved(testDir, date));
    }

    [TestMethod]
    public void FindSavedMissingDirectoryIsNull()
    {
        Assert.IsNull(ImageFile.FindSaved(Path.Combine(testDir, "missing"), new DateOnly(2026, 10, 30)));
    }

    [TestMethod]
    public void ExtensionsRoundTrip()
    {
        foreach (var format in Enum.GetValues<ImageFormat>())
            Assert.AreEqual(format, ImageFile.FromExtension(ImageFile.Extension(format)));
        Assert.AreEqual(ImageFormat.Jpeg, ImageFile.FromExtension(".JPEG"));
        Assert.IsNull(ImageFile.FromExtension(".json"));
        Assert.IsNull(ImageFile.FromExtension(""));
    }
}
