namespace UcxStats.Map;

public sealed class MapSettingsService
{
    private readonly MapSettingsStore _store;

    public MapSettingsService(MapSettingsStore? store = null)
    {
        _store = store ?? new MapSettingsStore();
        var loaded = _store.Load();
        Current = loaded.Settings;
        Error = loaded.Error;
    }

    public MapSettings Current { get; private set; }
    public string? Error { get; private set; }
    public string Path => _store.Path;
    public event Action<MapSettings>? Changed;

    public void Save(MapSettings settings)
    {
        var normalized = settings.Normalize();
        _store.Save(normalized);
        Error = null;
        if (Current == normalized)
            return;

        Current = normalized;
        Changed?.Invoke(Current);
    }
}
