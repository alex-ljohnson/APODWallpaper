using System.Globalization;
using System.Windows;
using System.Windows.Data;
using APODWallpaper.Utils;
using ConfiguratorGUI.Controls;

namespace ConfiguratorGUI.Converters
{
    /// <summary>
    /// Convert bindings to an ImageRequest for the ImageRequestControl. Takes the source (string path or Uri) then the decode width
    /// </summary>
    internal class ImageRequestConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            Uri? uri;
            if (values[0] is Uri u)
                uri = u;
            else if (values[0] is string s && !string.IsNullOrEmpty(s))
                Uri.TryCreate(s, UriKind.Absolute, out uri);
            else
                uri = null;
            int decodeWidth = values.Length > 1 ? DecodeWidthOf(values[1]) : 0;

            if (uri == null) return DependencyProperty.UnsetValue;

            // NASA's proxy resizes server side, the full image is the fallback
            if (!uri.IsFile && ApodImageUrl.Resized(uri, decodeWidth) is Uri resized)
                return new ImageRequest(resized, decodeWidth, Fallback: uri);
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
