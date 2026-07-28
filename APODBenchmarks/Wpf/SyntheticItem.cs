namespace APODBenchmarks.Wpf;

// model holds only a seed and text — NO bitmap, so only realized containers hold images
public sealed class SyntheticItem
{
    public int Seed { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Body { get; set; } = "";

    public static IReadOnlyList<SyntheticItem> Generate(int count)
    {
        var list = new List<SyntheticItem>(count);
        for (int i = 0; i < count; i++)
            list.Add(new SyntheticItem
            {
                Seed = i,
                Title = $"Item {i}",
                Subtitle = $"2026-01-{(i % 28) + 1:D2}",
                Body = $"Synthetic benchmark item number {i} with some descriptive text.",
            });
        return list;
    }
}
