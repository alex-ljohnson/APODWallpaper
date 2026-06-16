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
    public class Configuration : INotifyPropertyChanged, IDisposable, Interfaces.IConfigurationService
    {
        private static readonly HashSet<string> openConfigs = [];

        public bool isReady = false;

        private readonly string base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        private readonly bool autoSave = false;
        private readonly bool fileTied = true;

        private readonly FileStream fileStream;
        private readonly StreamWriter writer;
        private readonly StreamReader reader;


        private readonly SemaphoreSlim _saveLock = new(1, 1);
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
        
        private const long MinNetworkTimeoutSeconds = 1L;
        public long NetworkTimeout { get { return Math.Max((long)_configuration.GetValueOrDefault(nameof(NetworkTimeout), 20L), MinNetworkTimeoutSeconds); } set { _configuration[nameof(NetworkTimeout)] = value; AutoSave(); OnPropertyChanged(nameof(NetworkTimeout)); } }
        public long PreviewQuality { get { return _configuration.GetValueOrDefault(nameof(PreviewQuality), 200L); } set { 
                _configuration[nameof(PreviewQuality)] = value; AutoSave(); OnPropertyChanged(nameof(PreviewQuality)); } }
        public long WallpaperStyle { get { return (long)_configuration.GetValueOrDefault(nameof(WallpaperStyle), WallpaperStyleEnum.Fill); } set { _configuration[nameof(WallpaperStyle)] = value; AutoSave(); OnPropertyChanged(nameof(WallpaperStyle)); } }
        public string API_KEY { get { return _configuration.GetValueOrDefault(nameof(API_KEY), "5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ"); } set { _configuration[nameof(API_KEY)] = value; AutoSave(); OnPropertyChanged(nameof(API_KEY)); } }

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

        public static readonly Configuration DefaultConfiguration = new("Default", false, false) { BaseUrl = "https://api.nasa.gov/planetary/apod", UseHD = true, RunStartup = true, ExplainImage = false, DownloadInfo = false, WallpaperStyle = (int)WallpaperStyleEnum.Fill, ConfiguratorTheme = "Light.xaml", PreviewQuality = 100, API_KEY= "5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ", NetworkTimeout= 10L};
        
        public Configuration(string ID = "None", bool autoSave = true, bool file = true)
        {
            Trace.WriteLine("LOADING CONFIG...");
            if (openConfigs.Contains(ID))
            {
                throw new Exception($"Config with ID {ID} already open");
            }
            openConfigs.Add(ID);
            this.autoSave = autoSave;
            this.ID = ID;
            fileTied = file;
            var configPath = Utilities.GetDataPath($"{ID.ToLower()}.json");
            bool exists = File.Exists(configPath);
            if (!exists) { File.Create(configPath); }
            try
            {
                fileStream = new(configPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, true);
                writer = new(fileStream, Encoding.UTF8);
                reader = new(fileStream, Encoding.UTF8);
            }
            catch
            {
                openConfigs.Remove(ID);
                throw;
            }
        }

        public async Task InitialiseAsync()
        {
            Trace.WriteLine("Init " + ID);
            await LoadDataAsync(fileTied);
            isReady = true;
        }

        private async Task LoadDataAsync(bool fromFile = true)
        {
            if (fromFile)
            {
                string jsonString = await reader.ReadToEndAsync();
                _configuration = JsonConvert.DeserializeAnonymousType(jsonString, new Dictionary<string, dynamic>()) ?? [];

            }

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
            if (autoSave && fileTied)
            {
                _ = SaveConfigAsync();
            }
        }

        public async Task SaveConfigAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                string jsonString = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                fileStream.SetLength(0);
                fileStream.Position = 0;
                await writer.WriteAsync(jsonString);
                await writer.FlushAsync();
                Trace.WriteLine($"Save Config: {jsonString}");
            }
            finally
            {
                _saveLock.Release();
            }
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
        public static bool CheckStartupSet()
        {
            var reg = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            return reg?.GetValue("APODWallpaper") != null;
        }

        /// <summary>
        /// Set the current configuration, returns itself
        /// </summary>
        public Configuration CopyConfiguration(Configuration newConfiguration)
        {
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

        public void Dispose()
        {
            writer.Flush();
            writer.Close();
            writer?.Dispose();
            reader.Close();
            reader.Dispose();
            fileStream.Flush();
            fileStream.Close();
            fileStream?.Dispose();
            openConfigs.Remove(ID);
            _saveLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
