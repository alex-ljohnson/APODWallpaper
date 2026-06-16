using APODWallpaper.Utils;

namespace APODTesting;

[TestClass]
public sealed class APODInfoTests
{
    private static APODInfo MakeInfo() =>
        new(null, DateOnly.FromDateTime(DateTime.UtcNow), "test", null, "image", "v1", "Test", "https://example.com/img.jpg");

    [TestMethod]
    public void IsValidWithUrlReturnsTrue()
    {
        var info = MakeInfo();
        Assert.IsTrue(info.IsValid);
    }

    [TestMethod]
    public void IsValidWithNullUrlsReturnsFalse()
    {
        var info = MakeInfo();
        info.Url = null;
        Assert.IsFalse(info.IsValid);
    }

    [TestMethod]
    public void IsValidWithHDUrlOnlyReturnsTrue()
    {
        var info = new APODInfo(null, DateOnly.FromDateTime(DateTime.UtcNow), "test", "https://example.com/hd.jpg", "image", "v1", "Test", "https://example.com/img.jpg")
        {
            Url = null
        };
        Assert.IsTrue(info.IsValid);
    }
}
