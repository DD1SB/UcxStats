using System.Text.Json;
using System.Text.Json.Nodes;

namespace UcxStats.Map;

public sealed record MapSettingsLoadResult(MapSettings Settings, string? Error = null);

public sealed class MapSettingsStore
{
    private readonly Action<string, JsonObject> _write;

    public MapSettingsStore(string? path = null, string? legacyDxccPath = null)
        : this(path ?? DefaultPath, legacyDxccPath, AtomicJsonFile.WriteIfChanged)
    {
    }

    internal MapSettingsStore(string path, string? legacyDxccPath, Action<string, JsonObject> write)
    {
        Path = System.IO.Path.GetFullPath(path);
        LegacyDxccPath = System.IO.Path.GetFullPath(legacyDxccPath ?? System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Path)!, "map-dxcc.json"));
        _write = write;
    }

    public string Path { get; }
    public string LegacyDxccPath { get; }
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UcxStats", "map-settings.json");

    public MapSettingsLoadResult Load()
    {
        var errors = new List<string>();
        var general = Read(Path, errors, out var canWriteGeneral);
        var legacy = Read(LegacyDxccPath, errors, out _);
        var shortpath = ReadWidth(general, nameof(MapSettings.ShortpathWidth));
        var longpath = ReadWidth(general, nameof(MapSettings.LongpathWidth));
        var settings = new MapSettings(
            shortpath ?? ReadWidth(legacy, nameof(MapSettings.ShortpathWidth)) ?? MapPathWidths.DefaultShortpathWidth,
            longpath ?? ReadWidth(legacy, nameof(MapSettings.LongpathWidth)) ?? MapPathWidths.DefaultLongpathWidth,
            ReadBool(general, nameof(MapSettings.StatisticsVisible)),
            ReadBool(general, nameof(MapSettings.ContestTimeVisible)),
            ReadInt(general, nameof(MapSettings.ContestPauseThresholdMinutes)) ?? 60,
            ReadInt(general, nameof(MapSettings.OperatingTimeAllowanceMinutes)),
            ReadInt(general, nameof(MapSettings.LiveWarningThresholdMinutes)) ?? 10,
            ReadInt(general, nameof(MapSettings.LiveProlongedGapThresholdMinutes)) ?? 15)
        {
            LiveTimelineHeightPx = ReadInt(general, nameof(MapSettings.LiveTimelineHeightPx)),
            LiveTimelineMinutes = ReadInt(general, nameof(MapSettings.LiveTimelineMinutes)) ?? 60,
            LiveTimelineSnap = ReadBool(general, nameof(MapSettings.LiveTimelineSnap), true),
            LiveTimelineBucketMinutes = ReadInt(general, nameof(MapSettings.LiveTimelineBucketMinutes)) ?? 5,
            LiveTimelineVisible = ReadBool(general, nameof(MapSettings.LiveTimelineVisible), true),
            LiveTimelineSettingsVisible = ReadBool(general, nameof(MapSettings.LiveTimelineSettingsVisible), true),
            LiveTimelineSettingsCollapsed = ReadBool(general, nameof(MapSettings.LiveTimelineSettingsCollapsed), true),
            ContentsPanelCollapsed = ReadBool(general, nameof(MapSettings.ContentsPanelCollapsed)),
            StatisticsPanelCollapsed = ReadBool(general, nameof(MapSettings.StatisticsPanelCollapsed)),
            ContentsPanelPosition = ReadPosition(general, nameof(MapSettings.ContentsPanelPosition)),
            StatisticsPanelPosition = ReadPosition(general, nameof(MapSettings.StatisticsPanelPosition)),
            TimelinePanelPosition = ReadPosition(general, nameof(MapSettings.TimelinePanelPosition))
        }.Normalize();
        var legacyHasWidths = HasLegacyWidths(legacy);
        var generalComplete = shortpath.HasValue && longpath.HasValue;

        // A fresh install needs no file. A partial valid JSON object is completed
        // once, so later startups cannot resurrect stale legacy values.
        if (canWriteGeneral && !generalComplete && (general is not null || legacyHasWidths))
        {
            general ??= new JsonObject();
            SetWidths(general, settings);
            try
            {
                _write(Path, general);
                generalComplete = true;
            }
            catch (Exception ex) when (IsConfigurationError(ex))
            {
                errors.Add($"Kartenoptionen konnten nicht migriert werden: {ex.Message}");
            }
        }

        // Never remove the source until both effective widths are committed.
        // If cleanup is interrupted, the valid new values win on the next run.
        if (generalComplete && legacyHasWidths)
        {
            legacy!.Remove(nameof(MapSettings.ShortpathWidth));
            legacy.Remove(nameof(MapSettings.LongpathWidth));
            try { _write(LegacyDxccPath, legacy); }
            catch (Exception ex) when (IsConfigurationError(ex))
            {
                errors.Add($"Alte Kartenoptionen konnten nicht bereinigt werden: {ex.Message}");
            }
        }

        return new(settings, errors.Count == 0 ? null : string.Join(Environment.NewLine, errors));
    }

    public void Save(MapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // Re-read to retain unknown properties. Malformed JSON is left untouched.
        var document = AtomicJsonFile.ReadObject(Path) ?? new JsonObject();
        settings = settings.Normalize();
        SetWidths(document, settings);
        document[nameof(MapSettings.StatisticsVisible)] = settings.StatisticsVisible;
        document[nameof(MapSettings.ContestTimeVisible)] = settings.ContestTimeVisible;
        document[nameof(MapSettings.ContestPauseThresholdMinutes)] = settings.ContestPauseThresholdMinutes;
        document[nameof(MapSettings.OperatingTimeAllowanceMinutes)] = settings.OperatingTimeAllowanceMinutes;
        document[nameof(MapSettings.LiveWarningThresholdMinutes)] = settings.LiveWarningThresholdMinutes;
        document[nameof(MapSettings.LiveProlongedGapThresholdMinutes)] = settings.LiveProlongedGapThresholdMinutes;
        var serialized = JsonSerializer.SerializeToNode(settings)!;
        foreach (var name in PresentationPropertyNames)
            document[name] = serialized[name]?.DeepClone();
        _write(Path, document);
    }

    private static readonly string[] PresentationPropertyNames =
    [
        nameof(MapSettings.LiveTimelineHeightPx), nameof(MapSettings.LiveTimelineMinutes), nameof(MapSettings.LiveTimelineSnap), nameof(MapSettings.LiveTimelineBucketMinutes),
        nameof(MapSettings.LiveTimelineVisible), nameof(MapSettings.LiveTimelineSettingsVisible), nameof(MapSettings.LiveTimelineSettingsCollapsed),
        nameof(MapSettings.ContentsPanelCollapsed), nameof(MapSettings.StatisticsPanelCollapsed),
        nameof(MapSettings.ContentsPanelPosition), nameof(MapSettings.StatisticsPanelPosition), nameof(MapSettings.TimelinePanelPosition)
    ];

    private static MapPanelPosition? ReadPosition(JsonObject? document, string name) =>
        document?[name] is JsonObject position && position["X"] is JsonValue x && x.TryGetValue<double>(out var xv) &&
        position["Y"] is JsonValue y && y.TryGetValue<double>(out var yv) ? new(xv, yv) : null;

    private static void SetWidths(JsonObject document, MapSettings settings)
    {
        document[nameof(MapSettings.ShortpathWidth)] = settings.ShortpathWidth;
        document[nameof(MapSettings.LongpathWidth)] = settings.LongpathWidth;
    }

    private static bool ReadBool(JsonObject? document, string name, bool fallback = false) =>
        document?[name] is JsonValue node && node.TryGetValue<bool>(out var value) ? value : fallback;

    private static int? ReadInt(JsonObject? document, string name) =>
        document?[name] is JsonValue node && node.TryGetValue<int>(out var value) ? value : null;

    private static bool HasLegacyWidths(JsonObject? document) => document is not null &&
        (document.ContainsKey(nameof(MapSettings.ShortpathWidth)) || document.ContainsKey(nameof(MapSettings.LongpathWidth)));

    private static double? ReadWidth(JsonObject? document, string name) =>
        document?[name] is JsonValue node && node.TryGetValue<double>(out var value) && MapPathWidths.IsValidWidth(value)
            ? value : null;

    private static JsonObject? Read(string path, List<string> errors, out bool canWrite)
    {
        try
        {
            var document = AtomicJsonFile.ReadObject(path);
            canWrite = true;
            return document;
        }
        catch (Exception ex) when (IsConfigurationError(ex))
        {
            canWrite = false;
            errors.Add($"Konfiguration konnte nicht gelesen werden ({path}): {ex.Message}");
            return null;
        }
    }

    private static bool IsConfigurationError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException;
}
