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
        private ObservableCollection<PictureData> pictureData = [];
        public ObservableCollection<PictureData> MyPictureData
        {
            get => pictureData;
            set
            {
                pictureData = value;
                OnPropertyChanged(nameof(MyPictureData));
            }
        }

        private PictureData? selectedItem;
        public PictureData? SelectedItem
        {
            get { return selectedItem; }
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
                _saveImgCommand ??= new RelayCommand<PictureData?>(SaveImage, (s)=> true);
                return _saveImgCommand;
            } set
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
            if (await APOD.CheckNew())
            {
                MessageBox.Show("New image found.", "Downloading image");
                var newData = await APOD.Update(true);
                if (newData != null)
                {
                    MyPictureData.Insert(0, new PictureData(newData));
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

        private async Task SortDataAsync(IEnumerable<Task> tasks)
        {
            Trace.WriteLine("Awaiting all items loaded");
            await Task.WhenAll(tasks);
            MyPictureData = new ObservableCollection<PictureData>(MyPictureData.OrderDescending());

        }
        public async Task Initialise()
        {
            var load = LoadData();
            var sort = SortDataAsync(load);
            await Task.WhenAny(load);
        }

        public ViewModel()
        {

        }

    }
}
