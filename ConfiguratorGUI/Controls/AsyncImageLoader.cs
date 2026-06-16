using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ConfiguratorGUI.Controls
{
    /// <summary>
    /// A resolved image request: which URI to load and at what decode width.
    /// Produced by <see cref="Converters.ImageRequestConverter"/> and consumed by
    /// <see cref="AsyncImageLoader"/>. Value equality lets the cache key on (uri, width).
    /// </summary>
    public sealed record ImageRequest(Uri Uri, int DecodeWidth);

    /// <summary>
    /// Attached property that loads an <see cref="Image.Source"/> off the UI thread.
    ///
    /// Local files (the saved-images grid) are decoded on a background thread, frozen, and
    /// cached, so dragging the scrollbar no longer freezes the UI on first traversal and
    /// scroll-back is instant. Remote URLs (the explore tab) keep the previous UI-thread
    /// path - WPF already downloads those asynchronously and they can't be frozen up front.
    ///
    /// A per-Image generation token guards against container recycling: if a tile is recycled
    /// to a new request while a decode is in flight, the stale result is discarded.
    /// </summary>
    public static class AsyncImageLoader
    {
        // Frozen decode cache shared across tiles; only local images are cached.
        private static readonly ConcurrentDictionary<(string Uri, int DecodeWidth), BitmapImage> Cache = new();

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached(
                "Source", typeof(object), typeof(AsyncImageLoader),
                new PropertyMetadata(null, OnSourceChanged));

        public static void SetSource(DependencyObject element, object value) => element.SetValue(SourceProperty, value);
        public static object GetSource(DependencyObject element) => element.GetValue(SourceProperty);

        // Monotonic per-Image counter; bumped on every request so in-flight decodes for a
        // recycled container can be detected and dropped.
        private static readonly DependencyProperty TokenProperty =
            DependencyProperty.RegisterAttached(
                "Token", typeof(int), typeof(AsyncImageLoader), new PropertyMetadata(0));

        private static async void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image) return;

            int token = (int)image.GetValue(TokenProperty) + 1;
            image.SetValue(TokenProperty, token);

            if (e.NewValue is not ImageRequest req)
            {
                image.Source = null;
                return;
            }

            var key = (req.Uri.AbsoluteUri, req.DecodeWidth);
            if (Cache.TryGetValue(key, out var cached))
            {
                image.Source = cached;
                return;
            }

            if (!req.Uri.IsFile)
            {
                // Remote: let WPF download and decode asynchronously, as before. Not cached.
                image.Source = DecodeOnUiThread(req);
                return;
            }

            // Local file: decode off the UI thread, then assign if still current.
            image.Source = null;
            BitmapImage? bmp = null;
            try
            {
                bmp = await Task.Run(() => DecodeFrozen(req, key));
            }
            catch
            {
                // leave Source null on a decode failure
            }

            if (bmp != null && (int)image.GetValue(TokenProperty) == token)
                image.Source = bmp;
        }

        // Background-thread decode for local files. The frozen result is safe to hand to the
        // UI thread and is cached so recycled/returning tiles don't re-read from disk.
        private static BitmapImage DecodeFrozen(ImageRequest req, (string, int) key)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = req.Uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            if (req.DecodeWidth > 0)
                bmp.DecodePixelWidth = req.DecodeWidth;
            bmp.EndInit();
            if (bmp.CanFreeze)
            {
                bmp.Freeze();
                Cache.TryAdd(key, bmp);
            }
            return bmp;
        }

        // Remote URLs: build on the UI thread and let WPF complete the download in the
        // background (the image is still loading, so CanFreeze is false and it isn't cached).
        private static BitmapImage DecodeOnUiThread(ImageRequest req)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = req.Uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            if (req.DecodeWidth > 0)
                bmp.DecodePixelWidth = req.DecodeWidth;
            bmp.EndInit();
            return bmp;
        }
    }
}
