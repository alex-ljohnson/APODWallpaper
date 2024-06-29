
namespace APODWallpaper.Utils
{
    [Serializable]
    public class NotImageException : Exception
    {
        public NotImageException() { }
        public NotImageException(string message) : base(message) { }
        public NotImageException(string message, Exception inner) : base(message, inner) { }
    }
}
