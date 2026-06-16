using APODWallpaper.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Web;

namespace APODWallpaper.Utils
{
    public sealed class APODCache : IAPODCache
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfigurationService config;
        private static readonly string CacheFolder = Utilities.GetDataPath("cache/");
        private static readonly string MetadataCacheFile = Utilities.GetDataPath("cache/metadata.cache");

        private Dictionary<DateOnly, APODInfo> _metadataCache = [];

        public APODCache(IHttpClientFactory httpClientFactory, IConfigurationService config)
        {
            _httpClientFactory = httpClientFactory;
            this.config = config;
            EnsureCacheExists();
            LoadCache();
        }

        public static void EnsureCacheExists()
        {
            Directory.CreateDirectory(CacheFolder);
        }

        public bool IsCached(DateOnly date)
        {
            return _metadataCache.ContainsKey(date);
        }


        #region Cache Ops
        public void LoadCache()
        {
            if (!File.Exists(MetadataCacheFile)) return;
            string cacheData = File.ReadAllText(MetadataCacheFile);
            List<DateOnly> stale = [];
            JsonConvert.DeserializeObject<APODInfo[]>(cacheData)?.ToList().ForEach(info =>
            {
                if (!info.IsValid)
                {
                    stale.Add(info.Date);
                }
                _metadataCache[info.Date] = info;
            });

            if (stale.Count > 0)
                _ = Task.Run(() => RefreshStaleEntriesAsync(stale));
        }

        private async Task RefreshStaleEntriesAsync(List<DateOnly> dates)
        {
            foreach (var date in dates)
            {
                await RefreshAsync(date);
            }
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

        public async Task<APODInfo?> GetToday()
        {
            var info = await SendRequestAsync();
            return info != null && info.Length > 0 ? info[0] : null;
        }

        public async Task<APODInfo?> GetAsync(DateOnly date)
        {
            // Invalid cache fall through to a fetch.
            if (_metadataCache.TryGetValue(date, out var info) && info.IsValid)
            {
                return info;
            }

            var reqInfo = await SendRequestAsync(date: date);
            return reqInfo is { Length: > 0 } ? reqInfo[0] : null;
        }

        public async Task<APODInfo[]?> GetRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            if (endDate > APODDate.Today()) throw new ArgumentException("end_date was in the future");
            List<APODInfo> infos = [];
            int count = endDate.DayNumber - startDate.DayNumber + 1;
            for (int i = 0; i < count; i++)
            {
                DateOnly date = startDate.AddDays(i);
                var info = _metadataCache.GetValueOrDefault(date);
                if (info != null && info.IsValid)
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
        /// fetches info for a specific date directly from the API, ignoring and replacing cached entry.
        /// </summary>
        public async Task<APODInfo?> RefreshAsync(DateOnly date)
        {
            var result = await SendRequestAsync(date: date);
            return result is { Length: > 0 } ? result[0] : null;
        }

        /// <summary>
        /// Fetches info for random APOD images. Randomisation occurs on the server side, so this method does not check the cache, but does cache results.
        /// </summary>
        /// <param name="count">Number of random images to fetch</param>
        public async Task<APODInfo[]?> FetchRandAsync(int count)
        {
            return await SendRequestAsync(count: count);
        }

        // TODO: Abstract out requests into another class to remove dependencies and API-specific logic from cache class
        /// <summary>
        /// Fetch info from API and add to cache
        /// </summary>
        /// <param name="date"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private async Task<APODInfo[]?> SendRequestAsync(DateOnly? date = null, DateOnly? startDate = null, DateOnly? endDate = null, int? count = null)
        {
            if (endDate != null && endDate > APODDate.Today()) throw new ArgumentException("end_date was in the future");
            var urlParams = HttpUtility.ParseQueryString("");
            if (date != null)
            {
                urlParams["date"] = date is DateOnly d ? APODDate.ToIsoString(d) : null;
            } else if (startDate != null || endDate != null)
            {
                if (startDate is DateOnly sd) urlParams["start_date"] = APODDate.ToIsoString(sd);
                
                if (endDate is DateOnly ed) urlParams["end_date"] = APODDate.ToIsoString(ed);
            } else if (count != null)
            {
                urlParams["count"] = count.ToString();
            }
            urlParams["api_key"] = config.API_KEY;
            Uri uri = new($"{config.BaseUrl}?{urlParams}");
            APODInfo[] imageInfo;
            var httpClient = _httpClientFactory.CreateClient("APODCache");
            try
            {
                string responseContent = await httpClient.GetStringAsync(uri);
                
                if (endDate != null || count != null)
                {
                    imageInfo = JsonConvert.DeserializeObject<APODInfo[]>(responseContent)!;
                }
                else
                {
                    imageInfo = [JsonConvert.DeserializeObject<APODInfo>(responseContent)!];
                }
                // Stamp retrieval date
                var retrieved = APODDate.Today();
                foreach (var info in imageInfo)
                    info?.RetrievalDate = retrieved;
                await AddToCacheAsync(imageInfo);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is HttpRequestException || ex is TaskCanceledException)
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again.\nThis also occurs when the NASA API is down.", "Connection error", Utilities.MessageBoxType.Error);
                Console.WriteLine(ex.Message);
                return null;
            } 
            return imageInfo;
        }

        public async Task<string> DownloadURLAsync(Uri? url, string filepath, IProgress<(long, long?)>? progress = null)
        {
            //string filename;
            ArgumentNullException.ThrowIfNull(url);
            var httpClient = _httpClientFactory.CreateClient("APODCache");
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Console.WriteLine(response.Content.Headers.ToString());
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                if (!contentLength.HasValue)
                {
                    Console.WriteLine("Content length not provided");
                }
                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Write);
                if (config.DownloadInfo && contentLength.HasValue)
                {
                    long totalReadBytes = 0L;
                    var buffer = new byte[81920];
                    int readBytes;
                    while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, readBytes);
                        totalReadBytes += readBytes;
                        progress?.Report((totalReadBytes, contentLength));
                    }
                }
                else
                {
                    var copyTask = contentStream.CopyToAsync(fileStream);
                    // Simple progress reporter for copy operation
                    await copyTask;

                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TimeoutException)
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again", "Connection error", Utilities.MessageBoxType.Error);
                Console.WriteLine(ex.StackTrace);
                throw;
            }
            return filepath;
        }
    }
}