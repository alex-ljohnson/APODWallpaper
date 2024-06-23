using APODWallpaper.Utils;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace APODConfiguratorNeo
{
    internal class ViewModel : INotifyPropertyChanged
    {
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;

        private DateOnly exploreEnd = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);

        const int ExploreCount = 12;

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

        private ObservableCollection<APODInfo> exploreData = [];
        public ObservableCollection<APODInfo> ExploreData
        {
            get => exploreData;
            private set
            {
                exploreData = value;
                OnPropertyChanged(nameof(ExploreData));
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

        private APODInfo? exploreSelected;
        public APODInfo? ExploreSelected
        {
            get => exploreSelected;
            set
            {
                if (exploreSelected != value)
                {
                    exploreSelected = value;
                    OnPropertyChanged(nameof(ExploreSelected));
                }
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public async void SaveExploreAsync(APODInfo? data)
        {
            if (data == null) return;
            if (MyPictureData.Any(x => x.Equals(data)))
            {
                await new ContentDialog() { Title = "Already saved", Content = "Image was previously saved!" }.ShowAsync();
                return;
            }
            PictureData pictureData;
            var task = APOD.DownloadImageAsync(data);
            ExploreData.Remove(data);
            try
            {
                pictureData = await task;
                MyPictureData.Insert(0, pictureData);
            }
            catch (Exception)
            {
            }
            _ = SortDataAsync();
        }
        private async Task LoadExplore()
        {
            ExploreData = new(await APOD.GetInfoAsync(exploreEnd, ExploreCount));
        }
        public async void ExploreNext()
        {
            Trace.WriteLine("Loading next...");
            if (exploreEnd.AddDays(ExploreCount) <= DateOnly.FromDateTime(DateTime.Now).AddDays(-1))
            {
                exploreEnd = exploreEnd.AddDays(ExploreCount);
                await LoadExplore();
            }
        }
        public async void ExplorePrev()
        {
            Trace.WriteLine("Loading prev...");
            if (exploreEnd.AddDays(-ExploreCount) >= DateOnly.ParseExact("1995-06-16", "yyyy-MM-dd"))
            {
                exploreEnd = exploreEnd.AddDays(-ExploreCount);
                await LoadExplore();
            }
        }
        public void DeleteOption(string? source)
        {
            if (source == null) { return; }
            MyPictureData.Remove(MyPictureData.First(x => x.Source == source));
            File.Delete(source);
            File.Delete(source + ".json");
        }
        public void SelectOption(string? source)
        {
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
            var explore = LoadExplore();
            var load = LoadData();
            var sort = SortDataAsync(load);
            if (load.Length > 0)
            {
                await Task.WhenAny(load);
            }
        }

        public ViewModel()
        {

        }

    }
}
