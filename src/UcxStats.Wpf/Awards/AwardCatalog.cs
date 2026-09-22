namespace UcxStats.Wpf.Awards;

public sealed record AwardCatalogEntry(
    string Id,
    string Name,
    string Variant,
    string Description,
    bool Available);

public static class AwardCatalog
{
    public static IReadOnlyList<AwardCatalogEntry> Entries { get; } =
    [
        new(
            "wae-mixed",
            "Worked All Europe",
            "Mixed",
            "WAE-Länder und bestätigte Länder/Band-Kombinationen; Klassen III, II, I und TOP.",
            true)
    ];
}
