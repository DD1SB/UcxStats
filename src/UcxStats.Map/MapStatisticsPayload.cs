using System.Text.Json;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;

namespace UcxStats.Map;

public static class MapStatisticsPayload
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Serialize(QsoStatisticsSnapshot snapshot, MapSettings settings)
    {
        settings = settings.Normalize();
        var contest = ContestTimeEstimator.Estimate(snapshot.TimestampsUtc, settings.ContestPauseThresholdMinutes,
            settings.OperatingTimeAllowanceMinutes, snapshot.InvalidTimestampCount);
        return JsonSerializer.Serialize(new
        {
            bands = snapshot.Bands.Select(x => new { x.BandCode, band = UcxDisplayFormat.Band((byte)x.BandCode), x.Summary }),
            snapshot.DxccMemberships, snapshot.FieldMemberships, snapshot.GridMemberships,
            contest = new
            {
                contest.FirstUtc, contest.LastUtc, contest.GrossSeconds, contest.OperatingSeconds, contest.PauseSeconds,
                contest.PauseCount, pauses = contest.Pauses.Take(100), contest.AllowanceSeconds, contest.RemainingSeconds,
                contest.InvalidTimestampCount
            }
        }, Options);
    }

    public static string SerializeActivity(LatestQso? latest) => JsonSerializer.Serialize(latest, Options);
    public static string SerializeSettings(MapSettings settings) => JsonSerializer.Serialize(settings.Normalize(), Options);
    public static string BuildSnapshotScript(string json) => $"window.ucxSetMapStatistics && window.ucxSetMapStatistics({json});";
    public static string BuildActivityScript(LatestQso? latest) => $"window.ucxSetLiveActivity && window.ucxSetLiveActivity({SerializeActivity(latest)});";
    public static string BuildSettingsScript(MapSettings settings) => $"window.ucxSetStatisticsSettings && window.ucxSetStatisticsSettings({SerializeSettings(settings)});";
}
