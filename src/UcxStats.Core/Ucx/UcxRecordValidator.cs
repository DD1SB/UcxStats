namespace UcxStats.Core.Ucx;

public sealed class UcxRecordValidator
{
    public IReadOnlyList<UcxDiagnostic> Validate(UcxQsoRecord qso)
    {
        var result = new List<UcxDiagnostic>();
        void Warn(string message) => result.Add(new UcxDiagnostic(qso.RecordIndex, UcxDiagnosticSeverity.Warning, message));

        if (qso.RecordTypeRaw is not (0 or 1))
            Warn($"Unbekannter Record-Typ {qso.RecordTypeRaw} an Offset 0.");

        if (qso.Band is null)
            Warn($"Unbekannter Band-Code {qso.BandCode}.");

        if (qso.Mode is null)
            Warn($"Unbekannter Mode-Code {qso.ModeCode}.");

        if (qso.CqZone > 40)
            Warn($"Auffällige CQ-Zone {qso.CqZone}.");

        if (qso.ItuZone > 90)
            Warn($"Auffällige ITU-Zone {qso.ItuZone}.");

        if (qso.ContinentCode > 7)
            Warn($"Unbekannter Kontinent-Code {qso.ContinentCode}.");

        if (qso.IotaContinentCode > 6 && qso.IotaNumber != 0)
            Warn($"Unbekannter IOTA-Kontinent-Code {qso.IotaContinentCode}.");

        if (qso.CbaRaw > 1)
            Warn($"CBA enthält unerwarteten Wert {qso.CbaRaw}.");

        if (qso.ManagerOkRaw > 1)
            Warn($"Manager-OK enthält unerwarteten Wert {qso.ManagerOkRaw}.");

        if ((qso.Status151Raw & 0x04) != 0)
            Warn("Unbekanntes Statusbit 151 & 0x04 ist gesetzt.");

        if (!string.IsNullOrWhiteSpace(qso.Locator) && !IsPlausibleLocator(qso.Locator))
            Warn($"Locator '{qso.Locator}' ist syntaktisch auffällig.");

        return result;
    }

    private static bool IsPlausibleLocator(string locator)
    {
        var text = locator.Trim().ToUpperInvariant();
        if (text.Length is not (2 or 4 or 6 or 8)) return false;
        if (text[0] is < 'A' or > 'R' || text[1] is < 'A' or > 'R') return false;
        if (text.Length >= 4 && (!char.IsDigit(text[2]) || !char.IsDigit(text[3]))) return false;
        if (text.Length >= 6 && (text[4] is < 'A' or > 'X' || text[5] is < 'A' or > 'X')) return false;
        if (text.Length >= 8 && (!char.IsDigit(text[6]) || !char.IsDigit(text[7]))) return false;
        return true;
    }
}
