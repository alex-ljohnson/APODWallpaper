using APODWallpaper.Interfaces;
using System.Diagnostics;

namespace APODWallpaper.Utils
{
    /// <summary>
    /// Key error handling for the startup updater, which has no window of its own
    /// </summary>
    public sealed class PromptApiKeyErrorHandler : IApiKeyErrorHandler
    {
        public const string SetupArg = "--setup-key";
        private const string Title = "APOD Wallpaper - NASA API key";
        private static readonly string ConfiguratorPath = Path.Combine(AppContext.BaseDirectory, "ConfiguratorGUI.exe");

        private int handled;

        public void Handle(ApiKeyError error)
        {
            if (Interlocked.Exchange(ref handled, 1) == 1) return;

            if (!error.SetupHelps)
            {
                Utilities.ShowMessageBox(error.Message, Title, Utilities.MessageBoxType.Warning);
                return;
            }

            var answer = Utilities.ShowMessageBox($"{error.Message}\n\nSet up your own free API key now?", Title,
                Utilities.MessageBoxType.YesNo | Utilities.MessageBoxType.Warning);
            if (answer != Utilities.MessageBoxResult.Yes) return;

            try
            {
                if (File.Exists(ConfiguratorPath))
                {
                    Process.Start(new ProcessStartInfo(ConfiguratorPath, SetupArg) { WorkingDirectory = AppContext.BaseDirectory });
                    return;
                }
                // Lightweight install, no Configurator to paste the key into
                Utilities.OpenInBrowser(ApiKey.SignupUrl);
                Utilities.ShowMessageBox(
                    "Once you have your key, run the APODWallpaper installer again and paste it on the \"NASA API key\" page.\n\n" +
                    "Or run:\n" +
                    $"    \"{Path.Combine(AppContext.BaseDirectory, "APODWallpaper.exe")}\" --set-key YOUR_KEY",
                    Title, Utilities.MessageBoxType.Information);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Console.WriteLine($"Couldn't open key setup: {ex.Message}");
                Utilities.ShowMessageBox($"Couldn't open key setup. You can get a free key at {ApiKey.SignupUrl}", Title, Utilities.MessageBoxType.Error);
            }
        }
    }
}
