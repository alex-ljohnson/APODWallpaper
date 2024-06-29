using APODWallpaper.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

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
            private set
            {
                exploreData = value;
                OnPropertyChanged(nameof(ExploreData));
            }
        }
        public Explore()
        {
            var explore = LoadExplore();
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
                //MyPictureData.Insert(0, pictureData);
            }
            catch (Exception)
            {
            }
            //_ = SortDataAsync();
        }
        private async Task LoadExplore()
        {
            ExploreData = new(await APOD.GetInfoAsync(exploreEnd, ExploreCount));
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
    }
}
