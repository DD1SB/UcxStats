using System.Collections.Frozen;

namespace UcxStats.Data.Dxcc;

public enum UcxDxccMappingKind
{
    Direct,
    Alias,
    Historical,
    Ambiguous,
    Unknown
}

public sealed record UcxCountryDxccMapping(
    string CountryCode,
    int? EntityId,
    UcxDxccMappingKind Kind,
    string? Rationale = null)
{
    public bool IsResolved => EntityId.HasValue &&
        Kind is UcxDxccMappingKind.Direct or UcxDxccMappingKind.Alias or UcxDxccMappingKind.Historical;
}

public sealed record UcxCountryDxccMappingSourceMetadata(
    string Product,
    string Version,
    DateOnly CountryDataDate,
    string ArchiveFileName,
    string ResourcePath,
    string ArchiveSha256,
    string ResourceSha256,
    string DecodedResourceSha256);

public sealed record UcxCountryDxccMappingAudit(
    int SourceEntityRowCount,
    int DistinctCountryCodeCount,
    int CurrentMappings,
    int DeletedMappings,
    int ExplicitNonDxccMappings,
    int UnresolvedMappings,
    IReadOnlyList<string> UnresolvedCountryCodes,
    IReadOnlyList<string> DuplicateSourceCountryCodes,
    IReadOnlyList<int> DuplicateTargetEntityIds,
    IReadOnlyList<string> UnknownTargetCountryCodes);

internal static class UcxCountryDxccMappings
{
    public static readonly UcxCountryDxccMappingSourceMetadata Source = new(
        Product: "UcxLog",
        Version: "8.50",
        CountryDataDate: new DateOnly(2026, 6, 11),
        ArchiveFileName: "UcxLog.zip",
        ResourcePath: "COUNTRY.DAT / Country0.txt entity-definition section",
        ArchiveSha256: "6c972c70cf6284f784610394de2afe31039f51608d21f530a4e6c05015d1461f",
        ResourceSha256: "4564deb87791947228984bc92d1181ab8574d517e1e9e038ecf91145effc2186",
        DecodedResourceSha256: "05685def3be5880e8e09ace9fc87257f78ec27710d6cc3d73e0f414f80abce3f");

    public static readonly IReadOnlyDictionary<string, UcxCountryDxccMapping> Entries =
        UcxLog850CountryDxccMappingData.Entries
            .ToFrozenDictionary(x => x.CountryCode, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, int> ResolvedEntityIds = Entries.Values
        .Where(x => x.IsResolved)
        .ToFrozenDictionary(x => x.CountryCode, x => x.EntityId!.Value, StringComparer.OrdinalIgnoreCase);

    public static UcxCountryDxccMappingAudit Audit(IDxccEntityCatalog catalog)
    {
        var current = 0;
        var deleted = 0;
        var unresolved = new List<string>();
        var unknownTargets = new List<string>();

        foreach (var mapping in Entries.Values)
        {
            if (!mapping.IsResolved)
            {
                unresolved.Add(mapping.CountryCode);
                continue;
            }

            switch (catalog.GetStatus(mapping.EntityId!.Value))
            {
                case DxccCatalogEntityStatus.Current:
                    current++;
                    break;
                case DxccCatalogEntityStatus.Deleted:
                    deleted++;
                    break;
                default:
                    unresolved.Add(mapping.CountryCode);
                    unknownTargets.Add(mapping.CountryCode);
                    break;
            }
        }

        var duplicateTargets = Entries.Values
            .Where(x => x.IsResolved)
            .GroupBy(x => x.EntityId!.Value)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Order()
            .ToArray();

        return new UcxCountryDxccMappingAudit(
            UcxLog850CountryDxccMappingData.SourceEntityRowCount,
            UcxLog850CountryDxccMappingData.SourceDistinctCountryCodeCount,
            current,
            deleted,
            UcxLog850CountryDxccMappingData.SourceExplicitNonDxccCount,
            unresolved.Count,
            unresolved.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            UcxLog850CountryDxccMappingData.DuplicateSourceCountryCodes,
            duplicateTargets,
            unknownTargets.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
