using APODWallpaper.Utils;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Windows.Input;
using System.Net.Http;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConfiguratorGUI
{
    internal class ViewModel : INotifyPropertyChanged
    {
        //private static readonly HttpClient client = new();
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        public ObservableCollection<PictureData> MyPictureData { get; set; } = [];

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
#endregion
        public async void CheckNew(object? param)
        {
            if (await APOD.CheckNew())
            {
                MessageBox.Show("New image found.", "Downloading image");
                var newData = await APOD.Update(true);
                if (newData != null )
                {
                    MyPictureData.Insert(0, new PictureData(newData));
                }
            }
            else
            {
                MessageBox.Show("No new image found.", "No new image");
            }
        }
        private async Task LoadItemAsync(string itemPath)
        {
            Trace.WriteLine(itemPath);
            if (itemPath.EndsWith(".json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(itemPath);
                    var data = JsonConvert.DeserializeObject<PictureData>(json);
                    if (data != null)
                    {
                        MyPictureData.Add(data);
                    }
                }
                catch
                {

                }
            }
        }
        public void LoadData()
        {
            var imagesPath = Utilities.GetDataPath("images");
            Directory.CreateDirectory(imagesPath);
            foreach (var item in Directory.EnumerateFiles(imagesPath))
            {
                _ = LoadItemAsync(item);
            }

        }

        public ViewModel() 
        {
            LoadData();
        }
    
    }
}
