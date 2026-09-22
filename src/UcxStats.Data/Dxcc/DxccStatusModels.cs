namespace UcxStats.Data.Dxcc;

public enum DxccOverlayStatus
{
    Unknown = 0,
    NotWorked = 1,
    Worked = 2,
    OtherConfirmed = 3,
    DxccCredited = 4
}

public sealed record DxccEntityBandStatus(
    int EntityId,
    int BandCode,
    int QsoCount,
    int EqslOrClubLogConfirmedCount,
    int DxccCreditedQsoCount);

public sealed record DxccStatusSnapshot(
    IReadOnlyList<DxccEntityBandStatus> Bands,
    int UnresolvedQsoCount,
    int ExplicitNonDxccQsoCount = 0);

public static class DxccOverlayStatusEvaluator
{
    public static DxccOverlayStatus Evaluate(
        DxccCatalogEntityStatus catalogStatus,
        IEnumerable<DxccEntityBandStatus> entityBands,
        IReadOnlyCollection<int>? selectedBandCodes = null)
    {
        if (catalogStatus != DxccCatalogEntityStatus.Current)
            return DxccOverlayStatus.Unknown;

        var selected = selectedBandCodes is null
            ? entityBands.ToArray()
            : entityBands.Where(x => selectedBandCodes.Contains(x.BandCode)).ToArray();

        if (selected.Sum(x => x.QsoCount) == 0)
            return DxccOverlayStatus.NotWorked;
        if (selected.Any(x => x.DxccCreditedQsoCount > 0))
            return DxccOverlayStatus.DxccCredited;
        if (selected.Any(x => x.EqslOrClubLogConfirmedCount > 0))
            return DxccOverlayStatus.OtherConfirmed;
        return DxccOverlayStatus.Worked;
    }
}
