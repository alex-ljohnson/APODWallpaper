using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;

namespace APODTesting;

internal sealed class StubConfig : IConfigurationService
{
    public Task InitialiseAsync() => Task.CompletedTask;
    public bool RunStartup { get; set; }
    public bool DownloadInfo { get; set; }
    public bool ExplainImage { get; set; }
    public bool UseVideoStills { get; set; } = true;
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

internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        var b = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(8), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(20), height);
        return b;
    }

    public static byte[] Gif(int width, int height)
    {
        var b = new byte[13];
        "GIF89a"u8.CopyTo(b);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8), (ushort)height);
        return b;
    }

    public static byte[] Bmp(int width, int height, bool core = false)
    {
        var b = new byte[54];
        "BM"u8.CopyTo(b);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(14), core ? 12u : 40u);
        if (core)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(18), (ushort)width);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(20), (ushort)height);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18), width);
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22), height);
        }
        return b;
    }

    // SOI, an EXIF APP1 segment, then SOF0
    public static byte[] Jpeg(int width, int height, bool sosFirst = false)
    {
        using var s = new MemoryStream();
        s.Write([0xFF, 0xD8]);
        s.Write([0xFF, 0xE1, 0x00, 0x08]);
        s.Write("Exif\0\0"u8);
        if (sosFirst) s.Write([0xFF, 0xDA, 0x00, 0x02]);
        s.Write([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(size, (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(size[2..], (ushort)width);
        s.Write(size);
        s.Write(new byte[10]);
        s.Write([0xFF, 0xD9]);
        return s.ToArray();
    }

    public static byte[] Tiff(int width, int height, bool little)
    {
        var b = new byte[8 + 2 + 2 * 12 + 4];
        void U16(int at, int v) { if (little) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), (ushort)v); else BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(at), (ushort)v); }
        void U32(int at, int v) { if (little) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), (uint)v); else BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(at), (uint)v); }
        (little ? "II"u8 : "MM"u8).CopyTo(b);
        U16(2, 42);
        U32(4, 8);
        U16(8, 2);
        U16(10, 256); U16(12, 3); U32(14, 1); U16(18, width);
        U16(22, 257); U16(24, 4); U32(26, 1); U32(30, height);
        return b;
    }

    public static readonly byte[] Html = Encoding.ASCII.GetBytes("<!DOCTYPE html><html><body>Not found</body></html>");
}
