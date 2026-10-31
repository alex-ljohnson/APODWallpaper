using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace APODWallpaper.Utils
{
    /// <summary>
    /// Documented apod-basic fields plus the undocumented credit, copyright and alt
    /// </summary>
    internal sealed class ApodResponse
    {
        [JsonProperty("date")] public string? Date { get; set; }
        [JsonProperty("title")] public string? Title { get; set; }
        [JsonProperty("permalink")] public string? Permalink { get; set; }
        [JsonProperty("media_type")] public string? MediaType { get; set; }
        [JsonProperty("explanation")] public string? Explanation { get; set; }
        [JsonProperty("hdurl")] public string? HdUrl { get; set; }
        // Copyright is the fallback for credit
        [JsonProperty("credit")] public string? Credit { get; set; }
        [JsonProperty("copyright")] public string? Copyright { get; set; }
        [JsonProperty("alt")] public string? Alt { get; set; }
    }

    /// <summary>
    /// The only code relying on undocumented behaviour, each rule falls back to the field as given
    /// </summary>
    internal static partial class ApodResponseMapper
    {
        // hdurl for videos and GIFs
        private const string PlaceholderImage = "/news-thumbnail.png";
        private const string AltBoilerplate = "See Explanation";
        // Bump on any mapping change, cached entries from older versions are dropped
        public const int Version = 2;

        /// <summary>
        /// Null without a usable date
        /// </summary>
        public static APODInfo? Map(ApodResponse response, DateOnly retrieved)
        {
            if (!DateOnly.TryParseExact(response.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                Trace.WriteLine($"Skipping APOD response with bad date '{response.Date}'");
                return null;
            }
            return new APODInfo(
                date,
                HtmlToText(response.Title),
                CleanExplanation(HtmlToText(response.Explanation)),
                CleanCredit(HtmlToText(response.Credit)) ?? CleanCredit(HtmlToText(response.Copyright)),
                MediaTypeOf(response.MediaType),
                ImageUrlOf(response.HdUrl),
                AbsoluteUri(response.Permalink),
                AltTextOf(response.Alt),
                retrieved);
        }

        // Most pre-2021 posts only have the stock "See Explanation..."
        private static string? AltTextOf(string? alt)
        {
            var text = HtmlToText(alt);
            return text.Length == 0 || text.StartsWith(AltBoilerplate, StringComparison.OrdinalIgnoreCase) ? null : text;
        }

        private static string MediaTypeOf(string? mediaType) =>
            string.IsNullOrWhiteSpace(mediaType) ? "other" : mediaType.Trim().ToLowerInvariant();

        private static Uri? ImageUrlOf(string? hdurl)
        {
            var uri = AbsoluteUri(hdurl);
            if (uri == null || uri.AbsolutePath.EndsWith(PlaceholderImage, StringComparison.OrdinalIgnoreCase)) return null;
            return uri;
        }

        private static Uri? AbsoluteUri(string? s) =>
            Uri.TryCreate(s?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;

        /// <summary>
        /// Keeps paragraph breaks, no-op on plain text
        /// </summary>
        public static string HtmlToText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            // Source newlines are whitespace in HTML
            var text = SourceNewline().Replace(html, " ");
            text = LineBreak().Replace(text, "\n");
            text = ParagraphEnd().Replace(text, "\n\n");
            text = Tag().Replace(text, "");
            text = WebUtility.HtmlDecode(text);
            var lines = text.Split('\n').Select(line => Spaces().Replace(line, " ").Trim());
            return ExtraBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
        }

        // Drops the label (doubled in 1997), nav footer and a closing "News: ..." style notes paragraph
        private static string CleanExplanation(string text)
        {
            text = ExplanationLabel().Replace(text, "");
            var footer = ExplanationFooter().Match(text);
            if (footer.Success) text = text[..footer.Index];
            text = text.Trim();
            int notesAt = text.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (notesAt < 0) return text;
            var notes = text[(notesAt + 2)..];
            // Quiz notes are about the picture
            if (!QuizNote().IsMatch(notes) && notes.Split('\n').All(NoteLine().IsMatch)) text = text[..notesAt];
            return text.Trim();
        }

        // Drops labels. 1997 has the title line first, some 2000s have no colon
        private static string? CleanCredit(string text)
        {
            if (!CreditLabel().IsMatch(text)) text = TitleBeforeCredit().Replace(text, "");
            text = CreditLabel().IsMatch(text) ? CreditLabel().Replace(text, "") : BareCreditLabel().Replace(text, "");
            text = text.Trim();
            return text.Length == 0 ? null : text;
        }

        [GeneratedRegex(@"\r?\n")]
        private static partial Regex SourceNewline();
        [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
        private static partial Regex LineBreak();
        [GeneratedRegex(@"</p\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex ParagraphEnd();
        [GeneratedRegex(@"<[^>]*>")]
        private static partial Regex Tag();
        // Decoded &nbsp; too
        [GeneratedRegex(@"[ \t\u00A0]+")]
        private static partial Regex Spaces();
        [GeneratedRegex(@"\n{3,}")]
        private static partial Regex ExtraBlankLines();
        [GeneratedRegex(@"^\s*(Explanation\s*:\s*)+", RegexOptions.IgnoreCase)]
        private static partial Regex ExplanationLabel();
        [GeneratedRegex(@"^(Your Sky Surprise|Tomorrow['’]s picture|Next picture):", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        private static partial Regex ExplanationFooter();
        [GeneratedRegex(@"^[^:\n]{1,60}:\s*\S[^\n]{0,200}$")]
        private static partial Regex NoteLine();
        [GeneratedRegex(@"^Quiz\s*:", RegexOptions.IgnoreCase)]
        private static partial Regex QuizNote();
        // No , or ; before the colon, keeps "NASA ; Processing & Copyright: X" whole
        [GeneratedRegex(@"^[^:;,\n]{0,40}\b(Credits?|Copyright)\b[^:;,\n]{0,40}:\s*", RegexOptions.IgnoreCase)]
        private static partial Regex CreditLabel();
        [GeneratedRegex(@"^[^\n]*\n(?=[^:;,\n]{0,40}\bCredits?\b[^:;,\n]{0,40}:)", RegexOptions.IgnoreCase)]
        private static partial Regex TitleBeforeCredit();
        [GeneratedRegex(@"^Credits?\s+(?=\p{Lu})")]
        private static partial Regex BareCreditLabel();
    }

    /// <summary>
    /// Observed assets.science.nasa.gov URL forms, each null when hdurl isn't one so callers use hdurl as given
    /// </summary>
    public static class ApodImageUrl
    {
        private const string Host = "assets.science.nasa.gov";
        // Serves the original file, ignores the query
        private const string OriginalPrefix = "/content/dam/";
        // Resizing proxy, re-encodes JPEGs at q80 and upscales past the original
        private const string ProxyPrefix = "/dynamicimage/assets/";

        /// <summary>
        /// Original file, null when hdurl already serves it (any query is ignored there)
        /// </summary>
        public static Uri? Original(Uri hdurl)
        {
            if (AssetPath(hdurl) is not string asset || IsOriginal(hdurl)) return null;
            return new Uri($"{hdurl.Scheme}://{hdurl.Authority}{OriginalPrefix}{asset}");
        }

        private static bool IsOriginal(Uri hdurl) => hdurl.AbsolutePath.StartsWith(OriginalPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resized by the proxy, never wider than the original
        /// </summary>
        public static Uri? Resized(Uri hdurl, int width)
        {
            if (width <= 0 || AssetPath(hdurl) is not string asset) return null;
            if (ExpectedSize(hdurl) is ImageSize expected) width = Math.Min(width, expected.Width);
            return new Uri($"{hdurl.Scheme}://{hdurl.Authority}{ProxyPrefix}{asset}?w={width}");
        }

        /// <summary>
        /// The w and h hdurl carries, the original's size in every sample
        /// </summary>
        public static ImageSize? ExpectedSize(Uri hdurl)
        {
            if (AssetPath(hdurl) == null) return null;
            var query = HttpUtility.ParseQueryString(hdurl.Query);
            return int.TryParse(query["w"], NumberStyles.None, CultureInfo.InvariantCulture, out var w) && w > 0
                && int.TryParse(query["h"], NumberStyles.None, CultureInfo.InvariantCulture, out var h) && h > 0
                ? new ImageSize(w, h) : null;
        }

        // Escaped path after either prefix
        private static string? AssetPath(Uri hdurl)
        {
            if (!hdurl.Host.Equals(Host, StringComparison.OrdinalIgnoreCase)) return null;
            var path = hdurl.AbsolutePath;
            if (path.StartsWith(OriginalPrefix, StringComparison.OrdinalIgnoreCase)) return path[OriginalPrefix.Length..];
            if (path.StartsWith(ProxyPrefix, StringComparison.OrdinalIgnoreCase)) return path[ProxyPrefix.Length..];
            return null;
        }
    }
}
