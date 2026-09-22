using System.Text.Json;

namespace UcxStats.Map;

public sealed record DxccMapConfiguration(
    string? GeoJsonPath = null,
    bool EnabledByDefault = false);

public sealed record DxccMapConfigurationLoadResult(
    DxccMapConfiguration Configuration,
    string? Error = null);

public sealed class DxccMapConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public DxccMapConfigurationStore(string? path = null)
    {
        _path = path ?? DefaultPath;
    }

    public string Path => _path;

    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UcxStats",
        "map-dxcc.json");

    public DxccMapConfigurationLoadResult Load()
    {
        if (!File.Exists(_path))
            return new(new DxccMapConfiguration());

        try
        {
            var configuration = JsonSerializer.Deserialize<DxccMapConfiguration>(File.ReadAllText(_path));
            if (configuration is null)
                return new(new DxccMapConfiguration(), "Die DXCC-Kartenkonfiguration ist leer oder ungültig.");

            var geoJsonPath = string.IsNullOrWhiteSpace(configuration.GeoJsonPath)
                ? null
                : configuration.GeoJsonPath.Trim();
            if (geoJsonPath is not null && !System.IO.Path.IsPathFullyQualified(geoJsonPath))
                return new(new DxccMapConfiguration(), "Der DXCC-GeoJSON-Pfad muss absolut sein.");

            return new(configuration with { GeoJsonPath = geoJsonPath });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(new DxccMapConfiguration(), $"DXCC-Kartenkonfiguration konnte nicht gelesen werden: {ex.Message}");
        }
    }

    public void Save(DxccMapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var geoJsonPath = string.IsNullOrWhiteSpace(configuration.GeoJsonPath)
            ? null
            : configuration.GeoJsonPath.Trim();
        if (geoJsonPath is not null && !System.IO.Path.IsPathFullyQualified(geoJsonPath))
            throw new ArgumentException("The DXCC GeoJSON path must be absolute.", nameof(configuration));

        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The DXCC configuration path has no parent directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            _path,
            JsonSerializer.Serialize(configuration with { GeoJsonPath = geoJsonPath }, JsonOptions));
    }
}
