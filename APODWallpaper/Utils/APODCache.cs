using APODWallpaper.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;

namespace APODWallpaper.Utils
{
    public sealed class APODCache : IAPODCache
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfigurationService config;
        private readonly IApiKeyErrorHandler keyErrorHandler;
        private readonly ApodApiClient client;
        public static readonly string MetadataCacheFile = Utilities.GetDataPath("cache/metadata.v2.cache");
        private readonly string cacheFile;

        private readonly Lock cacheLock = new();
        private readonly SemaphoreSlim saveLock = new(1, 1);
        private readonly Dictionary<DateOnly, APODInfo> _metadataCache = [];
        // Past dates with no APOD, memory only
        private readonly HashSet<DateOnly> knownGaps = [];

        private int showingConnectionError;

        private sealed record CacheFile(int MapperVersion, APODInfo[]? Entries);

        public APODCache(IHttpClientFactory httpClientFactory, IConfigurationService config, IApiKeyErrorHandler keyErrorHandler, ApodApiClient client)
            : this(httpClientFactory, config, keyErrorHandler, client, MetadataCacheFile) { }

        internal APODCache(IHttpClientFactory httpClientFactory, IConfigurationService config, IApiKeyErrorHandler keyErrorHandler, ApodApiClient client, string cacheFile)
        {
            _httpClientFactory = httpClientFactory;
            this.config = config;
            this.keyErrorHandler = keyErrorHandler;
            this.client = client;
            this.cacheFile = cacheFile;
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            LoadCache();
        }

        public bool IsCached(DateOnly date)
        {
            lock (cacheLock) return _metadataCache.ContainsKey(date);
        }

        private bool TryGetCached(DateOnly date, out APODInfo info)
        {
            lock (cacheLock) return _metadataCache.TryGetValue(date, out info!);
        }

        private bool IsKnownGap(DateOnly date)
        {
            lock (cacheLock) return knownGaps.Contains(date);
        }

        // Today may not be out yet
        private void MarkGap(DateOnly date)
        {
            if (date >= APODDate.Today()) return;
            lock (cacheLock) knownGaps.Add(date);
        }

        #region Cache Ops
        public void LoadCache()
        {
            if (!File.Exists(cacheFile)) return;
            CacheFile? file;
            try
            {
                file = JsonConvert.DeserializeObject<CacheFile>(File.ReadAllText(cacheFile));
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                Trace.WriteLine($"Couldn't read metadata cache, starting empty: {ex.Message}");
                return;
            }
            if (file?.MapperVersion != ApodResponseMapper.Version)
            {
                Trace.WriteLine("Metadata cache is from another mapper version, starting empty");
                return;
            }
            lock (cacheLock)
            {
                foreach (var info in file.Entries ?? [])
                {
                    if (info != null) _metadataCache[info.Date] = info;
                }
            }
        }

        public async Task SaveCacheAsync()
        {
            await saveLock.WaitAsync();
            try
            {
                // Inside saveLock so an older snapshot can't overwrite a newer one
                APODInfo[] snapshot;
                lock (cacheLock) snapshot = [.. _metadataCache.Values.OrderBy(info => info.Date)];
                await File.WriteAllTextAsync(cacheFile, JsonConvert.SerializeObject(new CacheFile(ApodResponseMapper.Version, snapshot), Formatting.Indented));
            }
            finally
            {
                saveLock.Release();
            }
        }

        public async Task AddToCacheAsync(APODInfo info)
        {
            if (info == null) return;
            await AddToCacheAsync([info]);
        }
        public async Task AddToCacheAsync(IEnumerable<APODInfo> infos)
        {
            if (infos == null) return;
            lock (cacheLock)
            {
                foreach (var info in infos)
                {
                    if (info == null) continue;
                    _metadataCache[info.Date] = info;
                }
            }
            await SaveCacheAsync();
        }
        #endregion

        /// <summary>
        /// Newest APOD, yesterday's until today's is out
        /// </summary>
        public async Task<APODInfo?> GetToday()
        {
            var result = await client.GetLatestAsync();
            if (result.Outcome == ApodOutcome.NotFound)
            {
                Console.WriteLine("NASA has no APOD for yesterday or today");
                return null;
            }
            if (result.Outcome == ApodOutcome.Failed)
            {
                ReportFailure(result.KeyError);
                return null;
            }
            await AddToCacheAsync(result.Value!);
            return result.Value;
        }

        public async Task<APODInfo?> GetAsync(DateOnly date)
        {
            if (TryGetCached(date, out var info)) return info;
            if (IsKnownGap(date)) return null;
            return await FetchAsync(date);
        }

        public async Task<APODInfo[]?> GetRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            if (endDate > APODDate.Today()) throw new ArgumentException("end_date was in the future");

            // Fetch only the uncached span
            DateOnly? fetchFrom = null, fetchTo = null;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (IsCached(date) || IsKnownGap(date)) continue;
                fetchFrom ??= date;
                fetchTo = date;
            }

            if (fetchFrom is DateOnly from && fetchTo is DateOnly to)
            {
                var result = await client.GetRangeAsync(from, to);
                if (result.Outcome != ApodOutcome.Found)
                {
                    ReportFailure(result.KeyError);
                    return null;
                }
                await AddToCacheAsync(result.Value!);
                if (result.Complete)
                {
                    var found = result.Value!.Select(info => info.Date).ToHashSet();
                    for (var date = from; date <= to; date = date.AddDays(1))
                    {
                        if (!found.Contains(date)) MarkGap(date);
                    }
                }
            }

            List<APODInfo> infos = [];
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (TryGetCached(date, out var info)) infos.Add(info);
            }
            return [.. infos];
        }

        /// <summary>
        /// Random dates picked client-side, cached where possible. Stops at the first failure with what it has
        /// </summary>
        /// <param name="count">Number of random images to fetch</param>
        public async Task<APODInfo[]?> FetchRandAsync(int count)
        {
            // Not today, may not be out yet
            var last = APODDate.Today().AddDays(-1);
            int span = last.DayNumber - APODDate.InceptionDate.DayNumber + 1;
            int attemptsLeft = count * 3;
            HashSet<DateOnly> tried = [];
            List<APODInfo> found = [];
            List<APODInfo> fetched = [];
            ApodResult<APODInfo>? failure = null;

            while (found.Count < count && attemptsLeft > 0 && failure == null)
            {
                List<DateOnly> batch = [];
                while (found.Count + batch.Count < count && attemptsLeft > 0)
                {
                    var date = APODDate.InceptionDate.AddDays(Random.Shared.Next(span));
                    if (!tried.Add(date)) continue;
                    attemptsLeft--;
                    if (TryGetCached(date, out var cached)) found.Add(cached);
                    else if (!IsKnownGap(date)) batch.Add(date);
                }

                var results = await Task.WhenAll(batch.Select(date => client.GetAsync(date)));
                for (int i = 0; i < batch.Count; i++)
                {
                    switch (results[i].Outcome)
                    {
                        case ApodOutcome.Found:
                            found.Add(results[i].Value!);
                            fetched.Add(results[i].Value!);
                            break;
                        case ApodOutcome.NotFound:
                            MarkGap(batch[i]);
                            break;
                        default:
                            failure ??= results[i];
                            break;
                    }
                }
            }

            if (fetched.Count > 0) await AddToCacheAsync(fetched);
            if (failure != null)
            {
                ReportFailure(failure.KeyError);
                if (found.Count == 0) return null;
            }
            return [.. found];
        }

        private async Task<APODInfo?> FetchAsync(DateOnly date)
        {
            var result = await client.GetAsync(date);
            switch (result.Outcome)
            {
                case ApodOutcome.Found:
                    await AddToCacheAsync(result.Value!);
                    return result.Value;
                case ApodOutcome.NotFound:
                    MarkGap(date);
                    return null;
                default:
                    ReportFailure(result.KeyError);
                    return null;
            }
        }

        // One connection dialog however many requests fail
        private void ReportFailure(ApiKeyError? keyError)
        {
            if (keyError != null)
            {
                keyErrorHandler.Handle(keyError);
                return;
            }
            if (Interlocked.Exchange(ref showingConnectionError, 1) == 1) return;
            try
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again.\nThis also occurs when the NASA API is down.", "Connection error", Utilities.MessageBoxType.Error);
            }
            finally
            {
                Interlocked.Exchange(ref showingConnectionError, 0);
            }
        }

        public async Task<string> DownloadURLAsync(Uri? url, string filepath, IProgress<(long, long?)>? progress = null)
        {
            //string filename;
            ArgumentNullException.ThrowIfNull(url);
            var httpClient = _httpClientFactory.CreateClient(ApodApiClient.HttpClientName);
            // Moved into place when complete, a broken download must not look saved
            var partPath = Path.Combine(Path.GetDirectoryName(cacheFile)!, Path.GetFileName(filepath) + ".part");
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
                using (FileStream fileStream = new(partPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long totalReadBytes = 0L;
                    var buffer = new byte[81920];
                    int readBytes;
                    while ((readBytes = await ReadWithTimeoutAsync(contentStream, buffer)) != 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, readBytes));
                        totalReadBytes += readBytes;
                        if (config.DownloadInfo && contentLength.HasValue) progress?.Report((totalReadBytes, contentLength));
                    }
                }
                File.Move(partPath, filepath, overwrite: true);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is HttpIOException || ex is TimeoutException || ex is TaskCanceledException)
            {
                Utilities.ShowMessageBox("Please check your internet connection and try again", "Connection error", Utilities.MessageBoxType.Error);
                Console.WriteLine(ex.StackTrace);
                throw;
            }
            finally
            {
                DeletePartFile(partPath);
            }
            return filepath;
        }

        // Must not hide the download's own exception
        private static void DeletePartFile(string partPath)
        {
            try
            {
                File.Delete(partPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"Couldn't delete '{partPath}': {ex.Message}");
            }
        }

        private async Task<int> ReadWithTimeoutAsync(Stream stream, byte[] buffer)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.NetworkTimeout));
            try
            {
                return await stream.ReadAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Download stalled for {config.NetworkTimeout} seconds");
            }
        }
    }
}
