using System.Windows;

namespace ConfiguratorGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //private List<string> Themes { get; set; } = new();

        public const string AppVersion = "2024.03.16.1";
        protected override void OnStartup(StartupEventArgs e)
        {
            // Theme detection
            //string[] temp = Directory.GetFiles(Path.GetFullPath("Styles"), "*.xaml", SearchOption.TopDirectoryOnly);
            //Themes = temp.Select(x => x[(x.LastIndexOf(@"\")+1)..]).ToList();
            //string currentTheme = Themes.FirstOrDefault(x => x == Configuration.Config.ConfiguratorTheme, "Light.xaml");
            //StreamReader streamReader = new StreamReader(@"Styles\" + currentTheme);
            //ResourceDictionary dict = (ResourceDictionary)XamlReader.Load(streamReader.BaseStream);
            //dict.Source = new Uri(@"\Styles\" + currentTheme);
            //this.Resources.MergedDictionaries.Add(dict);
            //foreach (ResourceDictionary r in Resources.MergedDictionaries)
            //{
            //    Trace.WriteLine(r.Source);
            //}
            base.OnStartup(e);
        }

    }
}
