using System.IO;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Geo.Maidenhead;

namespace UcxStats.Wpf;

internal sealed class UcxImportService
{
    private readonly IQsoRepository _repository;

    public UcxImportService(IQsoRepository repository)
    {
        _repository = repository;
    }

    public async Task<(QsoImportResult Result, int RecordCount)> SyncFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var sourcePath = Path.GetFullPath(fileName);
        var reader = new UcxFileReader();
        var sourceRecords = reader.ReadWithMetadata(sourcePath).ToList();
        var importRecords = new List<QsoImportRecord>(sourceRecords.Count);

        foreach (var source in sourceRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var qso = source.Qso;
            BuildGeo(qso, out var latitude, out var longitude,
                out var grid2, out var grid4, out var grid6, out var grid8);

            importRecords.Add(new QsoImportRecord
            {
                SourceFile = sourcePath,
                SourceRecordIndex = qso.RecordIndex,
                SourceRecordHash = source.RecordHash,
                Qso = qso,
                Latitude = latitude,
                Longitude = longitude,
                Grid2 = grid2,
                Grid4 = grid4,
                Grid6 = grid6,
                Grid8 = grid8
            });
        }

        var result = await _repository.UpsertManyAsync(importRecords, cancellationToken);
        return (result, sourceRecords.Count);
    }

    private static void BuildGeo(
        UcxQsoRecord qso,
        out double? latitude,
        out double? longitude,
        out string? grid2,
        out string? grid4,
        out string? grid6,
        out string? grid8)
    {
        latitude = longitude = null;
        grid2 = grid4 = grid6 = grid8 = null;

        if (string.IsNullOrWhiteSpace(qso.Locator))
            return;

        try
        {
            var locator = qso.Locator.Trim().ToUpperInvariant();
            var coordinate = MaidenheadConverter.ToCenter(locator);
            latitude = coordinate.Latitude;
            longitude = coordinate.Longitude;
            if (locator.Length >= 2) grid2 = locator[..2];
            if (locator.Length >= 4) grid4 = locator[..4];
            if (locator.Length >= 6) grid6 = locator[..6];
            if (locator.Length >= 8) grid8 = locator[..8];
        }
        catch (ArgumentException)
        {
            // Invalid locators remain importable; geo fields stay NULL.
        }
    }
}
