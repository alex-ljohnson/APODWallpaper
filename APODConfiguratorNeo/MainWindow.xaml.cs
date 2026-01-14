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
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        internal ViewModel VM {get;set;}
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        public Frame MainFrame { get => mainframe; }
        public MainWindow()
        {
            InitializeComponent();
            VM = new ViewModel();
        }


        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            await VM.Initialise();
            _ = Updater.CheckUpdate(true);
            Trace.WriteLine("\nWINDOW LOADED\n");

        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            await Updater.CheckUpdate();
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NotIntegerRegex();
        private static bool NotIntegerValidation(string text)
        {
            Regex regex = NotIntegerRegex();
            return regex.IsMatch(text);
        }

        private void MainTabControl_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected) {
                MainFrame.Navigate(typeof(Pages.Settings));
            } else
            {
                var selectedItem = (NavigationViewItem)args.SelectedItem;
                var tag = selectedItem.Tag.ToString();
                MainFrame.Navigate(Type.GetType($"APODConfiguratorNeo.Pages.{tag}"));
            }
        }

        //private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        //{
        //    if (e.DataObject.GetDataPresent(typeof(string)))
        //    {
        //        string text = (string)e.DataObject.GetData(typeof(string));
        //        if (NotIntegerValidation(text))
        //        {
        //            e.CancelCommand();
        //        }
        //    }
        //    else
        //    {
        //        e.CancelCommand();
        //    }
        //}
    }
}
