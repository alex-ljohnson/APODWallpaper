using APODWallpaper.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APODWallpaper.Interfaces
{
    // TODO: Implement interfaces for different classes requierd in APODWallpaper
    public interface IAPODCache
    {
        public Task AddToCacheAsync(IEnumerable<APODInfo> infos);
        public Task AddToCacheAsync(APODInfo info);
        public Task SaveCacheAsync();
        public void LoadCache();
        public APODInfo? ReadLatest();
        public Task<APODInfo?> GetAsync(DateOnly date);
        public Task<APODInfo?> GetToday();
        public Task<APODInfo[]?> GetRangeAsync(DateOnly startDate, DateOnly endDate);
        public Task<APODInfo[]?> FetchRandAsync(int count);
        //public Task<APODInfo[]> GetAllFromCacheAsync();
        //public Task<bool> ExistsInCacheAsync(DateOnly date);
        //public Task ClearCacheAsync();

    }
}
