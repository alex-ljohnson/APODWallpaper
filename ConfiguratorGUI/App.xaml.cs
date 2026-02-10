using APODWallpaper;
using APODWallpaper.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace ConfiguratorGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider serviceProvider;
        private readonly Configuration Config;
        public const string AppVersion = "2026.01.14.1";
        //private HostApplicationBuilder appBuilder;
        public App()
        {
            //appBuilder = Host.CreateApplicationBuilder();
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();

            Config = serviceProvider.GetRequiredService<Configuration>();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Configuration>(s => new("Config", true, true));
            
            services.AddSingleton<APODCache>();
            services.AddSingleton<APODWallpaper.APODWallpaper>();
            services.AddSingleton<ViewModel>();

            services.AddHttpClient<APODCache>(client => client.Timeout = TimeSpan.FromSeconds(20));
            services.AddLogging(services => services.AddConsole());
            services.AddTransient<MainWindow>(s => new(s.GetRequiredService<APODWallpaper.APODWallpaper>())
            {
                DataContext = s.GetRequiredService<ViewModel>()
            });
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Trace.WriteLine("At app startup");
            var startTime = DateTime.Now;
            await Config.InitialiseAsync();
            LoadThemesIntoConfig();
            await SetTheme();
            Trace.WriteLine($"App startup time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
        }

        #region Theme handling
        [GeneratedRegex(@"[\s]{2,}", RegexOptions.None)]
        private static partial Regex WhitespaceRegex();
        private async Task<bool> CheckThemeAsync()
        {
            using var stream = new FileStream($"./Styles/{Config.ConfiguratorTheme}", FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream, true);
            string contents = (await reader.ReadToEndAsync()).Trim().ReplaceLineEndings(" ");
            contents = WhitespaceRegex().Replace(contents, " ");
            return contents.StartsWith("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">")
            && contents.EndsWith("</ResourceDictionary>");
        }
        public async Task SetTheme()
        {
            // Not a default theme
            if (!Configuration.DefaultThemes.Contains(Config.ConfiguratorTheme))
            {
                if (!(File.Exists($"./Styles/{Config.ConfiguratorTheme}") && await CheckThemeAsync()))
                {
                    Trace.WriteLine("Invalid theme");
                    await ResetTheme();
                    return;
                }
            }
            try
            {
                Resources.MergedDictionaries[0].Source = new Uri($"./Styles/{Config.ConfiguratorTheme}", UriKind.Relative);
            }
            catch (IOException)
            {
                Resources.MergedDictionaries[0].Source = new Uri(Path.GetFullPath($"./Styles/{Config.ConfiguratorTheme}"), UriKind.Absolute);
            }
        }
        private async Task ResetTheme()
        {
            Config.ConfiguratorTheme = "Light.xaml";
            MessageBox.Show("Error in loading custom theme, default theme applied", "Theme error", MessageBoxButton.OK, MessageBoxImage.Error);
            await SetTheme();
        }

        private void LoadThemesIntoConfig()
        {
            List<string> themes = ["Light.xaml", "Dark.xaml"];
            if (Directory.Exists("./Styles"))
            {
                foreach (var file in Directory.EnumerateFiles("./Styles"))
                {
                    Trace.WriteLine($"Found theme: {file}");
                    if (file.EndsWith(".xaml") && File.Exists(file))
                    {
                        var name = file[(file.LastIndexOf('\\') + 1)..];
                        Trace.WriteLine("Found theme: " + name);
                        themes.Add(name);
                    }
                }
                Config.AvailableThemes = new(themes);
            }
            else
            {
                Directory.CreateDirectory("./Styles");
            }

        }
        #endregion
    }
}
