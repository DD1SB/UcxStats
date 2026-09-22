namespace UcxStats.Wpf;

public readonly record struct MapViewState(Guid SourceId, double Latitude, double Longitude, double Zoom);

public static class MapSyncService
{
    public static event Action<MapViewState>? ViewChanged;

    public static void Publish(Guid sourceId, double latitude, double longitude, double zoom)
        => ViewChanged?.Invoke(new MapViewState(sourceId, latitude, longitude, zoom));
}
