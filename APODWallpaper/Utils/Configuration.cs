using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.ComponentModel;
using System.Collections.ObjectModel;

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

        public static readonly Configuration DefaultConfiguration = new(true) { BaseUrl = "https://api.nasa.gov/planetary/apod", UseHD = true, ExplainImage = false, DownloadInfo = false,  WallpaperStyle = (long)WallpaperStyleEnum.Fill };
        
        private static readonly object padlock = new();
        private static Configuration? _instance = null;
        public static Configuration Config
        {
            get
            {
                lock (padlock)
                {
                    _instance ??= new Configuration();
                    return _instance;
                }
            }
        }

        private readonly string base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        private readonly bool autoSave = false;
        private readonly string config_path;

        private Dictionary<string, dynamic> _configuration;
        // To add new setting:
        // copy one of the properties below (change occurances of name and default value)
        // Add to default config
        public bool UseHD { get { return _configuration.GetValueOrDefault(nameof(UseHD), true); } set { _configuration[nameof(UseHD)] = value; AutoSave(); OnPropertyChanged(nameof(UseHD)); } }
        public bool DownloadInfo { get { return _configuration.GetValueOrDefault(nameof(DownloadInfo), false); } set { _configuration[nameof(DownloadInfo)] = value; AutoSave(); OnPropertyChanged(nameof(DownloadInfo)); } }
        public bool ExplainImage { get { return _configuration.GetValueOrDefault(nameof(ExplainImage), false); } set { _configuration[nameof(ExplainImage)] = value; AutoSave(); OnPropertyChanged(nameof(ExplainImage)); } }
        public string BaseUrl { get { return _configuration.GetValueOrDefault(nameof(BaseUrl), "https://api.nasa.gov/planetary/apod/"); } set { _configuration[nameof(BaseUrl)] = value; AutoSave(); OnPropertyChanged(nameof(BaseUrl)); } }
        public string ConfiguratorTheme { get { return _configuration.GetValueOrDefault(nameof(ConfiguratorTheme), "Light.xaml"); } set { _configuration[nameof(ConfiguratorTheme)] = value; AutoSave(); OnPropertyChanged(nameof(ConfiguratorTheme)); } }
        public long WallpaperStyle { get { return (long)_configuration.GetValueOrDefault(nameof(WallpaperStyle), WallpaperStyleEnum.Fill); } set { _configuration[nameof(WallpaperStyle)] = value; AutoSave(); OnPropertyChanged(nameof(WallpaperStyle)); } }
        public ObservableCollection<string> AvailableThemes { get; set; } = ["Light.xaml", "Dark.xaml"];

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            Trace.WriteLine($"Changing {propertyName}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private Configuration(bool autoSave = true)
        {
            this.autoSave = autoSave;
            config_path = Utilities.GetDataPath("config.json");
            if (File.Exists(config_path) == false) 
            {   
                _configuration = new Dictionary<string, dynamic>();
                foreach (var i in GetType().GetProperties())
                {
                    _configuration[i.Name] = i.GetValue(this)!;
                }
                File.WriteAllText(config_path, JsonConvert.SerializeObject(_configuration, Formatting.Indented));
            }
            else
            {
                string jsonString = File.ReadAllText(config_path);  
                _configuration = JsonConvert.DeserializeAnonymousType(jsonString, new Dictionary<string, dynamic>())!;
            }
            Trace.WriteLine("_Loaded config;;");
            foreach (var (key, val) in _configuration)
            {
                Trace.WriteLine($"{key} -> {val}");
            }
        }

        async private void AutoSave()
        {
            if (autoSave)
            {
                await SaveConfigAsync();
            }
        }

        async public Task SaveConfigAsync()
        {
            string jsonString = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
            try
            {
                await File.WriteAllTextAsync(config_path, jsonString);
            } catch (IOException)
            {

            }
        }

        /// <summary>
        /// Set the current configuration, returns itself
        /// </summary>
        public Configuration SetConfiguration(Configuration newConfiguration)
        {
            foreach (var i in GetType().GetProperties())
            {
                if (i.PropertyType != this.GetType())
                {
                    i.SetValue(this, i.GetValue(newConfiguration));
                }
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
