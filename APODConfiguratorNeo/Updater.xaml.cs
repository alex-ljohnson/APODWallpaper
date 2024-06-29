using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Updater : Window
    {
        internal string labelText =
            @"A new version is available (VERSION_NAME).
Press the download button below to download and install it.";

        private Dictionary<string, dynamic> updateData;
        private static readonly HttpClient client = new();
        private Task? updateTask;
        public Updater(Dictionary<string, dynamic> updateData)
        {
            this.updateData = updateData;
            InitializeComponent();
            lblInfo.Text = $"A new version is available ({updateData.GetValueOrDefault("tag_name", "VERSION_NOT_FOUND")}).";

        }
        async Task ProgressHook()
        {
            if (updateTask == null) { return; }
            int c = 0;
            progressDownload.IsIndeterminate = true;
            while (!updateTask.IsCompleted)
            {
                c++;
                await Task.Delay(100);
            }
            // Is complete
            if (updateTask.IsFaulted)
            {
                progressDownload.IsIndeterminate = false;
                progressDownload.Value = 0;
                lblInfo.Text = $"Download failed with reason:\n{updateTask.Exception?.Message}";
            }
            else
            {
                progressDownload.IsIndeterminate = false;
                progressDownload.Value = 100;
                lblInfo.Text = $"Download completed successfully in {c * 0.1} seconds.";
            }
        }

        private async void RunUpdate(object sender, RoutedEventArgs e)
        {
            updateTask = Update();
            await ProgressHook();
        }

        private async Task Update()
        {
            Trace.WriteLine((string)updateData["assets"].ToString());
            string url = updateData["assets"][0]["url"];
            // Make request
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            string fname = response.Content.Headers.ContentDisposition!.FileName!;
            string filePath = Path.GetFullPath(fname, Path.GetTempPath());
            Trace.WriteLine(fname);
            // Download file
            using FileStream file = new(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await response.Content.CopyToAsync(file);
            file.Close();
            // Run file
            Process.Start(filePath, "/NOCANCEL /RESTARTAPPLICATIONS /SP- /SILENT /NOICONS /TYPE=\"Full\" \"/DIR=expand:{autopf}\\APODWallpaper\"");
            Environment.Exit(0);
        }
        public async static Task CheckUpdate(bool startUp = false)
        {

            // Update Check
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("APODWallpaper_Configurator", App.AppVersion));
            HttpResponseMessage response;
            Task<HttpResponseMessage> responseTask = client.GetAsync("https://api.github.com/repos/MrTransparentBox/APODWallpaper/releases/latest");
            response = await responseTask;
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                Trace.WriteLine($"{(int)response.StatusCode}: {response.ReasonPhrase}");
                return;
            }
            string contents = await response.Content.ReadAsStringAsync();
            Dictionary<string, dynamic> content = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(contents)!;
            string tag = ((string)content["tag_name"])[1..];
            Trace.WriteLine(tag);
            Version tagVersion = new(tag);
            Version version = new(App.AppVersion);
            if (tagVersion == version)
            {
                if (!startUp)
                {
                    ContentDialog dialog = new()
                    {
                        Title = "No update available",
                        Content = "You are already running the latest version."
                    };
                    await dialog.ShowAsync();
                }
            }
            else if (tagVersion > version)
            {
                var dialog = new ContentDialog
                {
                    Title = "Update available",
                    Content = $"New version found: {tag}\nWould you like to update to the new version?",
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No"
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var window = new Updater(content);
                    window.Activate();

                }
            }
            else if (tagVersion < version)
            {

                if (!startUp)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Preview Version",
                        PrimaryButtonText = "Yes",
                        Content = "Looks like you have a special preview or pre-release version! Do you want to install the last full release?"
                    };
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        var window = new Updater(content);
                        window.Activate();
                    }
                }
            }
        }
    }
}
