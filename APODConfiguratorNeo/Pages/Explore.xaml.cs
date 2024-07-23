using APODWallpaper.Utils;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Explore : Page, INotifyPropertyChanged
    {
        private APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;
        private DateOnly exploreEnd = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);

        const int ExploreCount = 12;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

        private ObservableCollection<APODInfo> exploreData = [];
        public ObservableCollection<APODInfo> ExploreData
        {
            get => exploreData;
            set
            {
                exploreData = value;
                OnPropertyChanged(nameof(ExploreData));
            }
        }
        public Explore()
        {
            _ = LoadExplore();
            InitializeComponent();
        }

        public async void SaveExploreAsync(APODInfo? data)
        {
            if (data == null) return;
            //if (MyPictureData.Any(x => x.Equals(data)))
            //{
            //    await new ContentDialog() { Title = "Already saved", Content = "Image was previously saved!" }.ShowAsync();
            //    return;
            //}
            PictureData pictureData;
            var task = APOD.DownloadImageAsync(data);
            ExploreData.Remove(data);
            try
            {
                pictureData = await task;
            }
            catch (Exception)
            {
            }
        }

        private async Task LoadExplore()
        {
            ExploreData = new(await APOD.GetInfoAsync(exploreEnd, ExploreCount));
            exploreView.ItemsSource = ExploreData;
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
        
        private void SaveBarButton(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var button = e.OriginalSource as AppBarButton;
            SaveExploreAsync(ExploreData.First(x => x.Date.Equals(button.CommandParameter)));
        }
    }
}
