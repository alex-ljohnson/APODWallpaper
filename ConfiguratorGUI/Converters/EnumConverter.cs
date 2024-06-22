using APODWallpaper.Utils;
using System.Globalization;
using System.Windows.Data;

namespace ConfiguratorGUI
{
    [ValueConversion(typeof(long), typeof(WallpaperStyleEnum))]
    internal class EnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long wallpaperStyleEnum = (long)value;
            return (WallpaperStyleEnum)wallpaperStyleEnum;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            WallpaperStyleEnum wallpaperStyleEnum = (WallpaperStyleEnum)value;
            return (long)wallpaperStyleEnum;
        }
    }
}
