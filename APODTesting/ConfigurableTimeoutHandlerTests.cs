using APODWallpaper.Utils;
using System.Net;

namespace APODTesting;

[TestClass]
public sealed class ConfigurableTimeoutHandlerTests
{
    // Never answers until cancelled
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    private static HttpClient CreateClient() =>
        // StubConfig times out after 1s
        new(new ConfigurableTimeoutHandler(new StubConfig()) { InnerHandler = new HangingHandler() }) { Timeout = Timeout.InfiniteTimeSpan };

    [TestMethod]
    public async Task TimeoutBecomesRequestTimeout()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("https://example.com/");
        Assert.AreEqual(HttpStatusCode.RequestTimeout, response.StatusCode);
    }

    [TestMethod]
    public async Task CallerCancellationIsNotATimeout()
    {
        using var client = CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => client.GetAsync("https://example.com/", cts.Token));
    }
}
