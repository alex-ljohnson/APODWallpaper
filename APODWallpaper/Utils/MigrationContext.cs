namespace APODWallpaper.Utils;

public sealed class MigrationContext
{
    public string DataRoot { get; }

    public MigrationContext() : this(Utilities.GetDataPath("")) { }

    public MigrationContext(string dataRoot)
    {
        DataRoot = dataRoot;
    }

    public string GetPath(string relative) => Path.Combine(DataRoot, relative);
}
