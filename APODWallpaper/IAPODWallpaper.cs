using APODWallpaper.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace APODWallpaper
{
    public interface IAPODWallpaper
    {
        public Task<PictureData?> UpdateAsync(bool force = false);
        public Task<PictureData?> DownloadImageAsync(APODInfo? information = null);
        public Task<PictureData?> DownloadTodayAsync(APODInfo? info = null);
        public bool CheckNewAsync();
        public void UpdateBackground(string? file = null, WallpaperStyleEnum style = WallpaperStyleEnum.Fill);
    }
}
