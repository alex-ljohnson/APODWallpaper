using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ImageManager : Page, INotifyPropertyChanged
    {
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;

        private ObservableCollection<PictureData> pictureData = [];
        public ObservableCollection<PictureData> MyPictureData
        {
            get => pictureData;
            private set
            {
                pictureData = value;
                OnPropertyChanged(nameof(MyPictureData));
            }
        }

        private PictureData? selectedItem;
        public PictureData? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (selectedItem != value)
                {
                    selectedItem = value;
                    OnPropertyChanged(nameof(SelectedItem));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ImageManager()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await Initialise();
            base.OnNavigatedTo(e);
        }

        async private void Description(object sender, RoutedEventArgs e)
        {
            var param = SelectedItem;
            await new ContentDialog() { Content = param?.Description, Title = $"Description of {param?.Name}" }.ShowAsync();
        }

        async private void SaveImage(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("Saving image");
            PictureData? item = SelectedItem;
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

        public void DeleteOption(string? source)
        {
            if (source == null) { return; }
            MyPictureData.Remove(MyPictureData.First(x => x.Source == source));
            File.Delete(source);
            File.Delete(source + ".json");
        }
        public void SelectOption(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as PictureData;
            var source = item?.Source;
            if (source == null) { return; }
            APOD.UpdateBackground(source, (WallpaperStyleEnum)Configuration.Config.WallpaperStyle);
        }
        public async void CheckNewAsync()
        {
            if (await APOD.CheckNewAsync())
            {
                await new ContentDialog() { Title = "Downloading Image", Content = "New image found" }.ShowAsync();
                var newData = await APOD.UpdateAsync(true);
                if (newData != null)
                {
                    MyPictureData.Insert(0, newData);
                }
            }
            else
            {
                await new ContentDialog() { Title = "Image up to date", Content = "No new image found" }.ShowAsync();
            }
        }

        private async Task LoadItemAsync(string itemPath)
        {
            if (!itemPath.EndsWith(".json")) return;
            using StreamReader sr = new(itemPath);
            Trace.WriteLine(itemPath + " before read");
            var json = await sr.ReadToEndAsync();
            Trace.WriteLine(itemPath + " is now done");
            var data = JsonConvert.DeserializeObject<PictureData>(json);
            if (data != null)
            {
                MyPictureData.Add(data);
            }
        }

        public Task[] LoadData()
        {
            var imagesPath = Utilities.GetDataPath("images");
            Directory.CreateDirectory(imagesPath);
            string[] files = Directory.EnumerateFiles(imagesPath).Where(x => x.EndsWith(".json")).ToArray();
            var tasks = files.Select(LoadItemAsync).ToArray();
            return tasks;

        }

        private async Task SortDataAsync(IEnumerable<Task>? tasks = null)
        {
            if (tasks != null && tasks.Any())
            {
                Trace.WriteLine("Awaiting all items loaded");
                await Task.WhenAll(tasks);
            }
            MyPictureData = new ObservableCollection<PictureData>(MyPictureData.OrderDescending());

        }

        public async Task Initialise()
        {
            var load = LoadData();
            var sort = SortDataAsync(load);
            if (load.Length > 0)
            {
                await Task.WhenAny(load);
            }
        }

        private void BtnCheckNew_Click(object sender, RoutedEventArgs e)
        {
            CheckNewAsync();
        }
    }
}
