namespace UcxStats.Data.Dxcc;

public enum DxccIdentityEvidence
{
    Unresolved,
    ExplicitNonDxcc,
    UcxOnly,
    DclFallback,
    Agreement,
    Conflict
}

public enum DxccIdentityState
{
    Resolved,
    Unresolved,
    ExplicitNonDxcc
}

public sealed record DxccIdentityResolution(
    int? UcxEntityId,
    int? DclEntityId,
    int? EffectiveEntityId,
    DxccIdentityEvidence Evidence)
{
    public bool HasConflict => Evidence == DxccIdentityEvidence.Conflict;

    public DxccIdentityState State => Evidence switch
    {
        DxccIdentityEvidence.Unresolved => DxccIdentityState.Unresolved,
        DxccIdentityEvidence.ExplicitNonDxcc => DxccIdentityState.ExplicitNonDxcc,
        _ => DxccIdentityState.Resolved
    };
}

public static class DxccIdentityResolver
{
    public const int MappingVersion = 3;
    public const int AdifExplicitNonDxccCode = 0;
    public const int DclExplicitNonDxccCode = 999;

    // These are explicit UCX country classifications, not a callsign-prefix resolver.
    public static IReadOnlyDictionary<string, int> ExplicitCountryMappings =>
        UcxCountryDxccMappings.ResolvedEntityIds;

    public static IReadOnlyDictionary<string, UcxCountryDxccMapping> CountryMappingMetadata =>
        UcxCountryDxccMappings.Entries;

    public static UcxCountryDxccMappingSourceMetadata CountryMappingSource =>
        UcxCountryDxccMappings.Source;

    public static UcxCountryDxccMappingAudit AuditCountryMappings() =>
        UcxCountryDxccMappings.Audit(DxccEntityCatalog.ReleasedAdif317);

    public static UcxCountryDxccMapping ClassifyUcxCountry(string? country)
    {
        var normalized = country?.Trim() ?? string.Empty;
        return normalized.Length > 0 && UcxCountryDxccMappings.Entries.TryGetValue(normalized, out var mapping)
            ? mapping
            : new UcxCountryDxccMapping(
                normalized,
                null,
                UcxDxccMappingKind.Unknown,
                "No explicit, unambiguous UCX-country mapping is available.");
    }

    public static int? ResolveUcxCountry(string? country)
    {
        var mapping = ClassifyUcxCountry(country);
        return mapping.IsResolved ? mapping.EntityId : null;
    }

    public static bool IsExplicitNonDxccCode(int? entityId) =>
        entityId is AdifExplicitNonDxccCode or DclExplicitNonDxccCode;

    public static bool IsPlausibleDclEntityId(int? entityId)
    {
        if (entityId is null || IsExplicitNonDxccCode(entityId))
            return false;

        return DxccEntityCatalog.ReleasedAdif317.Find(entityId.Value)?.Status is
            DxccCatalogEntityStatus.Current or DxccCatalogEntityStatus.Deleted;
    }

    public static bool IsRecognizedDclIdentityCode(int? entityId) =>
        IsPlausibleDclEntityId(entityId) || IsExplicitNonDxccCode(entityId);

    public static DxccIdentityResolution Resolve(string? ucxCountry, int? dclDxcc)
    {
        var ucx = ResolveUcxCountry(ucxCountry);

        if (IsExplicitNonDxccCode(dclDxcc))
            return new DxccIdentityResolution(
                ucx,
                dclDxcc,
                null,
                DxccIdentityEvidence.ExplicitNonDxcc);

        var dcl = IsPlausibleDclEntityId(dclDxcc) ? dclDxcc : null;

        if (ucx is null && dcl is null)
            return new DxccIdentityResolution(null, null, null, DxccIdentityEvidence.Unresolved);
        if (ucx is not null && dcl is null)
            return new DxccIdentityResolution(ucx, null, ucx, DxccIdentityEvidence.UcxOnly);
        if (ucx is null)
            return new DxccIdentityResolution(null, dcl, dcl, DxccIdentityEvidence.DclFallback);
        if (ucx == dcl)
            return new DxccIdentityResolution(ucx, dcl, ucx, DxccIdentityEvidence.Agreement);

        // UCX supplies the effective identity; the differing DCL assignment is
        // retained as diagnostic evidence. APP_DCL_ADIF2 is not an input here.
        return new DxccIdentityResolution(ucx, dcl, ucx, DxccIdentityEvidence.Conflict);
    }
}
