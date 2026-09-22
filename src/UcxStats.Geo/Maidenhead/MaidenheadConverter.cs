namespace UcxStats.Geo.Maidenhead;

public static class MaidenheadConverter
{
    public static MaidenheadCoordinate ToCenter(string locator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);
        var text = locator.Trim().ToUpperInvariant();
        if (text.Length is not (2 or 4 or 6 or 8 or 10))
            throw new ArgumentException("Maidenhead locator length must be 2, 4, 6, 8 or 10 characters.", nameof(locator));

        if (text[0] is < 'A' or > 'R' || text[1] is < 'A' or > 'R')
            throw new ArgumentException("Invalid Maidenhead field.", nameof(locator));

        double lon = (text[0] - 'A') * 20.0 - 180.0;
        double lat = (text[1] - 'A') * 10.0 - 90.0;
        double cellLon = 20.0;
        double cellLat = 10.0;

        if (text.Length >= 4)
        {
            if (!char.IsDigit(text[2]) || !char.IsDigit(text[3]))
                throw new ArgumentException("Invalid Maidenhead square.", nameof(locator));
            cellLon = 2.0;
            cellLat = 1.0;
            lon += (text[2] - '0') * cellLon;
            lat += (text[3] - '0') * cellLat;
        }

        if (text.Length >= 6)
        {
            if (text[4] is < 'A' or > 'X' || text[5] is < 'A' or > 'X')
                throw new ArgumentException("Invalid Maidenhead subsquare.", nameof(locator));
            cellLon = 2.0 / 24.0;
            cellLat = 1.0 / 24.0;
            lon += (text[4] - 'A') * cellLon;
            lat += (text[5] - 'A') * cellLat;
        }

        if (text.Length >= 8)
        {
            if (!char.IsDigit(text[6]) || !char.IsDigit(text[7]))
                throw new ArgumentException("Invalid Maidenhead extended square.", nameof(locator));
            cellLon = (2.0 / 24.0) / 10.0;
            cellLat = (1.0 / 24.0) / 10.0;
            lon += (text[6] - '0') * cellLon;
            lat += (text[7] - '0') * cellLat;
        }

        if (text.Length >= 10)
        {
            if (text[8] is < 'A' or > 'X' || text[9] is < 'A' or > 'X')
                throw new ArgumentException("Invalid Maidenhead extended subsquare.", nameof(locator));
            cellLon = ((2.0 / 24.0) / 10.0) / 24.0;
            cellLat = ((1.0 / 24.0) / 10.0) / 24.0;
            lon += (text[8] - 'A') * cellLon;
            lat += (text[9] - 'A') * cellLat;
        }

        return new MaidenheadCoordinate(lat + cellLat / 2.0, lon + cellLon / 2.0);
    }
}
