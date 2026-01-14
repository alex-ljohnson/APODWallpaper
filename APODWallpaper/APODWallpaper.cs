using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

bool force = false;
string verName = APODWallpaper.APODWallpaper.Version;
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
        public static string Version => "2026.01.14.1";
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
                if (fileInfo == null) {
                    Utilities.ShowMessageBox("Could not download image.", "Download error", Utilities.MessageBoxType.Error | Utilities.MessageBoxType.OK);
                    return null;
                }
                UpdateBackground(fileInfo.Source, style: (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
                string? todayExp = (await APODCache.Instance.GetToday())?.Explanation;
                if (Configuration.Config.ExplainImage && todayExp != null) { Utilities.ShowMessageBox(todayExp, "Image Updated", Utilities.MessageBoxType.Information | Utilities.MessageBoxType.OK); }
                return fileInfo;
            }
            else
            {
                Console.WriteLine("No new image");
                return null;
            }
        }
        
        // TODO: Refactor progress reporting and separate it from download logic
        protected async Task<string> DownloadURLAsync(Uri url, string filepath, IProgress<(long, long?)>? progress = null)
        {
            //string filename;
            try
            {
                using HttpResponseMessage response = await NetClient.InstanceClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                if (!contentLength.HasValue)
                {
                    Console.WriteLine("Content length not provided");
                }
                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Write);
                // TODO: remove hardcoded 'false' when progress reporting is implemented
                if (Configuration.Config.DownloadInfo && contentLength.HasValue)
                {
                    long totalReadBytes = 0L;
                    var buffer = new byte[81920];
                    int readBytes;
                    while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, readBytes);
                        totalReadBytes += readBytes;
                        progress?.Report((totalReadBytes, contentLength));
                    }
                }
                else
                {
                    var copyTask = contentStream.CopyToAsync(fileStream);
                    // Simple progress reporter for copy operation
                    await copyTask;

                }
            } catch (Exception ex) when (ex is HttpRequestException || ex is TimeoutException)
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again", "Connection error", Utilities.MessageBoxType.Error);
                Console.WriteLine(ex.StackTrace);
                throw;
            }
            return filepath;
        }
        public async Task<PictureData?> DownloadImageAsync(APODInfo? information = null)
        {
            APODInfo? imageInfo = information ?? await APODCache.Instance.GetToday();
            if (imageInfo == null) return null;
            if (imageInfo.RealUri == null) { Utilities.ShowMessageBox("No media URL is given.", "No URL available"); Environment.Exit(1); }
            if (imageInfo.MediaType != "image") { Utilities.ShowMessageBox("APOD is not an image.", "Not an image"); throw new NotImageException("APOD is not an image"); }
            Console.WriteLine("Getting image data");

            DateTime startTime = DateTime.UtcNow;
            string filepath = Utilities.GetDataPath("images/" + imageInfo.Filename);
            if (File.Exists(filepath))
            {
                Utilities.ShowMessageBox("Image already downloaded", "Already downloaded");
                return new PictureData(imageInfo.Title, imageInfo.Explanation, filepath, imageInfo.Date);
            }
            var filename = await DownloadURLAsync(imageInfo.RealUri, filepath);
            PictureData downloadedInfo = new(imageInfo.Title, imageInfo.Explanation, filename, imageInfo.Date);
            var infoJson = JsonConvert.SerializeObject(downloadedInfo, Formatting.Indented);
            await File.WriteAllTextAsync(filename + ".json", infoJson);
            Console.WriteLine($"Time Taken: {(DateTime.UtcNow - startTime).TotalSeconds} seconds");
            return downloadedInfo;
        }
        public async Task<PictureData?> DownloadTodayAsync(APODInfo? info = null)
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