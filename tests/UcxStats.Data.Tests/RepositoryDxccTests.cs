using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using Microsoft.Data.Sqlite;

namespace UcxStats.Data.Tests;

public sealed class RepositoryDxccTests
{
    [Fact]
    public async Task SchemaMigrationAndInitializationAreIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();

        await database.Repository.InitializeAsync();
        await database.Repository.InitializeAsync();

        var columns = await database.Repository.ExecuteReadOnlyQueryAsync("SELECT * FROM pragma_table_info('qso')");
        Assert.Contains(columns.Rows, row => Equals(row[1], "dxcc_entity_id"));
        var indexes = await database.Repository.ExecuteReadOnlyQueryAsync("SELECT * FROM pragma_index_list('qso')");
        Assert.Contains(indexes.Rows, row => Equals(row[1], "idx_qso_dxcc_band"));
    }

    [Fact]
    public async Task ExistingDatabaseGetsNullableColumnIndexAndVersionedBackfillWithoutDataLoss()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ucxstats-dxcc-old-schema-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE qso (
                        id INTEGER PRIMARY KEY,
                        qso_date TEXT NOT NULL,
                        start_time TEXT NOT NULL,
                        callsign TEXT NOT NULL,
                        band_code INTEGER NOT NULL,
                        mode_code INTEGER NOT NULL,
                        country TEXT NOT NULL,
                        locator TEXT NOT NULL,
                        status151_raw INTEGER NOT NULL,
                        status161_raw INTEGER NOT NULL,
                        frequency_hz INTEGER NOT NULL
                    );
                    INSERT INTO qso VALUES(7, '2026-01-01', '12:00:00', 'EA1AAA', 4, 0, 'EA', '', 0, 0, 14000000);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var repository = new SqliteQsoRepository(path);
            await repository.InitializeAsync();
            await repository.InitializeAsync();

            var row = await repository.ExecuteReadOnlyQueryAsync(
                "SELECT id, dxcc_entity_id, rx_frequency_hz FROM qso");
            Assert.Equal(7L, row.Rows[0][0]);
            Assert.Equal(281L, row.Rows[0][1]);
            Assert.Equal(14_000_000L, row.Rows[0][2]);
            var indexes = await repository.ExecuteReadOnlyQueryAsync("SELECT * FROM pragma_index_list('qso')");
            Assert.Contains(indexes.Rows, x => Equals(x[1], "idx_qso_dxcc_band"));
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task MappingVersionChangeFromTwoToThreeResolvesExistingR1fjRow()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import("franz-josef-land.ucx", 1, "R1FJ", 4, callSign: "RI1FJL"));

        await using (var connection = new SqliteConnection($"Data Source={database.Path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE qso SET dxcc_entity_id=NULL;
                UPDATE ucxstats_metadata
                SET value='2'
                WHERE key='dxcc_country_mapping_version';
                """;
            await command.ExecuteNonQueryAsync();
        }

        await database.Repository.InitializeAsync();

        var row = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dxcc_entity_id FROM qso");
        Assert.Equal(61L, row.Rows[0][0]);

        var metadata = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT value FROM ucxstats_metadata WHERE key='dxcc_country_mapping_version'");
        Assert.Equal("3", metadata.Rows[0][0]);
        var policy = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT value FROM ucxstats_metadata WHERE key='dxcc_identity_resolution_policy_version'");
        Assert.Equal("2", policy.Rows[0][0]);

        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.Equal("R1FJ", wae.Country);
    }

    [Fact]
    public async Task NewlyEncounteredR1fjQsoResolvesAndLotwMakesItDxccCredited()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import(
                "franz-josef-land.ucx",
                1,
                "R1FJ",
                4,
                status151: 0x80,
                callSign: "RI1FJL"));

        var stored = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dxcc_entity_id, locator FROM qso");
        Assert.Equal(61L, stored.Rows[0][0]);
        Assert.Equal(string.Empty, stored.Rows[0][1]);

        var aggregate = Assert.Single(
            (await database.Repository.GetDxccStatusAsync(QsoScope.All)).Bands);
        Assert.Equal(61, aggregate.EntityId);
        Assert.Equal(4, aggregate.BandCode);
        Assert.Equal(1, aggregate.QsoCount);
        Assert.Equal(1, aggregate.DxccCreditedQsoCount);
        Assert.Equal(
            DxccOverlayStatus.DxccCredited,
            DxccOverlayStatusEvaluator.Evaluate(
                DxccCatalogEntityStatus.Current,
                [aggregate],
                [4]));
    }

    [Fact]
    public async Task ExistingIdentitiesAreRecalculatedByIdempotentPolicyMigration()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import("marine.ucx", 1, "9A", 4, callSign: "9A/S53ZO/MM"),
            Import("spain.ucx", 2, "EA", 4));

        await using (var connection = new SqliteConnection($"Data Source={database.Path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE qso
                SET dcl_status='c',
                    dcl_dxcc=CASE country WHEN '9A' THEN 999 ELSE 29 END,
                    dcl_adif2=999,
                    dxcc_entity_id=CASE country WHEN '9A' THEN 999 ELSE 29 END;
                DELETE FROM ucxstats_metadata
                WHERE key='dxcc_identity_resolution_policy_version';
                """;
            await command.ExecuteNonQueryAsync();
        }

        await database.Repository.InitializeAsync();
        await database.Repository.InitializeAsync();

        var rows = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT country, dcl_dxcc, dxcc_entity_id FROM qso ORDER BY country");
        var marine = rows.Rows.Single(x => Equals(x[0], "9A"));
        Assert.Equal(999L, marine[1]);
        Assert.Null(marine[2]);
        var spain = rows.Rows.Single(x => Equals(x[0], "EA"));
        Assert.Equal(29L, spain[1]);
        Assert.Equal(281L, spain[2]);

        var metadata = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT value FROM ucxstats_metadata WHERE key='dxcc_country_mapping_version'");
        Assert.Equal("3", metadata.Rows[0][0]);
    }

    [Fact]
    public async Task AggregationHonorsEveryScopeFormAndDoesNotRequireLocator()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import("a.ucx", 1, "EA", 4, new DateOnly(2026, 1, 1)),
            Import("b.ucx", 2, "EA", 6, new DateOnly(2026, 1, 2), status151: 0x40),
            Import("a.ucx", 3, "EA", 8, new DateOnly(2026, 1, 3), status151: 0x80),
            Import("a.ucx", 4, "XYZ", 4, new DateOnly(2026, 1, 4)));

        var all = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(3, all.Bands.Count);
        Assert.Equal(1, all.UnresolvedQsoCount);
        Assert.Contains(all.Bands, x => x.BandCode == 6 && x.EqslOrClubLogConfirmedCount == 1);
        Assert.Contains(all.Bands, x => x.BandCode == 8 && x.DxccCreditedQsoCount == 1);

        var source = await database.Repository.GetDxccStatusAsync(QsoScope.ForSources(["a.ucx"]));
        Assert.Equal(2, source.Bands.Count);
        Assert.Equal(1, source.UnresolvedQsoCount);

        var sourceRecord = await database.Repository.GetDxccStatusAsync(
            QsoScope.ForSourceRecords([new QsoSourceRecordKey("b.ucx", 2)]));
        Assert.Single(sourceRecord.Bands);
        Assert.Equal(6, sourceRecord.Bands[0].BandCode);

        var rows = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT id, source_record_index FROM qso ORDER BY source_record_index");
        var id = Convert.ToInt64(rows.Rows.Single(x => Convert.ToInt64(x[1]) == 3)[0]);
        var explicitId = await database.Repository.GetDxccStatusAsync(QsoScope.ForQsoIds([id]));
        Assert.Single(explicitId.Bands);
        Assert.Equal(8, explicitId.Bands[0].BandCode);

        var live = await database.Repository.GetDxccStatusAsync(
            QsoScope.ForSources(["a.ucx"]).WithFromUtc(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Single(live.Bands);
        Assert.Equal(1, live.UnresolvedQsoCount);

        Assert.All(database.Imports, x => Assert.Null(x.Latitude));
        Assert.All(database.Imports, x => Assert.Equal(string.Empty, x.Qso.Locator));
    }

    [Fact]
    public async Task EmptySelectionsMatchNoQsos()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "EA", 4));

        var sources = await database.Repository.GetDxccStatusAsync(QsoScope.ForSources([]));
        var ids = await database.Repository.GetDxccStatusAsync(QsoScope.ForQsoIds([]));
        var records = await database.Repository.GetDxccStatusAsync(QsoScope.ForSourceRecords([]));

        Assert.Empty(sources.Bands);
        Assert.Empty(ids.Bands);
        Assert.Empty(records.Bands);
    }

    [Fact]
    public async Task MapPointsAndDxccAggregationUseTheSameTypedScope()
    {
        var mappedA = Import("a.ucx", 1, "EA", 4) with
        {
            Latitude = 40,
            Longitude = -3
        };
        var mappedB = Import("b.ucx", 2, "DL", 6) with
        {
            Latitude = 51,
            Longitude = 10
        };
        var withoutLocator = Import("a.ucx", 3, "EA", 8);
        await using var database = await TestDatabase.CreateAsync(mappedA, mappedB, withoutLocator);
        var profileId = await database.Repository.SaveStationProfileAsync(
            new StationProfile(0, "Home", "JN58", 48, 11, null, string.Empty, null));
        await database.Repository.AssignStationProfileToSourcesAsync(["a.ucx", "b.ucx"], profileId);

        var sourceScope = QsoScope.ForSources(["a.ucx"]);
        var sourcePoints = await database.Repository.GetMapQsoPointsAsync(sourceScope);
        var sourceStatus = await database.Repository.GetDxccStatusAsync(sourceScope);
        Assert.Single(sourcePoints);
        Assert.Equal(2, sourceStatus.Bands.Count);
        Assert.Contains(sourceStatus.Bands, x => x.BandCode == 8);

        var explicitScope = QsoScope.ForSourceRecords([new QsoSourceRecordKey("b.ucx", 2)]);
        Assert.Single(await database.Repository.GetMapQsoPointsAsync(explicitScope));
        Assert.Single((await database.Repository.GetDxccStatusAsync(explicitScope)).Bands);

        var emptyScope = QsoScope.ForQsoIds([]);
        Assert.Empty(await database.Repository.GetMapQsoPointsAsync(emptyScope));
        Assert.Empty((await database.Repository.GetDxccStatusAsync(emptyScope)).Bands);
    }

    [Fact]
    public async Task DclOnlyConfirmationDoesNotPromoteOverlayStatus()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "EA", 4));
        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "281", "999"));

        var snapshot = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        var aggregate = Assert.Single(snapshot.Bands);
        Assert.Equal(1, aggregate.QsoCount);
        Assert.Equal(0, aggregate.EqslOrClubLogConfirmedCount);
        Assert.Equal(0, aggregate.DxccCreditedQsoCount);
        Assert.Equal(DxccOverlayStatus.Worked,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [aggregate]));

        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.True(wae.DclConfirmed);
        Assert.True(wae.Confirmed);
    }

    [Fact]
    public async Task Dcl999Suppresses9aDxccAggregationAndSnapshotRemovalRestoresIt()
    {
        const string callSign = "9A/S53ZO/MM";
        await using var database = await TestDatabase.CreateAsync(
            Import("marine.ucx", 1, "9A", 4, callSign: callSign));

        var beforeDcl = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(497, Assert.Single(beforeDcl.Bands).EntityId);
        Assert.Equal(0, beforeDcl.ExplicitNonDxccQsoCount);

        await database.Repository.ImportDclAdifAsync(Adif(callSign, "999", "999"));

        var stored = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT country, dcl_dxcc, dcl_adif2, dxcc_entity_id FROM qso");
        Assert.Equal("9A", stored.Rows[0][0]);
        Assert.Equal(999L, stored.Rows[0][1]);
        Assert.Equal(999L, stored.Rows[0][2]);
        Assert.Null(stored.Rows[0][3]);

        var excluded = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Empty(excluded.Bands);
        Assert.Equal(0, excluded.UnresolvedQsoCount);
        Assert.Equal(1, excluded.ExplicitNonDxccQsoCount);

        var audit = await database.Repository.GetDxccIdentityAuditAsync();
        Assert.Equal(0, audit.ResolvedQsos);
        Assert.Equal(0, audit.UnresolvedQsos);
        Assert.Equal(1, audit.ExplicitNonDxccQsos);
        Assert.Empty(audit.Conflicts);
        Assert.Equal(1, audit.EvidenceCounts[DxccIdentityEvidence.ExplicitNonDxcc]);
        var evidence = Assert.Single(audit.ExplicitNonDxccEvidence);
        Assert.Equal("9A", evidence.CountryCode);
        Assert.Equal(497, evidence.UcxEntityId);
        Assert.Equal(999, evidence.DclCode);

        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.True(wae.DclConfirmed);
        Assert.True(wae.Confirmed);
        Assert.Equal(999, wae.DclDxcc);
        Assert.Equal(999, wae.DclAdif2);

        await database.Repository.ImportDclAdifAsync(string.Empty);

        var restored = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(497, Assert.Single(restored.Bands).EntityId);
        Assert.Equal(0, restored.ExplicitNonDxccQsoCount);
        var restoredRow = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dcl_dxcc, dxcc_entity_id FROM qso");
        Assert.Null(restoredRow.Rows[0][0]);
        Assert.Equal(497L, restoredRow.Rows[0][1]);
    }

    [Fact]
    public async Task AdifDxccZeroSuppressesMappedUcxIdentity()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "EA", 4));

        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "0", "999"));

        var stored = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dcl_dxcc, dxcc_entity_id FROM qso");
        Assert.Equal(0L, stored.Rows[0][0]);
        Assert.Null(stored.Rows[0][1]);
        var snapshot = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Empty(snapshot.Bands);
        Assert.Equal(0, snapshot.UnresolvedQsoCount);
        Assert.Equal(1, snapshot.ExplicitNonDxccQsoCount);
        var audit = await database.Repository.GetDxccIdentityAuditAsync();
        Assert.Equal(1, audit.ExplicitNonDxccQsos);
        Assert.Empty(audit.Conflicts);
        Assert.Equal(0, Assert.Single(audit.ExplicitNonDxccEvidence).DclCode);
        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.True(wae.DclConfirmed);
        Assert.True(wae.Confirmed);
    }

    [Fact]
    public async Task AggregationUsesPaperLotwEqslAndClubLogSourceSemantics()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import("paper.ucx", 1, "EA", 4, status151: 0x20),
            Import("lotw.ucx", 2, "EA", 4, status151: 0x80),
            Import("paper-lotw.ucx", 3, "EA", 4, status151: 0xA0),
            Import("eqsl.ucx", 4, "EA", 4, status151: 0x40),
            Import("clublog.ucx", 5, "EA", 4, status161: 0x20),
            Import("eqsl-clublog.ucx", 6, "EA", 4, status151: 0x40, status161: 0x20));

        var aggregate = Assert.Single((await database.Repository.GetDxccStatusAsync(QsoScope.All)).Bands);
        Assert.Equal(6, aggregate.QsoCount);
        Assert.Equal(3, aggregate.EqslOrClubLogConfirmedCount);
        Assert.Equal(3, aggregate.DxccCreditedQsoCount);
        Assert.Equal(DxccOverlayStatus.DxccCredited,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [aggregate]));
    }

    [Fact]
    public async Task UcxWinsDclConflictWhichRemainsAuditedAndSnapshotRemovalRecalculates()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "EA", 4));
        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "29", "999"));

        var audit = await database.Repository.GetDxccIdentityAuditAsync();
        var conflict = Assert.Single(audit.Conflicts);
        Assert.Equal(281, conflict.UcxEntityId);
        Assert.Equal(29, conflict.DclEntityId);
        Assert.Equal(281, conflict.EffectiveEntityId);
        Assert.Equal(1, audit.EvidenceCounts[DxccIdentityEvidence.Conflict]);

        var snapshot = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(281, Assert.Single(snapshot.Bands).EntityId);

        var stored = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dcl_dxcc, dxcc_entity_id FROM qso");
        Assert.Equal(29L, stored.Rows[0][0]);
        Assert.Equal(281L, stored.Rows[0][1]);

        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.Equal(999, wae.DclAdif2);
        Assert.Equal("EA", wae.Country);

        await database.Repository.ImportDclAdifAsync(string.Empty);

        var restoredAudit = await database.Repository.GetDxccIdentityAuditAsync();
        Assert.Empty(restoredAudit.Conflicts);
        Assert.Equal(1, restoredAudit.EvidenceCounts[DxccIdentityEvidence.UcxOnly]);
        var restored = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(281, Assert.Single(restored.Bands).EntityId);
    }

    [Fact]
    public async Task DclFallbackAggregatesUnmappedUcxAndRemovalRestoresUnresolvedState()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "XYZ", 4));
        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "123", "999"));

        var snapshot = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Equal(123, Assert.Single(snapshot.Bands).EntityId);
        Assert.Equal(0, snapshot.UnresolvedQsoCount);
        var audit = await database.Repository.GetDxccIdentityAuditAsync();
        Assert.Equal(1, audit.EvidenceCounts[DxccIdentityEvidence.DclFallback]);
        Assert.Empty(audit.Conflicts);

        await database.Repository.ImportDclAdifAsync(string.Empty);

        var restored = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Empty(restored.Bands);
        Assert.Equal(1, restored.UnresolvedQsoCount);

        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "600", "999"));
        var rejected = await database.Repository.ExecuteReadOnlyQueryAsync(
            "SELECT dcl_dxcc, dxcc_entity_id FROM qso");
        Assert.Null(rejected.Rows[0][0]);
        Assert.Null(rejected.Rows[0][1]);
    }

    [Fact]
    public async Task DclAdif2NeverSuppliesDxccIdentity()
    {
        await using var database = await TestDatabase.CreateAsync(Import("a.ucx", 1, "XYZ", 4));
        await database.Repository.ImportDclAdifAsync(Adif("EA1AAA", "", "281"));

        var snapshot = await database.Repository.GetDxccStatusAsync(QsoScope.All);
        Assert.Empty(snapshot.Bands);
        Assert.Equal(1, snapshot.UnresolvedQsoCount);

        var wae = Assert.Single(await database.Repository.GetWaeQsoCandidatesAsync());
        Assert.Equal(281, wae.DclAdif2);
        Assert.Null(wae.DclDxcc);
    }

    [Fact]
    public async Task AuditReportsCountryCountsAndUnresolvedRowsWithoutFailing()
    {
        await using var database = await TestDatabase.CreateAsync(
            Import("a.ucx", 1, "EA", 4),
            Import("a.ucx", 2, "EA", 6),
            Import("a.ucx", 3, "XYZ", 4));

        var audit = await database.Repository.GetDxccIdentityAuditAsync();

        Assert.Equal(3, audit.TotalQsos);
        Assert.Equal(2, audit.ResolvedQsos);
        Assert.Equal(1, audit.UnresolvedQsos);
        Assert.Equal(new[] { "XYZ" }, audit.DistinctUnresolvedCountryCodes);
        Assert.Contains(audit.Countries, x => x is
        {
            CountryCode: "EA", MappedEntityId: 281, QsoCount: 2,
            Status: DxccCountryMappingStatus.Mapped
        });
        Assert.Contains(audit.Countries, x => x is
        {
            CountryCode: "XYZ", MappedEntityId: null, QsoCount: 1,
            Status: DxccCountryMappingStatus.Unresolved
        });
    }

    private static QsoImportRecord Import(
        string source,
        long recordIndex,
        string country,
        byte band,
        DateOnly? date = null,
        byte status151 = 0,
        byte status161 = 0,
        string callSign = "EA1AAA") => new()
    {
        SourceFile = source,
        SourceRecordIndex = recordIndex,
        SourceRecordHash = $"hash-{source}-{recordIndex}",
        Qso = new UcxQsoRecord
        {
            RecordIndex = recordIndex,
            RecordTypeRaw = 1,
            Date = date ?? new DateOnly(2026, 1, 1),
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(12, 1),
            CallSign = callSign,
            SentRst = "599",
            SentExchange = "",
            ReceivedRst = "599",
            ReceivedExchange = "",
            BandCode = band,
            ModeCode = 0,
            Qth = "",
            Name = "",
            Remarks = "",
            Status151Raw = status151,
            Country = country,
            ContinentCode = 0,
            Status161Raw = status161,
            UnknownString162 = "",
            Unknown171 = 0,
            RxFrequencyHz = 14_000_000,
            Multiplier1 = "",
            Multiplier2 = "",
            Points = 0,
            Locator = "",
            IotaNumber = 0,
            IotaContinentCode = 0,
            District = "",
            CqZone = 0,
            ItuZone = 0,
            Manager = "",
            Unknown243 = 0,
            CbaRaw = 0,
            ManagerOkRaw = 0,
            QslSendDateRaw = 0,
            Unknown254 = 0,
            UnknownString255 = "",
            UnknownString261 = "",
            Award1 = "",
            Count1 = "",
            Award2 = "",
            Count2 = "",
            InternalMetadata320 = new byte[22],
            Reserved342 = new byte[20],
            UnknownTrailer = 0
        }
    };

    private static string Adif(string call, string dxcc, string adif2)
    {
        static string Field(string name, string value) => $"<{name}:{value.Length}>{value}";
        return Field("CALL", call) +
               Field("QSO_DATE", "20260101") +
               Field("TIME_ON", "1200") +
               Field("BAND", "20m") +
               Field("MODE", "CW") +
               Field("APP_DCL_STATUS", "c") +
               Field("DXCC", dxcc) +
               Field("APP_DCL_ADIF2", adif2) +
               "<EOR>";
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(string path, SqliteQsoRepository repository, IReadOnlyList<QsoImportRecord> imports)
        {
            Path = path;
            Repository = repository;
            Imports = imports;
        }

        public string Path { get; }
        public SqliteQsoRepository Repository { get; }
        public IReadOnlyList<QsoImportRecord> Imports { get; }

        public static async Task<TestDatabase> CreateAsync(params QsoImportRecord[] imports)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ucxstats-dxcc-test-{Guid.NewGuid():N}.db");
            var repository = new SqliteQsoRepository(path);
            await repository.InitializeAsync();
            if (imports.Length > 0)
                await repository.UpsertManyAsync(imports);
            return new TestDatabase(path, repository, imports);
        }

        public ValueTask DisposeAsync()
        {
            DeleteDatabaseFiles(Path);
            return ValueTask.CompletedTask;
        }
    }

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var candidate = path + suffix;
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
