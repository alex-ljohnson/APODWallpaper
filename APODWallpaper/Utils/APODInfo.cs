using Newtonsoft.Json;

namespace APODWallpaper.Utils
{
    /// <summary>
    /// Single APOD entry. Built from API responses by <see cref="ApodResponseMapper"/>, its own JSON shape is the metadata.v2.cache format
    /// </summary>
    [method: JsonConstructor]
    public class APODInfo(DateOnly date, string title, string explanation, string? credit, string mediaType, Uri? imageUrl, Uri? permalink, string? altText = null, DateOnly? retrievalDate = null)
    {
        public DateOnly Date { get; set; } = date;
        public string Title { get; set; } = title;
        public string Explanation { get; set; } = explanation;
        public string? Credit { get; set; } = credit;
        // image, video, iframe or other
        public string MediaType { get; set; } = mediaType;
        // Full image, or a still for recent videos
        public Uri? ImageUrl { get; set; } = imageUrl;
        // science.nasa.gov article
        public Uri? Permalink { get; set; } = permalink;
        // Screen reader text, mostly posts since 2021
        public string? AltText { get; set; } = altText;

        public DateOnly? RetrievalDate { get; set; } = retrievalDate;

        [JsonIgnore]
        public string Filename => APODDate.ToIsoString(Date);

        [JsonIgnore]
        public string DateFormatted => Filename;

        [JsonIgnore]
        public bool IsImage => MediaType == "image" && ImageUrl != null;

        // Something to save, the image or a video's still
        [JsonIgnore]
        public bool HasImage => ImageUrl != null;

        public bool Equals(APODInfo? other)
        {
            if (other == null) return false;
            return Date.Equals(other.Date);
        }
        public bool Equals(PictureData? other)
        {
            if (other == null) return false;
            return Date.Equals(other.Date);
        }

        public override int GetHashCode()
        {
            return Date.GetHashCode();
        }
    }
}
