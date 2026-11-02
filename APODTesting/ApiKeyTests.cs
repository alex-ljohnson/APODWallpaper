using APODWallpaper.Utils;
using System.Net;
using System.Web;

namespace APODTesting;

[TestClass]
public sealed class ApiKeyTests
{
    private static string GatewayError(string code) => $$$"""{"error":{"code":"{{{code}}}","message":"..."}}""";
    private const string WordPressError = """{"code":"rest_forbidden","message":"Sorry","data":{"status":403}}""";

    [TestMethod]
    public void UnauthorizedIsInvalidKey()
    {
        var error = ApiKey.ParseError(HttpStatusCode.Unauthorized, "", usingDemoKey: false);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, error?.Kind);
        Assert.IsTrue(error!.SetupHelps);
    }

    [TestMethod]
    public void ParsesGatewayKeyCodes()
    {
        Assert.AreEqual(ApiKeyErrorKind.Invalid, ApiKey.ParseError(HttpStatusCode.Forbidden, GatewayError("API_KEY_INVALID"), false)?.Kind);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, ApiKey.ParseError(HttpStatusCode.Forbidden, GatewayError("API_KEY_DISABLED"), false)?.Kind);
        Assert.AreEqual(ApiKeyErrorKind.Missing, ApiKey.ParseError(HttpStatusCode.Forbidden, GatewayError("API_KEY_MISSING"), false)?.Kind);
    }

    [TestMethod]
    public void WordPressForbiddenIsInvalidKey()
    {
        Assert.AreEqual(ApiKeyErrorKind.Invalid, ApiKey.ParseError(HttpStatusCode.Forbidden, WordPressError, false)?.Kind);
    }

    [TestMethod]
    public void ParsesRateLimitFromStatusAlone()
    {
        var error = ApiKey.ParseError(HttpStatusCode.TooManyRequests, "not json", usingDemoKey: true, limit: 10);
        Assert.AreEqual(ApiKeyErrorKind.RateLimited, error?.Kind);
        StringAssert.Contains(error!.Message, "demo key");
        StringAssert.Contains(error.Message, "10 an hour");
    }

    [TestMethod]
    public void PersonalRateLimitDoesNotOfferSetup()
    {
        var error = ApiKey.ParseError(HttpStatusCode.Forbidden, GatewayError("OVER_RATE_LIMIT"), usingDemoKey: false);
        Assert.AreEqual(ApiKeyErrorKind.RateLimited, error?.Kind);
        Assert.IsFalse(error!.SetupHelps);
    }

    [TestMethod]
    public void DemoRateLimitOffersSetup()
    {
        var error = ApiKey.ParseError(HttpStatusCode.TooManyRequests, null, usingDemoKey: true);
        Assert.IsTrue(error!.SetupHelps);
    }

    [TestMethod]
    public void IgnoresNonKeyFailures()
    {
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.InternalServerError, GatewayError("API_KEY_INVALID"), false));
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.NotFound, """{"code":"apod_basic_not_found"}""", false));
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.OK, "{}", false));
        // HTML block page, or JSON with no error code
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.Forbidden, "<html></html>", false));
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.Forbidden, "{}", false));
    }

    private static ApodApiClient Client(HttpMessageHandler handler) =>
        new(new StubFactory(handler), new StubConfig(), "https://example.test/apod-basic");

    private static string KeyOf(Uri uri) => HttpUtility.ParseQueryString(uri.Query)["api_key"]!;

    private static FakeHandler ByKey(Func<HttpResponseMessage> user, Func<HttpResponseMessage> madeUp) =>
        new(uri => KeyOf(uri) == ApiKey.MadeUpKey ? madeUp() : user());

    private static HttpResponseMessage Ok(int? limit = null)
    {
        var response = FakeHandler.Json("{}");
        if (limit is int l)
        {
            response.Headers.Add("X-RateLimit-Limit", l.ToString());
            response.Headers.Add("X-RateLimit-Remaining", (l - 1).ToString());
        }
        return response;
    }

    // Never answers until cancelled
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    [TestMethod]
    public async Task CheckUsesSingleRouteForYesterday()
    {
        var handler = ByKey(() => Ok(), () => Ok());
        await ApiKey.CheckAsync(Client(handler), "abc123");
        handler.Requests.TryPeek(out var uri);
        Assert.AreEqual("/apod-basic/" + APODDate.ToCode(APODDate.Today().AddDays(-1)), uri!.AbsolutePath);
        Assert.AreEqual("abc123", KeyOf(uri));
    }

    [TestMethod]
    public async Task CheckReportsEnforcedKeys()
    {
        var handler = ByKey(() => Ok(limit: 1000), () => FakeHandler.Json(GatewayError("API_KEY_INVALID"), HttpStatusCode.Forbidden));
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
        Assert.AreEqual(true, check.Enforced);
        Assert.AreEqual(1000, check.Limit);
        Assert.AreEqual(999, check.Remaining);
    }

    [TestMethod]
    public async Task CheckReportsKeysNotEnforced()
    {
        var handler = ByKey(() => Ok(), () => Ok());
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
        Assert.AreEqual(false, check.Enforced);
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CheckRejectsInvalidKeyWithoutProbing()
    {
        var handler = ByKey(() => FakeHandler.Json(GatewayError("API_KEY_INVALID"), HttpStatusCode.Forbidden), () => Ok());
        var check = await ApiKey.CheckAsync(Client(handler), "nope");
        Assert.IsTrue(check.Reachable);
        Assert.IsFalse(check.Accepted);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, check.Error?.Kind);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CheckAcceptsRateLimitedKey()
    {
        var handler = ByKey(() => FakeHandler.Json("", HttpStatusCode.TooManyRequests), () => FakeHandler.Json(GatewayError("API_KEY_INVALID"), HttpStatusCode.Forbidden));
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
    }

    [TestMethod]
    public async Task CheckAcceptsKeyWhenDateIsMissing()
    {
        // A 404 got past any key check
        var handler = ByKey(() => FakeHandler.Json(Fixtures.Read("not-found"), HttpStatusCode.NotFound), () => Ok());
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
    }

    [TestMethod]
    public async Task CheckGivesNoVerdictWhenServerFails()
    {
        var check = await ApiKey.CheckAsync(Client(new FakeHandler(_ => FakeHandler.Json("", HttpStatusCode.BadGateway))), "abc123");
        Assert.IsFalse(check.Reachable);
        Assert.IsFalse(check.Accepted);
    }

    [TestMethod]
    public async Task CheckLeavesEnforcedUnknownWhenProbeFails()
    {
        var handler = ByKey(() => Ok(), () => FakeHandler.Json("", HttpStatusCode.BadGateway));
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
        Assert.IsNull(check.Enforced);
    }

    [TestMethod]
    public async Task CheckLeavesEnforcedUnknownWhenProbeIsRateLimited()
    {
        var handler = ByKey(() => Ok(), () => FakeHandler.Json("", HttpStatusCode.TooManyRequests));
        var check = await ApiKey.CheckAsync(Client(handler), "abc123");
        Assert.IsTrue(check.Accepted);
        Assert.IsNull(check.Enforced);
    }

    [TestMethod]
    public async Task CheckStopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var check = await ApiKey.CheckAsync(Client(new HangingHandler()), "abc123", cts.Token);
        Assert.IsFalse(check.Reachable);
    }

    [TestMethod]
    public async Task CheckReportsUnreachable()
    {
        var check = await ApiKey.CheckAsync(Client(new FakeHandler(_ => throw new HttpRequestException("offline"))), "abc123");
        Assert.IsFalse(check.Reachable);
        Assert.IsFalse(check.Accepted);
    }
}

[TestClass]
public sealed class ConfigurationApiKeyTests
{
    // Writes a throwaway config under %AppData%\APODWallpaper and loads it
    private static async Task<(Configuration, string)> LoadAsync(string json)
    {
        var id = $"Test_{Guid.NewGuid():N}";
        var path = Utilities.GetDataPath($"{id.ToLower()}.json");
        Utilities.EnsureDataDirectory();
        await File.WriteAllTextAsync(path, json);
        var config = new Configuration(id, autoSave: false);
        await config.InitialiseAsync();
        return (config, path);
    }

    private static void Cleanup(Configuration config, string path)
    {
        try { config.Dispose(); }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task NoKeyFallsBackToDemoKey()
    {
        var (config, path) = await LoadAsync("{}");
        try
        {
            Assert.AreEqual("", config.API_KEY);
            Assert.AreEqual(ApiKey.DemoKey, config.EffectiveApiKey);
            Assert.IsTrue(config.UsingDemoKey);
        }
        finally { Cleanup(config, path); }
    }

    [TestMethod]
    public async Task LegacySharedKeyIsCleared()
    {
        var (config, path) = await LoadAsync($$"""{"API_KEY":"{{ApiKey.LegacySharedKey}}"}""");
        try
        {
            Assert.AreEqual("", config.API_KEY);
            Assert.IsTrue(config.UsingDemoKey);
        }
        finally { Cleanup(config, path); }
    }

    [TestMethod]
    public async Task PersonalKeyIsTrimmedAndUsed()
    {
        var (config, path) = await LoadAsync("""{"API_KEY":"  abc123  "}""");
        try
        {
            Assert.AreEqual("abc123", config.EffectiveApiKey);
            Assert.IsFalse(config.UsingDemoKey);
        }
        finally { Cleanup(config, path); }
    }

    [TestMethod]
    public async Task LegacyApiSettingsAreRemoved()
    {
        var (config, path) = await LoadAsync("""{"BaseUrl":"https://api.nasa.gov/planetary/apod","UseHD":true,"KeySetupDismissed":true,"ExplainImage":true}""");
        try
        {
            var settings = config.ToDictionary();
            Assert.IsFalse(settings.ContainsKey("BaseUrl"));
            Assert.IsFalse(settings.ContainsKey("UseHD"));
            Assert.IsFalse(settings.ContainsKey("KeySetupDismissed"));
            Assert.IsTrue(config.ExplainImage);
        }
        finally { Cleanup(config, path); }
    }

    [TestMethod]
    public void ResetToDefaultsKeepsKey()
    {
        Assert.IsFalse(Configuration.DefaultConfiguration.ToDictionary().ContainsKey(nameof(Configuration.API_KEY)));
    }

    // Both used to repoint the real startup entry at whichever exe built them
    [TestMethod]
    public async Task UnsavedConfigsDontOwnStartupEntry()
    {
        Assert.IsFalse(Configuration.DefaultConfiguration.OwnsStartup);
        var (config, path) = await LoadAsync("{}");
        try
        {
            Assert.IsFalse(config.OwnsStartup);
        }
        finally { Cleanup(config, path); }
    }
}
