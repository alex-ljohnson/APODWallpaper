using APODWallpaper.Utils;
using System.Collections.ObjectModel;

namespace APODWallpaper.Interfaces
{
    public interface IConfigurationService
    {
        Task InitialiseAsync();

        bool UseHD { get; set; }
        bool RunStartup { get; set; }
        bool DownloadInfo { get; set; }
        bool ExplainImage { get; set; }
        string BaseUrl { get; set; }
        string ConfiguratorTheme { get; set; }
        long NetworkTimeout { get; set; }
        long PreviewQuality { get; set; }
        long WallpaperStyle { get; set; }
        string API_KEY { get; set; }

        ObservableCollection<string> AvailableThemes { get; }

        void LoadThemes(IEnumerable<string> themes);
        void ChangeStartup();
    }
}
