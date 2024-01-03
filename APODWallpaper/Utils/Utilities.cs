namespace APODWallpaper.Utils
{
    public static class Utilities
    {

        public static string current = GetDataPath("current.jpg");
        public static string last = GetDataPath("last.jpg");
        public static string today = GetDataPath("today.cache");
        public static string GetDataPath(string path)
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string result;
            if (string.IsNullOrWhiteSpace(path)) {
                result = Path.Combine(appdata, "APODWallpaper");
            } else {
                result =  Path.Combine(appdata, "APODWallpaper", path);
            }
            return Path.GetFullPath(result);
        }
    }
}
