using APODWallpaper.Utils;
using Newtonsoft.Json;

namespace APODTesting;

[TestClass]
public sealed class APODInfoTests
{
    private static APODInfo MakeInfo(string mediaType = "image", string? imageUrl = "https://example.com/img.jpg") =>
        new(new DateOnly(2026, 10, 4), "Test", "test", null, mediaType, imageUrl == null ? null : new Uri(imageUrl), new Uri("https://example.com/post/"));

    [TestMethod]
    public void IsImageWithImageUrl()
    {
        Assert.IsTrue(MakeInfo().IsImage);
    }

    [TestMethod]
    public void IsImageFalseWithoutImageUrl()
    {
        Assert.IsFalse(MakeInfo(imageUrl: null).IsImage);
    }

    [TestMethod]
    public void IsImageFalseForVideoWithStill()
    {
        Assert.IsFalse(MakeInfo(mediaType: "video").IsImage);
    }

    [TestMethod]
    public void CacheFormatRoundTrips()
    {
        var info = MakeInfo();
        info.RetrievalDate = new DateOnly(2026, 10, 28);
        info.Credit = "Someone";
        info.AltText = "A house under a rainbow";
        var copy = JsonConvert.DeserializeObject<APODInfo>(JsonConvert.SerializeObject(info))!;
        Assert.AreEqual(info.Date, copy.Date);
        Assert.AreEqual(info.Title, copy.Title);
        Assert.AreEqual(info.Credit, copy.Credit);
        Assert.AreEqual(info.AltText, copy.AltText);
        Assert.AreEqual(info.MediaType, copy.MediaType);
        Assert.AreEqual(info.ImageUrl, copy.ImageUrl);
        Assert.AreEqual(info.Permalink, copy.Permalink);
        Assert.AreEqual(info.RetrievalDate, copy.RetrievalDate);
    }
}
