using APODWallpaper.Utils;

namespace APODWallpaper.Interfaces
{
    /// <summary>
    /// Tells the user NASA refused the API key and points them at a fix. May be called from any thread
    /// </summary>
    public interface IApiKeyErrorHandler
    {
        void Handle(ApiKeyError error);
    }
}
