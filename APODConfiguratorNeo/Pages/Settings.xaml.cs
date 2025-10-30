using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Settings : Page
    {
        private List<string> enums = [.. Enum.GetNames(typeof(WallpaperStyleEnum))];
        public Settings()
        {
            InitializeComponent();
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            Configuration.Config.SetConfiguration(Configuration.DefaultConfiguration);
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            Settings.UpdateBackground(null, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }
    }
}
