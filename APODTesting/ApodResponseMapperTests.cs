using APODWallpaper.Utils;
using Newtonsoft.Json;

namespace APODTesting;

[TestClass]
public sealed class ApodResponseMapperTests
{
    private static readonly DateOnly Retrieved = new(2026, 10, 28);

    private static APODInfo MapFixture(string name) => MapJson(Fixtures.Read(name));

    private static APODInfo MapJson(string json) =>
        ApodResponseMapper.Map(JsonConvert.DeserializeObject<ApodResponse>(json)!, Retrieved)!;

    [TestMethod]
    public void MapsModernImage()
    {
        var info = MapFixture("image-2026-10-04");
        Assert.AreEqual(new DateOnly(2026, 10, 4), info.Date);
        Assert.AreEqual("Supernumerary Rainbows over New Jersey", info.Title);
        Assert.AreEqual("image", info.MediaType);
        Assert.IsTrue(info.IsImage);
        StringAssert.Contains(info.ImageUrl!.AbsolutePath, "SupernumeraryRainbows_Entwistle");
        Assert.AreEqual("https://science.nasa.gov/image-article/apod-2026-october-4-supernumerary-rainbows-over-new-jersey/", info.Permalink!.ToString());
        Assert.AreEqual("John Entwistle", info.Credit);
        Assert.AreEqual(Retrieved, info.RetrievalDate);
    }

    [TestMethod]
    public void ExplanationIsPlainTextWithoutLabelOrFooter()
    {
        var info = MapFixture("image-2026-10-04");
        StringAssert.StartsWith(info.Explanation, "Yes, but can your rainbow do this?");
        StringAssert.EndsWith(info.Explanation, "light's wave nature.");
        Assert.IsFalse(info.Explanation.Contains('<'));
        Assert.IsFalse(info.Explanation.Contains("Tomorrow's picture"));
        Assert.IsFalse(info.Explanation.Contains("Your Sky Surprise"));
    }

    [TestMethod]
    public void RecentVideoKeepsItsStill()
    {
        var info = MapFixture("video-still-2026-10-28");
        Assert.AreEqual("video", info.MediaType);
        Assert.IsNotNull(info.ImageUrl);
        StringAssert.Contains(info.ImageUrl.AbsolutePath, "occultation");
        Assert.IsFalse(info.IsImage);
        Assert.IsFalse(info.Explanation.Contains("Tomorrow's picture"));
    }

    [TestMethod]
    public void OldVideoPlaceholderIsNoImage()
    {
        var info = MapFixture("video-placeholder-2012-03-05");
        Assert.AreEqual("video", info.MediaType);
        Assert.IsNull(info.ImageUrl);
        Assert.IsFalse(info.IsImage);
    }

    [TestMethod]
    public void GifPlaceholderIsNotAnImage()
    {
        var info = MapFixture("gif-placeholder-1995-06-20");
        Assert.AreEqual("image", info.MediaType);
        Assert.IsNull(info.ImageUrl);
        Assert.IsFalse(info.IsImage);
    }

    [TestMethod]
    public void NullMediaTypeIsOther()
    {
        var info = MapFixture("null-media-1997-03-04");
        Assert.AreEqual("other", info.MediaType);
        Assert.IsNull(info.Credit);
        Assert.IsNull(info.AltText);
        Assert.AreEqual("", info.Explanation);
        // &#8211; decoded
        Assert.AreEqual("APOD: 1997 March 04 – Solar Wind And Milky Way", info.Title);
    }

    [TestMethod]
    public void EmptyHdurlIsNoImage()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","permalink":"https://example.test/","media_type":"image","explanation":"","hdurl":""}""");
        Assert.IsNull(info.ImageUrl);
        Assert.IsFalse(info.IsImage);
    }

    [TestMethod]
    public void MissingCreditIsNull()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","permalink":"https://example.test/","media_type":"image","explanation":"","hdurl":"https://example.test/a.jpg"}""");
        Assert.IsNull(info.Credit);
        Assert.IsTrue(info.IsImage);
    }

    [TestMethod]
    public void CreditLabelIsStripped()
    {
        var info = MapJson("""{"date":"2012-03-02","title":"T","permalink":"https://example.test/","media_type":"image","explanation":"","hdurl":"","credit":"<b> Drawing Credit &amp; <a href=\"x\">Copyright</a>:</b> <a href=\"y\">Frédéric Burgeot</a>"}""");
        Assert.AreEqual("Frédéric Burgeot", info.Credit);
    }

    [TestMethod]
    public void PostIdAsIntOrStringDoesNotBreak()
    {
        Assert.IsNotNull(MapJson("""{"date":"2026-01-01","post_id":123,"title":"T","media_type":"image","hdurl":""}"""));
        Assert.IsNotNull(MapJson("""{"date":"2026-01-01","post_id":"123","title":"T","media_type":"image","hdurl":""}"""));
    }

    [TestMethod]
    public void BadDateIsSkipped()
    {
        var response = JsonConvert.DeserializeObject<ApodResponse>("""{"date":"261004","title":"T"}""")!;
        Assert.IsNull(ApodResponseMapper.Map(response, Retrieved));
    }

    [TestMethod]
    public void HtmlToTextKeepsParagraphs()
    {
        var text = ApodResponseMapper.HtmlToText("<p>One\n  two&amp;three</p><p>Four<br>Five</p>");
        Assert.AreEqual("One two&three\n\nFour\nFive", text);
    }

    [TestMethod]
    public void HtmlToTextLeavesPlainTextAlone()
    {
        Assert.AreEqual("Plain text.", ApodResponseMapper.HtmlToText("Plain text."));
    }

    [TestMethod]
    public void CreditIsPreferredOverCopyright()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","media_type":"image","hdurl":"","credit":"Plain Name","copyright":"<a href=\"x\">Linked Name</a>"}""");
        Assert.AreEqual("Plain Name", info.Credit);
    }

    [TestMethod]
    public void CopyrightIsTheFallbackForAnEmptyCredit()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","media_type":"image","hdurl":"","credit":"","copyright":"Image Credit: Someone"}""");
        Assert.AreEqual("Someone", info.Credit);
    }

    [TestMethod]
    public void CreditDropsMarkupWhenItDiffersFromCopyright()
    {
        // Credit plain, copyright linked
        Assert.AreEqual("Michael Wong", MapFixture("video-still-2026-10-28").Credit);
    }

    [TestMethod]
    public void TitleLineBeforeCreditIsDropped()
    {
        var info = MapFixture("credit-title-line-1997-03-01");
        StringAssert.StartsWith(info.Credit, "G. S. Hughes");
        Assert.IsFalse(info.Credit!.Contains("Dwingeloo"));
    }

    [TestMethod]
    public void CreditLabelWithoutColonIsStripped()
    {
        StringAssert.StartsWith(MapFixture("credit-no-colon-2002-07-04").Credit, "T. H. Puzia");
    }

    [TestMethod]
    public void PluralCreditsLabelIsStripped()
    {
        StringAssert.StartsWith(MapFixture("video-credits-2020-06-16").Credit, "Idea & Lead: Alex Dantart");
    }

    [TestMethod]
    public void CopyrightMidCreditIsKept()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","media_type":"image","hdurl":"","credit":"NASA , ESA , Hubble ; Processing &amp; Copyright: Diego Gravinese"}""");
        Assert.AreEqual("NASA , ESA , Hubble ; Processing & Copyright: Diego Gravinese", info.Credit);
    }

    [TestMethod]
    public void SecondCopyrightLineIsKept()
    {
        var info = MapJson("""{"date":"1996-03-01","title":"T","media_type":"image","hdurl":"","credit":"<b>Credit:</b> David Malin<br>Copyright: Anglo-Australian Telescope Board"}""");
        Assert.AreEqual("David Malin\nCopyright: Anglo-Australian Telescope Board", info.Credit);
    }

    [TestMethod]
    public void DoubledExplanationLabelIsStripped()
    {
        StringAssert.StartsWith(MapFixture("credit-title-line-1997-03-01").Explanation, "Sometimes you can't see the forest for the trees.");
    }

    [TestMethod]
    public void SiteFooterIsCut()
    {
        var info = MapFixture("site-footer-2020-06-09");
        StringAssert.StartsWith(info.Explanation, "Do you recognize the constellation of Orion?");
        Assert.IsFalse(info.Explanation.Contains("Next picture"));
        Assert.IsFalse(info.Explanation.Contains("| Archive |"));
    }

    [TestMethod]
    public void NotesParagraphIsDropped()
    {
        var info = MapFixture("notes-2026-09-01");
        StringAssert.StartsWith(info.Explanation, "Did you need to be on the right side of this airplane");
        Assert.IsFalse(info.Explanation.Contains("\n\n"));
        Assert.IsFalse(info.Explanation.Contains("Gallery:"));
        Assert.IsFalse(info.Explanation.Contains("is moving"));
    }

    [TestMethod]
    public void ContentParagraphIsKept()
    {
        var info = MapFixture("alt-entities-2026-10-21");
        StringAssert.StartsWith(info.Explanation, "Jinkies, look at all those ghosts!");
        StringAssert.EndsWith(info.Explanation, "Read here for more information on how you can help the observing team behind today's APOD.");
    }

    [TestMethod]
    public void QuizNoteIsKept()
    {
        StringAssert.EndsWith(MapFixture("quiz-2021-09-13").Explanation, "Quiz: What is pictured in the double-reflection below the main mirror?");
    }

    [TestMethod]
    public void NotesLineInsideTheOnlyParagraphIsKept()
    {
        var info = MapJson("""{"date":"2026-01-01","title":"T","media_type":"image","hdurl":"","explanation":"<p>Note: this is the whole explanation.</p>"}""");
        Assert.AreEqual("Note: this is the whole explanation.", info.Explanation);
    }

    [TestMethod]
    public void AltTextIsMapped()
    {
        StringAssert.StartsWith(MapFixture("image-2026-10-04").AltText, "The top of a house is shown beneath a dark and cloudy sky.");
    }

    [TestMethod]
    public void AltTextEntitiesAreDecoded()
    {
        StringAssert.Contains(MapFixture("alt-entities-2026-10-21").AltText, "across the image's equator");
    }

    [TestMethod]
    public void StockAltTextIsNull()
    {
        Assert.IsNull(MapFixture("gif-placeholder-1995-06-20").AltText);
    }

    [TestMethod]
    public void IframeIsNotAnImage()
    {
        var info = MapFixture("iframe-2015-12-22");
        Assert.AreEqual("iframe", info.MediaType);
        Assert.IsNull(info.ImageUrl);
        Assert.IsFalse(info.IsImage);
        Assert.IsNull(info.AltText);
        Assert.IsFalse(info.Credit!.StartsWith("Video Credit"));
    }
}
