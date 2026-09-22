using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using UcxStats.Data.Database;

namespace UcxStats.Wpf;

public partial class LiveMapWindow : Window
{
    private readonly IQsoRepository _repository;
    private readonly UcxImportService _importService;
    private readonly Window _mainOwner;

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCancellation;
    private CancellationTokenSource? _refreshCancellation;
    private MapWindow? _mapWindow;
    private SourceSummary? _activeSource;
    private DateTime _fromUtc;
    private bool _refreshRunning;
    private bool _refreshPending;

    public LiveMapWindow(IQsoRepository repository, Window owner)
    {
        _repository = repository;
        _importService = new UcxImportService(repository);
        _mainOwner = owner;
        InitializeComponent();
        Owner = owner;
        Loaded += LiveMapWindow_Loaded;
        Closed += (_, _) => StopMonitoring();
    }

    private async void LiveMapWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var sources = await _repository.GetSourcesAsync();
        SourceCombo.ItemsSource = sources;
        SourceCombo.SelectedIndex = sources.Count > 0 ? 0 : -1;
        SetNow();
    }

    private void Now_Click(object sender, RoutedEventArgs e) => SetNow();

    private void SetNow()
    {
        var now = DateTime.UtcNow;
        FromDatePicker.SelectedDate = now.Date;
        FromTimeBox.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private DateTime ReadFromUtc()
    {
        if (FromDatePicker.SelectedDate is not DateTime date)
            throw new ArgumentException("Bitte ein Startdatum auswählen.");

        if (!TimeSpan.TryParseExact(
                FromTimeBox.Text.Trim(),
                new[] { "h\\:mm", "hh\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss" },
                CultureInfo.InvariantCulture,
                out var time))
            throw new ArgumentException("UTC-Zeit bitte als HH:mm oder HH:mm:ss eingeben.");

        return DateTime.SpecifyKind(date.Date + time, DateTimeKind.Utc);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SourceCombo.SelectedItem is not SourceSummary source)
                throw new ArgumentException("Bitte eine UCX-Quelle auswählen.");
            if (!File.Exists(source.FullPath))
                throw new FileNotFoundException("Die ausgewählte UCX-Datei wurde nicht gefunden.", source.FullPath);

            StopMonitoring(closeMap: false);
            _activeSource = source;
            _fromUtc = ReadFromUtc();

            StartButton.IsEnabled = false;
            SourceCombo.IsEnabled = false;
            FromDatePicker.IsEnabled = false;
            FromTimeBox.IsEnabled = false;
            AutoUpdateCheck.IsEnabled = false;
            StopButton.IsEnabled = true;

            await RefreshLiveAsync(initial: true);

            if (AutoUpdateCheck.IsChecked == true)
                StartWatcher(source.FullPath);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            ResetControls();
            MessageBox.Show(this, ex.Message, "Live-Map", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopMonitoring(closeMap: false);
        StatusText.Text = "Überwachung gestoppt. Die Karte bleibt geöffnet.";
    }

    private void StartWatcher(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += Watcher_Changed;
        _watcher.Created += Watcher_Changed;
        _watcher.Renamed += Watcher_Renamed;
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e) => ScheduleRefresh();
    private void Watcher_Renamed(object sender, RenamedEventArgs e) => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = new CancellationTokenSource();
        var token = _debounceCancellation.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                await Dispatcher.InvokeAsync(async () => await RefreshLiveAsync(initial: false));
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task RefreshLiveAsync(bool initial)
    {
        if (_activeSource is null)
            return;

        if (_refreshRunning)
        {
            _refreshPending = true;
            return;
        }

        _refreshRunning = true;
        try
        {
            do
            {
                _refreshPending = false;
                _refreshCancellation?.Cancel();
                _refreshCancellation?.Dispose();
                _refreshCancellation = new CancellationTokenSource();
                var token = _refreshCancellation.Token;

                StatusText.Text = $"Synchronisiere {Path.GetFileName(_activeSource.FullPath)} …";

                (QsoImportResult Result, int RecordCount) sync;
                try
                {
                    sync = await _importService.SyncFileAsync(_activeSource.FullPath, token);
                }
                catch (IOException)
                {
                    // UcxLog may still be replacing/writing the file. One delayed retry is sufficient.
                    await Task.Delay(500, token);
                    sync = await _importService.SyncFileAsync(_activeSource.FullPath, token);
                }

                var scope = QsoScope.ForSources([_activeSource.FullPath]).WithFromUtc(_fromUtc);
                var points = await _repository.GetMapQsoPointsAsync(scope, token);

                if (initial || _mapWindow is null || !_mapWindow.IsLoaded)
                {
                    var title = $"LIVE · {_activeSource.FileName} · ab {_fromUtc:dd.MM.yyyy HH:mm} UTC · {points.Count:N0} QSOs";
                    _mapWindow = new MapWindow(_repository, scope, points, title) { Owner = _mainOwner };
                    _mapWindow.Closed += (_, _) => _mapWindow = null;
                    _mapWindow.Show();
                }
                else
                {
                    var title = $"LIVE · {_activeSource.FileName} · ab {_fromUtc:dd.MM.yyyy HH:mm} UTC · {points.Count:N0} QSOs";
                    await _mapWindow.UpdateLiveDataAsync(points, title, scope);
                }

                StatusText.Text =
                    $"{points.Count:N0} QSOs seit {_fromUtc:dd.MM.yyyy HH:mm} UTC · " +
                    $"Sync: +{sync.Result.Inserted:N0} / Δ{sync.Result.Updated:N0} / ={sync.Result.Unchanged:N0} · " +
                    $"zuletzt {DateTime.UtcNow:HH:mm:ss} UTC";
            }
            while (_refreshPending);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Live-Aktualisierung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            _refreshRunning = false;
        }
    }

    private void StopMonitoring(bool closeMap = false)
    {
        _watcher?.Dispose();
        _watcher = null;

        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = null;

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;

        if (closeMap)
            _mapWindow?.Close();

        _activeSource = null;
        ResetControls();
    }

    private void ResetControls()
    {
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        SourceCombo.IsEnabled = true;
        FromDatePicker.IsEnabled = true;
        FromTimeBox.IsEnabled = true;
        AutoUpdateCheck.IsEnabled = true;
    }
}
