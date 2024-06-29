using APODWallpaper.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace ConfiguratorGUI
{
    internal class ViewModel : INotifyPropertyChanged
    {
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;

        private DateOnly exploreEnd = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);

        const int ExploreCount = 12;

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
                _selectCommand = value;
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

        private async void SaveExplore(APODInfo? data)
        {
            if (data == null) return;
            if (MyPictureData.Any(x => x.Equals(data))) { MessageBox.Show("Image was previously saved!", "Already saved"); return; }
            PictureData pictureData;
            Task<PictureData>? task = default;
            try { 
            
                task = APOD.DownloadImageAsync(data);
            } catch (NotImageException ex)
            {
                MessageBox.Show(ex.Message, "APOD is not an image");
                return;
            }
            ExploreData.Remove(data);
            try
            {
                pictureData = await task;
                MyPictureData.Insert(0, pictureData);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            _ = SortDataAsync();
        }
        private async Task LoadExplore()
        {
            ExploreData = new(await APOD.GetInfoAsync(exploreEnd, ExploreCount));
        }
        private async void ExploreNext()
        {
            Trace.WriteLine("Loading next...");
            if (exploreEnd.AddDays(ExploreCount) <= DateOnly.FromDateTime(DateTime.Now).AddDays(-1))
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
        public async void CheckNew(object? param)
        {
            if (await APOD.CheckNewAsync())
            {
                MessageBox.Show("New image found.", "Downloading image");
                PictureData? newData = default;
                try
                {
                    newData = await APOD.UpdateAsync(true);
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
