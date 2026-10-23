using System.Text.Json.Serialization;

namespace UcxStats.Map;

public sealed record MapSettings(
    double ShortpathWidth = MapPathWidths.DefaultShortpathWidth,
    double LongpathWidth = MapPathWidths.DefaultLongpathWidth)
{
    [JsonIgnore]
    public MapPathWidths PathWidths => new MapPathWidths(ShortpathWidth, LongpathWidth).Normalize();

    public MapSettings Normalize()
    {
        var widths = PathWidths;
        return this with { ShortpathWidth = widths.ShortpathWidth, LongpathWidth = widths.LongpathWidth };
    }
}
