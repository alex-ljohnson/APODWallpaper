using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;

bool force = false;
const string verName = "2024.04.01.1";
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
await instance.UpdateAsync(force);
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
        private APODInfo? todayInfo;

        public APODInfo TodayInfo
        {
            get
            {
                todayInfo ??= Task.Run(GetToday).Result;
                return todayInfo;
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
            client = new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 2
            });
            Configuration.Config.ChangeStartup();
        }

        public async Task<PictureData?> UpdateAsync(bool force = false)
        {
            if (force || await CheckNewAsync())
            {
                var fileInfo = await DownloadTodayAsync();
                UpdateBackground(fileInfo.Source, style: (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
                if (Configuration.Config.ExplainImage) { _=MessageBoxW(hwnd, TodayInfo.Explanation, "Image Updated", 0x40 | 0x00); }
                return fileInfo;
            }
            else
            {
                Console.WriteLine("No new image");
                return null;
            }
        }

        public async Task<APODInfo[]> GetInfoAsync(DateOnly? end_date = null, int count = 1)
        {
            if (end_date > DateOnly.FromDateTime(DateTime.Now).AddDays(-1)) throw new ArgumentException("end_date was in the future");
            string url = $"{Configuration.Config.BaseUrl}?";
            if (end_date != null && count > 1) { 
                url += $"start_date={end_date?.AddDays(-(count-1)).ToString("yyyy-MM-dd")}&end_date={end_date?.ToString("yyyy-MM-dd")}&"; 
            }
            url += $"api_key={API_KEY}";
            APODInfo[] imageInfo;
            try
            {
                Trace.WriteLine(url);
                HttpResponseMessage responseMessage = await client.GetAsync(url);
                if (!responseMessage.IsSuccessStatusCode)
                {

                }
                string response = await responseMessage.Content.ReadAsStringAsync();
                Task writeTask = File.WriteAllTextAsync(Utilities.GetDataPath("today.cache"), response);
                if (count > 1)
                {
                    imageInfo = JsonConvert.DeserializeObject<APODInfo[]>(response)!;
                } else
                {
                    imageInfo = [JsonConvert.DeserializeObject<APODInfo>(response)!];
                }
            }
            catch (Exception ex)  when (ex is JsonException || ex is NotSupportedException || ex is HttpRequestException)
            {
                _ = MessageBoxW(hwnd, "Please check your internet connection and try again", "Connection error", 0x10);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
                return null;
            }
            return imageInfo;
        }
        public async Task<APODInfo> GetToday()
        {
            return (await GetInfoAsync())[0];
        }

        protected async Task<string> DownloadURLAsync(Uri url, DateOnly? date = null)
        {
            string filename;
            client.DefaultRequestHeaders.Clear();
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                filename = Utilities.GetDataPath("images/") + (date?.ToString("D") ?? response.Content.Headers.ContentDisposition?.FileName ?? DateOnly.FromDateTime(DateTime.Now).ToString("D"));
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
            }
            return filename;
        }
        public async Task<PictureData> DownloadImageAsync(APODInfo? information = null)
        {
            APODInfo imageInfo = information ?? TodayInfo;
            Console.WriteLine("HD");
            if (imageInfo.MediaType != "image") { throw new NotImageException("APOD was not an image"); }
            Console.WriteLine("Getting image data");

            DateTime startTime = DateTime.Now;
            var filename = await DownloadURLAsync(imageInfo.RealUri, imageInfo.Date);
            PictureData downloadedInfo = new(imageInfo.Title, imageInfo.Explanation, filename, imageInfo.Date);
            var infoJson = JsonConvert.SerializeObject(downloadedInfo, Formatting.Indented);
            await File.WriteAllTextAsync(filename + ".json", infoJson);
            Console.WriteLine($"Time Taken: {(DateTime.Now - startTime).TotalSeconds} seconds");
            // Write date cache
            return downloadedInfo;
        }
        public async Task<PictureData> DownloadTodayAsync()
        {
            APODInfo info = await GetToday();
            await File.WriteAllTextAsync(Utilities.GetDataPath("current_date.cache"), info.Date.ToString("yyyy-MM-dd"));
            return await DownloadImageAsync(info);
        }

        public async Task<bool> CheckNewAsync()
        {
            string current_date;
            string date = TodayInfo.Date.ToString("yyyy-MM-dd");
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

        public void UpdateBackground(string? file = null, WallpaperStyleEnum style = WallpaperStyleEnum.Fill)
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
                if (file == null) return;
                key.SetValue(@"WallPaper", file);
            }
            if (file == null) return;
            Console.WriteLine(file);
            uint SPI_SETDESKWALLPAPER = 0x0014;
            uint SPIF_UPDATEINIFILE = 0x01;
            uint SPIF_SENDWININICHANGE = 0x02;
            int r = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, file, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            Console.WriteLine(r.ToString());
        }

    }
}