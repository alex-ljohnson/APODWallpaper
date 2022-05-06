using Newtonsoft.Json;
using System.Net.Http.Headers;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using APODWallpaper.Utils;
using System;

Console.WriteLine(string.Join(", ", args));
bool force = false;
if (args.Length > 0)
{
    foreach (string i in args)
    {
        string arg = i.Trim('-').ToLower();
        if (arg == "force")
        {
            force = true;
        }
    }
}
APODWallpaper.APODWallpaper instance = new();
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
        private readonly string file_name;

        // Configuration
        private readonly HttpClient client;
        public Dictionary<string, dynamic>? info;

        public APODWallpaper()
        {
            Directory.CreateDirectory(Path.GetFullPath("data", base_path));
            file_name = Path.GetFullPath("data\\current.jpg", base_path);
            client = new HttpClient() { BaseAddress = new Uri(Configuration.Config.BaseUrl) };

        }

        async public Task Update(bool force = false)
        {
            info = await GetToday();
            if (await CheckNew() || force)
            {
                await DownloadImage();
                UpdateBackground(style: (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
                if (Configuration.Config.ExplainImage) { MessageBoxW(hwnd, info["explanation"], "Image Updated", 0x40 | 0x00); }
            }
            else
            {
                Console.WriteLine("No new image");
            }
        }

        public async Task<Dictionary<string, dynamic>> GetToday()
        {
            try
            {
                string response = await client.GetStringAsync("?api_key=DEMO_KEY");
                Task writeTask = File.WriteAllTextAsync(Path.GetFullPath("data\\today.cache", base_path), response);
                info = JsonConvert.DeserializeAnonymousType(response, new Dictionary<string, dynamic>())!;
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is HttpRequestException)
            {
                _ = MessageBoxW(hwnd, "Please check your internet connection and try again", "Connection error", 0x10);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
                return null;
            }
            return info;
        }

        public async Task<bool> DownloadImage()
        {
            string url;
            if (info == null) { info = await GetToday(); }
            if (Configuration.Config.UseHD)
            {
                if (info.ContainsKey("hdurl"))
                {
                    url = info["hdurl"];
                    Console.WriteLine("HD");
                }
                else { url = info["url"]; }
            }
            else
            {
                url = info["url"];
            }
            if (info["media_type"] != "image") { throw new Exception("APOD was not an image"); }
            await File.WriteAllTextAsync(Path.GetFullPath("data\\explanation.txt", base_path), info["explanation"]);
            Console.WriteLine("Getting image data");
            try
            {
                using (FileStream old = new(file_name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), newf = new(Path.GetFullPath("data\\last.jpg", base_path), FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    await old.CopyToAsync(newf);
                }
                //File.Move(file_name, Path.GetFullPath("data\\last.jpg", base_path), true);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("current.jpg doesn't exist");
            }
            catch (UnauthorizedAccessException)
            {
                _ = MessageBoxW(hwnd, "The required image file is currently in use.\nClose the any programs which may be using it, update will try again in 10 secs.", "Permission error", 0x10);
                await Task.Delay(10000);
                return await DownloadImage();
            }
            DateTime startTime = DateTime.Now;
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                using (FileStream writer = new(file_name, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
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
            }
            Console.WriteLine($"Time Taken: {(DateTime.Now - startTime).TotalSeconds} seconds");
            // Write date cache
            File.WriteAllTextAsync(Path.GetFullPath("data\\current_date.cache", base_path), info["date"]);
            return true;
        }

        public async Task<bool> CheckNew()
        {
            string current_date;
            if (info == null) { info = await GetToday(); }
            string date = info["date"];
            try
            {
                current_date = File.ReadAllText(Path.GetFullPath("data\\current_date.cache", base_path));
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            return date != current_date;
        }

        public void UpdateBackground(string? file = null, WallpaperStyleEnum style = WallpaperStyleEnum.Fill)
        {
            if (file == null)
            {
                file = file_name;
            }
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)!;
            if (key == null)
            {
                _ = MessageBoxW(hwnd, "The registry key couldn't be opened\nSkipping, background may not be changed", "Registry error", 0x00);
            } else
            {
                if (style == WallpaperStyleEnum.Centred)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                } else if (style == WallpaperStyleEnum.Tiled)
                {
                    key.SetValue(@"WallpaperStyle", 0.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                } else
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