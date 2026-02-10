using APODWallpaper.Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace APODWallpaper.Interfaces
{

    public interface IConfigurationService
    {
        Task InitialiseAsync();
        string ConfiguratorTheme { get; set; }
        ObservableCollection<string> AvailableThemes { get; }
    }

    // Theme service implementation
    // Configuration service implementation
    
}
