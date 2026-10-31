using System.Runtime.InteropServices;

namespace APODWallpaper.Utils
{
    public static class Utilities
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern nint GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(nint hWnd, string msg, string title, int type);
        private static readonly nint hwnd = GetConsoleWindow();

        public static string GetDataPath(string? path = null)
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string result;
            if (string.IsNullOrWhiteSpace(path))
            {
                result = Path.Combine(appdata, "APODWallpaper");
            }
            else
            {
                result = Path.Combine(appdata, "APODWallpaper", path);
            }
            return Path.GetFullPath(result);
        }

        public static void EnsureDataDirectory()
        {
            var dataDir = GetDataPath("");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
        }

        public enum MessageBoxType
        {

            OK = 0x00,
            OKCancel = 0x01,
            YesNo = 0x04,
            Warning = 0x30,
            Error = 0x10,
            Information = 0x40,
        }
        public enum MessageBoxResult
        {
            None = 0,
            OK = 1,
            Cancel = 2,
            Yes = 6,
            No = 7,
        }
        public static MessageBoxResult ShowMessageBox(string msg, string title = "APOD Wallpaper", MessageBoxType type = MessageBoxType.OK)
        {
            if (OperatingSystem.IsWindows())
            {
                return (MessageBoxResult)MessageBoxW(hwnd, msg, title, (int)type);
            }
            Console.WriteLine($"{title}\n{msg}");
            return MessageBoxResult.None;
        }

        public static void OpenInBrowser(string url)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }

    }
}
