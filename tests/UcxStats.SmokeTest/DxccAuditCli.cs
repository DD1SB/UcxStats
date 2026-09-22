using System.Globalization;
using System.Text;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;

namespace UcxStats.SmokeTest;

public sealed record DxccAuditOptions(string DatabasePath, string? CsvPath);

public static class DxccAuditCommandLine
{
    public const string Usage =
        "Usage: UcxStats.SmokeTest dxcc-audit <database-path> [--csv <csv-path>]";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out DxccAuditOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (args.Count == 0 || !string.Equals(args[0], "dxcc-audit", StringComparison.OrdinalIgnoreCase))
        {
            error = "Expected the 'dxcc-audit' command.";
            return false;
        }

        if (args.Count is not (2 or 4) || string.IsNullOrWhiteSpace(args[1]))
        {
            error = "A database path is required.";
            return false;
        }

        string? csvPath = null;
        if (args.Count == 4)
        {
            if (!string.Equals(args[2], "--csv", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(args[3]))
            {
                error = "Optional CSV output must be specified as '--csv <path>'.";
                return false;
            }
            csvPath = args[3];
        }

        options = new DxccAuditOptions(args[1], csvPath);
        return true;
    }
}

public static class DxccAuditCli
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (!DxccAuditCommandLine.TryParse(args, out var options, out var parseError))
        {
            await error.WriteLineAsync(parseError);
            await error.WriteLineAsync(DxccAuditCommandLine.Usage);
            return 2;
        }

        string databasePath;
        try
        {
            databasePath = Path.GetFullPath(options!.DatabasePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            await error.WriteLineAsync($"Invalid database path: {ex.Message}");
            return 2;
        }

        // SqliteQsoRepository uses ReadWriteCreate. Check first so a typo cannot
        // create an empty database as a side effect of running this diagnostic.
        if (!File.Exists(databasePath))
        {
            await error.WriteLineAsync($"Database does not exist: {databasePath}");
            return 2;
        }

        try
        {
            var repository = new SqliteQsoRepository(databasePath);
            var audit = await repository.GetDxccIdentityAuditAsync(cancellationToken);
            await DxccAuditFormatter.WriteTextAsync(output, databasePath, audit);

            if (options.CsvPath is not null)
            {
                var csvPath = Path.GetFullPath(options.CsvPath);
                await using var stream = new FileStream(
                    csvPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await DxccAuditFormatter.WriteCsvAsync(writer, audit);
                await writer.FlushAsync(cancellationToken);
                await output.WriteLineAsync();
                await output.WriteLineAsync($"CSV: {csvPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"DXCC audit failed: {ex.Message}");
            return 1;
        }
    }
}

public static class DxccAuditFormatter
{
    public static async Task WriteTextAsync(
        TextWriter writer,
        string databasePath,
        DxccIdentityAuditResult audit)
    {
        var resolutionRate = audit.TotalQsos == 0
            ? 0d
            : audit.ResolvedQsos * 100d / audit.TotalQsos;
        var resolutionRateText = resolutionRate.ToString("F2", CultureInfo.InvariantCulture);

        await writer.WriteLineAsync("DXCC identity audit");
        await writer.WriteLineAsync("===================");
        await writer.WriteLineAsync($"Database: {databasePath}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"Total QSOs:           {audit.TotalQsos,10}");
        await writer.WriteLineAsync($"DXCC resolved:        {audit.ResolvedQsos,10}");
        await writer.WriteLineAsync($"DXCC unresolved:      {audit.UnresolvedQsos,10}");
        await writer.WriteLineAsync($"Explicit non-DXCC QSOs:{audit.ExplicitNonDxccQsos,10}");
        await writer.WriteLineAsync(
            $"DCL fallback QSOs:    {audit.EvidenceCounts.GetValueOrDefault(DxccIdentityEvidence.DclFallback),10}");
        await writer.WriteLineAsync($"Resolution rate:      {resolutionRateText,9} %");
        await writer.WriteLineAsync($"Distinct UCX codes:   {audit.Countries.Count,10}");
        await writer.WriteLineAsync($"Unresolved UCX codes: {audit.DistinctUnresolvedCountryCodes.Count,10}");
        await writer.WriteLineAsync($"DCL/UCX conflicts:    {audit.Conflicts.Count,10}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("UCX           QSOs    DXCC  Status");
        await writer.WriteLineAsync("----------------------------------------");

        foreach (var row in OrderedCountries(audit))
        {
            var dxcc = row.MappedEntityId?.ToString(CultureInfo.InvariantCulture) ?? "-";
            var status = row.Status == DxccCountryMappingStatus.Mapped ? "mapped" : "unresolved";
            await writer.WriteLineAsync($"{row.CountryCode,-10}{row.QsoCount,8}{dxcc,8}  {status}");
        }

        if (audit.ExplicitNonDxccEvidence.Count > 0)
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("Explicit non-DXCC evidence");
            await writer.WriteLineAsync("==========================");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("QSO ID       UCX   UCX DXCC   DCL DXCC  Status");
            await writer.WriteLineAsync("------------------------------------------------");
            foreach (var evidence in audit.ExplicitNonDxccEvidence.OrderBy(x => x.QsoId))
            {
                var ucxDxcc = evidence.UcxEntityId?.ToString(CultureInfo.InvariantCulture) ?? "-";
                await writer.WriteLineAsync(
                    $"{evidence.QsoId,6}  {evidence.CountryCode,-8}{ucxDxcc,8}{evidence.DclCode,11}  explicit-non-dxcc");
            }
        }

        if (audit.Conflicts.Count == 0)
            return;

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("DCL / UCX conflicts");
        await writer.WriteLineAsync("===================");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("QSO ID       UCX   UCX DXCC   DCL DXCC   Effective  Source");
        await writer.WriteLineAsync("----------------------------------------------------------");
        foreach (var conflict in audit.Conflicts.OrderBy(x => x.QsoId))
            await writer.WriteLineAsync(
                $"{conflict.QsoId,6}  {conflict.CountryCode,-8}{conflict.UcxEntityId,8}" +
                $"{conflict.DclEntityId,11}{conflict.EffectiveEntityId,12}  UCX");
    }

    public static async Task WriteCsvAsync(TextWriter writer, DxccIdentityAuditResult audit)
    {
        var explicitByCountry = audit.ExplicitNonDxccEvidence
            .GroupBy(x => x.CountryCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        await writer.WriteLineAsync("UCX,QSOs,DXCC,Status,ExplicitNonDxccQSOs");
        foreach (var row in OrderedCountries(audit))
        {
            var dxcc = row.MappedEntityId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            var status = row.Status == DxccCountryMappingStatus.Mapped ? "mapped" : "unresolved";
            await writer.WriteLineAsync(string.Join(",",
                EscapeCsv(row.CountryCode),
                row.QsoCount.ToString(CultureInfo.InvariantCulture),
                dxcc,
                status,
                explicitByCountry.GetValueOrDefault(row.CountryCode).ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static IEnumerable<DxccCountryAuditRow> OrderedCountries(DxccIdentityAuditResult audit) =>
        audit.Countries
            .OrderByDescending(x => x.QsoCount)
            .ThenBy(x => x.CountryCode, StringComparer.Ordinal);

    private static string EscapeCsv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";
}
