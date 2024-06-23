using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Text.RegularExpressions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public const string AppVersion = "2024.01.10.1";
        public static MainWindow MainWindow = new();
        private static string StylesPath = Utilities.GetDataPath("Styles");

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        /// 
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        async protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Trace.WriteLine("At app startup");
            await Configuration.DefaultConfiguration.Initialise();
            await Configuration.Config.Initialise();
            //GetThemes();
            //await SetTheme();
            MainWindow.Activate();
            //Trace.WriteLine("Themes loaded into config");
        }

        //[GeneratedRegex(@"[\s]{2,}", RegexOptions.None)]
        //private static partial Regex WhitespaceRegex();
        //private static async Task<bool> CheckThemeAsync()
        //{
        //    using var stream = new FileStream(Path.Combine(StylesPath, Configuration.Config.ConfiguratorTheme), FileMode.Open, FileAccess.Read);
        //    using var reader = new StreamReader(stream, true);
        //    string contents = await reader.ReadToEndAsync();
        //    contents = contents.Trim().ReplaceLineEndings(" ");
        //    Regex regex = WhitespaceRegex();
        //    contents = regex.Replace(contents, " ");
        //    return contents.StartsWith("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">")
        //    && contents.EndsWith("</ResourceDictionary>");
        //}
        //public async Task SetTheme()
        //{
        //    // Not a default theme
        //    if (!Configuration.DefaultThemes.Contains(Configuration.Config.ConfiguratorTheme))
        //    {
        //        if (!(File.Exists(Path.Combine(StylesPath, Configuration.Config.ConfiguratorTheme)) && await CheckThemeAsync()))
        //        {
        //            Trace.WriteLine("Invalid theme");
        //            await ResetTheme();
        //            return;
        //        }
        //    }
        //    try
        //    {
        //        Resources.MergedDictionaries[0].Source = new Uri(Path.Combine(StylesPath, Configuration.Config.ConfiguratorTheme), UriKind.Relative);
        //    }
        //    catch (IOException)
        //    {
        //        Resources.MergedDictionaries[0].Source = new Uri(Path.GetFullPath(Path.Combine(StylesPath, Configuration.Config.ConfiguratorTheme)), UriKind.Absolute);
        //    }
        //}
        //private async Task ResetTheme()
        //{
        //    Configuration.Config.ConfiguratorTheme = "Light.xaml";
        //    await new ContentDialog() { Title = "Theme error", Content = "Error in loading custom theme, default applied.", CloseButtonText = "Ok" }.ShowAsync();
        //    await SetTheme();
        //}

        //private static void GetThemes()
        //{
        //    List<string> themes = ["Light.xaml", "Dark.xaml"];
        //    if (Directory.Exists(StylesPath))
        //    {
        //        foreach (var file in Directory.EnumerateFiles(StylesPath))
        //        {
        //            Trace.WriteLine(file);
        //            if (file.EndsWith(".xaml") && File.Exists(file))
        //            {
        //                var name = file[(file.LastIndexOf('\\') + 1)..];
        //                Trace.WriteLine("Found theme: " + name);
        //                themes.Add(name);
        //            }
        //        }
        //        Configuration.Config.LoadThemes(themes);
        //    }
        //    else
        //    {
        //        Directory.CreateDirectory(StylesPath);
        //    }

        //}


    }
}
