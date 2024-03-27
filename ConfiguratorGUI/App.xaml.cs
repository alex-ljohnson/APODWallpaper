using APODWallpaper.Utils;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ConfiguratorGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string AppVersion = "2024.03.27.1";
        protected override void OnStartup(StartupEventArgs e)
        {
            List<string> themes = ["Light.xaml", "Dark.xaml"];
            if (Directory.Exists("./Styles"))
            {
                foreach (var file in Directory.EnumerateFiles("./Styles"))
                {
                    Trace.WriteLine(file);
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
            base.OnStartup(e);
        }

    }
}
