using System.Globalization;
using System.Windows.Data;

namespace ConfiguratorGUI
{
    internal class LongConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString()!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return long.Parse(value.ToString()!.Replace(" ", ""));
        }
    }
}
