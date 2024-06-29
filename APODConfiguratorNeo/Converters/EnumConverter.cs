using APODWallpaper.Utils;
using Microsoft.UI.Xaml.Data;

namespace APODConfiguratorNeo
{
    internal class EnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            long wallpaperStyleEnum = (long)value;
            return (WallpaperStyleEnum)wallpaperStyleEnum;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            WallpaperStyleEnum wallpaperStyleEnum = (WallpaperStyleEnum)value;
            return (long)wallpaperStyleEnum;
        }
    }
}
