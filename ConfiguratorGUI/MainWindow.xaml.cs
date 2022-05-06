using System;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Markup;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
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
        private static readonly string base_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        private static readonly SPath PathNotMax = new() { Data = new RectangleGeometry() { Rect = new Rect(0, 0, 8, 8) }, Stroke = new SolidColorBrush(MColor.FromRgb(240, 240, 240)) };
        private static readonly SPath PathMax = new() { Fill=new SolidColorBrush(MColor.FromRgb(70, 72, 89)), Data = new GeometryGroup() { Children = { new RectangleGeometry() { Rect = new Rect(0, 0, 8, 8) }, new RectangleGeometry() { Rect = new Rect(2, -2, 8, 8) } } }, Stroke = new SolidColorBrush(MColor.FromRgb(240, 240, 240)) };
        //public Configuration Config = new(true);
        StdOutRedirect redirect;
        private APODWallpaper.APODWallpaper APOD = new();
        public List<string> AvailableThemes { get; set; } = new() { "Light.xaml", "Dark.xaml" };
        public MainWindow()
        {
            foreach (string i in AvailableThemes) { Trace.WriteLine(i); }   
            InitializeComponent();
            DataContext = Configuration.Config;
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
            try
            {
                BitmapFrame source = BitmapFrame.Create(new FileStream(Path.GetFullPath("data\\current.jpg", base_path), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                ImgCurrent.Source = source;
            }
            catch (FileNotFoundException)
            {
                ImgCurrent.Source = null;
            }
            try
            {

                BitmapFrame source = BitmapFrame.Create(new FileStream(Path.GetFullPath("data\\last.jpg", base_path), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                ImgLast.Source = source;
            }
            catch (FileNotFoundException)
            {
                ImgLast.Source = null;
            }
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
            using FileStream fileStream = new(Path.GetFullPath("data\\current.jpg"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite), outStream = (FileStream)sf.OpenFile();
            fileStream.CopyTo(outStream);
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This function is not yet available", "Not implemented");
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine(Configuration.Config.ToString());
            Configuration.Config.SetConfiguration(Configuration.DefaultConfiguration);
            BtnUpdateTheme_Click(sender, e);
            Trace.WriteLine(Configuration.Config.ToString());
        }
        private void BtnSelectCurrent_Click(object sender, RoutedEventArgs e)
        {
            string file = Path.GetFullPath("data\\current.jpg", base_path);
            if (File.Exists(file))
            {
                APOD.UpdateBackground(file, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
            }
        }

        private void BtnSelectLast_Click(object sender, RoutedEventArgs e)
        {
            string file = Path.GetFullPath("data\\last.jpg", base_path);
            if (File.Exists(file))
            {
                APOD.UpdateBackground(file, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
            }
        }
        private void BtnClearOut_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Clear();
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            APOD.UpdateBackground(null, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }

        private void BtnUpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri("/Styles/"+CmbConfiguratorTheme.SelectedItem.ToString(), UriKind.Relative);
        }
    }
}
