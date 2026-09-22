using System.Globalization;

namespace UcxStats.Core.Ucx;

public static class UcxDisplayFormat
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    public static string Band(byte bandCode) => bandCode switch
    {
        0 => "160 m", 1 => "80 m", 2 => "40 m", 3 => "30 m", 4 => "20 m",
        5 => "17 m", 6 => "15 m", 7 => "12 m", 8 => "10 m", 9 => "6 m",
        10 => "2 m", 11 => "70 cm", 12 => "23 cm", 13 => "13 cm",
        14 => "3400 MHz", 15 => "5600 MHz", 16 => "10 GHz",
        _ => $"Band {bandCode}"
    };

    public static string Mode(byte modeCode) => modeCode switch
    {
        0 => "CW", 1 => "SSB", 2 => "RTTY", 3 => "AM", 4 => "FM",
        5 => "PSK31", 7 => "PKT", 12 => "SAT", 30 => "FT8",
        _ => $"Mode {modeCode}"
    };

    public static string IotaContinent(byte continentCode) => continentCode switch
    {
        0 => "EU",
        1 => "AS",
        2 => "AF",
        3 => "NA",
        4 => "SA",
        5 => "OC",
        6 => "AN",
        _ => $"?{continentCode}"
    };

    public static string Iota(byte continentCode, ushort number) =>
        number == 0 ? string.Empty : $"{IotaContinent(continentCode)}-{number:000}";

    public static string Frequency(ulong frequencyHz)
    {
        if (frequencyHz >= 1_000_000_000UL)
            return $"{(frequencyHz / 1_000_000_000d).ToString("N6", GermanCulture)} GHz";

        if (frequencyHz >= 1_000_000UL)
            return $"{(frequencyHz / 1_000_000d).ToString("N6", GermanCulture)} MHz";

        return $"{(frequencyHz / 1_000d).ToString("N3", GermanCulture)} kHz";
    }
}
