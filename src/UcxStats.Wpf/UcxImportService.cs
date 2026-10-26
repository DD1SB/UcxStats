using System.IO;
using System.Collections.Concurrent;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Geo.Maidenhead;

namespace UcxStats.Wpf;

internal sealed class UcxImportService
{
    // Serialize before reading, across manual imports and all Live windows/service instances.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SourceLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IQsoRepository _repository;
    private readonly Func<CancellationToken, Task> _waitForStableRead;

    public UcxImportService(IQsoRepository repository) : this(repository, token => Task.Delay(250, token)) { }

    internal UcxImportService(IQsoRepository repository, Func<CancellationToken, Task> waitForStableRead)
    {
        _repository = repository;
        _waitForStableRead = waitForStableRead;
    }

    public async Task<(QsoImportResult Result, int RecordCount)> SyncFileAsync(
        string fileName, CancellationToken cancellationToken = default)
    {
        var snapshot = await SyncSnapshotAsync(fileName, cancellationToken);
        return (snapshot.Result, snapshot.Records.Count);
    }

    internal async Task<(QsoImportResult Result, IReadOnlyList<UcxSourceRecord> Records)> SyncSnapshotAsync(
        string fileName, CancellationToken cancellationToken = default)
    {
        var sourcePath = Path.GetFullPath(fileName);
        var gate = SourceLocks.GetOrAdd(Path.GetFullPath(_repository.DatabasePath) + "\0" + sourcePath, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken);
        FileStream? deletionGuard = null;
        try
        {
            var first = await Task.Run(() => ReadCompletedSnapshot(sourcePath, cancellationToken), cancellationToken);
            await _waitForStableRead(cancellationToken);
            var snapshot = await Task.Run(() => ReadCompletedSnapshot(sourcePath, cancellationToken), cancellationToken);
            EnsureSameSnapshot(sourcePath, first, snapshot);
            var imports = snapshot.Records.Select(source =>
            {
                var qso = source.Qso;
                BuildGeo(qso, out var latitude, out var longitude, out var grid2, out var grid4, out var grid6, out var grid8);
                return new QsoImportRecord
                {
                    SourceFile = sourcePath, SourceRecordIndex = qso.RecordIndex, SourceRecordHash = source.RecordHash,
                    Qso = qso, Latitude = latitude, Longitude = longitude, Grid2 = grid2, Grid4 = grid4, Grid6 = grid6, Grid8 = grid8
                };
            }).ToArray();
            var result = await Task.Run(() => _repository.ReconcileSourceAsync(sourcePath, imports, cancellationToken,
                (changes, token) => Task.Run(() =>
                {
                    // Windows sharing rules deny a live writer/replace during destructive commit.
                    // Append-only imports retain the existing shared-read behavior.
                    if (changes.Deleted > 0)
                        deletionGuard = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    EnsureSameSnapshot(sourcePath, snapshot, ReadCompletedSnapshot(sourcePath, token));
                }, token)), cancellationToken);
            return (result, snapshot.Records);
        }
        finally { deletionGuard?.Dispose(); gate.Release(); }
    }

    private sealed record FileStamp(long Length, DateTime LastWriteUtc, DateTime CreatedUtc);
    private sealed record CompletedSnapshot(FileStamp Stamp, IReadOnlyList<UcxSourceRecord> Records);

    private static FileStamp Stamp(string path)
    {
        var info = new FileInfo(path);
        return new(info.Length, info.LastWriteTimeUtc, info.CreationTimeUtc);
    }

    private static CompletedSnapshot ReadCompletedSnapshot(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var before = Stamp(path);
        var records = new List<UcxSourceRecord>();
        foreach (var record in new UcxFileReader().ReadWithMetadata(path))
        {
            token.ThrowIfCancellationRequested();
            records.Add(record);
        }
        token.ThrowIfCancellationRequested();
        if (before != Stamp(path) || before.Length != checked((long)records.Count * UcxConstants.RecordSize))
            throw SourceChanged(path);
        return new(before, records);
    }

    private static void EnsureSameSnapshot(string path, CompletedSnapshot expected, CompletedSnapshot actual)
    {
        if (expected.Stamp != actual.Stamp || !expected.Records.Select(x => x.RecordHash).SequenceEqual(actual.Records.Select(x => x.RecordHash)))
            throw SourceChanged(path);
    }

    private static IOException SourceChanged(string path) => new(
        $"UCX-Quelle '{path}' hat sich während der Synchronisation geändert. Snapshot nicht übernommen; bisherige Daten bleiben erhalten.");

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
