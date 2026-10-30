using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;

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
    /// Enforced says whether NASA rejected a fake key, null when un-checked
    /// </summary>
    public sealed record ApiKeyCheck(bool Reachable, ApiKeyError? Error, int? Limit, int? Remaining, bool? Enforced = null)
    {
        // Over the limit still proves the key exists
        public bool Accepted => Reachable && Error is null or { Kind: ApiKeyErrorKind.RateLimited };
    }

    public static class ApiKey
    {
        public const string DemoKey = "DEMO_KEY";
        public const string SignupUrl = "https://api.nasa.gov/";
        // Previous default key
        public const string LegacySharedKey = "5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ";
        
        //Real key length but obv fake
        internal const string MadeUpKey = "APODWallpaperKeyCheck0000000000000000000";

        public static ApiKeyError? ParseError(HttpStatusCode status, string? body, bool usingDemoKey, int? limit = null)
        {
            if (status == HttpStatusCode.TooManyRequests)
                return new(ApiKeyErrorKind.RateLimited, usingDemoKey, limit);
            if (status == HttpStatusCode.Unauthorized)
                return new(ApiKeyErrorKind.Invalid, usingDemoKey);
            if (status != HttpStatusCode.Forbidden) return null;

            // 403 is page block not key err
            JObject json;
            try
            {
                json = JObject.Parse(body ?? "");
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }

            // api.data.gov nests it under error
            var code = (string?)json["error"]?["code"] ?? (string?)json["code"];
            if (code == null) return null;
            return code switch
            {
                "OVER_RATE_LIMIT" => new(ApiKeyErrorKind.RateLimited, usingDemoKey, limit),
                "API_KEY_MISSING" => new(ApiKeyErrorKind.Missing, usingDemoKey),
                _ => new(ApiKeyErrorKind.Invalid, usingDemoKey)
            };
        }

        public static int? ReadLimit(HttpResponseMessage response) => ReadIntHeader(response, "X-RateLimit-Limit");
        public static int? ReadRemaining(HttpResponseMessage response) => ReadIntHeader(response, "X-RateLimit-Remaining");

        private static int? ReadIntHeader(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var n) ? n : null;

        /// <summary>
        /// Checks the key, then a fake one to see if keys are enforced
        /// </summary>
        public static async Task<ApiKeyCheck> CheckAsync(ApodApiClient client, string key, CancellationToken cancellationToken = default)
        {
            var check = await client.CheckKeyAsync(key, cancellationToken);
            if (!check.Accepted) return check;

            var probe = await client.CheckKeyAsync(MadeUpKey, cancellationToken);
            // Rate limited proves nothing either way
            bool? enforced = !probe.Reachable || probe.Error is { Kind: ApiKeyErrorKind.RateLimited } ? null : probe.Error != null;
            return check with { Enforced = enforced };
        }
    }
}
