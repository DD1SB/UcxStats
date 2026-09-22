namespace UcxStats.Data.Database;

public sealed record MapQsoPoint(
    long Id, string SourceFile, string CallSign, string QsoDate, string StartTime,
    int BandCode, int ModeCode, string Locator, double Latitude, double Longitude,
    string StationProfileName, double StationLatitude, double StationLongitude,
    double? DistanceKm, double? BearingDeg);
