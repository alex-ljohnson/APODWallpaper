using APODWallpaper.Utils;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MColor = System.Windows.Media.Color;
using SPath = System.Windows.Shapes.Path;
namespace ConfiguratorGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly SPath PathNotMax = new() { Data = new RectangleGeometry() { Rect = new Rect(0, 0, 8, 8) }, Stroke = new SolidColorBrush(MColor.FromRgb(240, 240, 240)) };
        private static readonly SPath PathMax = new() { Fill = new SolidColorBrush(MColor.FromRgb(70, 72, 89)), Data = new GeometryGroup() { Children = { new RectangleGeometry() { Rect = new Rect(0, 0, 8, 8) }, new RectangleGeometry() { Rect = new Rect(2, -2, 8, 8) } } }, Stroke = new SolidColorBrush(MColor.FromRgb(240, 240, 240)) };

        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        private readonly ViewModel VM;
        private readonly StdOutRedirect redirect;
        public MainWindow()
        {
            InitializeComponent();

#if DEPENDANT
            MessageBox.Show(Process.GetCurrentProcess().ProcessName);
#endif
            redirect = new StdOutRedirect(TxtOutput);
            Console.SetOut(redirect);
            VM = (ViewModel)DataContext;
        }

        #region Window Control
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool closeBoardComplete = false;
        private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Storyboard closeStory = (Storyboard)FindResource("closeAnim");
            if (!closeBoardComplete)
            {
                closeStory.Begin();
                e.Cancel = true;
            }
        }
        private async void window_Loaded(object sender, RoutedEventArgs e)
        {
            BtnUpdateTheme_Click(sender, e);
            await VM.Initialise();
            _ = Updater.CheckUpdate(true);
            Trace.WriteLine("\nWINDOW LOADED\n");

        }

        private void CloseStoryboard_Completed(object sender, EventArgs e)
        {
            closeBoardComplete = true;
            Close();
        }

        private void BtnMinimise_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximise_Click(object sender, RoutedEventArgs e)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                BtnMaximise.Content = PathNotMax;
            }
            else
            {
                WindowState = WindowState.Maximized;
                BtnMaximise.Content = PathMax;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    BtnMaximise_Click(sender, e);
                }
                else if (e.ClickCount == 1)
                {
                    DragMove();
                }
            }
        }
        #endregion

        async private void BtnForceRun_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Clear();
            OutputTab.Focus();
            try
            {
                await APOD.UpdateAsync(true);
            } catch (NotImageException ex)
            {
                MessageBox.Show(ex.Message, "APOD isn't an image");
                return;
            }
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
            TxtOutput.Clear();
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
            await ((App)Application.Current).SetTheme();
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NotIntegerRegex();
        private void TxtPreviewQuality_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NotIntegerValidation(e.Text);
        }
        private static bool NotIntegerValidation(string text)
        {
            Regex regex = NotIntegerRegex();
            return regex.IsMatch(text);
        }
        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (NotIntegerValidation(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
