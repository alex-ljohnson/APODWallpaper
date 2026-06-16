using Newtonsoft.Json;
using System.Globalization;

namespace APODWallpaper.Utils
{
    public class PictureData : IComparable<PictureData>
    {
        [JsonConstructor]
        public PictureData(string name, string description, string source, DateOnly date, string? originalUrl = null)
        {
            Name = name;
            Description = description;
            Source = source;
            Date = date;
            OriginalUrl = originalUrl;
        }

        public PictureData(APODInfo info)
        {
            Name = info.Title;
            Description = info.Explanation;
            Source = "";
            Date = info.Date;
            OriginalUrl = info.GetPreferredUri(true)?.ToString();

        }

        public PictureData(Dictionary<string, dynamic> data)
        {
            Name = data["Name"];
            Description = data["Description"];
            Source = data["Source"];
            Date = data.GetValueOrDefault("Date", APODDate.ParseIso(Path.GetFileNameWithoutExtension(Source)));
            OriginalUrl = data.TryGetValue("OriginalUrl", out var originalUrl) ? originalUrl : null;
        }

        public PictureData(PictureData data)
        {
            // Copy constructor
            Name = data.Name;
            Description = data.Description;
            Source = data.Source;
            Date = data.Date;
            OriginalUrl = data.OriginalUrl;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string? OriginalUrl { get; set; }


        private DateOnly? date;
        public DateOnly Date
        {
            get
            {
                date ??= APODDate.ParseIso(Path.GetFileNameWithoutExtension(Source));
                return (DateOnly)date;
            }
            set { date = value; }
        }

        public void SaveFile()
        {
            File.WriteAllText(Source + ".json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public async Task SaveFileAsync()
        {
            await File.WriteAllTextAsync(Source + ".json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public int CompareTo(PictureData? other)
        {
            if (other == null) return 1;
            return Date.CompareTo(other.Date);
        }

        public int CompareTo(APODInfo? other)
        {
            if (other == null) return 1;
            return Date.CompareTo(other.Date);
        }

        public bool Equals(PictureData? other)
        {
            if (other == null) return false;
            return Date.Equals(other.Date);
        }
        public bool Equals(APODInfo? other)
        {
            if (other == null) return false;
            return Date.Equals(other.Date);
        }
    }
}
