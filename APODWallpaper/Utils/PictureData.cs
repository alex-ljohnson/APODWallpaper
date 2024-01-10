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
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
    }
}
