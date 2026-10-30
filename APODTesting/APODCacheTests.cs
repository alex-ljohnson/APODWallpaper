using APODWallpaper.Utils;
using System.Net;

namespace APODTesting;

/// <summary>
/// No non-key failures, they show a message box
/// </summary>
[TestClass]
public sealed class APODCacheTests
{
    private string cacheFile = null!;

    [TestInitialize]
    public void Setup()
    {
        cacheFile = Path.Combine(Path.GetTempPath(), "APODCacheTest_" + Guid.NewGuid().ToString("N"), "metadata.v2.cache");
    }

    [TestCleanup]
    public void Cleanup()
    {
        var dir = Path.GetDirectoryName(cacheFile)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private APODCache Create(FakeHandler handler, RecordingKeyHandler? keyHandler = null)
    {
        var factory = new StubFactory(handler);
        var config = new StubConfig();
        var client = new ApodApiClient(factory, config, "https://example.test/apod-basic");
        return new APODCache(factory, config, keyHandler ?? new RecordingKeyHandler(), client, cacheFile);
    }

    private static APODInfo Info(DateOnly date, string mediaType = "image", string? imageUrl = "https://example.test/a.jpg") =>
        new(date, "T", "E", null, mediaType, imageUrl == null ? null : new Uri(imageUrl), new Uri("https://example.test/post/"));

    private static FakeHandler NoRequests() => new(_ => throw new AssertFailedException("Unexpected request"));

    [TestMethod]
    public async Task NonImageEntriesAreCacheHits()
    {
        var handler = NoRequests();
        var cache = Create(handler);
        var date = new DateOnly(2012, 3, 5);
        await cache.AddToCacheAsync(Info(date, "video", imageUrl: null));

        var info = await cache.GetAsync(date);
        Assert.AreEqual("video", info!.MediaType);
        Assert.AreEqual(0, handler.Requests.Count());
    }

    [TestMethod]
    public async Task RangeOverGapIsServedFromCacheAfterOneFetch()
    {
        var start = new DateOnly(1995, 6, 16);
        // 1995-06-17 to 19 is a real archive gap
        DateOnly[] published = [start, start.AddDays(4)];
        var handler = new FakeHandler(_ => FakeHandler.Json(Fixtures.List(published)));
        var cache = Create(handler);

        var first = await cache.GetRangeAsync(start, start.AddDays(4));
        var second = await cache.GetRangeAsync(start, start.AddDays(4));

        CollectionAssert.AreEqual(published, first!.Select(i => i.Date).ToArray());
        CollectionAssert.AreEqual(published, second!.Select(i => i.Date).ToArray());
        Assert.AreEqual(1, handler.Requests.Count());
    }

    [TestMethod]
    public async Task RangeOnlyFetchesUncachedSpan()
    {
        var start = new DateOnly(2020, 1, 1);
        var handler = new FakeHandler(_ => FakeHandler.Json(Fixtures.List([start.AddDays(3), start.AddDays(4)])));
        var cache = Create(handler);
        await cache.AddToCacheAsync([Info(start), Info(start.AddDays(1)), Info(start.AddDays(2))]);

        var result = await cache.GetRangeAsync(start, start.AddDays(4));

        Assert.AreEqual(5, result!.Length);
        handler.Requests.TryPeek(out var uri);
        StringAssert.Contains(uri!.Query, "date_from=200104");
    }

    [TestMethod]
    public async Task PartialRangeMarksNoGaps()
    {
        // Paging ignored, every page is the first 25 days
        var start = new DateOnly(2020, 1, 1);
        var handler = new FakeHandler(_ => FakeHandler.Json(Fixtures.List(Enumerable.Range(0, 25).Select(start.AddDays))));
        var cache = Create(handler);

        await cache.GetRangeAsync(start, start.AddDays(29));
        int firstRequests = handler.Requests.Count;
        await cache.GetRangeAsync(start, start.AddDays(29));

        Assert.IsTrue(handler.Requests.Count > firstRequests);
    }

    [TestMethod]
    public async Task KnownGapIsNotRefetched()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(Fixtures.Read("not-found"), HttpStatusCode.NotFound));
        var cache = Create(handler);
        var gap = new DateOnly(1995, 6, 17);

        Assert.IsNull(await cache.GetAsync(gap));
        Assert.IsNull(await cache.GetAsync(gap));
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CacheFileRoundTrips()
    {
        var date = new DateOnly(2026, 10, 4);
        await Create(NoRequests()).AddToCacheAsync(Info(date));

        var reloaded = Create(NoRequests());
        Assert.IsTrue(reloaded.IsCached(date));
        Assert.AreEqual(new Uri("https://example.test/a.jpg"), (await reloaded.GetAsync(date))!.ImageUrl);
    }

    [TestMethod]
    public void CorruptCacheFileStartsEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        File.WriteAllText(cacheFile, "{ not a cache");
        Assert.IsFalse(Create(NoRequests()).IsCached(new DateOnly(2026, 10, 4)));
    }

    [TestMethod]
    public async Task OtherMapperVersionStartsEmpty()
    {
        var date = new DateOnly(2026, 10, 4);
        await Create(NoRequests()).AddToCacheAsync(Info(date));
        File.WriteAllText(cacheFile, File.ReadAllText(cacheFile).Replace($"\"MapperVersion\": {ApodResponseMapper.Version}", "\"MapperVersion\": 0"));

        Assert.IsFalse(Create(NoRequests()).IsCached(date));
    }

    [TestMethod]
    public async Task DownloadWritesTheCompleteFile()
    {
        var target = Path.Combine(Path.GetDirectoryName(cacheFile)!, "2026-10-04.jpg");
        var cache = Create(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }));

        await cache.DownloadURLAsync(new Uri("https://example.test/a.jpg"), target);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(target));
        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(cacheFile)!, "2026-10-04.jpg.part")));
    }

    [TestMethod]
    public async Task BrokenDownloadLeavesNoFile()
    {
        var target = Path.Combine(Path.GetDirectoryName(cacheFile)!, "2026-10-04.jpg");
        var cache = Create(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BreakingStream()) }));

        await Assert.ThrowsExceptionAsync<IOException>(() => cache.DownloadURLAsync(new Uri("https://example.test/a.jpg"), target));

        Assert.IsFalse(File.Exists(target));
        Assert.IsFalse(File.Exists(target + ".part"));
        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(cacheFile)!, "2026-10-04.jpg.part")));
    }

    // Gives some bytes then fails, like a dropped connection
    private sealed class BreakingStream : MemoryStream
    {
        private bool sent;
        public BreakingStream() : base([1, 2, 3]) { }
        public override int Read(byte[] buffer, int offset, int count) => sent ? throw new IOException("dropped") : Send(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            sent ? throw new IOException("dropped") : new(Send(buffer.Span));
        private int Send(byte[] buffer, int offset, int count) => Send(buffer.AsSpan(offset, count));
        private int Send(Span<byte> buffer)
        {
            sent = true;
            return base.Read(buffer);
        }
    }

    [TestMethod]
    public async Task RandomReturnsDistinctDates()
    {
        var handler = new FakeHandler(uri => FakeHandler.Json(Fixtures.Item(DateFromRoute(uri))));
        var result = await Create(handler).FetchRandAsync(12);

        Assert.AreEqual(12, result!.Length);
        Assert.AreEqual(12, result!.Select(i => i.Date).Distinct().Count());
        Assert.IsTrue(result!.All(i => i.Date < APODDate.Today()));
    }

    [TestMethod]
    public async Task RandomRedrawsOnNotFoundAndStopsAtCap()
    {
        var handler = new FakeHandler(_ => FakeHandler.Json(Fixtures.Read("not-found"), HttpStatusCode.NotFound));
        var result = await Create(handler).FetchRandAsync(4);

        Assert.AreEqual(0, result!.Length);
        Assert.AreEqual(12, handler.Requests.Count);
    }

    [TestMethod]
    public async Task RandomStopsOnFirstFailure()
    {
        var keyHandler = new RecordingKeyHandler();
        var handler = new FakeHandler(_ => FakeHandler.Json("", HttpStatusCode.Unauthorized));
        var result = await Create(handler, keyHandler).FetchRandAsync(12);

        Assert.IsNull(result);
        // One batch, then stop
        Assert.AreEqual(12, handler.Requests.Count);
        Assert.AreEqual(1, keyHandler.Errors.Count);
    }

    private static DateOnly DateFromRoute(Uri uri) =>
        DateOnly.ParseExact(uri.AbsolutePath[^6..], "yyMMdd", System.Globalization.CultureInfo.InvariantCulture);
}
