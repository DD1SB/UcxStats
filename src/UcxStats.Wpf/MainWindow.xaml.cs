using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Geo.Maidenhead;
using UcxStats.Map;

namespace UcxStats.Wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private MapWindow? _singleMapWindow;
    private readonly MapSettingsService _mapSettings = App.MapSettings;

    private readonly IQsoRepository _repository = new SqliteQsoRepository();
    private readonly UcxRecordValidator _validator = new();
    private string _statusText = "Noch keine UCX-Datei geladen.";
    private string _activeSourceText = "Aktiv: keine UCX-Datei";
    private string _activeSourceTooltip = string.Empty;
    private string _databaseSummaryText = "Datenbank: – QSOs";
    private IReadOnlyList<UcxDiagnostic> _lastDiagnostics = [];
    private CancellationTokenSource? _geoJobCancellation;

    public ObservableCollection<QsoGridRow> Qsos { get; } = [];
    public ObservableCollection<SourceSummary> Sources { get; } = [];
    public string DatabasePath => _repository.DatabasePath;

    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string ActiveSourceText { get => _activeSourceText; private set => SetField(ref _activeSourceText, value); }
    public string ActiveSourceTooltip { get => _activeSourceTooltip; private set => SetField(ref _activeSourceTooltip, value); }
    public string DatabaseSummaryText { get => _databaseSummaryText; private set => SetField(ref _databaseSummaryText, value); }

    public MainWindow()
    {
        InitializeComponent();
        Title = $"UcxStats v{typeof(App).Assembly.GetName().Version}";
        DataContext = this;
        Loaded += async (_, _) => { await RefreshDatabaseSummaryAsync(); await RefreshProfilesAsync(); };
    }

    private async void OpenUcx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "UcxLog (*.UCX)|*.UCX|Alle Dateien (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            IsEnabled = false;
            StatusText = "UCX-Datei(en) werden gelesen und synchronisiert…";
            Qsos.Clear();
            var allDiagnostics = new List<UcxDiagnostic>();
            var totalInserted = 0;
            var totalUpdated = 0;
            var totalUnchanged = 0;
            var totalQsos = 0;
            var totalRecordsWithWarnings = 0;

            foreach (var fileName in dialog.FileNames)
            {
                var reader = new UcxFileReader();
                var sourceRecords = reader.ReadWithMetadata(fileName).ToList();
                var importRecords = new List<QsoImportRecord>(sourceRecords.Count);
                var sourcePath = Path.GetFullPath(fileName);
                var fileDiagnostics = new List<UcxDiagnostic>();

                foreach (var source in sourceRecords)
                {
                    var qso = source.Qso;
                    Qsos.Add(new QsoGridRow(sourcePath, qso));
                    var qsoDiagnostics = _validator.Validate(qso);
                    allDiagnostics.AddRange(qsoDiagnostics);
                    fileDiagnostics.AddRange(qsoDiagnostics);
                    BuildGeo(qso, out var latitude, out var longitude, out var grid2, out var grid4, out var grid6, out var grid8);
                    importRecords.Add(new QsoImportRecord
                    {
                        SourceFile = sourcePath,
                        SourceRecordIndex = qso.RecordIndex,
                        SourceRecordHash = source.RecordHash,
                        Qso = qso,
                        Latitude = latitude,
                        Longitude = longitude,
                        Grid2 = grid2,
                        Grid4 = grid4,
                        Grid6 = grid6,
                        Grid8 = grid8
                    });
                }

                var result = await _repository.UpsertManyAsync(importRecords);
                totalInserted += result.Inserted;
                totalUpdated += result.Updated;
                totalUnchanged += result.Unchanged;
                totalQsos += sourceRecords.Count;
                totalRecordsWithWarnings += fileDiagnostics.Select(x => x.RecordIndex).Distinct().Count();
            }

            _lastDiagnostics = allDiagnostics;
            DiagnosticsButton.IsEnabled = allDiagnostics.Count > 0;
            ActiveSourceTooltip = string.Join(Environment.NewLine, dialog.FileNames.Select(Path.GetFullPath));
            ActiveSourceText = dialog.FileNames.Length == 1
                ? $"Aktiv: {Path.GetFileName(dialog.FileNames[0])}"
                : $"Aktiv: {dialog.FileNames.Length:N0} UCX-Dateien";

            await RefreshDatabaseSummaryAsync();
            var ok = Math.Max(0, totalQsos - totalRecordsWithWarnings);
            StatusText =
                $"{totalQsos:N0} QSOs | {ok:N0} ohne Warnung | {allDiagnostics.Count:N0} Hinweise | " +
                $"DB: +{totalInserted:N0} / Δ{totalUpdated:N0} / ={totalUnchanged:N0}";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler beim Lesen oder Synchronisieren der UCX-Datei(en).";
            MessageBox.Show(this, ex.ToString(), "UcxStats", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private static void BuildGeo(UcxQsoRecord qso, out double? latitude, out double? longitude, out string? grid2, out string? grid4, out string? grid6, out string? grid8)
    {
        latitude = longitude = null;
        grid2 = grid4 = grid6 = grid8 = null;
        if (string.IsNullOrWhiteSpace(qso.Locator)) return;
        try
        {
            var locator = qso.Locator.Trim().ToUpperInvariant();
            var coordinate = MaidenheadConverter.ToCenter(locator);
            latitude = coordinate.Latitude;
            longitude = coordinate.Longitude;
            if (locator.Length >= 2) grid2 = locator[..2];
            if (locator.Length >= 4) grid4 = locator[..4];
            if (locator.Length >= 6) grid6 = locator[..6];
            if (locator.Length >= 8) grid8 = locator[..8];
        }
        catch (ArgumentException) { }
    }

    private async Task RefreshDatabaseSummaryAsync()
    {
        var total = await _repository.GetTotalQsoCountAsync();
        var sources = await _repository.GetSourcesAsync();
        DatabaseSummaryText = $"Datenbank: {total:N0} QSOs aus {sources.Count:N0} Quellen";
        Sources.Clear();
        foreach (var source in sources) Sources.Add(source);
    }

    private void SourceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = SourceList.SelectedItems.Cast<SourceSummary>().ToList();
        SourceSelectionText.Text = selected.Count switch
        {
            0 => "Keine Quelle ausgewählt",
            1 => $"1 Quelle ausgewählt: {selected[0].FileName}",
            _ => $"{selected.Count} Quellen ausgewählt"
        };

        if (selected.Count == 0) return;

        try
        {
            var reader = new UcxFileReader();
            Qsos.Clear();
            foreach (var source in selected)
            {
                var records = reader.ReadWithMetadata(source.FullPath);
                foreach (var recordData in records)
                    Qsos.Add(new QsoGridRow(source.FullPath, recordData.Qso));
            }

            if (selected.Count == 1)
            {
                ActiveSourceText = $"Ansicht: {selected[0].FileName} · {Qsos.Count:N0} QSOs";
                ActiveSourceTooltip = selected[0].FullPath;
                StatusText = $"{Qsos.Count:N0} QSOs aus {selected[0].FileName} geladen.";
            }
            else
            {
                ActiveSourceText = $"Ansicht: {selected.Count} Quellen · {Qsos.Count:N0} QSOs";
                ActiveSourceTooltip = string.Join(Environment.NewLine, selected.Select(x => x.FullPath));
                StatusText = $"{Qsos.Count:N0} QSOs aus {selected.Count} Quellen geladen.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "UcxStats", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private async Task RefreshProfilesAsync()
    {
        var profiles = await _repository.GetStationProfilesAsync();
        SourceProfileCombo.ItemsSource = profiles;
    }

    private async void StationProfiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new StationProfilesWindow(_repository) { Owner = this };
        window.ShowDialog();
        await RefreshProfilesAsync();
        await RefreshDatabaseSummaryAsync();
    }

    private async void AssignProfile_Click(object sender, RoutedEventArgs e)
    {
        var selected = SourceList.SelectedItems.Cast<SourceSummary>().ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Bitte zuerst mindestens eine UCX-Quelle auswählen.", "UcxStats", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (SourceProfileCombo.SelectedItem is not StationProfile profile)
        {
            MessageBox.Show(this, "Bitte ein Stationsprofil auswählen.", "UcxStats", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_geoJobCancellation is not null) return;

        _geoJobCancellation = new CancellationTokenSource();
        var cancellationToken = _geoJobCancellation.Token;
        var sourcePaths = selected.Select(x => x.FullPath).ToArray();

        AssignProfileButton.IsEnabled = false;
        SourceProfileCombo.IsEnabled = false;
        SourceList.IsEnabled = false;
        GeoProgressPanel.Visibility = Visibility.Visible;
        GeoProgressBar.IsIndeterminate = true;
        GeoProgressBar.Value = 0;
        GeoProgressText.Text = "Stationsprofil wird zugeordnet…";
        StatusText = $"Stationsprofil '{profile.Name}' wird {selected.Count:N0} Quelle(n) zugeordnet…";

        var progress = new Progress<GeoRebuildProgress>(value =>
        {
            GeoProgressBar.IsIndeterminate = false;
            GeoProgressBar.Value = value.Percent;
            GeoProgressText.Text = value.Total == 0
                ? "Keine Geodaten zu berechnen."
                : $"Geodaten: {value.Processed:N0} / {value.Total:N0} QSOs ({value.Percent:N0} %)";
        });

        try
        {
            // Microsoft.Data.Sqlite executes much of its work synchronously despite the async API.
            // Keep that work off the WPF dispatcher so the window remains responsive.
            await Task.Run(
                () => _repository.AssignStationProfileToSourcesAsync(sourcePaths, profile.Id, progress, cancellationToken),
                cancellationToken);

            await RefreshDatabaseSummaryAsync();
            StatusText = selected.Count == 1
                ? $"{selected[0].FileName}: Stationsprofil '{profile.Name}' zugeordnet; Geodaten aktualisiert."
                : $"Stationsprofil '{profile.Name}' wurde {selected.Count:N0} Quellen zugeordnet; Geodaten aktualisiert.";
        }
        catch (OperationCanceledException)
        {
            await RefreshDatabaseSummaryAsync();
            StatusText = $"Geo-Neuberechnung abgebrochen. Die Quellenzuordnung zu '{profile.Name}' wurde gespeichert.";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler bei der QTH-Zuordnung oder Geo-Neuberechnung.";
            MessageBox.Show(this, ex.ToString(), "UcxStats", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _geoJobCancellation.Dispose();
            _geoJobCancellation = null;
            GeoProgressPanel.Visibility = Visibility.Collapsed;
            GeoProgressBar.IsIndeterminate = false;
            AssignProfileButton.IsEnabled = true;
            SourceProfileCombo.IsEnabled = true;
            SourceList.IsEnabled = true;
        }
    }

    private void CancelGeo_Click(object sender, RoutedEventArgs e)
    {
        GeoProgressText.Text = "Abbruch wird angefordert…";
        _geoJobCancellation?.Cancel();
    }

    private async Task<(IReadOnlyList<MapQsoPoint> Points, string Title, QsoScope Scope)> GetGeoSelectionAsync()
    {
        var selectedRows = QsoGrid.SelectedItems.Cast<QsoGridRow>().ToList();
        var selectedSources = SourceList.SelectedItems.Cast<SourceSummary>().ToList();

        if (selectedRows.Count > 0)
        {
            var scope = QsoScope.ForSourceRecords(selectedRows.Select(x =>
                new QsoSourceRecordKey(x.SourceFile, x.Qso.RecordIndex)));
            var points = await _repository.GetMapQsoPointsAsync(scope);
            return (points, $"{selectedRows.Count:N0} ausgewählte QSOs · {points.Count:N0} kartierbar", scope);
        }

        var sourceScope = selectedSources.Count > 0
            ? QsoScope.ForSources(selectedSources.Select(x => x.FullPath))
            : QsoScope.All;
        var allPoints = await _repository.GetMapQsoPointsAsync(sourceScope);

        var title = selectedSources.Count switch
        {
            0 => $"Alle Quellen · {allPoints.Count:N0} QSOs",
            1 => $"{selectedSources[0].FileName} · {allPoints.Count:N0} QSOs",
            _ => $"{selectedSources.Count:N0} Quellen · {allPoints.Count:N0} QSOs"
        };
        return (allPoints, title, sourceScope);
    }

    private void LiveMap_Click(object sender, RoutedEventArgs e)
    {
        var window = new LiveMapWindow(_repository, this, _mapSettings);
        window.Show();
    }

    private void DxccSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new DxccMapSettingsWindow(_mapSettings) { Owner = this };
        if (window.ShowDialog() == true)
            StatusText = "Kartenoptionen gespeichert. Linienbreiten gelten sofort; DXCC-Konfiguration gilt für neu geladene Karten.";
    }

    private async void Map_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText = QsoGrid.SelectedItems.Count > 0
                ? $"{QsoGrid.SelectedItems.Count:N0} ausgewählte QSOs werden kartiert…"
                : "Kartendaten werden aus der Datenbank geladen…";
            var selection = await GetGeoSelectionAsync();
            var points = selection.Points;
            if (points.Count == 0)
            {
                MessageBox.Show(this, "Keine kartierbaren QSOs. Benötigt werden Locator und ein zugeordnetes Stationsprofil.",
                    "UcxStats Karte", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Keine kartierbaren QSOs für die Auswahl.";
                return;
            }
            var title = selection.Title;
            if (SingleMapWindowCheck.IsChecked == true && _singleMapWindow is { IsLoaded: true })
            {
                await _singleMapWindow.UpdateDataAsync(points, title, selection.Scope);
                _singleMapWindow.Activate();
            }
            else
            {
                var window = new MapWindow(_repository, selection.Scope, points, title, _mapSettings) { Owner = this };
                if (SingleMapWindowCheck.IsChecked == true)
                {
                    _singleMapWindow = window;
                    window.Closed += (_, _) =>
                    {
                        if (ReferenceEquals(_singleMapWindow, window)) _singleMapWindow = null;
                    };
                }
                window.Show();
            }
            StatusText = $"Karte mit {points.Count:N0} QSOs geöffnet.";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler beim Laden der Kartendaten.";
            MessageBox.Show(this, ex.ToString(), "UcxStats Karte", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void AzimuthDistance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText = QsoGrid.SelectedItems.Count > 0
                ? $"{QsoGrid.SelectedItems.Count:N0} ausgewählte QSOs werden ausgewertet…"
                : "Geo-Auswertung wird aus der Datenbank geladen…";
            var selection = await GetGeoSelectionAsync();
            var points = selection.Points;
            var usable = points.Where(x => x.DistanceKm.HasValue && x.BearingDeg.HasValue).ToList();
            if (usable.Count == 0)
            {
                MessageBox.Show(this, "Für die Auswahl sind keine Entfernung/Azimut-Daten vorhanden.",
                    "UcxStats Geo", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Keine Geo-Auswertung für die aktuelle Auswahl.";
                return;
            }

            var title = QsoGrid.SelectedItems.Count > 0
                ? $"{QsoGrid.SelectedItems.Count:N0} ausgewählte QSOs · {usable.Count:N0} mit Geo-Daten"
                : selection.Title;

            new AzimuthDistanceWindow(usable, title) { Owner = this }.Show();
            StatusText = $"Azimut/Distanz mit {usable.Count:N0} QSOs geöffnet.";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler beim Laden der Geo-Auswertung.";
            MessageBox.Show(this, ex.ToString(), "UcxStats Geo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DclImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "DCL-ADIF mit bestätigten QSOs öffnen",
            Filter = "ADIF-Dateien (*.adi;*.adif)|*.adi;*.adif|Alle Dateien (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            StatusText = "DCL-ADIF wird zugeordnet…";
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var result = await _repository.ImportDclAdifAsync(text);

            MessageBox.Show(this,
                $"DCL-Datensätze: {result.Records:N0}\n" +
                $"davon bestätigt (c/i/m/n/o): {result.ConfirmedRecords:N0}\n" +
                $"eindeutig UCX zugeordnet: {result.Matched:N0}\n" +
                $"nicht gefunden: {result.Unmatched:N0}\n" +
                $"mehrdeutig: {result.Ambiguous:N0}\n" +
                $"nicht bestätigte Statuscodes ignoriert: {result.IgnoredNotConfirmed:N0}",
                "DCL-Import",
                MessageBoxButton.OK,
                result.Unmatched == 0 && result.Ambiguous == 0
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);

            if (result.Diagnostics.Count > 0)
                new DclDiagnosticsWindow(result) { Owner = this }.Show();

            StatusText = $"DCL: {result.Matched:N0} bestätigt zugeordnet · {result.Unmatched:N0} offen · {result.Ambiguous:N0} mehrdeutig.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "DCL-Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText = "DCL-Import fehlgeschlagen.";
        }
    }

    private void Awards_Click(object sender, RoutedEventArgs e)
    {
        new AwardSelectionWindow(_repository) { Owner = this }.Show();
    }

    private void Query_Click(object sender, RoutedEventArgs e)
    {
        new QueryWindow(_repository, this).Show();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDiagnostics.Count == 0)
        {
            MessageBox.Show(this, "Keine Diagnosehinweise vorhanden.", "UcxStats Diagnose", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lines = _lastDiagnostics.Take(100).Select(x => $"Record {x.RecordIndex}: {x.Message}");
        var suffix = _lastDiagnostics.Count > 100 ? $"\n\n… weitere {_lastDiagnostics.Count - 100:N0} Hinweise nicht angezeigt." : string.Empty;
        MessageBox.Show(this, string.Join(Environment.NewLine, lines) + suffix, "UcxStats Diagnose", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}
