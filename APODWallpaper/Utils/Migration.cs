namespace APODWallpaper.Utils;

/// <summary>One ordered, run-once data migration step.</summary>
public sealed record Migration(int Version, string Name, Action<MigrationContext> Run);
