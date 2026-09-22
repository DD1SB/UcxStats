namespace UcxStats.Data.Dxcc;

public enum DxccCountryMappingStatus
{
    Mapped,
    Unresolved
}

public sealed record DxccCountryAuditRow(
    string CountryCode,
    int? MappedEntityId,
    long QsoCount,
    DxccCountryMappingStatus Status);

public sealed record DxccIdentityConflict(
    long QsoId,
    string CountryCode,
    int UcxEntityId,
    int DclEntityId,
    int EffectiveEntityId);

public sealed record DxccExplicitNonDxccEvidence(
    long QsoId,
    string CountryCode,
    int? UcxEntityId,
    int DclCode);

public sealed record DxccIdentityAuditResult(
    long TotalQsos,
    long ResolvedQsos,
    long UnresolvedQsos,
    IReadOnlyList<string> DistinctUnresolvedCountryCodes,
    IReadOnlyList<DxccIdentityConflict> Conflicts,
    IReadOnlyList<DxccCountryAuditRow> Countries,
    IReadOnlyDictionary<DxccIdentityEvidence, long> EvidenceCounts)
{
    public IReadOnlyList<DxccExplicitNonDxccEvidence> ExplicitNonDxccEvidence { get; init; } = [];
    public long ExplicitNonDxccQsos => ExplicitNonDxccEvidence.Count;
}
