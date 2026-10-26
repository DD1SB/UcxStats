using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Map;
using UcxStats.Wpf;

namespace UcxStats.Data.Tests;

public sealed class UcxSourceReconciliationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ucxstats-source-{Guid.NewGuid():N}");
    private readonly SqliteQsoRepository _repository;
    private string Source => Path.Combine(_directory, "source.ucx");
    private UcxImportService FastService => new(_repository, _ => Task.CompletedTask);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public UcxSourceReconciliationTests()
    {
        Directory.CreateDirectory(_directory);
        _repository = new(Path.Combine(_directory, "test.db"));
    }
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }

    private static byte[] Record(string call = "FIRST", int minute = 40, string locator = "JO40AA", string country = "DL")
    {
        var data = new byte[UcxConstants.RecordSize];
        data[0] = 1; data[1] = 1; data[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(3, 2), 2026);
        data[5] = (byte)minute; data[6] = 11; data[7] = (byte)minute; data[8] = 11;
        data[73] = 4; data[74] = 0;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(175, 8), 14_000_000);
        void Text(int offset, string text) { var bytes = Encoding.Latin1.GetBytes(text); data[offset] = (byte)bytes.Length; bytes.CopyTo(data, offset + 1); }
        Text(9, call); Text(25, "599"); Text(49, "599"); Text(152, country); Text(211, locator);
        return data;
    }
    private static Task Write(string path, params byte[][] records) => File.WriteAllBytesAsync(path, records.SelectMany(x => x).ToArray());
    private async Task Seed()
    {
        await Write(Source, Record(), Record("LATEST", 50, "FN31PR12", "EA"));
        await FastService.SyncFileAsync(Source);
    }
    private Task<QsoStatisticsSnapshot> Statistics(string? source = null) => _repository.GetMapStatisticsAsync(QsoScope.ForSources([source ?? Source]));
    private async Task Execute(string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _repository.DatabasePath, ForeignKeys = true }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
    private async Task<string> State() => JsonSerializer.Serialize(new
    {
        qsos = (await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM qso ORDER BY id")).Rows,
        sources = (await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM source_file ORDER BY full_path")).Rows,
        cache = (await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM qso_geo_cache ORDER BY qso_id,station_profile_id")).Rows,
        metadata = (await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM ucxstats_metadata ORDER BY key")).Rows,
        profiles = (await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM station_profile ORDER BY id")).Rows
    });
    private async Task<long> AssignProfile()
    {
        var id = await _repository.SaveStationProfileAsync(new(0, "Home", "JN58", 48, 11, null, "", null));
        await _repository.AssignStationProfileToSourceAsync(Source, id);
        return id;
    }

    [Fact]
    public async Task ActualFileImportThroughSqliteAndGeneratedLivePageRemovesLatestThenBecomesEmpty()
    {
        await Write(Source, Record(), Record("LATEST", 50, "FN31PR12", "EA"));
        var service = new UcxImportService(_repository); // Exercise the production settling delay too.
        var imported = await service.SyncFileAsync(Source);
        Assert.Equal(2, imported.Result.Inserted);
        var profile = await AssignProfile();
        var scope = QsoScope.ForSources([Source]).WithFromUtc(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        var initialPoints = await _repository.GetMapQsoPointsAsync(scope);
        var initial = await _repository.GetMapStatisticsAsync(scope);
        Assert.Equal(2, initial.Summarize().TotalQsos); Assert.Equal(2, initial.Summarize().MappableQsos);
        Assert.Equal(2, initial.Summarize().Dxccs); Assert.Equal(2, initial.Summarize().MajorFields); Assert.Equal(2, initial.Summarize().Grids);
        Assert.Equal("LATEST", initial.Summarize().Odx!.CallSign);
        Assert.Equal(600, ContestTimeEstimator.Estimate(initial.TimestampsUtc).OperatingSeconds);
        var dxcc = DxccMapPayloadFactory.Create(await _repository.GetDxccStatusAsync(scope));
        var html = MapPageBuilder.Build(initialPoints, new("Test", "https://tiles.example/{z}/{x}/{y}.png", "Test", 18),
            new(true, true, "https://ucxstats.local/dxcc.geojson", dxcc, "ready"), isLiveMap: true);
        var states = new List<object>();
        async Task Capture(int total, string? latest)
        {
            var points = await _repository.GetMapQsoPointsAsync(scope);
            var stats = await _repository.GetMapStatisticsAsync(scope);
            Assert.Equal(total, stats.Summarize().TotalQsos); Assert.Equal(total, points.Count);
            Assert.Equal(latest, stats.LatestQso?.CallSign);
            Assert.Equal(total, LiveTimelinePayload.Create(stats, Now).Samples.Where((_, i) => i % 2 == 1).Sum());
            states.Add(new
            {
                script = $"window.ucxReplaceQsos({MapPageBuilder.BuildQsoPayloadJson(points)});" +
                    MapStatisticsPayload.BuildSnapshotScript(MapStatisticsPayload.Serialize(stats, new())) +
                    LiveTimelinePayload.BuildScript(LiveTimelinePayload.Serialize(LiveTimelinePayload.Create(stats, Now))) +
                    $"window.ucxSetDxccStatus({DxccMapPayloadFactory.Serialize(DxccMapPayloadFactory.Create(await _repository.GetDxccStatusAsync(scope)))});",
                total, latest
            });
        }
        await Capture(2, "LATEST");
        var firstId = initialPoints.Single(x => x.CallSign == "FIRST").Id;
        await Write(Source, Record());
        var shortened = await service.SyncFileAsync(Source);
        Assert.Equal(1, shortened.Result.Deleted); Assert.Equal(1, shortened.Result.Unchanged);
        await Capture(1, "FIRST");
        Assert.Equal(firstId, (await Statistics()).LatestQso!.QsoId);
        var one = (await Statistics()).Summarize();
        Assert.Equal(1, one.Dxccs); Assert.Equal(1, one.MajorFields); Assert.Equal(1, one.Grids); Assert.Equal("FIRST", one.Odx!.CallSign);
        Assert.Equal(0, ContestTimeEstimator.Estimate((await Statistics()).TimestampsUtc).GrossSeconds);
        await Write(Source);
        var empty = await service.SyncFileAsync(Source);
        Assert.Equal(0, empty.RecordCount); Assert.Equal(1, empty.Result.Deleted);
        await Capture(0, null);
        Assert.Null((await Statistics()).Summarize().Odx);
        var source = Assert.Single(await _repository.GetSourcesAsync());
        Assert.Equal(0, source.QsoCount); Assert.Equal(profile, source.StationProfileId);
        Assert.Empty((await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM qso_geo_cache")).Rows);
        Assert.Empty((await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM pragma_foreign_key_check")).Rows);
        await RunPage(html, states);
    }

    [Fact]
    public async Task RepeatedEventsAndAnotherSourcePreserveIdsHashesDclAndConfiguration()
    {
        await Seed(); var profile = await AssignProfile();
        var other = Path.Combine(_directory, "other.ucx");
        await Write(other, Record("OTHER", 59)); await FastService.SyncFileAsync(other);
        static string Dcl(string call, string time)
        {
            static string Field(string name, string value) => $"<{name}:{value.Length}>{value}";
            return Field("CALL", call) + Field("QSO_DATE", "20260101") + Field("TIME_ON", time) +
                Field("BAND", "20m") + Field("MODE", "CW") + Field("APP_DCL_STATUS", "c") +
                Field("DXCC", "230") + Field("APP_DCL_ADIF2", "230") + "<EOR>";
        }
        var dcl = await _repository.ImportDclAdifAsync(Dcl("FIRST", "114000") + Dcl("LATEST", "115000") + Dcl("OTHER", "115900"));
        Assert.Equal(3, dcl.Matched);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => FastService.SyncFileAsync(Source)));
        Assert.All(results, r => { Assert.Equal(2, r.Result.Unchanged); Assert.Equal(0, r.Result.Deleted); Assert.Equal(0, r.Result.Updated); });
        var unchangedRows = (await _repository.ExecuteReadOnlyQueryAsync("SELECT COUNT(*) FROM qso WHERE dcl_status='c'")).Rows;
        Assert.Equal(3L, unchangedRows[0][0]);
        var ids = (await _repository.ExecuteReadOnlyQueryAsync("SELECT id FROM qso ORDER BY id")).Rows.Select(x => x[0]).ToArray();
        await Write(Source, Record()); await FastService.SyncFileAsync(Source);
        Assert.Equal(2, (await _repository.GetMapStatisticsAsync(QsoScope.All)).Summarize().TotalQsos);
        Assert.Equal("OTHER", (await Statistics(other)).LatestQso!.CallSign);
        Assert.Equal(profile, (await _repository.GetSourcesAsync()).Single(x => x.FullPath == Source).StationProfileId);
        Assert.Equal(ids[0], (await Statistics()).LatestQso!.QsoId); Assert.Equal(ids[2], (await Statistics(other)).LatestQso!.QsoId);
        Assert.Equal(2L, (await _repository.ExecuteReadOnlyQueryAsync("SELECT COUNT(*) FROM qso WHERE dcl_status='c'")).Rows[0][0]);
        await Write(Source); await FastService.SyncFileAsync(Source);
        Assert.Equal(1, (await _repository.GetMapStatisticsAsync(QsoScope.All)).Summarize().TotalQsos);
        Assert.Single(await _repository.GetStationProfilesAsync());
    }

    [Fact]
    public async Task DeletingLocatorlessLatestQsoUpdatesActivityEvenWhenMarkerSetIsUnchanged()
    {
        await Write(Source, Record(), Record("NOLOC", 59, ""));
        await FastService.SyncFileAsync(Source); await AssignProfile();
        var scope = QsoScope.ForSources([Source]);
        var points = await _repository.GetMapQsoPointsAsync(scope);
        Assert.Equal("NOLOC", (await Statistics()).LatestQso!.CallSign);
        Assert.Equal(2, (await Statistics()).Summarize().TotalQsos); Assert.Single(points);
        await Write(Source, Record()); await FastService.SyncFileAsync(Source);
        Assert.Equal(points, await _repository.GetMapQsoPointsAsync(scope));
        var stats = await Statistics(); Assert.Equal("FIRST", stats.LatestQso!.CallSign);
        Assert.Equal(1, LiveTimelinePayload.Create(stats, Now).Samples.Where((_, i) => i % 2 == 1).Sum());
    }

    [Fact]
    public async Task PerQsoProfileOverrideIsPreservedAndUsesTheSameDistanceCacheAfterAnEdit()
    {
        await Seed(); var home = await AssignProfile();
        var away = await _repository.SaveStationProfileAsync(new(0, "Away", "FN31", 41, -73, null, "", null));
        await Execute($"UPDATE qso SET station_profile_id={away} WHERE source_record_index=0;");
        await Write(Source, Record(locator: "JO50"), Record("LATEST", 50, "FN31PR12", "EA"));
        await FastService.SyncFileAsync(Source);
        var first = (await _repository.GetMapQsoPointsAsync(QsoScope.ForSources([Source]))).Single(x => x.CallSign == "FIRST");
        Assert.Equal("Away", first.StationProfileName); Assert.NotNull(first.DistanceKm);
        Assert.Equal(home, Assert.Single(await _repository.GetSourcesAsync()).StationProfileId);
        await Write(Source); await FastService.SyncFileAsync(Source);
        Assert.Equal(2, (await _repository.GetStationProfilesAsync()).Count);
        Assert.Empty((await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM qso_geo_cache")).Rows);
    }

    [Fact]
    public async Task ChangedRecordRetainsIdAndRebuildsGeoWithoutInheritingAnUnrelatedDclMatch()
    {
        await Seed(); await AssignProfile();
        await Execute("UPDATE qso SET dcl_status='c',dcl_dxcc=230,dcl_adif2=230;");
        var first = (await _repository.GetMapQsoPointsAsync(QsoScope.ForSources([Source]))).First();
        var edited = Record(locator: "JO50AA"); edited[151] = 0x20;
        await Write(Source, edited, Record("LATEST", 50, "FN31PR12", "EA"));
        var changed = await FastService.SyncFileAsync(Source);
        Assert.Equal(1, changed.Result.Updated); Assert.Equal(1, changed.Result.Unchanged);
        Assert.Equal(2L, (await _repository.ExecuteReadOnlyQueryAsync("SELECT COUNT(*) FROM qso WHERE dcl_status='c'")).Rows[0][0]);
        var after = (await _repository.GetMapQsoPointsAsync(QsoScope.ForSources([Source]))).First();
        Assert.Equal(first.Id, after.Id); Assert.NotEqual(first.DistanceKm, after.DistanceKm);
        await Write(Source, Record("REPLACED", 41, "IN80", "EA"));
        var replacement = await FastService.SyncFileAsync(Source);
        Assert.Equal(1, replacement.Result.Updated); Assert.Equal(1, replacement.Result.Deleted);
        Assert.Equal(first.Id, (await Statistics()).LatestQso!.QsoId);
        Assert.Equal("REPLACED", (await Statistics()).LatestQso!.CallSign);
        Assert.Equal(0L, (await _repository.ExecuteReadOnlyQueryAsync("SELECT COUNT(*) FROM qso WHERE dcl_status IS NOT NULL OR dcl_dxcc IS NOT NULL OR dcl_adif2 IS NOT NULL")).Rows[0][0]);
        Assert.Single((await _repository.ExecuteReadOnlyQueryAsync("SELECT * FROM qso_geo_cache")).Rows);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("sharing")]
    [InlineData("length")]
    [InlineData("date")]
    [InlineData("string")]
    [InlineData("overflow")]
    public async Task FailedFileReadParseOrDatabaseWritePreservesCommittedSnapshot(string failure)
    {
        await Seed(); await AssignProfile(); var before = await State();
        FileStream? locked = null;
        try
        {
            if (failure == "missing") File.Delete(Source);
            if (failure == "directory") { File.Delete(Source); Directory.CreateDirectory(Source); }
            if (failure == "sharing") locked = new(Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (failure == "length") await File.WriteAllBytesAsync(Source, new byte[UcxConstants.RecordSize - 1]);
            if (failure is "date" or "string" or "overflow")
            {
                var invalid = Record("INVALID");
                if (failure == "date") invalid[2] = 13;
                if (failure == "string") invalid[9] = 255;
                if (failure == "overflow") BinaryPrimitives.WriteUInt64LittleEndian(invalid.AsSpan(175, 8), ulong.MaxValue);
                await Write(Source, Record("CHANGED"), invalid);
            }
            await Assert.ThrowsAnyAsync<Exception>(() => FastService.SyncFileAsync(Source));
            Assert.Equal(before, await State());
        }
        finally { locked?.Dispose(); }
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("geo")]
    public async Task DatabaseFailureRollsBackUpsertsDeletionsCachesAndSourceMetadata(string failure)
    {
        await Seed(); await AssignProfile(); var before = await State();
        await Execute(failure == "delete"
            ? "CREATE TRIGGER reject_delete BEFORE DELETE ON qso BEGIN SELECT RAISE(ABORT,'injected delete failure'); END;"
            : "CREATE TRIGGER reject_geo BEFORE INSERT ON qso_geo_cache BEGIN SELECT RAISE(ABORT,'injected geo failure'); END;");
        await Write(Source, Record("CHANGED", locator: "JO50"));
        await Assert.ThrowsAsync<SqliteException>(() => FastService.SyncFileAsync(Source));
        Assert.Equal(before, await State());
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("cancelled")]
    [InlineData("partial")]
    public async Task FinalValidationFailureRollsBackAndSubsequentImportRecovers(string failure)
    {
        await Seed(); await AssignProfile(); var before = await State();
        await Write(Source, Record("CANDIDATE"));
        using var cancellation = new CancellationTokenSource();
        var proxy = RepositoryProxy.Wrap(_repository);
        proxy.BeforeValidation = async () =>
        {
            if (failure == "changed") await Write(Source, Record("NEWER", 59));
            if (failure == "missing") File.Delete(Source);
            if (failure == "partial") await File.WriteAllBytesAsync(Source, new byte[107]);
            if (failure == "cancelled") cancellation.Cancel();
        };
        var service = new UcxImportService((IQsoRepository)proxy, _ => Task.CompletedTask);
        await Assert.ThrowsAnyAsync<Exception>(() => service.SyncFileAsync(Source, cancellation.Token));
        Assert.Equal(before, await State());
        await Write(Source, Record("RECOVERED", 59)); await FastService.SyncFileAsync(Source);
        Assert.Equal(1, (await Statistics()).Summarize().TotalQsos); Assert.Equal("RECOVERED", (await Statistics()).LatestQso!.CallSign);
    }

    [Fact]
    public async Task NewEmptySourceIsRegisteredAndRepeatEmptyReconciliationIsIdempotent()
    {
        await Write(Source);
        var first = await FastService.SyncFileAsync(Source);
        var second = await FastService.SyncFileAsync(Source);
        Assert.Equal(new QsoImportResult(0, 0, 0), first.Result);
        Assert.Equal(first.Result, second.Result);
        var source = Assert.Single(await _repository.GetSourcesAsync());
        Assert.Equal(Source, source.FullPath); Assert.Equal(0, source.QsoCount);
        Assert.Equal(0, await _repository.GetTotalQsoCountAsync());
    }

    [Fact]
    public async Task DeletedDatabaseIdsAreNeverReusedBySnapshotOrIncrementalImports()
    {
        await Seed(); var old = (await Statistics()).LatestQso!.QsoId;
        await Write(Source); await FastService.SyncFileAsync(Source);
        await Write(Source, Record("NEW", 59)); await FastService.SyncFileAsync(Source);
        var snapshotId = (await Statistics()).LatestQso!.QsoId;
        Assert.True(snapshotId > old);
        Assert.Equal(0, (await _repository.GetMapStatisticsAsync(QsoScope.ForQsoIds([old]))).Summarize().TotalQsos);
        await Write(Source); await FastService.SyncFileAsync(Source);
        await Write(Source, Record("INCREMENTAL", 58));
        var row = Assert.Single(new UcxFileReader().ReadWithMetadata(Source));
        await _repository.UpsertManyAsync([new QsoImportRecord
        {
            SourceFile = Source, SourceRecordIndex = row.Qso.RecordIndex,
            SourceRecordHash = row.RecordHash, Qso = row.Qso
        }]);
        Assert.True((await Statistics()).LatestQso!.QsoId > snapshotId);
        Assert.Equal(0, (await _repository.GetMapStatisticsAsync(QsoScope.ForQsoIds([old, snapshotId]))).Summarize().TotalQsos);
    }

    [Fact]
    public async Task TransientEmptySnapshotIsNotCommitted()
    {
        await Seed(); var before = await State();
        await Write(Source);
        var service = new UcxImportService(_repository, _ => Write(Source, Record("WRITER", 59)));
        await Assert.ThrowsAsync<IOException>(() => service.SyncFileAsync(Source));
        Assert.Equal(before, await State());
        await FastService.SyncFileAsync(Source);
        Assert.Equal("WRITER", (await Statistics()).LatestQso!.CallSign);
    }

    [WindowsFact]
    public async Task ActiveSharedWindowsWriterPreventsDestructiveCommitAndRetryRecovers()
    {
        await Seed(); var before = await State();
        await Write(Source, Record());
        FileStream? writer = null;
        try
        {
            var proxy = RepositoryProxy.Wrap(_repository);
            proxy.BeforeValidation = () =>
            {
                writer = new(Source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
                return Task.CompletedTask;
            };
            await Assert.ThrowsAsync<IOException>(() => new UcxImportService((IQsoRepository)proxy, _ => Task.CompletedTask).SyncFileAsync(Source));
            Assert.Equal(before, await State());
        }
        finally { writer?.Dispose(); }
        Assert.Equal(1, (await FastService.SyncFileAsync(Source)).Result.Deleted);
    }

    public sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Windows FileShare write/delete exclusion requires native Windows.";
        }
    }

    [Fact]
    public async Task OverlappingServiceInstancesSerializeBeforeReadAndRejectOlderUnstableSnapshot()
    {
        await Seed();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new UcxImportService(_repository, async token => { entered.SetResult(); await resume.Task.WaitAsync(token); });
        var older = slow.SyncFileAsync(Source);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Write(Source, Record("NEWEST", 59));
        var newer = new UcxImportService(new SqliteQsoRepository(_repository.DatabasePath), _ => Task.CompletedTask).SyncFileAsync(Source);
        Assert.False(newer.IsCompleted);
        resume.SetResult();
        await Assert.ThrowsAsync<IOException>(() => older);
        await newer;
        Assert.Equal(1, (await Statistics()).Summarize().TotalQsos); Assert.Equal("NEWEST", (await Statistics()).LatestQso!.CallSign);
    }

    [Fact]
    public async Task StaleSnapshotCannotOverwriteAlreadyCommittedNewerDatabaseState()
    {
        await Seed();
        var proxy = RepositoryProxy.Wrap(_repository);
        proxy.BeforeReconciliation = async () =>
        {
            await Write(Source, Record("NEWER", 59));
            var row = Assert.Single(new UcxFileReader().ReadWithMetadata(Source));
            var newer = new QsoImportRecord { SourceFile = Source, SourceRecordIndex = 0, SourceRecordHash = row.RecordHash, Qso = row.Qso };
            await new SqliteQsoRepository(_repository.DatabasePath).ReconcileSourceAsync(Source, [newer]);
        };
        await Assert.ThrowsAsync<IOException>(() => new UcxImportService((IQsoRepository)proxy, _ => Task.CompletedTask).SyncFileAsync(Source));
        Assert.Equal(1, (await Statistics()).Summarize().TotalQsos); Assert.Equal("NEWER", (await Statistics()).LatestQso!.CallSign);
    }

    [Fact]
    public async Task CancellationBeforeReadAndWhileWaitingDoesNotDeleteOrLeaveSourceGateLocked()
    {
        await Seed(); var before = await State();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FastService.SyncFileAsync(Source, cancelled.Token));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var active = new CancellationTokenSource();
        var waiting = new UcxImportService(_repository, async token => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); }).SyncFileAsync(Source, active.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FastService.SyncFileAsync(Source, cancelled.Token));
        active.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(before, await State());
        Assert.Equal(2, (await FastService.SyncFileAsync(Source)).Result.Unchanged);
    }

    [Fact]
    public async Task InvalidSourceOrIncompleteIdentitySetIsRejectedBeforeAnyDatabaseMutation()
    {
        await Seed(); var before = await State();
        var row = Assert.Single(new UcxFileReader().ReadWithMetadata(Source).Take(1));
        var record = new QsoImportRecord { SourceFile = Source, SourceRecordIndex = 0, SourceRecordHash = row.RecordHash, Qso = row.Qso };
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.ReconcileSourceAsync("relative.ucx", []));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.ReconcileSourceAsync(Source, [record with { SourceFile = Source + ".other" }]));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.ReconcileSourceAsync(Source, [record with { SourceRecordIndex = 1 }]));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.ReconcileSourceAsync(Source, [record, record]));
        Assert.Equal(before, await State());
    }

    public class RepositoryProxy : DispatchProxy
    {
        private IQsoRepository _target = null!;
        public Func<Task>? BeforeReconciliation { get; set; }
        public Func<Task>? BeforeValidation { get; set; }
        public static RepositoryProxy Wrap(IQsoRepository target)
        {
            var result = (RepositoryProxy)Create<IQsoRepository, RepositoryProxy>(); result._target = target; return result;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IQsoRepository.ReconcileSourceAsync)) return Reconcile(args!);
            try { return method.Invoke(_target, args); }
            catch (TargetInvocationException ex) { ExceptionDispatchInfo.Capture(ex.InnerException!).Throw(); throw; }
        }
        private async Task<QsoImportResult> Reconcile(object?[] args)
        {
            if (BeforeReconciliation is not null) await BeforeReconciliation();
            var validate = (Func<QsoImportResult, CancellationToken, Task>?)args[3];
            return await _target.ReconcileSourceAsync((string)args[0]!, (IReadOnlyList<QsoImportRecord>)args[1]!, (CancellationToken)args[2]!, async (changes, token) =>
            {
                if (BeforeValidation is not null) await BeforeValidation();
                if (validate is not null) await validate(changes, token);
            });
        }
    }

    private static async Task RunPage(string html, List<object> states)
    {
        var info = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "map-source-reconciliation.cjs"));
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { html, states })); process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
