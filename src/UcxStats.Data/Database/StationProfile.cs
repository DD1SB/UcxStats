namespace UcxStats.Data.Database;

public sealed record StationProfile(
    long Id,
    string Name,
    string Locator,
    double Latitude,
    double Longitude,
    double? ElevationM,
    string Antenna,
    double? AntennaHeightM);
