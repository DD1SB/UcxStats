using System.Collections.Concurrent;
using System.Globalization;
using UcxStats.Data.Dxcc;
using UcxStats.Geo.Maidenhead;

namespace UcxStats.Data.Database;

public sealed record QsoOdx(long QsoId, string CallSign, double DistanceKm, string Locator,
    int BandCode, DateTimeOffset? TimestampUtc);
public sealed record QsoTimestampCount(DateTimeOffset TimestampUtc, int Count);
public sealed record LatestQso(long QsoId, string CallSign, int BandCode, DateTimeOffset TimestampUtc);
public sealed record DistinctBandMembership(int[] BandCodes, int Count);
public sealed record QsoStatisticsSummary(int TotalQsos, int MappableQsos, int Dxccs,
    int MajorFields, int Grids, QsoOdx? Odx)
{
    public int NonMappableQsos => TotalQsos - MappableQsos;
}
public sealed record BandQsoStatistics(int BandCode, QsoStatisticsSummary Summary);

// Memberships are counts of distinct identities sharing the same set of bands.
// No QSO rows or identity strings need to cross the WebView bridge.
public sealed record QsoStatisticsSnapshot(
    IReadOnlyList<BandQsoStatistics> Bands,
    IReadOnlyList<DistinctBandMembership> DxccMemberships,
    IReadOnlyList<DistinctBandMembership> FieldMemberships,
    IReadOnlyList<DistinctBandMembership> GridMemberships,
    IReadOnlyList<DateTimeOffset> TimestampsUtc,
    int InvalidTimestampCount,
    LatestQso? LatestQso)
{
    // Multiplicity is retained separately: contest estimation still uses distinct instants.
    public IReadOnlyList<QsoTimestampCount> TimestampCountsUtc { get; init; } = [];

    public QsoStatisticsSummary Summarize(IReadOnlyCollection<int>? bandCodes = null)
    {
        var selected = bandCodes?.ToHashSet() ?? Bands.Select(x => x.BandCode).ToHashSet();
        var bands = Bands.Where(x => selected.Contains(x.BandCode)).ToArray();
        int Count(IReadOnlyList<DistinctBandMembership> groups) =>
            groups.Where(x => x.BandCodes.Any(selected.Contains)).Sum(x => x.Count);
        return new(bands.Sum(x => x.Summary.TotalQsos), bands.Sum(x => x.Summary.MappableQsos),
            Count(DxccMemberships), Count(FieldMemberships), Count(GridMemberships),
            bands.Select(x => x.Summary.Odx).Where(x => x is not null)
                .OrderByDescending(x => x!.DistanceKm).ThenBy(x => x!.QsoId).FirstOrDefault());
    }
}

internal sealed class QsoStatisticsAccumulator(ConcurrentDictionary<string, string>? locatorCache = null)
{
    private readonly Dictionary<int, (int Total, int Mappable, QsoOdx? Odx)> _bands = new();
    private readonly Dictionary<int, HashSet<int>> _dxcc = new();
    private readonly Dictionary<string, HashSet<int>> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _grids = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _locators = locatorCache ?? new(StringComparer.Ordinal);
    private readonly List<DateTimeOffset> _timestamps = new();
    private int _invalidTimestamps;
    private LatestQso? _latest;

    public void Add(long id, string call, int band, string locator, string date, string time,
        int? effectiveDxcc, bool mappable, double? distance)
    {
        var timestamp = ParseTimestamp(date, time);
        if (timestamp is { } utc)
        {
            _timestamps.Add(utc);
            if (_latest is null || utc > _latest.TimestampUtc || (utc == _latest.TimestampUtc && id < _latest.QsoId))
                _latest = new(id, call, band, utc);
        }
        else _invalidTimestamps++;

        if (!_locators.TryGetValue(locator, out var normalized))
        {
            try
            {
                MaidenheadConverter.ToCenter(locator);
                normalized = locator.Trim().ToUpperInvariant();
            }
            catch (ArgumentException) { normalized = string.Empty; }
            // Retain pure validation results across map windows and Live snapshots,
            // but bound the cache for logs containing many unique/malformed strings.
            if (_locators.Count < 100_000) _locators.TryAdd(locator, normalized);
        }
        if (normalized.Length > 0)
        {
            AddIdentity(_fields, normalized[..2], band);
            if (normalized.Length >= 4) AddIdentity(_grids, normalized[..4], band);
        }
        if (DxccIdentityResolver.IsPlausibleDclEntityId(effectiveDxcc))
            AddIdentity(_dxcc, effectiveDxcc!.Value, band);

        var current = _bands.GetValueOrDefault(band);
        var odx = current.Odx;
        if (distance is { } km && double.IsFinite(km) && km >= 0 &&
            (odx is null || km > odx.DistanceKm || (km == odx.DistanceKm && id < odx.QsoId)))
            odx = new(id, call, km, normalized.Length > 0 ? normalized : locator, band, timestamp);
        _bands[band] = (current.Total + 1, current.Mappable + (mappable ? 1 : 0), odx);
    }

    public QsoStatisticsSnapshot Build()
    {
        var dxcc = Group(_dxcc); var fields = Group(_fields); var grids = Group(_grids);
        int Count(IReadOnlyList<DistinctBandMembership> groups, int band) =>
            groups.Where(x => x.BandCodes.Contains(band)).Sum(x => x.Count);
        var bands = _bands.OrderBy(x => x.Key).Select(x => new BandQsoStatistics(x.Key,
            new(x.Value.Total, x.Value.Mappable, Count(dxcc, x.Key), Count(fields, x.Key),
                Count(grids, x.Key), x.Value.Odx))).ToArray();
        var counts = _timestamps.GroupBy(x => x).OrderBy(x => x.Key)
            .Select(x => new QsoTimestampCount(x.Key, x.Count())).ToArray();
        return new(bands, dxcc, fields, grids, counts.Select(x => x.TimestampUtc).ToArray(), _invalidTimestamps, _latest)
        { TimestampCountsUtc = counts };
    }

    internal static DateTimeOffset? ParseTimestamp(string date, string time) =>
        DateTimeOffset.TryParseExact($"{date} {time}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) ? utc : null;

    private static void AddIdentity<T>(Dictionary<T, HashSet<int>> identities, T key, int band) where T : notnull
    {
        if (!identities.TryGetValue(key, out var bands)) identities[key] = bands = new();
        bands.Add(band);
    }

    private static DistinctBandMembership[] Group<T>(Dictionary<T, HashSet<int>> identities) where T : notnull =>
        identities.Values.Select(x => x.Order().ToArray()).GroupBy(x => string.Join(",", x), StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new DistinctBandMembership(x.First(), x.Count())).ToArray();
}
