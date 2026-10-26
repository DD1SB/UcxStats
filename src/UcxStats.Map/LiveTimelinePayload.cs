using System.Text.Json;
using UcxStats.Data.Database;

namespace UcxStats.Map;

// Flattened [second offset, QSO count] pairs, bounded independently of scope size.
// Second precision preserves exact partial-minute/bucket counts and duplicate QSOs.
public sealed record LiveTimelineSnapshot(long GeneratedAtUnixSeconds, long CoverageStartUnixSeconds,
    long CoverageEndUnixSeconds, int[] Samples, LatestQso? LatestQso, int FutureBeyondCoverageCount);

public static class LiveTimelinePayload
{
    public const int MaximumMinutes = 240;
    public const int ClockRecoveryMinutes = 10;
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static LiveTimelineSnapshot Create(QsoStatisticsSnapshot snapshot, DateTimeOffset nowUtc)
    {
        var now = nowUtc.ToUnixTimeSeconds();
        var start = now - (MaximumMinutes + ClockRecoveryMinutes) * 60;
        var end = now + MaximumMinutes * 60;
        var samples = new List<int>();
        var beyond = 0;
        foreach (var item in snapshot.TimestampCountsUtc)
        {
            var time = item.TimestampUtc.ToUnixTimeSeconds();
            if (time > end) { beyond += item.Count; continue; }
            if (time < start) continue;
            samples.Add(checked((int)(time - start)));
            samples.Add(item.Count);
        }
        return new(now, start, end, samples.ToArray(), snapshot.LatestQso, beyond);
    }

    public static string Serialize(LiveTimelineSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);
    public static string BuildScript(string json) => $"window.ucxSetLiveTimeline && window.ucxSetLiveTimeline({json});";
}
