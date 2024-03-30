using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

bool force = false;
const string verName = "2024.03.27.1";
if (args.Length > 0)
{
    foreach (string i in args)
    {
        string arg = i.Trim().Trim('-').ToLower();
        if (arg == "force")
        {
            force = true;
        }
        if (arg is "version" or "v")
        {
            Console.WriteLine($"APODWallpaper v{verName}");
            Environment.Exit(0);
        }
        if (arg == "check")
        {
            var val = Configuration.CheckStartup();
            if (val)
            {
                Console.WriteLine("Startup registry set");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Startup not enabled");
            }
        }
    }
}
APODWallpaper.APODWallpaper instance = APODWallpaper.APODWallpaper.Instance;
await instance.Update(force);
namespace APODWallpaper
{
    public class APODWallpaper
    {
        [DllImport("kernel32.dll")]
        static extern int GetConsoleWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int MessageBoxW(int hWnd, string msg, string title, int type);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        private readonly int hwnd = GetConsoleWindow();
        public readonly string base_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        //private readonly string file_name;

        private const string API_KEY = "5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ";

        // Configuration
        private readonly HttpClient client;
        private Dictionary<string, dynamic>? info;

        public Dictionary<string, dynamic> Info
        {
            get
            {
                info ??= Task.Run(GetToday).Result;
                return info;
            }
        }

        private static readonly object padlock = new();
        private static APODWallpaper? _instance = null;

        public static APODWallpaper Instance
        {
            get
            {
                lock (padlock)
                {
                    _instance ??= new APODWallpaper();
                    return _instance;
                }
            }
        }
        private APODWallpaper()
        {
            Directory.CreateDirectory(Utilities.GetDataPath(""));
            client = new HttpClient();
            Configuration.Config.ChangeStartup();
        }

        public async Task<Dictionary<string, dynamic>?> Update(bool force = false)
        {
            if (force || await CheckNew())
            {
                var fileInfo = await DownloadImage();
                UpdateBackground(fileInfo["Source"], style: (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
                if (Configuration.Config.ExplainImage) { MessageBoxW(hwnd, Info["explanation"], "Image Updated", 0x40 | 0x00); }
                return fileInfo;
            }
            else
            {
                Console.WriteLine("No new image");
                return null;
            }
        }

        public async Task<Dictionary<string, dynamic>> GetToday()
        {

            string url = $"{Configuration.Config.BaseUrl}?api_key={API_KEY}";
            Dictionary<string, dynamic>? today;
            try
            {
                string response = await client.GetStringAsync(url);
                Task writeTask = File.WriteAllTextAsync(Utilities.GetDataPath("today.cache"), response);
                today = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(response)!;
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is HttpRequestException)
            {
                _ = MessageBoxW(hwnd, "Please check your internet connection and try again", "Connection error", 0x10);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
                return null;
            }
            return today;
        }

        public async Task<Dictionary<string, dynamic>> DownloadImage()
        {
            string url;
            if (Configuration.Config.UseHD && Info.TryGetValue("hdurl", out dynamic? value))
            {
                url = value;
                Console.WriteLine("HD");
            }
            else
            {
                url = Info["url"];
            }
            if (Info["media_type"] != "image") { throw new Exception("APOD was not an image"); }
            Console.WriteLine("Getting image data");

            DateTime startTime = DateTime.Now;
            client.DefaultRequestHeaders.Clear();
            string filename;
            Dictionary<string, dynamic> downloadedInfo;
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                filename = Utilities.GetDataPath("images/") + (response.Content.Headers.ContentDisposition?.FileName ?? DateOnly.FromDateTime(DateTime.Now).ToString("D"));
                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream writer = new(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                if (Configuration.Config.DownloadInfo && contentLength.HasValue && false)
                {
                    var totalRead = 0L;
                    var buffer = new byte[4096];
                    while (totalRead < contentLength)
                    {
                        int read = await contentStream.ReadAsync(buffer);
                        if (read != 0)
                        {
                            await writer.WriteAsync(buffer);
                            totalRead += read;
                            Console.Write($"{totalRead} / {contentLength} bytes written ({(totalRead * 100) / contentLength}% Complete)\r");
                        }
                    }
                    Console.WriteLine($"\nDownload Complete ({totalRead} bytes written)");
                }
                else
                {
                    await contentStream.CopyToAsync(writer);
                }
                downloadedInfo = new Dictionary<string, dynamic>() { ["Name"] = Info["title"], ["Description"] = Info["explanation"], ["Source"] = filename, ["Date"] = DateOnly.FromDateTime(DateTime.Now) };
                var infoJson = JsonConvert.SerializeObject(downloadedInfo, Formatting.Indented);
                await File.WriteAllTextAsync(filename + ".json", infoJson);
            }
            Console.WriteLine($"Time Taken: {(DateTime.Now - startTime).TotalSeconds} seconds");
            // Write date cache
            File.WriteAllTextAsync(Utilities.GetDataPath("current_date.cache"), Info["date"]);
            return downloadedInfo;
        }

        public async Task<bool> CheckNew()
        {
            string current_date;
            string date = Info["date"];
            try
            {
                current_date = await File.ReadAllTextAsync(Utilities.GetDataPath("current_date.cache"));
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            return date != current_date;
        }

        public void UpdateBackground(string file, WallpaperStyleEnum style = WallpaperStyleEnum.Fill)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)!;
            if (key == null)
            {
                _ = MessageBoxW(hwnd, "The registry key couldn't be opened\nSkipping, background may not be changed", "Registry error", 0x00);
            }
            else
            {
                if (style == WallpaperStyleEnum.Centred)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                else if (style == WallpaperStyleEnum.Tiled)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                }
                else
                {
                    key.SetValue(@"WallpaperStyle", ((int)style).ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }
                key.SetValue(@"WallPaper", file);
            }
            Console.WriteLine(file);
            uint SPI_SETDESKWALLPAPER = 0x0014;
            uint SPIF_UPDATEINIFILE = 0x01;
            uint SPIF_SENDWININICHANGE = 0x02;
            int r = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, file, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            Console.WriteLine(r.ToString());
        }

    }
}