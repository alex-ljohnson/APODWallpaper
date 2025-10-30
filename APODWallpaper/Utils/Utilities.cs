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

        public static readonly string current = GetDataPath("current.jpg");
        public static readonly string last = GetDataPath("last.jpg");
        public static string GetDataPath(string path)
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

        public enum MessageBoxType
        {

            OK = 0x00,
            OKCancel = 0x01,
            YesNo = 0x04,
            Warning = 0x30,
            Error = 0x10,
            Information = 0x40,
        }
        public static void ShowMessageBox(string msg, string title = "APOD Wallpaper", MessageBoxType type = MessageBoxType.OK)
        {
            if (OperatingSystem.IsWindows())
            {
                _ = MessageBoxW(hwnd, msg, title, (int)type);
            } else
            {
                Console.WriteLine($"{title}\n{msg}");
            }
        }

    }
}
