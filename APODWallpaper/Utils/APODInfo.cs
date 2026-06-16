using APODWallpaper.Interfaces;
using Newtonsoft.Json;

namespace APODWallpaper.Utils
{
    [method: JsonConstructor]
    public class APODInfo(string? copyright, DateOnly date, string explanation, string? hdurl, string media_type, string service_version, string title, string url, DateOnly? retrievalDate = null)
    {
        //      {
        //  "copyright": "Panther Observatory",
        //  "date": "2006-04-15",
        //  "explanation": "In this stunning cosmic vista, galaxy M81 is on the left surrounded by blue spiral arms.  On the right marked by massive gas and dust clouds, is M82.  These two mammoth galaxies have been locked in gravitational combat for the past billion years.   The gravity from each galaxy dramatically affects the other during each hundred million-year pass.  Last go-round, M82's gravity likely raised density waves rippling around M81, resulting in the richness of M81's spiral arms.  But M81 left M82 with violent star forming regions and colliding gas clouds so energetic the galaxy glows in X-rays.  In a few billion years only one galaxy will remain.",
        //  "hdurl": "https://apod.nasa.gov/apod/image/0604/M81_M82_schedler_c80.jpg",
        //  "media_type": "image",
        //  "service_version": "v1",
        //  "title": "Galaxy Wars: M81 versus M82",
        //  "url": "https://apod.nasa.gov/apod/image/0604/M81_M82_schedler_c25.jpg"
        //},
        public string? Copyright { get; set; } = copyright;
        public DateOnly Date { get; set; } = date;
        public string Explanation { get; set; } = explanation;
        public Uri? HDUrl { get; set; } = (hdurl != null) ? new(hdurl, UriKind.Absolute) : null;
        public string MediaType { get; set; } = media_type;
        public string ServiceVersion { get; set; } = service_version;
        public string Title { get; set; } = title;
        public Uri? Url { get; set; } = (url != null) ? new(url, UriKind.Absolute) : null;

        public DateOnly? RetrievalDate { get; set; } = retrievalDate;

        [JsonIgnore]
        public string Filename => APODDate.ToIsoString(Date);

        [JsonIgnore]
        public string DateFormatted => Filename;

        // Valid only if at least one usable image URL
        [JsonIgnore]
        public bool IsValid => Url != null || HDUrl != null;
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

        public Uri? GetPreferredUri(bool useHd)
        {
            if (Url == null) return HDUrl;
            return (useHd && HDUrl != null) ? HDUrl : Url;
        }
    }
}
