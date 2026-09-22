using UcxStats.Core.Ucx;

namespace UcxStats.Wpf.Awards;

public sealed class WaeGridRow
{
    public required WaeEntityResult Result { get; init; }
    public string Code => Result.Entity.Code;
    public string Country => Result.Entity.Name;
    public int Points => Result.Points;
    public int ConfirmedBands => Result.ConfirmedBandCount;

    public string BandState(int bandCode)
    {
        if (!Result.Bands.TryGetValue(bandCode, out var state))
            return string.Empty;
        if (state.Confirmed) return "✓";
        if (state.Worked) return "○";
        return string.Empty;
    }

    public IReadOnlyList<UcxStats.Data.Database.WaeQsoCandidate> QsosForBand(int bandCode) =>
        Result.Bands.TryGetValue(bandCode, out var state) ? state.Qsos : [];
}
