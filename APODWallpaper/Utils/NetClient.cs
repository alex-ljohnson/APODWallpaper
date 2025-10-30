namespace APODWallpaper.Utils
{
    internal class NetClient
    {
        public static HttpClient InstanceClient
        {
            get
            {
                return Instance.client;
            }
        }

        private readonly HttpClient client;
        private static readonly object clientLock = new();
        private static NetClient? _instance = null;
        private static NetClient Instance
        {
            get
            {
                lock (clientLock)
                {
                    _instance ??= new NetClient();
                    return _instance;
                }
            }
        }
        public NetClient() {
            client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 2
                }
            );
            Console.WriteLine(client.DefaultRequestHeaders);
        }

    }
}
