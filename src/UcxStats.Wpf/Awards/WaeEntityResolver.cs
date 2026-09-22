using UcxStats.Data.Database;

namespace UcxStats.Wpf.Awards;

public static class WaeEntityResolver
{
    // Official current WAE list supplied for UcxStats. Codes follow the
    // official list where practical; a few UCX aliases are normalized below.
    public static IReadOnlyList<WaeEntity> Entities { get; } =
    [
        new("1A", "Sovereign Military Order of Malta"),
        new("3A", "Monaco"),
        new("4O", "Montenegro", new DateOnly(2006, 6, 28)),
        new("4U1I", "ITU Headquarters Geneva"),
        new("4U1V", "United Nations Vienna International Center"),
        new("9A", "Croatia", new DateOnly(1991, 6, 26)),
        new("9H", "Malta"),
        new("C3", "Andorra"),
        new("CT", "Portugal"),
        new("CU", "Azores"),
        new("DL", "Germany", new DateOnly(1973, 9, 17)),
        new("E7", "Bosnia & Herzegovina", new DateOnly(1991, 10, 10)),
        new("EA", "Spain"),
        new("EA6", "Balearic Islands"),
        new("EI", "Ireland"),
        new("ER", "Moldova"),
        new("ES", "Estonia"),
        new("EU", "Belarus"),
        new("F", "France"),
        new("G", "England"),
        new("GD", "Isle of Man"),
        new("GI", "Northern Ireland"),
        new("GJ", "Jersey Island"),
        new("GM", "Scotland"),
        new("GM/S", "Shetland Islands"),
        new("GU", "Guernsey, Alderney"),
        new("GW", "Wales"),
        new("HA", "Hungary"),
        new("HB", "Switzerland"),
        new("HB0", "Liechtenstein"),
        new("HV", "Vatican City"),
        new("I", "Italy"),
        new("IT9", "Sicily"),
        new("IS", "Sardinia"),
        new("JW", "Spitzbergen Island"),
        new("JW/B", "Bear Island"),
        new("JX", "Jan Mayen"),
        new("LA", "Norway"),
        new("LX", "Luxembourg"),
        new("LY", "Lithuania"),
        new("LZ", "Bulgaria"),
        new("OE", "Austria"),
        new("OH", "Finland"),
        new("OH0", "Aland Islands"),
        new("OJ0", "Market Reef"),
        new("OK", "Czech Republic", new DateOnly(1993, 1, 1)),
        new("OM", "Slovakia", new DateOnly(1993, 1, 1)),
        new("ON", "Belgium"),
        new("OY", "Faroe Islands"),
        new("OZ", "Denmark"),
        new("PA", "The Netherlands"),
        new("R1FJ", "Franz Josef Land"),
        new("S5", "Slovenia", new DateOnly(1991, 6, 26)),
        new("SM", "Sweden"),
        new("SP", "Poland"),
        new("SV", "Greece"),
        new("SV/A", "Mount Athos"),
        new("SV5", "Dodecanese"),
        new("SV9", "Crete Island"),
        new("T7", "San Marino"),
        new("TA1", "Turkey (European Part)"),
        new("TF", "Iceland"),
        new("TK", "Corse Island"),
        new("UA", "Russia (European Part)"),
        new("UA2", "Kaliningrad"),
        new("UR", "Ukraine"),
        new("YL", "Latvia"),
        new("YO", "Romania"),
        new("YU", "Serbia"),
        new("Z6", "Kosovo", new DateOnly(2008, 2, 17)),
        new("Z3", "Northern Macedonia", new DateOnly(1991, 9, 8)),
        new("ZA", "Albania"),
        new("ZB", "Gibraltar")
    ];


    // DCL APP_DCL_ADIF2 subdivisions observed in the complete DCL ADIF export.
    // The normal case is APP_DCL_ADIF2 == DXCC. Values 1000+DXCC identify
    // DCL subdivisions used for award/entity evaluation.
    private static readonly IReadOnlyDictionary<int, string> DclAdif2ToWae = new Dictionary<int, string>
    {
        [1206] = "4U1V", // 4U1A / United Nations Vienna International Center
        [1248] = "IT9",  // Sicily
        [1279] = "GM/S", // Shetland Islands
        [1390] = "TA1",  // European Turkey
        [1522] = "Z6"    // Kosovo
    };

    private static readonly IReadOnlyDictionary<string, WaeEntity> ByCode =
        Entities.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    // Known Sicilian calls from the user's DCL/WAE result which UCX stores as country=I.
    // Normal IT9*/IB9* calls are covered generically; this set is intentionally extendable.
    private static readonly HashSet<string> SicilyExactCalls = new(StringComparer.OrdinalIgnoreCase)
    {
        "IB9A", "IB9R", "IT9ECY"
    };

    public static WaeEntity? Resolve(WaeQsoCandidate qso)
    {
        var call = NormalizeCall(qso.CallSign);
        var country = qso.Country.Trim().ToUpperInvariant();

        if (qso.DclConfirmed && qso.DclAdif2 is { } dclAdif2 &&
            DclAdif2ToWae.TryGetValue(dclAdif2, out var dclCode))
            return GetValid(dclCode, qso.QsoDate);

        // WAE-specific distinctions which are not separate UCX country values.
        if (country == "TA" && HasOperatingPrefix(call, "TA1"))
            return GetValid("TA1", qso.QsoDate);

        if (country == "I" && IsSicilyCall(call))
            return GetValid("IT9", qso.QsoDate);

        // UCX aliases / subdivisions.
        var code = country switch
        {
            "4U_IT" => "4U1I",
            "UA3" => "UA",
            "ZB2" => "ZB",
            _ => country
        };

        return GetValid(code, qso.QsoDate);
    }


    public static bool IsConfirmationValidForEntity(WaeQsoCandidate qso, WaeEntity entity)
    {
        // A DCL confirmation carries its own entity assignment and is authoritative
        // for that exact matched QSO.
        if (qso.DclConfirmed && qso.DclAdif2 is { } dclAdif2 &&
            DclAdif2ToWae.TryGetValue(dclAdif2, out var dclCode))
            return string.Equals(dclCode, entity.Code, StringComparison.OrdinalIgnoreCase);

        // Without a DCL entity assignment, only confirmations belonging to the same
        // entity resolved from the UCX QSO may count. ResolveWithoutDcl prevents a
        // confirmation from another UCX country (e.g. OE) leaking into 4U1V.
        var ucxEntity = ResolveWithoutDcl(qso);
        return qso.Confirmed &&
               ucxEntity is not null &&
               string.Equals(ucxEntity.Code, entity.Code, StringComparison.OrdinalIgnoreCase);
    }

    private static WaeEntity? ResolveWithoutDcl(WaeQsoCandidate qso)
    {
        var call = NormalizeCall(qso.CallSign);
        var country = qso.Country.Trim().ToUpperInvariant();

        if (country == "TA" && HasOperatingPrefix(call, "TA1"))
            return GetValid("TA1", qso.QsoDate);

        if (country == "I" && IsSicilyCall(call))
            return GetValid("IT9", qso.QsoDate);

        var code = country switch
        {
            "4U_IT" => "4U1I",
            "UA3" => "UA",
            "ZB2" => "ZB",
            _ => country
        };
        return GetValid(code, qso.QsoDate);
    }

    private static bool IsSicilyCall(string call)
    {
        if (SicilyExactCalls.Contains(call))
            return true;

        // Covers ordinary IT9 and IB9 calls, including /P etc.
        return HasOperatingPrefix(call, "IT9") || HasOperatingPrefix(call, "IB9");
    }

    private static bool HasOperatingPrefix(string call, string prefix)
    {
        if (call.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        // Handles forms such as IT9/DL..., TA1/DL... and portable suffixes.
        var parts = call.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCall(string call) => call.Trim().ToUpperInvariant();

    private static WaeEntity? GetValid(string code, string qsoDate)
    {
        if (!ByCode.TryGetValue(code, out var entity))
            return null;

        if (!DateOnly.TryParseExact(qsoDate, "yyyy-MM-dd", out var date))
            return null;

        if (entity.ValidFrom is { } from && date < from)
            return null;
        if (entity.ValidTo is { } to && date > to)
            return null;

        return entity;
    }
}
