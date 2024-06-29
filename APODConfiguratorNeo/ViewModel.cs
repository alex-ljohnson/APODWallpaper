using APODWallpaper.Utils;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace APODConfiguratorNeo
{
    internal class ViewModel : INotifyPropertyChanged
    {
        private readonly APODWallpaper.APODWallpaper APOD = APODWallpaper.APODWallpaper.Instance;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        public async Task Initialise()
        {
        }

        public ViewModel()
        {

        }

    }
}
