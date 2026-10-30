using APODWallpaper.Interfaces;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;

namespace APODWallpaper.Utils
{
    public enum ApodOutcome
    {
        Found,
        NotFound,
        Failed
    }

    public sealed record ApodResult<T>(ApodOutcome Outcome, T? Value = default, ApiKeyError? KeyError = null)
    {
        public bool Complete { get; init; } = true;

        public static ApodResult<T> Found(T value) => new(ApodOutcome.Found, value);
        public static readonly ApodResult<T> NotFound = new(ApodOutcome.NotFound);
        public static ApodResult<T> Failed(ApiKeyError? keyError = null) => new(ApodOutcome.Failed, default, keyError);
    }

    /// <summary>
    /// NASA's apod-basic API, documented behaviour only. Never shows UI
    /// </summary>
    public sealed class ApodApiClient
    {
        public const string HttpClientName = "APODCache";
        public const string DefaultEndpoint = "https://science.nasa.gov/wp-json/wp/v2/apod-basic";
        // Documented cap on per_page
        internal const int PageSize = 25;

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IConfigurationService config;
        private readonly string endpoint;

        public ApodApiClient(IHttpClientFactory httpClientFactory, IConfigurationService config) : this(httpClientFactory, config, DefaultEndpoint) { }

        internal ApodApiClient(IHttpClientFactory httpClientFactory, IConfigurationService config, string endpoint)
        {
            this.httpClientFactory = httpClientFactory;
            this.config = config;
            this.endpoint = endpoint.TrimEnd('/');
        }

        public static void ConfigureHttpClient(HttpClient client)
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("APODWallpaper", UserAgentVersion));
        }

        private static string UserAgentVersion => VersionInfo.Numeric(typeof(ApodApiClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        public async Task<ApodResult<APODInfo>> GetAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            var (outcome, body, keyError) = await SendAsync(SingleUri(date, config.EffectiveApiKey), config.UsingDemoKey, cancellationToken);
            if (outcome != ApodOutcome.Found) return new(outcome, null, keyError);

            var info = Parse<ApodResponse>(body) is ApodResponse response ? ApodResponseMapper.Map(response, APODDate.Today()) : null;
            if (info == null) return ApodResult<APODInfo>.Failed();
            if (info.Date != date)
            {
                Console.WriteLine($"Asked NASA for {APODDate.ToIsoString(date)}, got {APODDate.ToIsoString(info.Date)}");
                return ApodResult<APODInfo>.NotFound;
            }
            return ApodResult<APODInfo>.Found(info);
        }

        /// <summary>
        /// Start to end inclusive, sorted. Gaps mean fewer results
        /// </summary>
        public async Task<ApodResult<APODInfo[]>> GetRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
        {
            if (start > end) return ApodResult<APODInfo[]>.Found([]);

            int days = end.DayNumber - start.DayNumber + 1;
            // One spare page in case page is ever ignored
            int maxPages = (days + PageSize - 1) / PageSize + 1;
            var retrieved = APODDate.Today();
            Dictionary<DateOnly, APODInfo> byDate = [];
            // Only a short page proves the end was reached
            bool complete = false;

            for (int page = 1; page <= maxPages; page++)
            {
                var (outcome, body, keyError) = await SendAsync(RangeUri(start, end, page), config.UsingDemoKey, cancellationToken);
                if (outcome != ApodOutcome.Found) return ApodResult<APODInfo[]>.Failed(keyError);

                if (Parse<ApodResponse[]>(body) is not ApodResponse[] items) return ApodResult<APODInfo[]>.Failed();

                int known = byDate.Count;
                foreach (var item in items)
                {
                    if (ApodResponseMapper.Map(item, retrieved) is APODInfo info && info.Date >= start && info.Date <= end)
                        byDate[info.Date] = info;
                }
                if (items.Length < PageSize)
                {
                    complete = true;
                    break;
                }
                if (byDate.Count == known) break;
            }
            if (!complete) Console.WriteLine($"Range {APODDate.ToIsoString(start)} to {APODDate.ToIsoString(end)} stopped paging early");
            return ApodResult<APODInfo[]>.Found([.. byDate.Values.OrderBy(info => info.Date)]) with { Complete = complete };
        }

        /// <summary>
        /// Newest of yesterday and today, today's is out at 00:05 Eastern
        /// </summary>
        public async Task<ApodResult<APODInfo>> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            var today = APODDate.Today();
            var range = await GetRangeAsync(today.AddDays(-1), today, cancellationToken);
            if (range.Outcome != ApodOutcome.Found) return new(range.Outcome, null, range.KeyError);
            return range.Value!.Length > 0 ? ApodResult<APODInfo>.Found(range.Value[^1]) : ApodResult<APODInfo>.NotFound;
        }

        public async Task<ApiKeyCheck> CheckKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var uri = SingleUri(APODDate.Today().AddDays(-1), key);
            try
            {
                using var response = await CreateClient().GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var body = await ReadBodyAsync(response, cancellationToken);
                var limit = ApiKey.ReadLimit(response);
                var error = ApiKey.ParseError(response.StatusCode, body, key == ApiKey.DemoKey, limit);
                // 404 or rate limit headers mean it got past any key check
                bool reachable = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound || error != null || limit != null;
                return reachable ? new(true, error, limit, ApiKey.ReadRemaining(response)) : new(false, null, null, null);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is HttpIOException || ex is OperationCanceledException)
            {
                Console.WriteLine($"Key check failed: {ex.Message}");
                return new(false, null, null, null);
            }
        }

        private async Task<(ApodOutcome, string?, ApiKeyError?)> SendAsync(Uri uri, bool usingDemoKey, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await CreateClient().GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var body = await ReadBodyAsync(response, cancellationToken);
                if (ApiKey.ParseError(response.StatusCode, body, usingDemoKey, ApiKey.ReadLimit(response)) is ApiKeyError keyError)
                {
                    Console.WriteLine(keyError.Message);
                    return (ApodOutcome.Failed, null, keyError);
                }
                if (response.StatusCode == HttpStatusCode.NotFound) return (ApodOutcome.NotFound, null, null);
                if (!response.IsSuccessStatusCode)
                {
                    // Don't log the URI, it contains the API key
                    Console.WriteLine($"NASA API returned {(int)response.StatusCode} {response.ReasonPhrase}");
                    return (ApodOutcome.Failed, null, null);
                }
                return (ApodOutcome.Found, body, null);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is HttpIOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                Console.WriteLine($"NASA API request failed: {ex.Message}");
                return (ApodOutcome.Failed, null, null);
            }
        }

        // The timeout handler only covers headers
        private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(config.NetworkTimeout));
            return await response.Content.ReadAsStringAsync(cts.Token);
        }

        private static T? Parse<T>(string? body) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(body ?? "");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Couldn't read NASA's response: {ex.Message}");
                return null;
            }
        }

        private HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

        private Uri SingleUri(DateOnly date, string key) => new($"{endpoint}/{APODDate.ToCode(date)}?{Query(("api_key", key))}");

        private Uri RangeUri(DateOnly start, DateOnly end, int page) => new($"{endpoint}?{Query(
            ("date_from", APODDate.ToCode(start)),
            ("date_to", APODDate.ToCode(end)),
            ("per_page", PageSize.ToString()),
            ("page", page.ToString()),
            ("api_key", config.EffectiveApiKey))}");

        private static string Query(params (string Name, string Value)[] args)
        {
            var query = HttpUtility.ParseQueryString("");
            foreach (var (name, value) in args) query[name] = value;
            return query.ToString()!;
        }
    }
}
