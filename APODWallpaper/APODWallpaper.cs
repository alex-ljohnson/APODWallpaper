using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

bool force = false;
const string verName = "2025.10.30.1";
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
            }
            else
            {
                Console.WriteLine("Startup not enabled");
            }
            Environment.Exit(0);
        }
    }
}
Console.WriteLine($"APODWallpaper v{verName}\n--------------------\n");
await APODWallpaper.APODWallpaper.Instance.UpdateAsync(force);
namespace APODWallpaper
{
    public class APODWallpaper
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
        private static extern int SystemParametersInfoW(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        public readonly string base_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

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
            Configuration.Config.ChangeStartup();
        }

        public async Task<PictureData?> UpdateAsync(bool force = false)
        {
            if (force || CheckNewAsync())
            {
                var fileInfo = await DownloadTodayAsync();
                UpdateBackground(fileInfo.Source, style: (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
                if (Configuration.Config.ExplainImage) { Utilities.ShowMessageBox((await APODCache.Instance.GetToday()).Explanation, "Image Updated", Utilities.MessageBoxType.Information | Utilities.MessageBoxType.OK); }
                return fileInfo;
            }
            else
            {
                Console.WriteLine("No new image");
                return null;
            }
        }
        

        protected async Task<string> DownloadURLAsync(Uri url, DateOnly? date = null)
        {
            string filename;
            using (HttpResponseMessage response = await NetClient.InstanceClient.GetAsync(url))
            {
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                filename = Utilities.GetDataPath("images/") + (date?.ToString("D") ?? response.Content.Headers.ContentDisposition?.FileName ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("D"));
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
            APODInfo imageInfo = information ?? await APODCache.Instance.GetToday();
            if (imageInfo.MediaType != "image") { Utilities.ShowMessageBox("APOD is not an image.", "Not an image"); Environment.Exit(1); }
            Console.WriteLine("Getting image data");

            DateTime startTime = DateTime.UtcNow;
            if (File.Exists(Utilities.GetDataPath("images/" + imageInfo.Filename)))
            {
                Utilities.ShowMessageBox("Image already downloaded", "Already downloaded");
                return new PictureData(imageInfo.Title, imageInfo.Explanation, Utilities.GetDataPath("images/" + imageInfo.Filename), imageInfo.Date);
            }
            var filename = await DownloadURLAsync(imageInfo.RealUri, imageInfo.Date);
            PictureData downloadedInfo = new(imageInfo.Title, imageInfo.Explanation, filename, imageInfo.Date);
            var infoJson = JsonConvert.SerializeObject(downloadedInfo, Formatting.Indented);
            await File.WriteAllTextAsync(filename + ".json", infoJson);
            Console.WriteLine($"Time Taken: {(DateTime.UtcNow - startTime).TotalSeconds} seconds");
            // Write date cache
            return downloadedInfo;
        }
        public async Task<PictureData> DownloadTodayAsync(APODInfo? info = null)
        {
            info ??= await APODCache.Instance.GetToday();
            return await DownloadImageAsync(info);
        }

        public bool CheckNewAsync()
        {
            var latest = APODCache.Instance.ReadLatest();
            var date = latest?.Date;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return date != today;
        }

        public void UpdateBackground(string? file = null, WallpaperStyleEnum style = WallpaperStyleEnum.Fill)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)!;
            if (key == null)
            {
                Utilities.ShowMessageBox("The registry key couldn't be opened\nSkipping, background may not be changed", "Registry error", Utilities.MessageBoxType.Warning | Utilities.MessageBoxType.OK);
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
            int r = SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, file, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            Console.WriteLine(r.ToString());
        }

    }
}