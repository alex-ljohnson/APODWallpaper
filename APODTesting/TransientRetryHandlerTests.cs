using APODWallpaper.Utils;
using System.Net;

namespace APODTesting;

[TestClass]
public sealed class TransientRetryHandlerTests
{
    // Returns queued status codes in order, repeating the last
    private sealed class StubHandler(params HttpStatusCode[] codes) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var code = codes[Math.Min(Calls, codes.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(code));
        }
    }

    private static async Task<(HttpStatusCode, int)> SendAsync(params HttpStatusCode[] codes)
    {
        var stub = new StubHandler(codes);
        using var client = new HttpClient(new TransientRetryHandler { InnerHandler = stub });
        using var response = await client.GetAsync("https://example.com/");
        return (response.StatusCode, stub.Calls);
    }

    [TestMethod]
    public async Task RetriesServerErrorUntilSuccess()
    {
        var (status, calls) = await SendAsync(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.OK, status);
        Assert.AreEqual(3, calls);
    }

    [TestMethod]
    public async Task GivesUpAfterMaxAttempts()
    {
        var (status, calls) = await SendAsync(HttpStatusCode.InternalServerError);
        Assert.AreEqual(HttpStatusCode.InternalServerError, status);
        Assert.AreEqual(TransientRetryHandler.MaxAttempts, calls);
    }

    [TestMethod]
    public async Task RetriesTimeout()
    {
        var (status, calls) = await SendAsync(HttpStatusCode.RequestTimeout, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.OK, status);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task DoesNotRetryClientError()
    {
        var (status, calls) = await SendAsync(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, status);
        Assert.AreEqual(1, calls);
    }
}
