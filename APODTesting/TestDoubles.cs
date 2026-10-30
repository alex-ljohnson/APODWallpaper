using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;

namespace APODTesting;

internal sealed class StubConfig : IConfigurationService
{
    public Task InitialiseAsync() => Task.CompletedTask;
    public bool RunStartup { get; set; }
    public bool DownloadInfo { get; set; }
    public bool ExplainImage { get; set; }
    public string ConfiguratorTheme { get; set; } = "";
    public long NetworkTimeout { get; set; } = 1;
    public long PreviewQuality { get; set; }
    public long WallpaperStyle { get; set; }
    public string API_KEY { get; set; } = "";
    public string EffectiveApiKey => string.IsNullOrWhiteSpace(API_KEY) ? ApiKey.DemoKey : API_KEY;
    public bool UsingDemoKey => EffectiveApiKey == ApiKey.DemoKey;
    public ObservableCollection<string> AvailableThemes { get; } = [];
    public void LoadThemes(IEnumerable<string> themes) { }
    public void ChangeStartup() { }
}

internal sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class FakeHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
{
    public ConcurrentQueue<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request.RequestUri!);
        return Task.FromResult(respond(request.RequestUri!));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}

internal sealed class RecordingKeyHandler : IApiKeyErrorHandler
{
    public ConcurrentQueue<ApiKeyError> Errors { get; } = new();
    public void Handle(ApiKeyError error) => Errors.Enqueue(error);
}

internal static class Fixtures
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json"));

    // Minimal list item in the apod-basic shape
    public static string Item(DateOnly date, string mediaType = "image") =>
        $$"""{"date":"{{APODDate.ToIsoString(date)}}","title":"T {{APODDate.ToIsoString(date)}}","permalink":"https://example.test/{{APODDate.ToCode(date)}}","media_type":"{{mediaType}}","explanation":"E","credit":"","copyright":"","alt":"","url":"https://example.test/{{APODDate.ToCode(date)}}","hdurl":"https://example.test/{{APODDate.ToCode(date)}}.jpg"}""";

    public static string List(IEnumerable<DateOnly> dates) => "[" + string.Join(",", dates.Select(d => Item(d))) + "]";
}
