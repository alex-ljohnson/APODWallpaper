using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Web;

namespace APODWallpaper.Utils
{
    public enum ApiKeyErrorKind
    {
        Invalid,
        Missing,
        RateLimited
    }

    /// <summary>
    /// A request NASA refused because of the API key, as opposed to a network or server fault
    /// </summary>
    public sealed record ApiKeyError(ApiKeyErrorKind Kind, bool UsingDemoKey, int? Limit = null)
    {
        // A personal key that hit its limit just needs waiting out
        public bool SetupHelps => UsingDemoKey || Kind != ApiKeyErrorKind.RateLimited;

        public string Message => (Kind, UsingDemoKey) switch
        {
            (ApiKeyErrorKind.RateLimited, true) =>
                $"NASA's shared demo key has run out of requests for your network{LimitSuffix}. " +
                "Get your own free API key to avoid this, it only takes a minute.",
            (ApiKeyErrorKind.RateLimited, false) =>
                $"Your NASA API key has hit its request limit{LimitSuffix}. It resets within the hour, try again later.",
            (ApiKeyErrorKind.Missing, _) =>
                "NASA didn't receive an API key. Set one up in the Configurator.",
            (_, true) =>
                "NASA rejected the demo key. Get your own free API key to keep pictures updating.",
            _ =>
                "NASA rejected your API key. Check it was copied correctly, or get a new one.",
        };

        private string LimitSuffix => Limit is int l ? $" ({l} an hour)" : "";
    }

    /// <summary>
    /// Result of checking a key against the API. Reachable is false when NASA gave no verdict on the key
    /// </summary>
    public sealed record ApiKeyCheck(bool Reachable, ApiKeyError? Error, int? Limit, int? Remaining)
    {
        // Over the limit still proves the key exists
        public bool Accepted => Reachable && Error is null or { Kind: ApiKeyErrorKind.RateLimited };
    }

    public static class ApiKey
    {
        public const string DemoKey = "DEMO_KEY";
        public const string SignupUrl = "https://api.nasa.gov/";
        // Previously shipped as the default, shared by every install so it hits its limit
        public const string LegacySharedKey = "5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ";

        /// <summary>
        /// Picks out key failures from an api.nasa.gov response, null for anything else
        /// </summary>
        public static ApiKeyError? ParseError(HttpStatusCode status, string? body, bool usingDemoKey, int? limit = null)
        {
            if (status == HttpStatusCode.TooManyRequests)
                return new(ApiKeyErrorKind.RateLimited, usingDemoKey, limit);
            if (status != HttpStatusCode.Forbidden) return null;

            string? code = null;
            try
            {
                code = (string?)JObject.Parse(body ?? "")["error"]?["code"];
            }
            catch (Newtonsoft.Json.JsonException) { }

            return code switch
            {
                "OVER_RATE_LIMIT" => new(ApiKeyErrorKind.RateLimited, usingDemoKey, limit),
                "API_KEY_MISSING" => new(ApiKeyErrorKind.Missing, usingDemoKey),
                // API_KEY_INVALID, API_KEY_DISABLED, API_KEY_UNVERIFIED etc
                string c when c.StartsWith("API_KEY_", StringComparison.Ordinal) => new(ApiKeyErrorKind.Invalid, usingDemoKey),
                _ => null
            };
        }

        public static int? ReadLimit(HttpResponseMessage response) => ReadIntHeader(response, "X-RateLimit-Limit");
        public static int? ReadRemaining(HttpResponseMessage response) => ReadIntHeader(response, "X-RateLimit-Remaining");

        private static int? ReadIntHeader(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var n) ? n : null;

        /// <summary>
        /// Makes one request with the key to see whether NASA accepts it
        /// </summary>
        public static async Task<ApiKeyCheck> CheckAsync(HttpClient client, string baseUrl, string key, CancellationToken cancellationToken = default)
        {
            var urlParams = HttpUtility.ParseQueryString("");
            urlParams["api_key"] = key;
            try
            {
                using var response = await client.GetAsync($"{baseUrl}?{urlParams}", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var limit = ReadLimit(response);
                var error = ParseError(response.StatusCode, body, key == DemoKey, limit);
                // Rate limit headers mean NASA's gateway let the key through even if APOD then 5xx'd,
                // without them a failure never reached the key check so proves nothing
                if (error is null && !response.IsSuccessStatusCode && limit is null)
                    return new(false, null, null, null);
                return new(true, error, limit, ReadRemaining(response));
            }
            // OperationCanceledException covers timeouts and the caller cancelling
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                Console.WriteLine($"Key check failed: {ex.Message}");
                return new(false, null, null, null);
            }
        }
    }
}
