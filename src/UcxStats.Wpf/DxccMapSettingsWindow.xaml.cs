using System.IO;
using System.Windows;
using Microsoft.Win32;
using UcxStats.Data.Dxcc;
using UcxStats.Map;

namespace UcxStats.Wpf;

public partial class DxccMapSettingsWindow : Window
{
    private readonly DxccMapConfigurationStore _store = new();
    private readonly MapSettingsService _mapSettings;
    private readonly DxccMapConfiguration _initialDxccConfiguration;

    public DxccMapSettingsWindow(MapSettingsService? mapSettings = null)
    {
        InitializeComponent();
        _mapSettings = mapSettings ?? App.MapSettings;
        var loaded = _store.Load();
        _initialDxccConfiguration = loaded.Configuration;
        GeoJsonPathBox.Text = loaded.Configuration.GeoJsonPath ?? string.Empty;
        EnabledByDefaultCheck.IsChecked = loaded.Configuration.EnabledByDefault;
        var widths = _mapSettings.Current.PathWidths;
        ShortpathWidthBox.ItemsSource = WidthChoices(widths.ShortpathWidth, MapPathWidths.DefaultShortpathWidth);
        LongpathWidthBox.ItemsSource = WidthChoices(widths.LongpathWidth, MapPathWidths.DefaultLongpathWidth);
        ShortpathWidthBox.SelectedItem = widths.ShortpathWidth;
        LongpathWidthBox.SelectedItem = widths.LongpathWidth;
        ValidationText.Text = _mapSettings.Error ?? loaded.Error ??
            (string.IsNullOrWhiteSpace(loaded.Configuration.GeoJsonPath)
                ? $"Keine Datei konfiguriert. DXCC: {_store.Path} · Kartenoptionen: {_mapSettings.Path}"
                : "Konfigurierte Datei noch nicht geprüft.");
    }

    private static double[] WidthChoices(double current, double defaultWidth) =>
        Enumerable.Range(0, (int)((MapPathWidths.Maximum - MapPathWidths.Minimum) / MapPathWidths.Step) + 1)
            .Select(index => MapPathWidths.Minimum + index * MapPathWidths.Step)
            .Append(defaultWidth)
            .Append(current) // Retain any valid fractional value supplied in the configuration.
            .Distinct()
            .Order()
            .ToArray();

    private void ResetWidths_Click(object sender, RoutedEventArgs e)
    {
        ShortpathWidthBox.SelectedItem = MapPathWidths.DefaultShortpathWidth;
        LongpathWidthBox.SelectedItem = MapPathWidths.DefaultLongpathWidth;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "DXCC GeoJSON auswählen",
            Filter = "GeoJSON (*.geojson;*.json)|*.geojson;*.json|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        GeoJsonPathBox.Text = Path.GetFullPath(dialog.FileName);
        await ValidateCurrentAsync();
    }

    private async void Validate_Click(object sender, RoutedEventArgs e) => await ValidateCurrentAsync();

    private async Task<bool> ValidateCurrentAsync()
    {
        var path = GeoJsonPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ValidationText.Text = "Kein GeoJSON-Pfad angegeben; das Overlay bleibt nicht verfügbar.";
            return true;
        }
        if (!Path.IsPathFullyQualified(path))
        {
            ValidationText.Text = "Der GeoJSON-Pfad muss absolut sein.";
            return false;
        }

        ValidationText.Text = "GeoJSON wird geprüft…";
        var result = await Task.Run(() => new DxccGeoJsonLoader().Load(path));
        if (!result.IsValid)
        {
            var errors = result.Diagnostics
                .Where(x => x.Severity == DxccGeoJsonDiagnosticSeverity.Error)
                .Select(x => x.Message)
                .Take(3);
            ValidationText.Text = "Nicht verwendbar: " + string.Join(" ", errors);
            return false;
        }

        var warnings = result.Diagnostics.Count(x => x.Severity == DxccGeoJsonDiagnosticSeverity.Warning);
        var profile = result.IsKnownAmazingProducerSource
            ? "bekanntes AmazingProducer-Normalisierungsprofil"
            : "unbekannter Hash; keine quellenspezifischen Korrekturen";
        ValidationText.Text =
            $"Gültig: {result.Features.Count:N0} Features, {result.UniqueDxccEntityIdCount:N0} numerische DXCC-IDs, " +
            $"{result.CoordinateCount:N0} Koordinaten · {profile} · {warnings:N0} Warnungen.";
        return true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        GeoJsonPathBox.Clear();
        EnabledByDefaultCheck.IsChecked = false;
        ValidationText.Text = "Der Pfad wird beim Speichern entfernt; das Overlay bleibt deaktiviert.";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var valid = await ValidateCurrentAsync();
        var path = string.IsNullOrWhiteSpace(GeoJsonPathBox.Text)
            ? null
            : GeoJsonPathBox.Text.Trim();
        if (path is not null && !Path.IsPathFullyQualified(path))
        {
            MessageBox.Show(this, "Bitte einen absoluten Pfad angeben.", "DXCC-Kartenoverlay",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var widthsSaved = false;
        try
        {
            // Commit the general settings before a DXCC save can remove legacy fields.
            _mapSettings.Save(new MapSettings(
                (double)ShortpathWidthBox.SelectedItem,
                (double)LongpathWidthBox.SelectedItem));
            widthsSaved = true;
            var configuration = new DxccMapConfiguration(path, EnabledByDefaultCheck.IsChecked == true);
            if (configuration != _initialDxccConfiguration)
                _store.Save(configuration);
            if (!valid)
            {
                MessageBox.Show(this,
                    "Der Pfad wurde gespeichert, ist aber derzeit nicht verwendbar. Normale Karten bleiben verfügbar; das DXCC-Overlay bleibt deaktiviert.",
                    "DXCC-Kartenoverlay", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            var message = widthsSaved
                ? $"Linienbreiten wurden gespeichert. DXCC-Einstellungen konnten nicht gespeichert werden: {ex.Message}"
                : ex.Message;
            MessageBox.Show(this, message, "Kartenoptionen",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
