using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using UcxStats.Map;

namespace UcxStats.Wpf;

public partial class MapWindow : Window
{
    private const string MapVirtualHost = "ucxstats.local";
    private const string DxccStagedFileName = "dxcc.geojson";

    private readonly IQsoRepository _repository;
    private readonly Guid _syncId = Guid.NewGuid();
    private readonly MapTileSettings _tileSettings = MapTileSettings.Load();
    private readonly DxccMapConfigurationStore _dxccConfigurationStore = new();
    private readonly string _mapWebFolder;
    private IReadOnlyList<MapQsoPoint> _points;
    private QsoScope _scope;
    private bool _fullscreen;
    private bool _dxccGeometryPrepared;
    private bool _dxccGeometryAvailable;
    private bool _dxccEnabledByDefault;
    private bool? _dxccVisibleOverride;
    private string _dxccDiagnostic = "DXCC: nicht konfiguriert.";
    private string _dxccPerformance = string.Empty;
    private string _mapStatus = string.Empty;
    private WindowStyle _oldStyle;
    private WindowState _oldState;
    private ResizeMode _oldResizeMode;

    public MapWindow(
        IQsoRepository repository,
        QsoScope scope,
        IReadOnlyList<MapQsoPoint> points,
        string title)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scope);
        InitializeComponent();
        _repository = repository;
        _scope = scope;
        _points = points;
        _mapWebFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UcxStats",
            "MapWeb",
            _syncId.ToString("N"));
        Directory.CreateDirectory(_mapWebFolder);
        HeaderText.Text = title;
        Loaded += MapWindow_Loaded;
        Closed += MapWindow_Closed;
        MapSyncService.ViewChanged += MapSyncService_ViewChanged;
    }

    private async void MapWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetMapStatus("Karte wird vorbereitet…");
            await MapWebView.EnsureCoreWebView2Async();
            var core = MapWebView.CoreWebView2;
            if (core is null)
                throw new InvalidOperationException("WebView2 konnte nicht initialisiert werden.");
            ConfigureWebView(core);
            core.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            core.WebMessageReceived += CoreWebView2_WebMessageReceived;
            var dxcc = await PrepareDxccPageAsync();
            NavigateMap(_points, dxcc);
            SetMapStatus($"{_points.Count:N0} QSOs mit Locator und Stations-QTH · Tiles: {_tileSettings.ProviderName}.");
        }
        catch (Exception ex)
        {
            SetMapStatus("Karte konnte nicht geladen werden.");
            MessageBox.Show(this, ex.ToString(), "UcxStats Karte", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task UpdateDataAsync(
        IReadOnlyList<MapQsoPoint> points,
        string title,
        QsoScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _points = points;
        _scope = scope;
        _dxccGeometryPrepared = false;
        _dxccGeometryAvailable = false;
        _dxccPerformance = string.Empty;
        HeaderText.Text = title;
        SetMapStatus("Kartendaten werden aktualisiert…");
        if (MapWebView.CoreWebView2 is null)
        {
            await MapWebView.EnsureCoreWebView2Async();
            var core = MapWebView.CoreWebView2;
            if (core is null)
                return;
            ConfigureWebView(core);
            core.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            core.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        var dxcc = await PrepareDxccPageAsync();
        NavigateMap(_points, dxcc);
        SetMapStatus($"{_points.Count:N0} QSOs mit Locator und Stations-QTH · Tiles: {_tileSettings.ProviderName}.");
    }

    public async Task UpdateLiveDataAsync(
        IReadOnlyList<MapQsoPoint> points,
        string title,
        QsoScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        HeaderText.Text = title;
        _scope = scope;

        if (MapWebView.CoreWebView2 is null)
        {
            await UpdateDataAsync(points, title, scope);
            return;
        }

        var oldIds = _points.Select(x => x.Id).ToHashSet();
        var newIds = points.Select(x => x.Id).ToHashSet();
        var oldById = _points.ToDictionary(x => x.Id);
        var requiresReplacement = !oldIds.IsSubsetOf(newIds) ||
            points.Any(x => oldById.TryGetValue(x.Id, out var old) && old != x);
        if (requiresReplacement)
        {
            var replacementJson = MapPageBuilder.BuildQsoPayloadJson(points);
            await MapWebView.ExecuteScriptAsync(
                $"window.ucxReplaceQsos && window.ucxReplaceQsos({replacementJson});");
        }
        var added = requiresReplacement
            ? new List<MapQsoPoint>()
            : points.Where(x => !oldIds.Contains(x.Id)).ToList();

        if (added.Count > 0)
        {
            var json = MapPageBuilder.BuildQsoPayloadJson(added);
            await MapWebView.ExecuteScriptAsync(
                $"window.ucxAppendQsos && window.ucxAppendQsos({json});");
        }

        // This is deliberately independent of marker additions: a locator-less QSO can
        // change an entity state even when the WebView receives no new MapQsoPoint.
        await RefreshDxccStatusAsync();
        _points = points;
        SetMapStatus(requiresReplacement
            ? $"{_points.Count:N0} QSOs · Live-Kartendaten aktualisiert."
            : added.Count == 0
            ? $"{_points.Count:N0} QSOs · keine neuen kartierbaren QSOs."
            : $"{_points.Count:N0} QSOs · +{added.Count:N0} live hinzugefügt.");
    }

    private async Task<MapDxccPageOptions> PrepareDxccPageAsync()
    {
        if (!_dxccGeometryPrepared)
        {
            try
            {
                await PrepareDxccGeometryAsync();
            }
            catch (Exception ex)
            {
                _dxccGeometryPrepared = true;
                _dxccGeometryAvailable = false;
                _dxccDiagnostic = $"DXCC: Vorbereitung fehlgeschlagen – {ex.Message}";
                UpdateStatusText();
            }
        }

        if (!_dxccGeometryAvailable)
            return MapDxccPageOptions.Unavailable(_dxccDiagnostic);

        var status = await LoadDxccStatusAsync();
        return new MapDxccPageOptions(
            true,
            _dxccVisibleOverride ?? _dxccEnabledByDefault,
            $"https://{MapVirtualHost}/{DxccStagedFileName}",
            status,
            _dxccDiagnostic);
    }

    private async Task PrepareDxccGeometryAsync()
    {
        _dxccGeometryPrepared = true;
        var configurationResult = _dxccConfigurationStore.Load();
        if (configurationResult.Error is not null)
        {
            _dxccDiagnostic = $"DXCC: {configurationResult.Error}";
            return;
        }

        var configuration = configurationResult.Configuration;
        _dxccEnabledByDefault = configuration.EnabledByDefault;
        if (string.IsNullOrWhiteSpace(configuration.GeoJsonPath))
        {
            _dxccDiagnostic = $"DXCC: keine GeoJSON-Datei konfiguriert ({_dxccConfigurationStore.Path}).";
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var loadResult = await Task.Run(() => new DxccGeoJsonLoader().Load(configuration.GeoJsonPath));
        stopwatch.Stop();
        if (!loadResult.IsValid)
        {
            var detail = loadResult.Diagnostics.FirstOrDefault(x =>
                x.Severity == DxccGeoJsonDiagnosticSeverity.Error)?.Message
                ?? "Validierung fehlgeschlagen.";
            _dxccDiagnostic = $"DXCC: GeoJSON nicht verfügbar – {detail}";
            return;
        }

        var stageWatch = Stopwatch.StartNew();
        var normalized = await Task.Run(() => DxccMapGeoJsonSerializer.Serialize(loadResult));
        await File.WriteAllTextAsync(Path.Combine(_mapWebFolder, DxccStagedFileName), normalized);
        stageWatch.Stop();

        _dxccGeometryAvailable = true;
        var warningCount = loadResult.Diagnostics.Count(x =>
            x.Severity == DxccGeoJsonDiagnosticSeverity.Warning);
        var sourceDescription = loadResult.IsKnownAmazingProducerSource
            ? "bekanntes Normalisierungsprofil"
            : "unbekannter Hash; nur allgemeine Validierung";
        _dxccDiagnostic =
            $"DXCC: {loadResult.UniqueDxccEntityIdCount:N0} IDs · {sourceDescription}" +
            (warningCount == 0 ? "." : $" · {warningCount:N0} Warnungen.");
        _dxccPerformance =
            $"GeoJSON {stopwatch.Elapsed.TotalMilliseconds:N0} ms, Staging {stageWatch.Elapsed.TotalMilliseconds:N0} ms";
        UpdateStatusText();
    }

    private async Task<DxccMapStatusPayload> LoadDxccStatusAsync()
    {
        try
        {
            return DxccMapPayloadFactory.Create(await _repository.GetDxccStatusAsync(_scope));
        }
        catch (Exception ex)
        {
            _dxccDiagnostic = $"DXCC: Statusabfrage fehlgeschlagen – {ex.Message}";
            UpdateStatusText();
            return DxccMapPayloadFactory.Unavailable(ex.Message);
        }
    }

    private async Task RefreshDxccStatusAsync()
    {
        if (!_dxccGeometryAvailable || MapWebView.CoreWebView2 is null)
            return;

        var status = await LoadDxccStatusAsync();
        var json = DxccMapPayloadFactory.Serialize(status);
        try
        {
            await MapWebView.ExecuteScriptAsync(
                $"window.ucxSetDxccStatus && window.ucxSetDxccStatus({json});");
        }
        catch
        {
            // The page may be navigating or closing. A later full refresh will resend it.
        }
    }

    private void MapWindow_Closed(object? sender, EventArgs e)
    {
        MapSyncService.ViewChanged -= MapSyncService_ViewChanged;
        if (MapWebView.CoreWebView2 is not null)
            MapWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;

        try
        {
            if (Directory.Exists(_mapWebFolder))
                Directory.Delete(_mapWebFolder, recursive: true);
        }
        catch
        {
            // Temporary map files are harmless if cleanup is delayed by WebView2.
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();
            if (type == "tileError")
            {
                SetMapStatus(
                    $"Kartentiles konnten nicht geladen werden ({_tileSettings.ProviderName}). " +
                    $"Provider-Konfiguration: {MapTileSettings.SettingsPath}");
                return;
            }

            if (type == "dxccError")
            {
                var message = root.TryGetProperty("message", out var value)
                    ? value.GetString()
                    : "unbekannter Browserfehler";
                var phase = root.TryGetProperty("phase", out var phaseValue)
                    ? phaseValue.GetString()
                    : null;
                var context = phase == "initialize" || string.IsNullOrWhiteSpace(phase)
                    ? "Browserinitialisierung fehlgeschlagen"
                    : $"Browserinteraktion fehlgeschlagen ({phase})";
                _dxccDiagnostic = $"DXCC: {context} – {message}";
                UpdateStatusText();
                return;
            }

            if (type == "dxccPerformance")
            {
                var phase = root.GetProperty("phase").GetString();
                var milliseconds = root.GetProperty("milliseconds").GetDouble();
                if (phase == "initialize")
                {
                    var features = root.TryGetProperty("features", out var count) ? count.GetInt32() : 0;
                    _dxccPerformance = $"WebView-Geometrie {milliseconds:N0} ms ({features:N0} Features)";
                }
                else
                {
                    _dxccPerformance = $"DXCC-Restyle {milliseconds:N1} ms";
                }
                UpdateStatusText();
                return;
            }

            if (type == "dxccVisibility")
            {
                _dxccVisibleOverride = root.GetProperty("visible").GetBoolean();
                return;
            }

            if (type != "view" || SyncMapsCheck.IsChecked != true)
                return;

            MapSyncService.Publish(
                _syncId,
                root.GetProperty("lat").GetDouble(),
                root.GetProperty("lon").GetDouble(),
                root.GetProperty("zoom").GetDouble());
        }
        catch
        {
            // Ignore malformed browser messages.
        }
    }

    private async void MapSyncService_ViewChanged(MapViewState state)
    {
        if (state.SourceId == _syncId || SyncMapsCheck.IsChecked != true || MapWebView.CoreWebView2 is null)
            return;

        try
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            await MapWebView.ExecuteScriptAsync(
                $"window.ucxApplyRemoteView && window.ucxApplyRemoteView({state.Latitude.ToString(culture)},{state.Longitude.ToString(culture)},{state.Zoom.ToString(culture)});");
        }
        catch
        {
            // Map synchronization is best-effort while a WebView reloads or closes.
        }
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping(
            MapVirtualHost,
            _mapWebFolder,
            CoreWebView2HostResourceAccessKind.Allow);

        var userAgent = core.Settings.UserAgent ?? string.Empty;
        if (!userAgent.Contains("UcxStats/", StringComparison.OrdinalIgnoreCase))
            core.Settings.UserAgent = $"{userAgent} UcxStats/0.12.3.3".Trim();
    }

    private void NavigateMap(IReadOnlyList<MapQsoPoint> points, MapDxccPageOptions dxcc)
    {
        var htmlPath = Path.Combine(_mapWebFolder, "map.html");
        var tileDefinition = new MapTileDefinition(
            _tileSettings.ProviderName,
            _tileSettings.UrlTemplate,
            _tileSettings.Attribution,
            _tileSettings.MaxZoom);
        File.WriteAllText(htmlPath, MapPageBuilder.Build(points, tileDefinition, dxcc));
        var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MapWebView.CoreWebView2?.Navigate($"https://{MapVirtualHost}/map.html?v={cacheBust}");
    }

    private void SetMapStatus(string value)
    {
        _mapStatus = value;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var parts = new[] { _mapStatus, _dxccDiagnostic, _dxccPerformance }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        StatusText.Text = string.Join(" · ", parts);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _fullscreen))
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _oldStyle = WindowStyle;
            _oldState = WindowState;
            _oldResizeMode = ResizeMode;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _fullscreen = true;
        }
        else
        {
            WindowStyle = _oldStyle;
            ResizeMode = _oldResizeMode;
            WindowState = _oldState;
            _fullscreen = false;
        }
    }
}
