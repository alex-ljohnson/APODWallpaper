using Microsoft.Win32;

namespace APODWallpaper.Utils;

public sealed class MigrationContext
{
    private const string DesktopKey = @"Control Panel\Desktop";

    public string DataRoot { get; }

    // Desktop wallpaper path in HKCU, swapped out in tests
    public Func<string?> ReadWallpaper { get; init; } = ReadDesktopWallpaper;
    public Action<string> WriteWallpaper { get; init; } = WriteDesktopWallpaper;

    public MigrationContext() : this(Utilities.GetDataPath("")) { }

    public MigrationContext(string dataRoot)
    {
        DataRoot = dataRoot;
    }

    public string GetPath(string relative) => Path.Combine(DataRoot, relative);

    private static string? ReadDesktopWallpaper()
    {
        using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
        return key?.GetValue("WallPaper") as string;
    }

    private static void WriteDesktopWallpaper(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true);
        key?.SetValue("WallPaper", path);
    }
}
