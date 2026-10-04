using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using System.Collections.ObjectModel;
using System.Net;

namespace APODTesting;

[TestClass]
public sealed class ConfigurableTimeoutHandlerTests
{
    // Only NetworkTimeout matters to the handler
    private sealed class StubConfig : IConfigurationService
    {
        public Task InitialiseAsync() => Task.CompletedTask;
        public bool UseHD { get; set; }
        public bool RunStartup { get; set; }
        public bool DownloadInfo { get; set; }
        public bool ExplainImage { get; set; }
        public string BaseUrl { get; set; } = "";
        public string ConfiguratorTheme { get; set; } = "";
        public long NetworkTimeout { get; set; } = 1;
        public long PreviewQuality { get; set; }
        public long WallpaperStyle { get; set; }
        public string API_KEY { get; set; } = "";
        public bool KeySetupDismissed { get; set; }
        public string EffectiveApiKey => ApiKey.DemoKey;
        public bool UsingDemoKey => true;
        public ObservableCollection<string> AvailableThemes { get; } = [];
        public void LoadThemes(IEnumerable<string> themes) { }
        public void ChangeStartup() { }
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

    private static HttpClient CreateClient() =>
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
