using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APODWallpaper.Utils
{
    public class PictureData()
    {
        public PictureData(Dictionary<string, dynamic> data) : this()
        {
            Name = data["Name"];
            Description = data["Description"];
            Source = data["Source"];
            Date = data.GetValueOrDefault("Date", DateOnly.Parse(Path.GetFileNameWithoutExtension(Source)));
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }

        private DateOnly? date;
        public DateOnly Date { get {
                if (date == null)
                {
                    date = DateOnly.Parse(Path.GetFileNameWithoutExtension(Source));
                    SaveFile();
                }
                
                return (DateOnly)date;
            } set { date = value; } }

        public void SaveFile()
        {
           File.WriteAllText(Source+".json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
