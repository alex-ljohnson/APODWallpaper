using APODWallpaper;
using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        private static readonly HttpClient client = new();
        APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        public ObservableCollection<PictureData> MyPictureData { get; set; } = [];
        public MainWindow()
        {
            this.InitializeComponent();
        }
        public void SelectOption(string? source)
        {
            if (source == null) { return; }
            APOD.UpdateBackground(source, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }
        async private void CheckNew(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new() { CloseButtonText = "Ok." };
            if (await APOD.CheckNew())
            {
                dialog.Content = "New image found.";
                dialog.Title = "Downloading image";
                var newData = await APOD.Update(true);
                if (newData != null)
                {
                    MyPictureData.Insert(0, new PictureData(newData));
                }
            }
            else
            {
                dialog.Content = "No new image found.";
                dialog.Title = "No new image";
            }
            await dialog.ShowAsync();
        }
        async private void BtnForceRun_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Text = "";
            OutputTab.Focus(FocusState.Programmatic);
            await APOD.Update(true);
            Console.WriteLine("\nProcess Finished!\n");
        }

        private async void BtnSaveImg_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker sf = new()
            {
                SuggestedFileName = "Wallpaper.jpg"
            };
            sf.FileTypeChoices.Add("JPEG Image (*.jpg)", ["*.jpg"]);
            sf.FileTypeChoices.Add("Bitmap Image (*.bmp)", ["*.bmp"]);
            var file = await sf.PickSaveFileAsync();
            using FileStream fileStream = new(Utilities.last, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.CopyTo(await file.OpenStreamForWriteAsync());
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
            TxtOutput.Text = "";
        }

        private void BtnStyleChange_Click(object sender, RoutedEventArgs e)
        {
            APOD.UpdateBackground(APOD.Info["Source"], (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }

        private void BtnUpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri("/Styles/" + CmbConfiguratorTheme.SelectedItem.ToString(), UriKind.Relative);
        }

    }
}
