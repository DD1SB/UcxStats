using System.Text.Json.Serialization;

namespace UcxStats.Map;

public sealed record MapPanelPosition(double X, double Y)
{
    public MapPanelPosition? Normalize() => double.IsFinite(X) && double.IsFinite(Y)
        ? new(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1)) : null;
}

public sealed record MapSettings(
    double ShortpathWidth = MapPathWidths.DefaultShortpathWidth,
    double LongpathWidth = MapPathWidths.DefaultLongpathWidth,
    bool StatisticsVisible = false,
    bool ContestTimeVisible = false,
    int ContestPauseThresholdMinutes = 60,
    int? OperatingTimeAllowanceMinutes = null,
    int LiveWarningThresholdMinutes = 10,
    int LiveProlongedGapThresholdMinutes = 15)
{
    public int? LiveTimelineHeightPx { get; init; }
    public int LiveTimelineMinutes { get; init; } = 60;
    public bool LiveTimelineSnap { get; init; } = true;
    public int LiveTimelineBucketMinutes { get; init; } = 5;
    public bool LiveTimelineVisible { get; init; } = true;
    public bool LiveTimelineSettingsVisible { get; init; } = true;
    public bool LiveTimelineSettingsCollapsed { get; init; } = true;
    public bool ContentsPanelCollapsed { get; init; }
    public bool StatisticsPanelCollapsed { get; init; }
    public MapPanelPosition? ContentsPanelPosition { get; init; }
    public MapPanelPosition? StatisticsPanelPosition { get; init; }
    public MapPanelPosition? TimelinePanelPosition { get; init; }

    // Open windows own presentation state. Saves establish defaults for new windows;
    // only shared calculation/path settings broadcast to existing windows.
    public MapSettings WithPresentationFrom(MapSettings source) => this with
    {
        StatisticsVisible = source.StatisticsVisible, ContestTimeVisible = source.ContestTimeVisible,
        LiveTimelineHeightPx = source.LiveTimelineHeightPx,
        LiveTimelineMinutes = source.LiveTimelineMinutes, LiveTimelineSnap = source.LiveTimelineSnap,
        LiveTimelineBucketMinutes = source.LiveTimelineBucketMinutes, LiveTimelineVisible = source.LiveTimelineVisible,
        LiveTimelineSettingsVisible = source.LiveTimelineSettingsVisible,
        LiveTimelineSettingsCollapsed = source.LiveTimelineSettingsCollapsed,
        ContentsPanelCollapsed = source.ContentsPanelCollapsed, StatisticsPanelCollapsed = source.StatisticsPanelCollapsed,
        ContentsPanelPosition = source.ContentsPanelPosition, StatisticsPanelPosition = source.StatisticsPanelPosition,
        TimelinePanelPosition = source.TimelinePanelPosition
    };

    [JsonIgnore]
    public MapPathWidths PathWidths => new MapPathWidths(ShortpathWidth, LongpathWidth).Normalize();

    public MapSettings Normalize()
    {
        var widths = PathWidths;
        var warning = LiveWarningThresholdMinutes is >= 1 and <= 1439 ? LiveWarningThresholdMinutes : 10;
        var prolonged = LiveProlongedGapThresholdMinutes is >= 2 and <= 1440 ? LiveProlongedGapThresholdMinutes : 15;
        if (warning >= prolonged) { warning = 10; prolonged = 15; }
        return this with
        {
            LiveTimelineHeightPx = LiveTimelineHeightPx is { } height ? Math.Clamp(height, 20, 320) : null,
            LiveTimelineMinutes = LiveTimelineMinutes is >= 15 and <= 240 ? LiveTimelineMinutes : 60,
            LiveTimelineBucketMinutes = LiveTimelineBucketMinutes is 1 or 2 or 5 or 10 ? LiveTimelineBucketMinutes : 5,
            ContentsPanelPosition = ContentsPanelPosition?.Normalize(),
            StatisticsPanelPosition = StatisticsPanelPosition?.Normalize(),
            TimelinePanelPosition = TimelinePanelPosition?.Normalize(),
            ShortpathWidth = widths.ShortpathWidth, LongpathWidth = widths.LongpathWidth,
            ContestPauseThresholdMinutes = ContestPauseThresholdMinutes is >= 5 and <= 180 ? ContestPauseThresholdMinutes : 60,
            OperatingTimeAllowanceMinutes = OperatingTimeAllowanceMinutes is > 0 and <= 525600 ? OperatingTimeAllowanceMinutes : null,
            LiveWarningThresholdMinutes = warning, LiveProlongedGapThresholdMinutes = prolonged
        };
    }
}
