using APODWallpaper.Utils;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Windows.Input;
using System.Net.Http;
using System.Windows;

namespace ConfiguratorGUI
{
    internal class ViewModel
    {
        private static readonly string base_path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;

        private static readonly HttpClient client = new();
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        public ObservableCollection<PictureData> MyPictureData { get; set; } = [];

        private ICommand _selectCommand;
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
        private ICommand _checkNewCommand;
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
                if (newData != null )
                {
                    MyPictureData.Add(new PictureData(newData));
                }
            }
            else
            {
                MessageBox.Show("No new image found.", "No new image");
            }
        }
        public ViewModel() 
        {
            foreach (var item in Directory.EnumerateFiles(Utilities.GetDataPath("images")))
            {
                if (item.EndsWith(".json"))
                {
                    try
                    {
                        var json = File.ReadAllText(item);
                        var data = JsonConvert.DeserializeObject<PictureData>(json);
                        if (data != null)
                        {
                            MyPictureData.Add(data);
                        }
                    } catch
                    {
                    
                    }
                }
            }
            Debug.Write(MyPictureData);
        }
    
    }
}
