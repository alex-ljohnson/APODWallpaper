using APODWallpaper;
using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using ConfiguratorGUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
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
        private static readonly string? rawVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
        // Numeric form for update comparison and HTTP headers (no SHA suffix)
        public static string AppVersion { get; } = VersionInfo.Numeric(rawVersion);
        // Display form with shortened commit SHA
        public static string AppVersionDisplay { get; } = VersionInfo.ForDisplay(rawVersion);
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
            // Configuration - registered as both concrete type and interface (same singleton instance)
            services.AddSingleton<Configuration>(s => new("Config", true, true));
            services.AddSingleton<IConfigurationService>(s => s.GetRequiredService<Configuration>());

            // Named HttpClient for APODCache timeout by ConfigurableTimeoutHandler
            services.AddTransient<ConfigurableTimeoutHandler>();
            services.AddHttpClient("APODCache")
                    .AddHttpMessageHandler<ConfigurableTimeoutHandler>();

            // Core services registered as interfaces
            services.AddSingleton<IAPODCache, APODCache>();
            services.AddSingleton<IAPODWallpaper, APODWallpaper.APODWallpaper>();
            services.AddSingleton<ViewModel>();

            // Theme service
            services.AddSingleton<IThemeService, ThemeStyleService>();

            services.AddLogging(services => services.AddConsole());
            services.AddTransient<MainWindow>();
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
            var startTime = DateTime.UtcNow;
            await Config.InitialiseAsync();
            FileMigration.Run(new MigrationContext());
            var themeService = serviceProvider.GetRequiredService<IThemeService>();
            await themeService.InitializeThemesAsync();
            await themeService.ApplyThemeAsync(Resources);
            Trace.WriteLine($"App startup time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");
        }

        public async Task SetTheme()
        {
            var themeService = serviceProvider.GetRequiredService<IThemeService>();
            await themeService.ApplyThemeAsync(Resources);
        }
    }
}
