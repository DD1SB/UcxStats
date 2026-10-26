using System.Text.Json;

namespace UcxStats.Map;

// Slider limits are deliberately narrower than the existing 1439/1440-minute
// configuration model. Never expand a native range while a pointer is dragging.
public static class LiveThresholdUi
{
    public const int WarningMinimum = 1;
    public const int WarningMaximum = 179;
    public const int ProlongedMinimum = 2;
    public const int ProlongedMaximum = 180;

    public static bool IsValidPair(int warning, int prolonged) =>
        warning is >= WarningMinimum and <= WarningMaximum &&
        prolonged is >= ProlongedMinimum and <= ProlongedMaximum && warning < prolonged;

    public static MapSettings Apply(MapSettings settings, int warning, int prolonged)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValidPair(warning, prolonged))
            throw new ArgumentOutOfRangeException(nameof(warning), "Live-Regler benötigen 1–179 / 2–180 Minuten und Warnung < sehr lange Lücke.");
        return settings with { LiveWarningThresholdMinutes = warning, LiveProlongedGapThresholdMinutes = prolonged };
    }

    public static string SerializeLimits() => JsonSerializer.Serialize(new
    {
        warningMinimum = WarningMinimum, warningMaximum = WarningMaximum,
        prolongedMinimum = ProlongedMinimum, prolongedMaximum = ProlongedMaximum
    });
}
