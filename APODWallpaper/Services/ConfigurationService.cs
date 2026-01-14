using APODWallpaper.Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace APODWallpaper.Services
{

    public interface IConfigurationService
    {
        Task InitializeAsync();
        string CurrentTheme { get; set; }
        List<string> AvailableThemes { get; }
    }

    // Theme service implementation
    // Configuration service implementation
    public class ConfigurationService(IConfiguration configuration) : IConfigurationService
    {
        public string CurrentTheme { get; set; } = "Light.xaml";
        public List<string> AvailableThemes { get; } = [];

        public async Task InitializeAsync()
        {
            Trace.WriteLine("Initializing configuration service");

            // Initialize legacy static configuration if needed
            await Configuration.DefaultConfiguration.Initialise();
            await Configuration.Config.Initialise();

            // Sync with legacy configuration
            CurrentTheme = Configuration.Config.ConfiguratorTheme;
        }
    }
}
