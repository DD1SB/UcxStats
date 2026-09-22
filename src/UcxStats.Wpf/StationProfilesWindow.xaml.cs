using System.Globalization;
using System.Windows;
using UcxStats.Data.Database;
using UcxStats.Geo.Maidenhead;

namespace UcxStats.Wpf;

public partial class StationProfilesWindow : Window
{
    private readonly IQsoRepository _repository;
    private long _editingId;

    public StationProfilesWindow(IQsoRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(long? selectId = null)
    {
        var items = await _repository.GetStationProfilesAsync();
        ProfileList.ItemsSource = items;
        if (selectId.HasValue)
            ProfileList.SelectedItem = items.FirstOrDefault(x => x.Id == selectId);
    }

    private void ProfileList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not StationProfile p) return;
        _editingId = p.Id;
        NameBox.Text = p.Name;
        LocatorBox.Text = p.Locator;
        LatitudeBox.Text = p.Latitude.ToString("F8", CultureInfo.CurrentCulture);
        LongitudeBox.Text = p.Longitude.ToString("F8", CultureInfo.CurrentCulture);
        ElevationBox.Text = p.ElevationM?.ToString(CultureInfo.CurrentCulture) ?? "";
        AntennaBox.Text = p.Antenna;
        AntennaHeightBox.Text = p.AntennaHeightM?.ToString(CultureInfo.CurrentCulture) ?? "";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _editingId = 0;
        ProfileList.SelectedItem = null;
        NameBox.Clear(); LocatorBox.Clear(); LatitudeBox.Clear(); LongitudeBox.Clear();
        ElevationBox.Clear(); AntennaBox.Clear(); AntennaHeightBox.Clear();
        NameBox.Focus();
    }

    private void LocatorToCoordinates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var c = MaidenheadConverter.ToCenter(LocatorBox.Text.Trim());
            LatitudeBox.Text = c.Latitude.ToString("F8", CultureInfo.CurrentCulture);
            LongitudeBox.Text = c.Longitude.ToString("F8", CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Locator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static double ParseCoordinate(string text, string name, double min, double max)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            throw new ArgumentException($"{name} ist keine gültige Zahl.");
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(name, $"{name} muss zwischen {min} und {max} liegen.");
        return value;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = NameBox.Text.Trim();
            var locator = LocatorBox.Text.Trim().ToUpperInvariant();
            if (name.Length == 0) throw new ArgumentException("Bitte einen Profilnamen angeben.");

            // Locator remains useful metadata and is validated when present.
            if (locator.Length > 0) _ = MaidenheadConverter.ToCenter(locator);

            double latitude;
            double longitude;
            if (string.IsNullOrWhiteSpace(LatitudeBox.Text) || string.IsNullOrWhiteSpace(LongitudeBox.Text))
            {
                if (locator.Length == 0)
                    throw new ArgumentException("Bitte Locator oder Latitude/Longitude angeben.");
                var c = MaidenheadConverter.ToCenter(locator);
                latitude = c.Latitude;
                longitude = c.Longitude;
            }
            else
            {
                latitude = ParseCoordinate(LatitudeBox.Text, "Latitude", -90, 90);
                longitude = ParseCoordinate(LongitudeBox.Text, "Longitude", -180, 180);
            }

            double? ParseOptional(string value) =>
                string.IsNullOrWhiteSpace(value) ? null : double.Parse(value, CultureInfo.CurrentCulture);

            var p = new StationProfile(_editingId, name, locator, latitude, longitude,
                ParseOptional(ElevationBox.Text), AntennaBox.Text.Trim(), ParseOptional(AntennaHeightBox.Text));
            var id = await _repository.SaveStationProfileAsync(p);
            await ReloadAsync(id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Stationsprofil", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId == 0) return;
        if (MessageBox.Show(this, "Stationsprofil wirklich löschen? Zuordnungen zu Quellen werden entfernt.",
            "Stationsprofil", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _repository.DeleteStationProfileAsync(_editingId);
        New_Click(sender, e);
        await ReloadAsync();
    }
}
