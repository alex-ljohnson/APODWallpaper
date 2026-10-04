using APODWallpaper.Utils;
using System.Net;

namespace APODTesting;

[TestClass]
public sealed class ApiKeyTests
{
    private static string ErrorBody(string code) => $$$"""{"error":{"code":"{{{code}}}","message":"..."}}""";

    [TestMethod]
    public void ParsesInvalidKey()
    {
        var error = ApiKey.ParseError(HttpStatusCode.Forbidden, ErrorBody("API_KEY_INVALID"), usingDemoKey: false);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, error?.Kind);
        Assert.IsTrue(error!.SetupHelps);
    }

    [TestMethod]
    public void ParsesOtherKeyCodesAsInvalid()
    {
        var error = ApiKey.ParseError(HttpStatusCode.Forbidden, ErrorBody("API_KEY_DISABLED"), usingDemoKey: false);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, error?.Kind);
    }

    [TestMethod]
    public void ParsesMissingKey()
    {
        var error = ApiKey.ParseError(HttpStatusCode.Forbidden, ErrorBody("API_KEY_MISSING"), usingDemoKey: false);
        Assert.AreEqual(ApiKeyErrorKind.Missing, error?.Kind);
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
        var error = ApiKey.ParseError(HttpStatusCode.Forbidden, ErrorBody("OVER_RATE_LIMIT"), usingDemoKey: false);
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
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.InternalServerError, ErrorBody("API_KEY_INVALID"), false));
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.OK, "{}", false));
        // Some other 403, e.g. an HTML error page
        Assert.IsNull(ApiKey.ParseError(HttpStatusCode.Forbidden, "<html></html>", false));
    }

    private sealed class StubHandler(HttpStatusCode status, string body, int? limit = null) : HttpMessageHandler
    {
        public string? Query { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Query = request.RequestUri?.Query;
            var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
            if (limit is int l)
            {
                response.Headers.Add("X-RateLimit-Limit", l.ToString());
                response.Headers.Add("X-RateLimit-Remaining", (l - 1).ToString());
            }
            return Task.FromResult(response);
        }
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

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    [TestMethod]
    public async Task CheckAcceptsWorkingKey()
    {
        var stub = new StubHandler(HttpStatusCode.OK, "{}", limit: 1000);
        var check = await ApiKey.CheckAsync(new HttpClient(stub), "https://example.com/apod", "abc123");
        Assert.IsTrue(check.Accepted);
        Assert.AreEqual(1000, check.Limit);
        Assert.AreEqual(999, check.Remaining);
        StringAssert.Contains(stub.Query, "api_key=abc123");
    }

    [TestMethod]
    public async Task CheckAcceptsKeyWhenUpstreamFails()
    {
        // NASA's key gateway let the key through, APOD itself 500'd
        var check = await ApiKey.CheckAsync(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "", limit: 1000)), "https://example.com/apod", "abc123");
        Assert.IsTrue(check.Accepted);
    }

    [TestMethod]
    public async Task CheckGivesNoVerdictWhenGatewayFails()
    {
        // No rate limit headers, the key was never looked at
        var check = await ApiKey.CheckAsync(new HttpClient(new StubHandler(HttpStatusCode.BadGateway, "")), "https://example.com/apod", "abc123");
        Assert.IsFalse(check.Reachable);
        Assert.IsFalse(check.Accepted);
    }

    [TestMethod]
    public async Task CheckRejectsInvalidKey()
    {
        var check = await ApiKey.CheckAsync(new HttpClient(new StubHandler(HttpStatusCode.Forbidden, ErrorBody("API_KEY_INVALID"))), "https://example.com/apod", "nope");
        Assert.IsTrue(check.Reachable);
        Assert.IsFalse(check.Accepted);
        Assert.AreEqual(ApiKeyErrorKind.Invalid, check.Error?.Kind);
    }

    [TestMethod]
    public async Task CheckAcceptsRateLimitedKey()
    {
        var check = await ApiKey.CheckAsync(new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, ErrorBody("OVER_RATE_LIMIT"))), "https://example.com/apod", "abc123");
        Assert.IsTrue(check.Accepted);
    }

    [TestMethod]
    public async Task CheckStopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var check = await ApiKey.CheckAsync(new HttpClient(new HangingHandler()), "https://example.com/apod", "abc123", cts.Token);
        Assert.IsFalse(check.Reachable);
    }

    [TestMethod]
    public async Task CheckReportsUnreachable()
    {
        var check = await ApiKey.CheckAsync(new HttpClient(new ThrowingHandler()), "https://example.com/apod", "abc123");
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
    public void ResetToDefaultsKeepsKey()
    {
        Assert.IsFalse(Configuration.DefaultConfiguration.ToDictionary().ContainsKey(nameof(Configuration.API_KEY)));
    }
}
