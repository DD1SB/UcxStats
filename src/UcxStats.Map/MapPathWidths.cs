namespace UcxStats.Map;

public sealed record MapPathWidths(
    double ShortpathWidth = MapPathWidths.DefaultShortpathWidth,
    double LongpathWidth = MapPathWidths.DefaultLongpathWidth)
{
    // Preserve the original Leaflet weights, including the off-step Shortpath default.
    public const double DefaultShortpathWidth = 1.3;
    public const double DefaultLongpathWidth = 1.0;
    public const double Minimum = 0.5;
    public const double Maximum = 8.0;
    public const double Step = 0.5;

    public static MapPathWidths Default { get; } = new();

    public MapPathWidths Normalize() => new(
        NormalizeWidth(ShortpathWidth, DefaultShortpathWidth),
        NormalizeWidth(LongpathWidth, DefaultLongpathWidth));

    public static bool IsValidWidth(double value) =>
        double.IsFinite(value) && value >= Minimum && value <= Maximum;

    private static double NormalizeWidth(double value, double fallback) =>
        IsValidWidth(value) ? value : fallback;
}
