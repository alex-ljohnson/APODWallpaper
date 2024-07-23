using System.Globalization;
using System.Windows.Data;

namespace ConfiguratorGUI.Converters
{
    [ValueConversion(typeof(long), typeof(int))]
    internal class LongConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            var v = (long)value;
            return (int)v;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            var v = (int)value;
            return (long)v;
        }
    }
}
