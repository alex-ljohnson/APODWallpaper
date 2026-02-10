using APODWallpaper;
using APODWallpaper.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
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
        public const string AppVersion = "2026.01.14.1";

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Configuration>();
            services.AddSingleton<APODWallpaper.APODWallpaper>();
            services.AddTransient<MainWindow>(s => new MainWindow
            {
                DataContext = s.GetRequiredService<ViewModel>()
            });
        }


        [GeneratedRegex(@"[\s]{2,}", RegexOptions.None)]
        private static partial Regex WhitespaceRegex();
        private static async Task<bool> CheckThemeAsync()
        {
            using var stream = new FileStream($"./Styles/{Configuration.Config.ConfiguratorTheme}", FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream, true);
            string contents = (await reader.ReadToEndAsync()).Trim().ReplaceLineEndings(" ");
            contents = WhitespaceRegex().Replace(contents, " ");
            return contents.StartsWith("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">")
            && contents.EndsWith("</ResourceDictionary>");
        }
        public async Task SetTheme()
        {
            // Not a default theme
            if (!Configuration.DefaultThemes.Contains(Configuration.Config.ConfiguratorTheme))
            {
                if (!(File.Exists($"./Styles/{Configuration.Config.ConfiguratorTheme}") && await CheckThemeAsync()))
                {
                    Trace.WriteLine("Invalid theme");
                    await ResetTheme();
                    return;
                }
            }
            try
            {
                Resources.MergedDictionaries[0].Source = new Uri($"./Styles/{Configuration.Config.ConfiguratorTheme}", UriKind.Relative);
            }
            catch (IOException)
            {
                Resources.MergedDictionaries[0].Source = new Uri(Path.GetFullPath($"./Styles/{Configuration.Config.ConfiguratorTheme}"), UriKind.Absolute);
            }
        }
        private async Task ResetTheme()
        {
            Configuration.Config.ConfiguratorTheme = "Light.xaml";
            MessageBox.Show("Error in loading custom theme, default theme applied", "Theme error", MessageBoxButton.OK, MessageBoxImage.Error);
            await SetTheme();
        }

        private static void LoadThemesIntoConfig()
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
                Configuration.Config.LoadThemes(themes);
            }
            else
            {
                Directory.CreateDirectory("./Styles");
            }

        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            Trace.WriteLine("At app startup");
            var startTime = DateTime.Now;
            await Configuration.Config.Initialise();
            LoadThemesIntoConfig();
            await SetTheme();
            Trace.WriteLine($"App startup time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
        }
    }
}
