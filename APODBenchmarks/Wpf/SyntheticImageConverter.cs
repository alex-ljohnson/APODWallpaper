using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace APODBenchmarks.Wpf;

// seed -> distinct frozen bitmap, generated at bind/realization time (mirrors AsyncImageLoader)
public sealed class SyntheticImageConverter : IValueConverter
{
    private const int W = 220, H = 160;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int seed = value is int i ? i : 0;
        // per-item pixel buffer so each realized image holds its own memory
        var pixels = new byte[W * H * 4];
        byte r = (byte)(seed * 53 % 256);
        byte g = (byte)(seed * 97 % 256);
        byte b = (byte)(seed * 193 % 256);
        for (int p = 0; p < pixels.Length; p += 4)
        {
            pixels[p] = b; pixels[p + 1] = g; pixels[p + 2] = r; pixels[p + 3] = 255;
        }
        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, pixels, W * 4);
        bmp.Freeze();
        return bmp;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
