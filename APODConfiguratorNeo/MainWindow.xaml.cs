using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private readonly ViewModel VM;
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        private readonly StdOutRedirect redirect;

        private List<string> enums = Enum.GetNames(typeof(WallpaperStyleEnum)).ToList();
        public MainWindow()
        {
            InitializeComponent();

#if DEPENDANT
            MessageBox.Show(Process.GetCurrentProcess().ProcessName);
#endif
            redirect = new StdOutRedirect(TxtOutput);
            Console.SetOut(redirect);
            VM = new ViewModel();
        }

        #region Window Control

        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            BtnUpdateTheme_Click(sender, e);
            await VM.Initialise();
            _ = Updater.CheckUpdate(true);
            Trace.WriteLine("\nWINDOW LOADED\n");

        }

        #endregion

        async private void BtnForceRun_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Text = "";
            OutputTab.Focus(FocusState.Programmatic);
            await APOD.UpdateAsync(true);
            Console.WriteLine("\nProcess Finished!\n");
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            await Updater.CheckUpdate();
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            Configuration.Config.SetConfiguration(Configuration.DefaultConfiguration);
            BtnUpdateTheme_Click(sender, e);
        }

        private void BtnClearOut_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Text = "";
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            APOD.UpdateBackground(null, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }

        private async void BtnUpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine(CmbConfiguratorTheme.SelectedItem);
            Trace.WriteLine(Configuration.Config.ConfiguratorTheme);
            if (CmbConfiguratorTheme.SelectedItem == null) { return; }
            //await ((App)Application.Current).SetTheme();
        }

        private void PreviousExplore(object sender, RoutedEventArgs e)
        {
            VM.ExplorePrev();
        }

        private void NextExplore(object sender, RoutedEventArgs e)
        {
            VM.ExploreNext();
        }

        private void DownloadExplore(object sender, RoutedEventArgs e)
        {
            VM.SaveExploreAsync(VM.ExploreSelected);
        }

        async private void Description(object sender, RoutedEventArgs e)
        {
            var param = VM.SelectedItem;
            if (VM == null) { return; }
            await new ContentDialog() { Content = param?.Description, Title = $"Description of {param?.Name}" }.ShowAsync();
        }

        async private void SaveImage(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("Saving image");
            PictureData? item = VM.SelectedItem;
            if (item == null) { return; }
            FileSavePicker sf = new() { SuggestedFileName = "Wallpaper.jpg" };
            sf.FileTypeChoices.Add("JPEG Image(*.jpg)", ["*.jpg"]);
            sf.FileTypeChoices.Add("Bitmap Image(*.bmp)", ["*.bmp"]);
            sf.FileTypeChoices.Add("PNG Image(*.png)", ["*.png"]);
            var result = await sf.PickSaveFileAsync();
            if (result != null)
            {
                using Stream outStream = await result.OpenStreamForWriteAsync();
                using FileStream fileStream = new(item.Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.CopyTo(outStream);
                await new ContentDialog() { Title = "Wallpaper saved", Content = $"Copied image to {result.DisplayName}" }.ShowAsync();
            }
        }

        private void CheckNew(object sender, RoutedEventArgs e)
        {
            VM.CheckNewAsync();
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            VM.DeleteOption(((AppBarButton)sender).CommandParameter.ToString());
        }

        private void Select(object sender, ItemClickEventArgs e)
        {
            VM.SelectOption(((PictureData)e.ClickedItem).Source);

        }

        //[GeneratedRegex("[^0-9]+")]
        //private static partial Regex NotIntegerRegex();
        //private void TxtPreviewQuality_PreviewTextInput(object sender, TextCompositionEventArgs e)
        //{
        //    e.Handled = NotIntegerValidation(e.Text);
        //}
        //private static bool NotIntegerValidation(string text)
        //{
        //    Regex regex = NotIntegerRegex();
        //    return regex.IsMatch(text);
        //}
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
