using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using APODWallpaper;
using APODWallpaper.Interfaces;

namespace ConfiguratorGUI
{
    public class ViewModel(IAPODWallpaper apod, IAPODCache cache, IConfigurationService config) : INotifyPropertyChanged
    {

        public static string APODAppVersion { get; } = APODWallpaper.APODWallpaper.Version;
        public static string ConfiguratorAppVersion { get; } = App.AppVersion;

        private DateOnly exploreEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        const int ExploreCount = 12;

        public static string HelpText { get; } = File.ReadAllText("Resources/help.html");

        private Cursor windowCursor = Cursors.Arrow;
        public Cursor WindowCursor
        {
            get => windowCursor;
            set
            {
                windowCursor = value;
                OnPropertyChanged(nameof(WindowCursor));
            }
        }

        private ObservableCollection<PictureData> myPictureData = [];
        public ObservableCollection<PictureData> MyPictureData
        {
            get => myPictureData;
            private set
            {
                myPictureData = value;
                OnPropertyChanged(nameof(MyPictureData));
            }
        }

#pragma warning disable CA1822 // Mark members as static
        public string ItemQuantity { get {
                var (items, size, cacheSize) = GetImagesSize();
                return $"Items: {items}; Storage space: {size / 1048576} MiB; Cache size: {cacheSize / 1024} KiB";
            }
#pragma warning restore CA1822 // Mark members as static
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

        #region Commands
        private ICommand? _deleteCommand;
        public ICommand DeleteCommand
        {
            get
            {
                _deleteCommand ??= new RelayCommand<string>(DeleteOption, (s) => true);
                return _deleteCommand;
            }
            set
            {
                _deleteCommand = value;
            }
        }

        private ICommand? _selectCommand;
        public ICommand SelectCommand
        {
            get
            {
                _selectCommand ??= new RelayCommand<string>(SelectOption, (s) => true);
                return _selectCommand;
            }
            set
            {
                _selectCommand = value;
            }
        }

        private ICommand? _checkNewCommand;
        public ICommand CheckNewCommand
        {
            get
            {
                _checkNewCommand ??= new RelayCommand<object>(CheckNew, (s) => true);
                return _checkNewCommand;
            }
            set
            {
                _checkNewCommand = value;
            }
        }

        private ICommand? _saveImgCommand;
        public ICommand SaveImgCommand
        {
            get
            {
                _saveImgCommand ??= new RelayCommand<PictureData?>(SaveImage, (s) => true);
                return _saveImgCommand;
            }
            set
            {
                _saveImgCommand = value;
            }
        }
        private ICommand? _descriptionCommand;
        public ICommand DescriptionCommand
        {
            get
            {
                _descriptionCommand ??= new RelayCommand<PictureData>(ReadDescription, (s) => true);
                return _descriptionCommand;
            }
            set
            {
                _descriptionCommand = value;
            }
        }
        private ICommand? _downloadCommand;
        public ICommand DownloadCommand
        {
            get
            {
                _downloadCommand ??= new RelayCommand<APODInfo>(SaveExplore, (s) => true);
                return _downloadCommand;
            }
            set
            {
                _downloadCommand = value;
            }
        }

        private ICommand? _viewContentCommand;
        public ICommand ViewContentCommand
        {
            get
            {
                _viewContentCommand ??= new RelayCommand<APODInfo>(async (data) =>
                {
                    if (data == null) return;
                    var uri = data.GetPreferredUri(config.UseHD);
                    MessageBox.Show($"{data.Explanation}\n\nCopyright: {data.Copyright}\n\nPress OK to open content in browser...", $"{data.Title} - {data.DateFormatted}");
                    if (uri == null) return;
                    Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
                }, (s) => true);
                return _viewContentCommand;
            }
            set
            {
                _viewContentCommand = value;
            }
        }
        private ICommand? _nextCommand;
        public ICommand NextCommand
        {
            get
            {
                _nextCommand ??= new RelayCommand(ExploreNext, (s) => true);
                return _nextCommand;
            }
            set
            {
                _nextCommand = value;
            }
        }
        private ICommand? _prevCommand;
        public ICommand PrevCommand
        {
            get
            {
                _prevCommand ??= new RelayCommand(ExplorePrev, (s) => true);
                return _prevCommand;
            }
            set
            {
                _prevCommand = value;
            }
        }

        private ICommand? _randCommand;
        public ICommand RandCommand
        {
            get
            {
                _randCommand ??= new RelayCommand(ExploreRandom, (s) => true);
                return _randCommand;
            }
            set
            {
                _randCommand = value;
            }
        }
        private async void SaveExplore(APODInfo? data)
        {
            if (data == null) return;
            if (MyPictureData.Any(x => x.Equals(data))) { MessageBox.Show("Image was previously saved!", "Already saved"); return; }
            PictureData? pictureData;
            Task<PictureData?>? downloadTask = default;
            try { 
            
                downloadTask = apod.DownloadImageAsync(data);
            } catch (NotImageException)
            {
                return;
            }
            ExploreData.Remove(data);
            try
            {
                pictureData = await downloadTask;
                if (pictureData == null) return;
                MyPictureData.Insert(0, pictureData);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            SortData();
        }
        private async Task LoadExplore()
        {
            var exploreStart = exploreEnd.AddDays(-ExploreCount + 1);
            var data = await cache.GetRangeAsync(exploreStart, exploreEnd);
            var filteredData = data?.Where(x => x.GetPreferredUri(config.UseHD) != null);
            if (filteredData != null)
                ExploreData = new(filteredData);
        }
        private async void ExploreNext()
        {
            Trace.WriteLine("Loading next...");
            if (exploreEnd.AddDays(ExploreCount) <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1))
            {
                WindowCursor = Cursors.Wait;
                exploreEnd = exploreEnd.AddDays(ExploreCount);
                await LoadExplore();
                WindowCursor = Cursors.Arrow;
            }
        }
        public async void ExplorePrev()
        {
            Trace.WriteLine("Loading prev...");
            if (exploreEnd.AddDays(-ExploreCount) >= DateOnly.ParseExact("1995-06-16", "yyyy-MM-dd"))
            {
                WindowCursor = Cursors.Wait;
                exploreEnd = exploreEnd.AddDays(-ExploreCount);
                await LoadExplore();
                WindowCursor = Cursors.Arrow;
            }
        }
        public async void ExploreRandom()
        {
            Trace.WriteLine("Loading random...");
            WindowCursor = Cursors.Wait;
            var data = await cache.FetchRandAsync(ExploreCount);
            if (data != null) ExploreData = new(data);
            
            WindowCursor = Cursors.Arrow;
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
            apod.UpdateBackground(source, (WallpaperStyleEnum)config.WallpaperStyle);
        }
        public async void CheckNew(object? param)
        {
            if (apod.CheckNew())
            {
                MessageBox.Show("New image found.", "Downloading image");
                PictureData? newData = default;
                try
                {
                    newData = await apod.UpdateAsync(true);
                } catch (NotImageException ex)
                {
                    MessageBox.Show(ex.Message, "APOD isn't an image");
                }
                if (newData != null)
                {
                    MyPictureData.Insert(0, newData);
                }
            }
            else
            {
                MessageBox.Show("No new image found.", "No new image");
            }
        }
        public void SaveImage(PictureData? param)
        {
            Trace.WriteLine("Saving image");
            PictureData? item = param ?? SelectedItem;
            if (item == null) { return; }
            SaveFileDialog sf = new() { Title = "Save image as...", AddExtension = true, FileName = "Wallpaper.jpg", Filter = "JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp|PNG Image (*.png)|*.png" };
            bool? result = sf.ShowDialog();
            if (result != null && (bool)result)
            {
                using FileStream fileStream = new(item.Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), outStream = (FileStream)sf.OpenFile();
                fileStream.CopyTo(outStream);
                MessageBox.Show($"Copied image to {outStream.Name}", "Wallpaper saved");
            }
        }

        public void ReadDescription(PictureData? param)
        {
            if (param == null) { return; }
            MessageBox.Show(param?.Description, $"Description of {param?.Name}");
        }
        #endregion

        private async Task<PictureData?> LoadItemAsync(string itemPath)
        {
            if (!itemPath.EndsWith(".json")) return null;
            var startTime = DateTime.UtcNow;

            PictureData? data = null;
            try
            {
                using FileStream fs = new(itemPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                using StreamReader sr = new(fs);
                var json = await sr.ReadToEndAsync().ConfigureAwait(false);
                
                // Run synchronous/CPU-bound deserialization on the thread pool
                data = await Task.Run(() => JsonConvert.DeserializeObject<PictureData>(json)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Deserialize failed for {itemPath}: {ex.Message}");
            }

            var endTime = DateTime.UtcNow;
            Trace.WriteLine(itemPath + $" is now done; Total load time: {(endTime - startTime).TotalMilliseconds}ms;");
            return data;
        }

        public async Task<PictureData[]> LoadData()
        {
            var imagesPath = Utilities.GetDataPath("images");
            Directory.CreateDirectory(imagesPath);
            string[] files = [.. Directory.EnumerateFiles(imagesPath).Where(x => x.EndsWith(".json"))];
            
            var tasks = files.Select(LoadItemAsync);
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            
            return [.. results.Where(x => x != null).Select(x => x!)];
        }

        private static (int, long, long) GetImagesSize()
        {
            var imagesPath = Utilities.GetDataPath("images");
            var files = Directory.GetFiles(imagesPath);
            int c = 0;
            long size = 0;
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Exists)
                {
                    size += fileInfo.Length;
                    c++;
                }
            }
            var cachePath = Utilities.GetDataPath("cache/metadata.cache");
            var cacheInfo = new FileInfo(cachePath);
            
            return (c/2, size, cacheInfo.Length);
        }

        private void SortData()
        {
            MyPictureData = new ObservableCollection<PictureData>(MyPictureData.OrderByDescending(x => x));
        }

        public async Task Initialise()
        {
            var startTime = DateTime.UtcNow;
            
            var exploreTask = LoadExplore();
            var loadTask = LoadData();
            var taskInitTime = DateTime.UtcNow;
            await Task.WhenAll(exploreTask, loadTask);
            var data = await loadTask;

            // Since this is the initial load, setting the property fires NotifyPropertyChanged once
            MyPictureData = new ObservableCollection<PictureData>(data.OrderByDescending(x => x));
            
            var endTime = DateTime.UtcNow;
            Trace.WriteLine($"Initialisation times: Total: {(endTime - startTime).TotalMilliseconds}ms; Task spinup: {(taskInitTime - startTime).TotalMilliseconds}ms");
        }

        //public MainViewModel(IUpdateService updateSvc, IImageLoader imgLoader)
        //{
        //    _updateService = updateSvc;
        //    _imageLoader = imgLoader;

        //    // Start non-blocking initialization
        //    _ = InitializeAsync();
        //}

        //private async Task InitializeAsync()
        //{
        //    // 1. Parallel start for speed
        //    var updateTask = _updateService.CheckForUpdatesAsync();
        //    var imagesTask = _imageLoader.LoadAllAsync();

        //    await Task.WhenAll(updateTask, imagesTask);

        //    // 2. Update UI properties safely via DataBinding
        //}
    }
}

