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
            longpath ?? ReadWidth(legacy, nameof(MapSettings.LongpathWidth)) ?? MapPathWidths.DefaultLongpathWidth);
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
        SetWidths(document, settings.Normalize());
        _write(Path, document);
    }

    private static void SetWidths(JsonObject document, MapSettings settings)
    {
        document[nameof(MapSettings.ShortpathWidth)] = settings.ShortpathWidth;
        document[nameof(MapSettings.LongpathWidth)] = settings.LongpathWidth;
    }

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
