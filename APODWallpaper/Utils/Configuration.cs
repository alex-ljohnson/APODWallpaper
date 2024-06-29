using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace APODWallpaper.Utils
{
    public enum WallpaperStyleEnum
    {
        Centred = 0,
        Tiled = 1,
        Streched = 2,
        Fit = 6,
        Fill = 10,
        Span = 22
    }
    public class Configuration : INotifyPropertyChanged
    {



        public bool isReady = false;

        private readonly string base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        private readonly bool autoSave = false;
        private readonly bool fileTied = true;

        private readonly FileStream fileStream;
        private readonly StreamWriter writer;
        private readonly StreamReader reader;


        private Dictionary<string, dynamic> _configuration = [];
        // To add new setting:
        // copy one of the properties below (change occurances of name and default value)
        // Add to default config

        private string ID { get; init; }
        public bool UseHD { get { return _configuration.GetValueOrDefault(nameof(UseHD), true); } set { _configuration[nameof(UseHD)] = value; AutoSave(); OnPropertyChanged(nameof(UseHD)); } }
        public bool RunStartup { get { return _configuration.GetValueOrDefault(nameof(RunStartup), true); } set { _configuration[nameof(RunStartup)] = value; AutoSave(); OnPropertyChanged(nameof(RunStartup)); ChangeStartup(); } }
        public bool DownloadInfo { get { return _configuration.GetValueOrDefault(nameof(DownloadInfo), false); } set { _configuration[nameof(DownloadInfo)] = value; AutoSave(); OnPropertyChanged(nameof(DownloadInfo)); } }
        public bool ExplainImage { get { return _configuration.GetValueOrDefault(nameof(ExplainImage), false); } set { _configuration[nameof(ExplainImage)] = value; AutoSave(); OnPropertyChanged(nameof(ExplainImage)); } }
        public string BaseUrl { get { return _configuration.GetValueOrDefault(nameof(BaseUrl), "https://api.nasa.gov/planetary/apod"); } set { _configuration[nameof(BaseUrl)] = value; AutoSave(); OnPropertyChanged(nameof(BaseUrl)); } }
        public string ConfiguratorTheme { get { return _configuration.GetValueOrDefault(nameof(ConfiguratorTheme), "Light.xaml"); } set { _configuration[nameof(ConfiguratorTheme)] = value; AutoSave(); OnPropertyChanged(nameof(ConfiguratorTheme)); } }
        public long PreviewQuality { get { return _configuration.GetValueOrDefault(nameof(PreviewQuality), 200); } set { _configuration[nameof(PreviewQuality)] = value; AutoSave(); OnPropertyChanged(nameof(PreviewQuality)); } }
        public long WallpaperStyle { get { return (long)_configuration.GetValueOrDefault(nameof(WallpaperStyle), WallpaperStyleEnum.Fill); } set { _configuration[nameof(WallpaperStyle)] = value; AutoSave(); OnPropertyChanged(nameof(WallpaperStyle)); } }

        public static readonly ReadOnlyCollection<string> DefaultThemes = new(["Light.xaml", "Dark.xaml"]);
        private ObservableCollection<string> availableThemes = new(DefaultThemes);
        public ObservableCollection<string> AvailableThemes
        {
            get => availableThemes; set
            {
                availableThemes = value;
                OnPropertyChanged(nameof(AvailableThemes));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {

            Trace.WriteLine($"Changing {propertyName}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        //public static readonly Configuration DefaultConfiguration = new("Default") { BaseUrl = "https://api.nasa.gov/planetary/apod", UseHD = true, RunStartup = true, ExplainImage = false, DownloadInfo = false,  WallpaperStyle = (long)WallpaperStyleEnum.Fill, ConfiguratorTheme = "Light.xaml" };
        public static readonly Configuration DefaultConfiguration = new("Default", false, false) { BaseUrl = "https://api.nasa.gov/planetary/apod", UseHD = true, RunStartup = true, ExplainImage = false, DownloadInfo = false, WallpaperStyle = (long)WallpaperStyleEnum.Fill, ConfiguratorTheme = "Light.xaml", PreviewQuality = 100 };
        private static readonly object padlock = new();
        private static Configuration? _instance = null;
        public static Configuration Config
        {
            get
            {
                lock (padlock)
                {
                    _instance ??= new Configuration("Config", true, true);
                    return _instance;
                }
            }
        }

        private Configuration(string ID = "None", bool autoSave = true, bool file = true)
        {
            Trace.WriteLine("LOADING CONFIG...");
            this.autoSave = autoSave;
            this.ID = ID;
            fileTied = file;
            var configPath = Utilities.GetDataPath("config.json");
            bool exists = File.Exists(configPath);
            if (!exists) { File.Create(configPath); }
            fileStream = new(configPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true);
            writer = new(fileStream, Encoding.UTF8);
            reader = new(fileStream, Encoding.UTF8);
        }

        public async Task Initialise()
        {
            Trace.WriteLine("Init " + ID);
            await LoadDataAsync(fileTied);
        }

        private async Task LoadDataAsync(bool fromFile = true)
        {
            if (fromFile)
            {
                string jsonString = await reader.ReadToEndAsync();
                _configuration = JsonConvert.DeserializeAnonymousType(jsonString, new Dictionary<string, dynamic>()) ?? [];

            }

            isReady = true;
            Trace.WriteLine($"-- ID: {ID} --");
            foreach (var (key, val) in _configuration)
            {
                Trace.WriteLine($"{key} -> {val}");
            }
            OnPropertyChanged("");

        }

        public void LoadThemes(IEnumerable<string> themes)
        {
            AvailableThemes = new(themes);
        }

        private void AutoSave()
        {
            if (autoSave && ID != "Default")
            {
                SaveConfigAsync();
            }
        }

        public async void SaveConfigAsync()
        {
            string jsonString = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            fileStream.SetLength(0);
            await writer.WriteAsync(jsonString);
            writer.Flush();
            fileStream.Flush();
            Trace.WriteLine($"Save Config: {jsonString}");
        }

        public void ChangeStartup()
        {
            string APOD = Path.Combine(base_path, "APODWallpaper.exe");
            RegistryKey? key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (RunStartup)
            {
                key?.SetValue("APODWallpaper", APOD);
            }
            else
            {
                key?.DeleteValue("APODWallpaper");
            }
        }
        public static bool CheckStartup()
        {
            var reg = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            return reg?.GetValue("APODWallpaper") != null;
        }

        /// <summary>
        /// Set the current configuration, returns itself
        /// </summary>
        public Configuration SetConfiguration(Configuration newConfiguration)
        {
            //foreach (var i in GetType().GetProperties())
            //{
            //    if (i.PropertyType != this.GetType())
            //    {

            //        _configuration[i.Name] = (this, i.GetValue(newConfiguration));
            //    }
            //}
            foreach (var (key, val) in newConfiguration._configuration)
            {
                _configuration[key] = val;
            }
            AutoSave();
            return this;
        }

        public Dictionary<string, dynamic> ToDictionary()
        {
            return _configuration;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(_configuration, Formatting.Indented);
        }
    }
}
