using System.Globalization;
using System.Windows.Data;

namespace ConfiguratorGUI.Converters
{
    internal class VideoUrlConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool useHd = values.Length > 2 && values[2] is bool b && b;
            return (useHd && values[1] is Uri hd) ? hd
                : values[0] is Uri std ? std
                : Binding.DoNothing;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}