using Newtonsoft.Json;
using System.Diagnostics;
using System.Web;

namespace APODWallpaper.Utils
{
    public sealed class APODCache
    {
        private static readonly string CacheFolder = Utilities.GetDataPath("cache/");
        private static readonly string MetadataCacheFile = Utilities.GetDataPath("cache/metadata.cache");

        private Dictionary<DateOnly, APODInfo> _metadataCache = [];
        private static readonly object CacheLock = new();
        private static APODCache? _instance = null;
        public static APODCache Instance
        {
            get
            {
                lock (CacheLock)
                {
                    _instance ??= new APODCache();
                    return _instance;
                }
            }
        }

        public APODCache()
        {
            EnsureCacheExists();
            LoadCache();
        }

        public static void EnsureCacheExists()
        {
            Directory.CreateDirectory(CacheFolder);
        }
        #region Cache Ops
        public void LoadCache()
        {
            if (!File.Exists(MetadataCacheFile)) return;
            string cacheData = File.ReadAllText(MetadataCacheFile);
            JsonConvert.DeserializeObject<APODInfo[]>(cacheData)?.ToList().ForEach(info =>
            {
                _metadataCache[info.Date] = info;
            });
        }
        public async Task SaveCacheAsync()
        {
            var serialized = JsonConvert.SerializeObject(_metadataCache.Values.ToArray(), Formatting.Indented);
            await File.WriteAllTextAsync(MetadataCacheFile, serialized);
        }

        public async Task AddToCacheAsync(APODInfo info)
        {
            if (info == null) return;
            _metadataCache[info.Date] = info;
            await SaveCacheAsync();
        }
        public async Task AddToCacheAsync(IEnumerable<APODInfo> infos)
        {
            if (infos == null) return;
            
            foreach (var info in infos)
            {
                if (info == null) continue;
                _metadataCache[info.Date] = info;
            }
            await SaveCacheAsync();
            
        }
        #endregion
        public APODInfo? ReadLatest()
        {
            _metadataCache = _metadataCache.OrderByDescending(kv => kv.Key).ToDictionary();
            return _metadataCache.Values.FirstOrDefault();
        }

        public async Task<APODInfo> GetToday()
        {
            return (await SendRequestAsync())[0];
        }

        public async Task<APODInfo> GetInfoAsync(DateOnly date)
        {
            if (_metadataCache.TryGetValue(date, out var info))
            {
                return info;
            }
            else
            {
                return (await SendRequestAsync(date: date))[0];
            }

        }

        public async Task<APODInfo[]> GetInfoRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            if (endDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("end_date was in the future");
            List<APODInfo> infos = [];
            int count = endDate.DayNumber - startDate.DayNumber + 1;
            for (int i = 0; i < count; i++)
            {
                DateOnly date = startDate.AddDays(i);
                var info = _metadataCache.GetValueOrDefault(date);
                if (info != null)
                {
                    infos.Add(info);
                }
            }
            if (infos.Count == count)
            {
                return [.. infos];
            }
            else
            {
                return await SendRequestAsync(startDate: startDate, endDate: endDate);
            }
        }

        /// <summary>
        /// Fetches info for random APOD images. Randomisation occurs on the server side, so this method does not check the cache, but does cache results.
        /// </summary>
        /// <param name="count">Number of random images to fetch</param>
        public async Task<APODInfo[]> FetchRandAsync(int count)
        {
            return await SendRequestAsync(count: count);
        }


        /// <summary>
        /// Fetch info from API and add to cache
        /// </summary>
        /// <param name="date"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<APODInfo[]> SendRequestAsync(DateOnly? date = null, DateOnly? startDate = null, DateOnly? endDate = null, int? count = null)
        {
            if (endDate != null && endDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("end_date was in the future");
            var urlParams = HttpUtility.ParseQueryString("");
            if (date != null)
            {
                urlParams["date"] = date?.ToString("yyyy-MM-dd");
            } else if (startDate != null || endDate != null)
            {
                if (startDate != null) urlParams["start_date"] = startDate?.ToString("yyyy-MM-dd");
                
                urlParams["end_date"] = endDate?.ToString("yyyy-MM-dd");
            } else if (count != null)
            {
                urlParams["count"] = count.ToString();
            }
            urlParams["api_key"] = Configuration.Config.API_KEY;
            Uri uri = new($"{Configuration.Config.BaseUrl}?{urlParams}");
            Trace.WriteLine(uri.ToString());
            APODInfo[] imageInfo;
            try
            {
                string responseContent = await NetClient.InstanceClient.GetStringAsync(uri).ConfigureAwait(false);
                
                if (endDate != null || count != null)
                {
                    imageInfo = JsonConvert.DeserializeObject<APODInfo[]>(responseContent)!;
                }
                else
                {
                    imageInfo = [JsonConvert.DeserializeObject<APODInfo>(responseContent)!];
                }
                await AddToCacheAsync(imageInfo);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is HttpRequestException)
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again", "Connection error", Utilities.MessageBoxType.Error);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
                return null;
            } 
            return imageInfo;
        }
    }
}