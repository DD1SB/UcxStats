using System.Collections.Frozen;

namespace UcxStats.Data.Dxcc;

public enum DxccCatalogEntityStatus
{
    Unknown,
    Current,
    Deleted
}

public sealed record DxccCatalogEntry(
    int EntityId,
    DxccCatalogEntityStatus Status,
    string Name,
    bool IsExplicitNonDxcc = false);

public sealed record DxccCatalogSourceMetadata(
    string Standard,
    string Version,
    DateOnly ReleasedOn,
    string ArchiveFileName,
    string ResourcePath,
    string ResourceSha256);

public interface IDxccEntityCatalog
{
    DxccCatalogSourceMetadata Source { get; }
    IReadOnlyList<DxccCatalogEntry> Entries { get; }
    DxccCatalogEntry? Find(int entityId);
    DxccCatalogEntityStatus GetStatus(int entityId);
}

public sealed class DxccEntityCatalog : IDxccEntityCatalog
{
    private readonly FrozenDictionary<int, DxccCatalogEntry> _entriesById;

    public DxccEntityCatalog(
        IEnumerable<DxccCatalogEntry> entries,
        DxccCatalogSourceMetadata source)
    {
        Source = source;
        Entries = entries.OrderBy(x => x.EntityId).ToArray();
        _entriesById = Entries.ToFrozenDictionary(x => x.EntityId);
    }

    public DxccCatalogSourceMetadata Source { get; }
    public IReadOnlyList<DxccCatalogEntry> Entries { get; }

    public DxccCatalogEntry? Find(int entityId) => _entriesById.GetValueOrDefault(entityId);

    public DxccCatalogEntityStatus GetStatus(int entityId) =>
        Find(entityId)?.Status ?? DxccCatalogEntityStatus.Unknown;

    public static DxccEntityCatalog ReleasedAdif317 { get; } = new(
        Adif317DxccCatalogData.Entries,
        new DxccCatalogSourceMetadata(
            Standard: "ADIF",
            Version: "3.1.7",
            ReleasedOn: new DateOnly(2026, 3, 22),
            ArchiveFileName: "ADIF_317_resources_2026_03_22.zip",
            ResourcePath: "317/exports/tsv/enumerations_dxcc_entity_code.tsv",
            ResourceSha256: "569e88410f5c69291e74a6b5f0d420647d4d109446c1596b4e3a38b73979dc15"));
}
