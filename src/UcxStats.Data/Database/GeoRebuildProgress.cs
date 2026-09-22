namespace UcxStats.Data.Database;

public sealed record GeoRebuildProgress(long Processed, long Total)
{
    public double Percent => Total <= 0 ? 100.0 : Processed * 100.0 / Total;
}
