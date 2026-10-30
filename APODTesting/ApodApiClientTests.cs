using APODWallpaper.Utils;
using System.Net;
using System.Web;

namespace APODTesting;

[TestClass]
public sealed class ApodApiClientTests
{
    private const string Endpoint = "https://example.test/apod-basic";

    private static (ApodApiClient, FakeHandler) Create(Func<Uri, HttpResponseMessage> respond, string apiKey = "")
    {
        var handler = new FakeHandler(respond);
        return (new ApodApiClient(new StubFactory(handler), new StubConfig { API_KEY = apiKey }, Endpoint), handler);
    }

    private static int PageOf(Uri uri) => int.Parse(HttpUtility.ParseQueryString(uri.Query)["page"]!);

    private static IEnumerable<DateOnly> Days(DateOnly start, int count) => Enumerable.Range(0, count).Select(start.AddDays);

    [TestMethod]
    public async Task SingleDateUsesCodeRouteAndKey()
    {
        var (client, handler) = Create(_ => FakeHandler.Json(Fixtures.Read("image-2026-10-04")), apiKey: "abc123");
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));

        Assert.AreEqual(ApodOutcome.Found, result.Outcome);
        Assert.AreEqual("Supernumerary Rainbows over New Jersey", result.Value!.Title);
        handler.Requests.TryPeek(out var uri);
        Assert.AreEqual("/apod-basic/261004", uri!.AbsolutePath);
        Assert.AreEqual("abc123", HttpUtility.ParseQueryString(uri.Query)["api_key"]);
    }

    [TestMethod]
    public async Task DemoKeySentWhenNoneSet()
    {
        var (client, handler) = Create(_ => FakeHandler.Json(Fixtures.Read("image-2026-10-04")));
        await client.GetAsync(new DateOnly(2026, 10, 4));
        handler.Requests.TryPeek(out var uri);
        Assert.AreEqual(ApiKey.DemoKey, HttpUtility.ParseQueryString(uri!.Query)["api_key"]);
    }

    [TestMethod]
    public async Task NotFoundIsNotAFailure()
    {
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.Read("not-found"), HttpStatusCode.NotFound));
        var result = await client.GetAsync(new DateOnly(1995, 6, 17));
        Assert.AreEqual(ApodOutcome.NotFound, result.Outcome);
        Assert.IsNull(result.KeyError);
    }

    [TestMethod]
    public async Task OtherDateOnSingleRouteIsNotFound()
    {
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.Read("image-2026-10-04")));
        var result = await client.GetAsync(new DateOnly(2026, 10, 5));
        Assert.AreEqual(ApodOutcome.NotFound, result.Outcome);
    }

    [TestMethod]
    public async Task ServerErrorFails()
    {
        var (client, _) = Create(_ => FakeHandler.Json("", HttpStatusCode.InternalServerError));
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
        Assert.IsNull(result.KeyError);
    }

    [TestMethod]
    public async Task NetworkErrorFails()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("offline"));
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task StalledBodyFails()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) });
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
    }

    private sealed class StallingStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    [TestMethod]
    public async Task UnreadableBodyFails()
    {
        var (client, _) = Create(_ => FakeHandler.Json("<html>not json</html>"));
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized, "")]
    [DataRow(HttpStatusCode.TooManyRequests, "")]
    [DataRow(HttpStatusCode.Forbidden, """{"error":{"code":"API_KEY_INVALID"}}""")]
    [DataRow(HttpStatusCode.Forbidden, """{"code":"rest_forbidden","message":"Sorry","data":{"status":403}}""")]
    public async Task KeyErrorsAreReported(HttpStatusCode status, string body)
    {
        var (client, _) = Create(_ => FakeHandler.Json(body, status));
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
        Assert.IsNotNull(result.KeyError);
    }

    [TestMethod]
    public async Task HtmlForbiddenIsNotAKeyError()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<html>Blocked</html>") });
        var result = await client.GetAsync(new DateOnly(2026, 10, 4));
        Assert.AreEqual(ApodOutcome.Failed, result.Outcome);
        Assert.IsNull(result.KeyError);
    }

    [TestMethod]
    public async Task RangeUsesDocumentedFilters()
    {
        var start = new DateOnly(2026, 10, 20);
        var (client, handler) = Create(_ => FakeHandler.Json(Fixtures.List(Days(start, 3))));
        await client.GetRangeAsync(start, start.AddDays(2));

        handler.Requests.TryPeek(out var uri);
        var query = HttpUtility.ParseQueryString(uri!.Query);
        Assert.AreEqual("261020", query["date_from"]);
        Assert.AreEqual("261022", query["date_to"]);
        Assert.AreEqual("25", query["per_page"]);
        Assert.AreEqual("1", query["page"]);
    }

    [TestMethod]
    public async Task RangeIsSortedWhateverTheResponseOrder()
    {
        var start = new DateOnly(2026, 10, 20);
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.List(Days(start, 5).Reverse())));
        var result = await client.GetRangeAsync(start, start.AddDays(4));
        CollectionAssert.AreEqual(Days(start, 5).ToArray(), result.Value!.Select(i => i.Date).ToArray());
    }

    [TestMethod]
    public async Task RangePagesUntilAShortPage()
    {
        var start = new DateOnly(2026, 1, 1);
        var all = Days(start, 30).ToArray();
        var (client, handler) = Create(uri => FakeHandler.Json(Fixtures.List(all.Skip((PageOf(uri) - 1) * 25).Take(25))));
        var result = await client.GetRangeAsync(start, all[^1]);

        Assert.AreEqual(30, result.Value!.Length);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsTrue(result.Complete);
    }

    [TestMethod]
    public async Task RangeStopsWhenPagesRepeat()
    {
        var start = new DateOnly(2026, 1, 1);
        var (client, handler) = Create(_ => FakeHandler.Json(Fixtures.List(Days(start, 25))));
        var result = await client.GetRangeAsync(start, start.AddDays(99));

        Assert.AreEqual(25, result.Value!.Length);
        Assert.AreEqual(2, handler.Requests.Count);
        Assert.IsFalse(result.Complete);
    }

    [TestMethod]
    public async Task RangeStopsAtThePageCap()
    {
        var start = new DateOnly(2026, 1, 1);
        var (client, handler) = Create(uri => FakeHandler.Json(Fixtures.List(Days(start.AddDays(PageOf(uri) - 1), 25))));
        var result = await client.GetRangeAsync(start, start.AddDays(99));

        // ceil(100 / 25) + 1
        Assert.AreEqual(5, handler.Requests.Count);
        Assert.IsFalse(result.Complete);
    }

    [TestMethod]
    public async Task RealListPageMaps()
    {
        var start = new DateOnly(2026, 9, 1);
        var (client, handler) = Create(_ => FakeHandler.Json(Fixtures.Read("list-2026-09-01-to-25")));
        var result = await client.GetRangeAsync(start, start.AddDays(24));

        Assert.AreEqual(ApodOutcome.Found, result.Outcome);
        CollectionAssert.AreEqual(Days(start, 25).ToArray(), result.Value!.Select(i => i.Date).ToArray());
        Assert.IsTrue(result.Value!.All(i => i.Title.Length > 0 && i.Permalink != null && i.Explanation.Length > 0));
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task RangeDropsDatesOutsideTheRange()
    {
        var start = new DateOnly(2026, 1, 10);
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.List(Days(start.AddDays(-2), 6))));
        var result = await client.GetRangeAsync(start, start.AddDays(1));
        CollectionAssert.AreEqual(new[] { start, start.AddDays(1) }, result.Value!.Select(i => i.Date).ToArray());
    }

    [TestMethod]
    public async Task LatestIsNewestOfYesterdayAndToday()
    {
        var today = APODDate.Today();
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.List([today, today.AddDays(-1)])));
        var result = await client.GetLatestAsync();
        Assert.AreEqual(today, result.Value!.Date);
    }

    [TestMethod]
    public async Task LatestFallsBackToYesterdayBeforeRelease()
    {
        var yesterday = APODDate.Today().AddDays(-1);
        var (client, _) = Create(_ => FakeHandler.Json(Fixtures.List([yesterday])));
        var result = await client.GetLatestAsync();
        Assert.AreEqual(yesterday, result.Value!.Date);
    }

    [TestMethod]
    public void HttpClientSendsUserAgent()
    {
        using var client = new HttpClient();
        ApodApiClient.ConfigureHttpClient(client);
        Assert.AreEqual("APODWallpaper", client.DefaultRequestHeaders.UserAgent.Single().Product!.Name);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, client.Timeout);
    }
}
