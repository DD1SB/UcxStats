using System.Diagnostics;
using System.Text;
using UcxStats.Data.Database;
using UcxStats.Map;
using Xunit.Abstractions;

namespace UcxStats.Data.Tests;

public sealed class StatisticsPerformanceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(11000)]
    [InlineData(100000)]
    public async Task ScopedDatabaseAggregationAndSerializationRemainCompact(int count)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ucxstats-stat-performance-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteQsoRepository(path);
            var template = RepositoryDxccTests.Import("performance.ucx", 1, "DL", 4);
            await repo.UpsertManyAsync(Enumerable.Range(1, count).Select(i => template with
            {
                SourceRecordIndex = i, SourceRecordHash = $"hash-{i}",
                Qso = template.Qso with { RecordIndex = i, BandCode = (byte)(i % 17), Locator = $"JO{i % 10}{i / 10 % 10}AA",
                    Date = new DateOnly(2026, 1, 1).AddDays(i / 1440), StartTime = new TimeOnly(i / 60 % 24, i % 60) }
            }));
            var watch = Stopwatch.StartNew();
            var snapshot = await repo.GetMapStatisticsAsync(QsoScope.ForSources(["performance.ucx"]));
            var aggregationMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var json = MapStatisticsPayload.Serialize(snapshot, new MapSettings());
            var serializationMs = watch.Elapsed.TotalMilliseconds;
            var bytes = Encoding.UTF8.GetByteCount(json);
            output.WriteLine($"{count:N0} synthetic QSOs: query/aggregation {aggregationMs:F1} ms; serialization {serializationMs:F1} ms; {bytes:N0} UTF-8 bytes.");
            Assert.Equal(count, snapshot.Summarize().TotalQsos);
            Assert.Equal(17, snapshot.Bands.Count);
            Assert.Equal(100, snapshot.Summarize().Grids);
            Assert.True(bytes < 15000, $"Unexpected QSO-level payload growth: {bytes} bytes");
            // Large selected/SQL scopes must not hit SQLite's expression/variable limits.
            var selectedCount = Math.Min(count, 11000);
            var records = QsoScope.ForSourceRecords(Enumerable.Range(1, selectedCount)
                .Select(i => new QsoSourceRecordKey("performance.ucx", i)));
            Assert.Equal(selectedCount, (await repo.GetMapStatisticsAsync(records)).Summarize().TotalQsos);
            var ids = QsoScope.ForQsoIds(Enumerable.Range(1, count).Select(i => (long)i));
            Assert.Equal(count, (await repo.GetMapStatisticsAsync(ids)).Summarize().TotalQsos);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
