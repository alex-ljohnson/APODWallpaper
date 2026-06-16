using APODWallpaper.Interfaces;
using System.Net.Http;

namespace ConfiguratorGUI.Services
{
    public class ConfigurableTimeoutHandler(IConfigurationService config) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(config.NetworkTimeout));
            try
            {
                return await base.SendAsync(request, cts.Token);
            } catch (TaskCanceledException ex)
            {
                HttpResponseMessage timeoutResponse = new(System.Net.HttpStatusCode.RequestTimeout)
                {
                    Content = new StringContent($"Request timed out after {config.NetworkTimeout} seconds: {ex.Message}")
                };
                return timeoutResponse;
            }
            
        }
    }
}
