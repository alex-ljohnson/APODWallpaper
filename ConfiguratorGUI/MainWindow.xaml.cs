using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using APODWallpaper.Utils;
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
        private static readonly SPath PathMax = new() { Fill=new SolidColorBrush(MColor.FromRgb(70, 72, 89)), Data = new GeometryGroup() { Children = { new RectangleGeometry() { Rect = new Rect(0, 0, 8, 8) }, new RectangleGeometry() { Rect = new Rect(2, -2, 8, 8) } } }, Stroke = new SolidColorBrush(MColor.FromRgb(240, 240, 240)) };

        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        private readonly StdOutRedirect redirect;
        public MainWindow()
        {
            InitializeComponent();
            #if DEPENDANT
            MessageBox.Show(Process.GetCurrentProcess().ProcessName);
            #endif  
            redirect = new StdOutRedirect(TxtOutput);
            Console.SetOut(redirect);
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
        private void window_Loaded(object sender, RoutedEventArgs e)
        {
            BtnUpdateTheme_Click(sender, e);
            _=Updater.CheckUpdate(true);

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
            } else
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
                } else if (e.ClickCount == 1)
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
            await APOD.Update(true);
            Console.WriteLine("\nProcess Finished!\n");
        }

        private void BtnSaveImg_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sf = new() { Title = "Save image as...", AddExtension = true, FileName = "Wallpaper.jpg", Filter = "JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp" };
            sf.ShowDialog();
            using FileStream fileStream = new(Utilities.last, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), outStream = (FileStream)sf.OpenFile();
            fileStream.CopyTo(outStream);
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            await Updater.CheckUpdate();
            //MessageBox.Show("This function is not yet available", "Not implemented");
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            Configuration.Config.SetConfiguration(Configuration.DefaultConfiguration);
            BtnUpdateTheme_Click(sender, e);
        }
        private void BtnSelectCurrent_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(Utilities.current))
            {
                APOD.UpdateBackground(Utilities.current, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
            }
        }

        private void BtnSelectLast_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(Utilities.last))
            {
                APOD.UpdateBackground(Utilities.last, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
            }
        }
        private void BtnClearOut_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Clear();
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            APOD.UpdateBackground(APOD.Info["Source"], (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }

        private void BtnUpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri("/Styles/"+CmbConfiguratorTheme.SelectedItem.ToString(), UriKind.Relative);
        }

        private async void BtnCheckNew_Click(object sender, RoutedEventArgs e)
        {
            if (await APOD.CheckNew())
            {
                MessageBox.Show("New image found.", "Downloading image");
                await APOD.Update(true);
                
            } else
            {
                MessageBox.Show("No new image found.", "No new image");
            }
        }
    }
}
