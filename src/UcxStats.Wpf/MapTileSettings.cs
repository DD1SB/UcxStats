using System.IO;
using System.Text.Json;
namespace UcxStats.Wpf;

internal sealed record MapTileSettings(
    string ProviderName,
    string UrlTemplate,
    string Attribution,
    int MaxZoom)
{
    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UcxStats",
            "map-tiles.json");

    public static MapTileSettings Load()
    {
        var defaults = Default;
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);

            if (!File.Exists(SettingsPath))
            {
                File.WriteAllText(
                    SettingsPath,
                    JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true }));
                return defaults;
            }

            var parsed = JsonSerializer.Deserialize<MapTileSettings>(File.ReadAllText(SettingsPath));
            if (parsed is null ||
                string.IsNullOrWhiteSpace(parsed.ProviderName) ||
                string.IsNullOrWhiteSpace(parsed.UrlTemplate) ||
                string.IsNullOrWhiteSpace(parsed.Attribution) ||
                parsed.MaxZoom < 1)
                return defaults;

            return parsed;
        }
        catch
        {
            return defaults;
        }
    }

    public static MapTileSettings Default { get; } = new(
        "OpenStreetMap",
        "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        "&copy; OpenStreetMap contributors",
        19);
}
