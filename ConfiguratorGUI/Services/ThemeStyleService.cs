using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using APODWallpaper.Interfaces;

namespace ConfiguratorGUI.Services
{
    // TODO: Separate theme handling from App.xaml.cs
    // Service interfaces for better testability and separation of concerns
    public interface IThemeService
    {
        Task InitializeThemesAsync();
        Task ApplyThemeAsync(ResourceDictionary resources);
        Task<bool> ValidateThemeAsync(string themeName);
    }
    public partial class ThemeStyleService(IConfigurationService configService) : IThemeService
    {
        private static readonly string[] DefaultThemes = ["Light.xaml", "Dark.xaml"];
        private static readonly string StylesDirectory = Path.Combine(AppContext.BaseDirectory, "Styles");

        public async Task InitializeThemesAsync()
        {
            var themes = new List<string>(DefaultThemes);

            if (!Directory.Exists(StylesDirectory))
            {
                Directory.CreateDirectory(StylesDirectory);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(StylesDirectory, "*.xaml"))
            {
                var name = Path.GetFileName(file);
                Trace.WriteLine($"Found theme: {name}");

                if (!themes.Contains(name))
                {
                    themes.Add(name);
                }
            }

            // Update available themes in configuration
            foreach (var theme in themes)
            {
                if (!configService.AvailableThemes.Contains(theme))
                {
                    configService.AvailableThemes.Add(theme);
                }
            }
        }

        public async Task ApplyThemeAsync(ResourceDictionary resources)
        {
            var themeName = configService.ConfiguratorTheme;

            // Validate custom themes
            if (!DefaultThemes.Contains(themeName) && !await ValidateThemeAsync(themeName))
            {
                    Trace.WriteLine($"Invalid theme: {themeName}");
                    await ResetThemeAsync(resources);
                    return;
            }
            

            try
            {
                // Defaults are compiled in, custom themes are loose files
                resources.MergedDictionaries[0].Source = DefaultThemes.Contains(themeName)
                    ? new Uri($"/Styles/{themeName}", UriKind.Relative)
                    : new Uri(Path.Combine(StylesDirectory, themeName), UriKind.Absolute);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error applying theme: {ex.Message}");
                await ResetThemeAsync(resources);
            }
        }

        public async Task<bool> ValidateThemeAsync(string themeName)
        {
            var themePath = Path.Combine(StylesDirectory, themeName);

            if (!File.Exists(themePath))
                return false;

            try
            {
                using var stream = new FileStream(themePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream, true);

                string contents = await reader.ReadToEndAsync();
                contents = contents.Trim().ReplaceLineEndings(" ");

                var regex = WhitespaceRegex();
                contents = regex.Replace(contents, " ");

                return contents.StartsWith("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">")
                    && contents.EndsWith("</ResourceDictionary>");
            }
            catch
            {
                return false;
            }
        }

        private async Task ResetThemeAsync(ResourceDictionary resources)
        {
            configService.ConfiguratorTheme = "Light.xaml";
            MessageBox.Show(
                "Error loading custom theme, default theme applied",
                "Theme Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await ApplyThemeAsync(resources);
        }

        [GeneratedRegex(@"[\s]{2,}", RegexOptions.None)]
        private static partial Regex WhitespaceRegex();
    }


}
