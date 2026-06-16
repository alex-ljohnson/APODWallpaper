using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ConfiguratorGUI.Controls;

namespace ConfiguratorGUI.Converters
{
    /// <summary>
    /// Convert bindings to an ImageRequest for the ImageRequestControl. Accepts either 2 or 4 values for saved image mode or explore tab mode.
    /// </summary>
    internal class ImageRequestConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Uri? uri;
            int decodeWidth;

            if (values.Length >= 4)
            {
                // Explore-tab mode: pick HD or standard URL based on UseHD flag
                bool useHd = values[2] is bool b && b;
                uri = (useHd && values[1] is Uri hd) ? hd
                    : values[0] is Uri std ? std
                    : null;
                decodeWidth = DecodeWidthOf(values[3]);
            }
            else
            {
                // saved image mode: single source (string path or Uri)
                if (values[0] is Uri u)
                    uri = u;
                else if (values[0] is string s && !string.IsNullOrEmpty(s))
                    Uri.TryCreate(s, UriKind.Absolute, out uri);
                else
                    uri = null;
                decodeWidth = values.Length > 1 ? DecodeWidthOf(values[1]) : 0;
            }

            if (uri == null) return DependencyProperty.UnsetValue;

            return new ImageRequest(uri, decodeWidth);
        }

        // Accepts the decode width whether it arrives as an int (narrowed by a per-binding
        // converter) or a long (the raw config value). Unrecognised values map to 0.
        private static int DecodeWidthOf(object value) => value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0
        };

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
