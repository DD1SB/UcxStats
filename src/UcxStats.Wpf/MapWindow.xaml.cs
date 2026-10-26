using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
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
    private readonly MapSettingsService _mapSettings;
    private readonly string _mapWebFolder;
    private IReadOnlyList<MapQsoPoint> _points;
    private QsoScope _scope;
    private readonly bool _isLiveMap;
    private MapSettings _presentationSettings;
    private MapSettings StatisticsSettings => _mapSettings.Current.WithPresentationFrom(_presentationSettings);
    private int _lastPauseThreshold;
    private int? _lastAllowance;
    private CancellationTokenSource? _statisticsCancellation;
    private QsoStatisticsSnapshot? _statisticsSnapshot;
    private int _statisticsGeneration;
    private int _statisticsPayloadGeneration;
    private int _dataGeneration;
    private bool _closed;
    private string _statisticsPerformance = string.Empty;
    private string _timelinePerformance = string.Empty;
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
        string title,
        MapSettingsService? mapSettings = null,
        bool isLiveMap = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scope);
        InitializeComponent();
        _repository = repository;
        _isLiveMap = isLiveMap;
        _scope = scope;
        _points = points;
        _mapSettings = mapSettings ?? App.MapSettings;
        _presentationSettings = _mapSettings.Current;
        _lastPauseThreshold = _mapSettings.Current.ContestPauseThresholdMinutes;
        _lastAllowance = _mapSettings.Current.OperatingTimeAllowanceMinutes;
        _mapWebFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UcxStats",
            "MapWeb",
            _syncId.ToString("N"));
        Directory.CreateDirectory(_mapWebFolder);
        // Keep the shared browser profile outside the replaceable installation directory.
        MapWebView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UcxStats", "WebView2")
        };
        CopyMapAssets();
        HeaderText.Text = title;
        Loaded += MapWindow_Loaded;
        Closed += MapWindow_Closed;
        MapSyncService.ViewChanged += MapSyncService_ViewChanged;
        _mapSettings.Changed += MapSettings_Changed;
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
            var generation = _dataGeneration;
            var dxcc = await PrepareDxccPageAsync();
            if (_closed || generation != _dataGeneration) return;
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
        var generation = ++_dataGeneration;
        InvalidateStatistics();
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
        if (_closed || generation != _dataGeneration) return;
        NavigateMap(_points, dxcc);
        SetMapStatus($"{_points.Count:N0} QSOs mit Locator und Stations-QTH · Tiles: {_tileSettings.ProviderName}.");
    }

    public async Task UpdateLiveDataAsync(
        IReadOnlyList<MapQsoPoint> points,
        string title,
        QsoScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var generation = ++_dataGeneration;
        InvalidateStatistics();
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

        if (_closed || generation != _dataGeneration) return;
        // This is deliberately independent of marker additions: a locator-less QSO can
        // change an entity state even when the WebView receives no new MapQsoPoint.
        await RefreshDxccStatusAsync();
        if (_closed || generation != _dataGeneration) return;
        await RefreshStatisticsAsync();
        if (_closed || generation != _dataGeneration) return;
        _points = points;
        SetMapStatus(requiresReplacement
            ? $"{_points.Count:N0} QSOs · Live-Kartendaten aktualisiert."
            : added.Count == 0
            ? $"{_points.Count:N0} QSOs · keine neuen kartierbaren QSOs."
            : $"{_points.Count:N0} QSOs · +{added.Count:N0} live hinzugefügt.");
    }

    private void InvalidateStatistics()
    {
        _statisticsGeneration++;
        _statisticsPayloadGeneration++;
        _statisticsCancellation?.Cancel();
        _statisticsSnapshot = null;
    }

    private async Task RefreshStatisticsAsync()
    {
        _statisticsCancellation?.Cancel();
        _statisticsCancellation?.Dispose();
        _statisticsCancellation = new CancellationTokenSource();
        var token = _statisticsCancellation.Token;
        var generation = ++_statisticsGeneration;
        var scope = _scope;
        try
        {
            var watch = Stopwatch.StartNew();
            // Microsoft.Data.Sqlite async reads can run synchronously. Keep the entire
            // query and aggregation off the WPF dispatcher, including initialization.
            var snapshot = await Task.Run(() => _repository.GetMapStatisticsAsync(scope, token), token);
            if (_closed || token.IsCancellationRequested || generation != _statisticsGeneration) return;
            _statisticsSnapshot = snapshot;
            _statisticsPerformance = $"Statistik: Abfrage/Aggregation {watch.Elapsed.TotalMilliseconds:N1} ms";
            await ApplyStatisticsAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_closed || generation != _statisticsGeneration) return;
            _statisticsPerformance = $"Statistik nicht verfügbar: {ex.Message}";
            UpdateStatusText();
        }
    }

    private async Task ApplyStatisticsAsync(bool includeSnapshot = true)
    {
        if (_closed || MapWebView.CoreWebView2 is null) return;
        var generation = includeSnapshot ? ++_statisticsPayloadGeneration : _statisticsPayloadGeneration;
        var snapshot = _statisticsSnapshot;
        var settings = StatisticsSettings;
        try
        {
            await MapWebView.ExecuteScriptAsync(MapStatisticsPayload.BuildSettingsScript(settings));
            if (!includeSnapshot || snapshot is null) return;
            var watch = Stopwatch.StartNew();
            var payload = await Task.Run(() => (
                Statistics: MapStatisticsPayload.Serialize(snapshot, settings),
                Timeline: _isLiveMap ? LiveTimelinePayload.Serialize(LiveTimelinePayload.Create(snapshot, DateTimeOffset.UtcNow)) : null));
            var json = payload.Statistics;
            if (_closed || generation != _statisticsPayloadGeneration) return;
            _statisticsPerformance = _statisticsPerformance.Split(" · JSON", StringSplitOptions.None)[0] +
                $" · JSON {watch.Elapsed.TotalMilliseconds:N1} ms / {System.Text.Encoding.UTF8.GetByteCount(json):N0} Bytes" +
                (payload.Timeline is null ? "" : $" · Verlauf {System.Text.Encoding.UTF8.GetByteCount(payload.Timeline):N0} Bytes");
            await MapWebView.ExecuteScriptAsync(MapStatisticsPayload.BuildSnapshotScript(json));
            if (payload.Timeline is not null && !_closed && generation == _statisticsPayloadGeneration)
                await MapWebView.ExecuteScriptAsync(LiveTimelinePayload.BuildScript(payload.Timeline));
            UpdateStatusText();
        }
        catch
        {
            // A ready page receives the latest snapshot on NavigationCompleted.
        }
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
        _closed = true;
        InvalidateStatistics();
        _statisticsCancellation?.Dispose();
        MapSyncService.ViewChanged -= MapSyncService_ViewChanged;
        _mapSettings.Changed -= MapSettings_Changed;
        if (MapWebView.CoreWebView2 is not null)
        {
            MapWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            MapWebView.CoreWebView2.NavigationCompleted -= MapWebView_NavigationCompleted;
        }

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
            if (type == "mapPresentation")
            {
                var requested = root.GetProperty("settings").Deserialize<MapSettings>(new JsonSerializerOptions
                { PropertyNameCaseInsensitive = true })?.Normalize();
                if (requested is null) return;
                _presentationSettings = _presentationSettings.WithPresentationFrom(requested);
                _mapSettings.Save(_mapSettings.Current.WithPresentationFrom(_presentationSettings));
                return;
            }
            if (type == "timelinePerformance")
            {
                _timelinePerformance = $"Verlauf render: {root.GetProperty("milliseconds").GetDouble():N2} ms / {root.GetProperty("samples").GetInt32():N0} Zeitwerte";
                UpdateStatusText();
                return;
            }
            if (type == "statisticsVisibility")
            {
                _presentationSettings = _presentationSettings with { StatisticsVisible = root.GetProperty("visible").GetBoolean() };
                _mapSettings.Save(_mapSettings.Current.WithPresentationFrom(_presentationSettings));
                return;
            }
            if (type == "liveThresholdPreferences")
            {
                var warning = root.GetProperty("warningMinutes").GetInt32();
                var prolonged = root.GetProperty("prolongedMinutes").GetInt32();
                _mapSettings.Save(LiveThresholdUi.Apply(_mapSettings.Current, warning, prolonged));
                return;
            }
            if (type == "statisticsPreferences")
            {
                var value = root.GetProperty("settings");
                _presentationSettings = _presentationSettings with { ContestTimeVisible = value.GetProperty("contestTimeVisible").GetBoolean() };
                _mapSettings.Save(_mapSettings.Current with
                {
                    ContestTimeVisible = _presentationSettings.ContestTimeVisible,
                    ContestPauseThresholdMinutes = value.GetProperty("contestPauseThresholdMinutes").GetInt32(),
                    OperatingTimeAllowanceMinutes = value.GetProperty("operatingTimeAllowanceMinutes").ValueKind == JsonValueKind.Null
                        ? null : value.GetProperty("operatingTimeAllowanceMinutes").GetInt32(),
                    LiveWarningThresholdMinutes = value.GetProperty("liveWarningThresholdMinutes").GetInt32(),
                    LiveProlongedGapThresholdMinutes = value.GetProperty("liveProlongedGapThresholdMinutes").GetInt32()
                });
                return;
            }
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
        catch (Exception ex)
        {
            SetMapStatus($"Kartenmeldung konnte nicht verarbeitet werden: {ex.Message}");
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

    private async void MapSettings_Changed(MapSettings settings)
    {
        var recalculateTime = settings.ContestPauseThresholdMinutes != _lastPauseThreshold ||
            settings.OperatingTimeAllowanceMinutes != _lastAllowance;
        _lastPauseThreshold = settings.ContestPauseThresholdMinutes;
        _lastAllowance = settings.OperatingTimeAllowanceMinutes;
        await ApplyPathWidthsAsync();
        await ApplyStatisticsAsync(includeSnapshot: recalculateTime);
    }

    private async void MapWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // A save during page initialization must also reach the newly ready page.
        if (e.IsSuccess && !_closed)
        {
            await ApplyPathWidthsAsync();
            await RefreshStatisticsAsync();
        }
    }

    private async Task ApplyPathWidthsAsync()
    {
        if (MapWebView.CoreWebView2 is null)
            return;

        try
        {
            await MapWebView.ExecuteScriptAsync(MapPageBuilder.BuildPathWidthsScript(_mapSettings.Current.PathWidths));
        }
        catch
        {
            // Navigation completion retries the latest settings if the page was not ready.
            // Closing maps need no further update.
        }
    }

    private void MapSettings_Click(object sender, RoutedEventArgs e) =>
        new DxccMapSettingsWindow(_mapSettings) { Owner = this }.ShowDialog();

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.NavigationCompleted -= MapWebView_NavigationCompleted;
        core.NavigationCompleted += MapWebView_NavigationCompleted;
        core.SetVirtualHostNameToFolderMapping(
            MapVirtualHost,
            _mapWebFolder,
            CoreWebView2HostResourceAccessKind.Allow);

        var userAgent = core.Settings.UserAgent ?? string.Empty;
        if (!userAgent.Contains("UcxStats/", StringComparison.OrdinalIgnoreCase))
            core.Settings.UserAgent = $"{userAgent} UcxStats/{typeof(App).Assembly.GetName().Version}".Trim();
    }

    private void CopyMapAssets()
    {
        var sourceFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Leaflet");
        var destinationFolder = Path.Combine(_mapWebFolder, "leaflet");
        foreach (var source in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationFolder, Path.GetRelativePath(sourceFolder, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private void NavigateMap(IReadOnlyList<MapQsoPoint> points, MapDxccPageOptions dxcc)
    {
        var htmlPath = Path.Combine(_mapWebFolder, "map.html");
        var tileDefinition = new MapTileDefinition(
            _tileSettings.ProviderName,
            _tileSettings.UrlTemplate,
            _tileSettings.Attribution,
            _tileSettings.MaxZoom);
        File.WriteAllText(htmlPath, MapPageBuilder.Build(points, tileDefinition, dxcc, _mapSettings.Current.PathWidths, StatisticsSettings, _isLiveMap));
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
        var parts = new[] { _mapStatus, _dxccDiagnostic, _dxccPerformance, _statisticsPerformance, _timelinePerformance }
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
