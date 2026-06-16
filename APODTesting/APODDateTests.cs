using APODWallpaper.Utils;

namespace APODTesting;

[TestClass]
public sealed class APODDateTests
{
    [TestMethod]
    public void TodayReturnsEasternTimeDate()
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, eastern));
        var result = APODDate.Today();

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ToIsoStringReturnsInvariantFormat()
    {
        var date = new DateOnly(2024, 3, 5);
        var result = APODDate.ToIsoString(date);

        Assert.AreEqual("2024-03-05", result);
    }

    [TestMethod]
    public void ParseIsoParsesInvariantFormat()
    {
        var result = APODDate.ParseIso("2024-03-05");

        Assert.AreEqual(new DateOnly(2024, 3, 5), result);
    }

    [TestMethod]
    public void ParseIsoRejectsCultureSpecificFormat()
    {
        Assert.ThrowsException<FormatException>(() => APODDate.ParseIso("05/03/2024"));
    }

    [TestMethod]
    public void InceptionDateIsJune161995()
    {
        Assert.AreEqual(new DateOnly(1995, 6, 16), APODDate.InceptionDate);
    }
}
