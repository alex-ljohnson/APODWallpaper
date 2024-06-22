using Newtonsoft.Json;

namespace APODWallpaper.Utils
{
    public class PictureData : IComparable<PictureData>
    {
        [JsonConstructor]
        public PictureData(string name, string description, string source, DateOnly date)
        {
            Name = name;
            Description = description;
            Source = source;
            Date = date;
        }

        public PictureData(Dictionary<string, dynamic> data)
        {
            Name = data["Name"];
            Description = data["Description"];
            Source = data["Source"];
            Date = data.GetValueOrDefault("Date", DateOnly.Parse(Path.GetFileNameWithoutExtension(Source)));
        }

        public PictureData(PictureData data) 
        {
            // Copy constructor
            Name = data.Name;
            Description = data.Description;
            Source = data.Source;
            Date = data.Date;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }

        private DateOnly? date;
        public DateOnly Date
        {
            get
            {
                if (date == null)
                {
                    date = DateOnly.Parse(Path.GetFileNameWithoutExtension(Source));
                    SaveFile();
                }

                return (DateOnly)date;
            }
            set { date = value; }
        }

        public void SaveFile()
        {
            File.WriteAllText(Source + ".json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public int CompareTo(PictureData? other)
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
