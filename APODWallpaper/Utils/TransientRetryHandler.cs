using System.Net;
using System.Net.Http;

namespace APODWallpaper.Utils
{
    /// <summary>
    /// Retries requests failing with transient errors. NASA's APOD API intermittently returns 5xx
    /// </summary>
    public class TransientRetryHandler : DelegatingHandler
    {
        public const int MaxAttempts = 3;
        private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            for (int attempt = 1; ; attempt++)
            {
                HttpResponseMessage response;
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex) when (attempt < MaxAttempts)
                {
                    Console.WriteLine($"Request failed ({ex.Message}), retrying {attempt}/{MaxAttempts - 1}");
                    await Task.Delay(BaseDelay * attempt, cancellationToken);
                    continue;
                }

                if (attempt >= MaxAttempts || !IsTransient(response.StatusCode)) return response;

                // Don't log the URI, it contains the API key
                Console.WriteLine($"Request returned {(int)response.StatusCode} {response.ReasonPhrase}, retrying {attempt}/{MaxAttempts - 1}");
                response.Dispose();
                await Task.Delay(BaseDelay * attempt, cancellationToken);
            }
        }

        private static bool IsTransient(HttpStatusCode status) =>
            status == HttpStatusCode.RequestTimeout || (int)status >= 500;
    }
}
