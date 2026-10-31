using APODWallpaper.Utils;

namespace APODTesting;

[TestClass]
public sealed class ApodImageUrlTests
{
    // Live hdurls, 2026-10-27 and 2021 reposted on 2026-10-25
    private static readonly Uri Proxy = new("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/NocClouds_Hofer_6048.jpg?w=6048&h=4030&fit=clip&crop=faces%2Cfocalpoint");
    private static readonly Uri Original = new("https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2021/october/DarkMatter_KipacAmnh_1200.jpg?w=1200&h=900&fit=clip&crop=faces%2Cfocalpoint");
    private static readonly Uri Foreign = new("https://example.test/image/a.jpg?w=100&h=100");

    [TestMethod]
    public void OriginalRewritesTheProxy()
    {
        Assert.AreEqual(new Uri("https://assets.science.nasa.gov/content/dam/science/cds/apod/apod/2026/october/NocClouds_Hofer_6048.jpg"), ApodImageUrl.Original(Proxy));
    }

    [TestMethod]
    public void OriginalIsNullForContentDamWithAQuery()
    {
        // Same file, the query is ignored there
        Assert.IsNull(ApodImageUrl.Original(Original));
    }

    [TestMethod]
    public void OriginalIsNullWhenAlreadyTheOriginal()
    {
        Assert.IsNull(ApodImageUrl.Original(ApodImageUrl.Original(Proxy)!));
    }

    [TestMethod]
    public void ForeignUrlsAreLeftAlone()
    {
        Assert.IsNull(ApodImageUrl.Original(Foreign));
        Assert.IsNull(ApodImageUrl.Resized(Foreign, 100));
        Assert.IsNull(ApodImageUrl.ExpectedSize(Foreign));
        Assert.IsNull(ApodImageUrl.Original(new Uri("https://assets.science.nasa.gov/other/a.jpg")));
    }

    [TestMethod]
    public void ResizedGoesThroughTheProxy()
    {
        Assert.AreEqual(new Uri("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2021/october/DarkMatter_KipacAmnh_1200.jpg?w=200"), ApodImageUrl.Resized(Original, 200));
    }

    [TestMethod]
    public void ResizedNeverUpscales()
    {
        StringAssert.EndsWith(ApodImageUrl.Resized(Original, 5000)!.Query, "?w=1200");
        Assert.IsNull(ApodImageUrl.Resized(Original, 0));
    }

    [TestMethod]
    public void EscapedPathsSurvive()
    {
        var spaced = new Uri("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/a%20b.jpg?w=10&h=10");
        StringAssert.Contains(ApodImageUrl.Original(spaced)!.AbsoluteUri, "/content/dam/science/cds/apod/apod/2026/october/a%20b.jpg");
    }

    [TestMethod]
    public void ExpectedSizeFromTheQuery()
    {
        Assert.AreEqual(new ImageSize(6048, 4030), ApodImageUrl.ExpectedSize(Proxy));
    }

    [TestMethod]
    [DataRow("?w=1600")]
    [DataRow("?w=abc&h=10")]
    [DataRow("?w=0&h=10")]
    [DataRow("?w=-5&h=10")]
    [DataRow("")]
    public void ExpectedSizeNeedsBothDimensions(string query)
    {
        Assert.IsNull(ApodImageUrl.ExpectedSize(new Uri("https://assets.science.nasa.gov/dynamicimage/assets/science/cds/apod/apod/2026/october/occultation.jpeg" + query)));
    }
}
