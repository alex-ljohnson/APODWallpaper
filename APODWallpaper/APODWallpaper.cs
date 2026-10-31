using APODWallpaper;
using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Reflection;
using System.Runtime.InteropServices;

[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool AllocConsole();

bool force = false;
string? verName = APODWallpaper.APODWallpaper.Version;
// Run by the installer, keys are case sensitive so handle before the loop below lowercases args
int setKeyAt = Array.FindIndex(args, a => a.Trim().Trim('-').Equals("set-key", StringComparison.OrdinalIgnoreCase));
if (setKeyAt >= 0)
{
    string key = setKeyAt + 1 < args.Length ? args[setKeyAt + 1].Trim() : "";
    try
    {
        using Configuration keyConfig = new("Config");
        await keyConfig.InitialiseAsync();
        keyConfig.API_KEY = key;
        await keyConfig.SaveConfigAsync();
        Console.WriteLine(key.Length == 0 ? "API key cleared, using NASA's demo key" : "API key saved");
    }
    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
    {
        // Installer reports a non-zero exit code to the user
        Console.WriteLine($"Couldn't save API key: {ex.Message}");
        Environment.ExitCode = 1;
    }
    return;
}
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
            Console.WriteLine($"APODWallpaper version: v{verName ?? "Unknown"}");
            Environment.Exit(0);
        }
        if (arg == "check")
        {
            var val = Configuration.CheckStartupSet();
            Console.WriteLine(val ? "Startup registry set" : "Startup not enabled");
            Environment.Exit(0);
        }
    }
}

Configuration config = new("Config");
await config.InitialiseAsync();
if (config.ShowConsole) { AllocConsole(); Console.WriteLine("Console active..."); }
Console.WriteLine($"APODWallpaper v{verName}\n--------------------\n");

var services = new ServiceCollection();
services.AddSingleton<Configuration>(config);
services.AddSingleton<IConfigurationService>(config);
// Retries outermost so each attempt gets its own timeout
services.AddTransient<TransientRetryHandler>();
services.AddTransient<ConfigurableTimeoutHandler>();
services.AddHttpClient(ApodApiClient.HttpClientName, ApodApiClient.ConfigureHttpClient)
        .AddHttpMessageHandler<TransientRetryHandler>()
        .AddHttpMessageHandler<ConfigurableTimeoutHandler>();
services.AddSingleton<ApodApiClient>();
services.AddSingleton<IApiKeyErrorHandler, PromptApiKeyErrorHandler>();
services.AddSingleton<IAPODCache, APODCache>();
services.AddSingleton<IAPODWallpaper, APODWallpaper.APODWallpaper>();

var provider = services.BuildServiceProvider();
var apod = provider.GetRequiredService<IAPODWallpaper>();
await apod.UpdateAsync(force);
namespace APODWallpaper
{
    public class APODWallpaper : IAPODWallpaper
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)]
        private static extern int SystemParametersInfoW(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        public readonly string base_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        private static string? RawVersion => Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
        // Display form with shortened commit SHA
        public static string Version => VersionInfo.ForDisplay(RawVersion);

        private readonly IAPODCache APODCache;
        private readonly IConfigurationService Config;
        public APODWallpaper(IAPODCache apodCache, IConfigurationService config)
        {
            APODCache = apodCache;
            Config = config;
            Directory.CreateDirectory(Utilities.GetDataPath(""));
            FileMigration.Run(new MigrationContext());
        }

        public async Task<PictureData?> UpdateAsync(bool force = false)
        {
            if (force || CheckNew())
            {
                var latest = await APODCache.GetToday();
                if (latest == null)
                {
                    // Fetch already told the user
                    Console.WriteLine("Could not get today's APOD");
                    return null;
                }
                // Not out until 00:05 Eastern, next run retries
                if (WaitingForToday(latest.Date, ReadLastUpdate(LastUpdateFile), APODDate.Today()))
                {
                    Console.WriteLine("Today's APOD isn't out yet");
                    // Startup runs stay quiet, a user asked for this one
                    if (force) Utilities.ShowMessageBox("Today's APOD isn't out yet, NASA publishes it at 00:05 US Eastern time.", "Not out yet");
                    return null;
                }
                if (latest.Date != APODDate.Today()) Console.WriteLine($"Today's APOD isn't out yet, using {latest.DateFormatted}");
                if (!ShouldSetWallpaper(latest, Config.UseVideoStills))
                {
                    Utilities.ShowMessageBox($"The APOD for {latest.DateFormatted} isn't an image, so it won't be set as the wallpaper.\nSee it at {latest.Permalink}", "Not an image");
                    WriteLastUpdate(LastUpdateFile, latest.Date);
                    return null;
                }
                if (!latest.IsImage) Console.WriteLine($"The APOD for {latest.DateFormatted} is {DescribeMedia(latest.MediaType)}, using its still. See it at {latest.Permalink}");
                PictureData? fileInfo;
                try
                {
                    fileInfo = await DownloadImageAsync(latest, announceSaved: force);
                }
                // Already shown, a connection error retries next run
                catch (Exception ex) when (Utils.APODCache.IsReportedDownloadError(ex))
                {
                    Console.WriteLine($"Could not download image: {ex.Message}");
                    if (ex is InvalidDataException) WriteLastUpdate(LastUpdateFile, latest.Date);
                    return null;
                }
                if (fileInfo == null) {
                    Console.WriteLine("Could not download image");
                    return null;
                }
                UpdateBackground(fileInfo.Source, style: (WallpaperStyleEnum)Config.WallpaperStyle);
                WriteLastUpdate(LastUpdateFile, latest.Date);
                string? todayExp = fileInfo.Description;
                if (Config.ExplainImage && !string.IsNullOrEmpty(todayExp)) { Utilities.ShowMessageBox(todayExp, "Image Updated", Utilities.MessageBoxType.Information | Utilities.MessageBoxType.OK); }
                return fileInfo;
            }
            else
            {
                Console.WriteLine("No new image");
                return null;
            }
        }
                
        public Task<PictureData?> DownloadImageAsync(APODInfo? information = null) => DownloadImageAsync(information, announceSaved: true);

        private async Task<PictureData?> DownloadImageAsync(APODInfo? information, bool announceSaved)
        {
            APODInfo? imageInfo = information ?? await APODCache.GetToday();
            if (imageInfo == null) return null;
            if (!imageInfo.HasImage)
            {
                Utilities.ShowMessageBox($"The APOD for {imageInfo.DateFormatted} has no image, so it can't be saved or set as the wallpaper.\nSee it at {imageInfo.Permalink}", "Not an image");
                return null;
            }
            Console.WriteLine("Getting image data");

            DateTime startTime = DateTime.UtcNow;
            string imagesPath = Directory.CreateDirectory(Utilities.GetDataPath("images")).FullName;
            if (ImageFile.FindSaved(imagesPath, imageInfo.Date) is string saved)
            {
                if (announceSaved) Utilities.ShowMessageBox("Image already downloaded", "Already downloaded");
                return new PictureData(imageInfo.Title, imageInfo.Explanation, saved, imageInfo.Date, imageInfo.ImageUrl?.ToString());
            }
            var image = await APODCache.DownloadImageAsync(imageInfo, Path.Combine(imagesPath, imageInfo.Filename));
            PictureData downloadedInfo = new(imageInfo.Title, imageInfo.Explanation, image.Path, imageInfo.Date, image.Source.ToString());
            var infoJson = JsonConvert.SerializeObject(downloadedInfo, Formatting.Indented);
            await File.WriteAllTextAsync(image.Path + ".json", infoJson);
            Console.WriteLine($"Time Taken: {(DateTime.UtcNow - startTime).TotalSeconds} seconds");
            return downloadedInfo;
        }

        public static readonly string LastUpdateFile = Utilities.GetDataPath("last-update.state");

        public bool CheckNew() => ReadLastUpdate(LastUpdateFile) != APODDate.Today();

        internal static bool ShouldSetWallpaper(APODInfo info, bool useVideoStills) =>
            info.IsImage || (info.HasImage && useVideoStills);

        internal static string DescribeMedia(string mediaType) => mediaType switch
        {
            "video" => "a video",
            "iframe" => "interactive",
            _ => "not an image",
        };

        internal static bool WaitingForToday(DateOnly latest, DateOnly? lastUpdate, DateOnly today) =>
            latest != today && latest <= lastUpdate;

        internal static DateOnly? ReadLastUpdate(string statePath)
        {
            try
            {
                return File.Exists(statePath) ? APODDate.ParseIso(File.ReadAllText(statePath).Trim()) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
            {
                Console.WriteLine($"Couldn't read last update '{statePath}': {ex.Message}");
                return null;
            }
        }

        internal static void WriteLastUpdate(string statePath, DateOnly date)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
                File.WriteAllText(statePath, APODDate.ToIsoString(date));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.WriteLine($"Couldn't save last update: {ex.Message}");
            }
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