using System.Text.RegularExpressions;

namespace UcxStats.Data.Database;

public static partial class DclAdifParser
{
    [GeneratedRegex(@"<(?<name>[^:>]+):(?<len>\d+)(?::[^>]*)?>(?<value>[^<]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FieldRegex();

    public static IReadOnlyList<DclAdifRecord> Parse(string text)
    {
        var result = new List<DclAdifRecord>();
        foreach (var rawRecord in Regex.Split(text, @"(?i)<EOR>"))
        {
            if (!rawRecord.Contains("<CALL:", StringComparison.OrdinalIgnoreCase))
                continue;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in FieldRegex().Matches(rawRecord))
            {
                var length = int.Parse(match.Groups["len"].Value);
                var value = match.Groups["value"].Value;
                if (value.Length > length)
                    value = value[..length];
                fields[match.Groups["name"].Value] = value.Trim();
            }

            if (!fields.TryGetValue("CALL", out var call) ||
                !fields.TryGetValue("QSO_DATE", out var date) ||
                !fields.TryGetValue("TIME_ON", out var time) ||
                !fields.TryGetValue("BAND", out var band) ||
                !fields.TryGetValue("MODE", out var mode))
                continue;

            result.Add(new DclAdifRecord(
                call.ToUpperInvariant(),
                NormalizeDate(date),
                NormalizeTime(time),
                band.ToUpperInvariant(),
                mode.ToUpperInvariant(),
                fields.GetValueOrDefault("APP_DCL_STATUS", "").ToLowerInvariant(),
                fields.GetValueOrDefault("STATION_CALLSIGN", "").ToUpperInvariant(),
                fields.GetValueOrDefault("MY_GRIDSQUARE", "").ToUpperInvariant(),
                fields.GetValueOrDefault("DXCC", ""),
                fields.GetValueOrDefault("APP_DCL_ADIF2", "")));
        }

        return result;
    }

    public static bool IsConfirmedStatus(string status) =>
        status is "c" or "i" or "m" or "n" or "o";

    private static string NormalizeDate(string value) =>
        value.Length >= 8 ? $"{value[..4]}-{value.Substring(4, 2)}-{value.Substring(6, 2)}" : value;

    private static string NormalizeTime(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return value;
        return $"{digits[..2]}:{digits.Substring(2, 2)}";
    }
}
