using Microsoft.Data.Sqlite;

namespace UcxStats.Data.Database;

public sealed class SqliteQsoRepository : IQsoRepository
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public SqliteQsoRepository(string? databasePath = null)
    {
        DatabasePath = databasePath ?? GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS station_profile (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                locator TEXT NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                elevation_m REAL NULL,
                antenna TEXT NOT NULL DEFAULT '',
                antenna_height_m REAL NULL
            );

            CREATE TABLE IF NOT EXISTS source_file (
                full_path TEXT PRIMARY KEY,
                file_name TEXT NOT NULL,
                qso_count INTEGER NOT NULL,
                last_imported_utc TEXT NOT NULL,
                station_profile_id INTEGER NULL REFERENCES station_profile(id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS qso (
                id INTEGER PRIMARY KEY,
                source_file TEXT NOT NULL,
                source_record_index INTEGER NOT NULL,
                source_record_hash TEXT NOT NULL,
                record_type_raw INTEGER NOT NULL,
                qso_date TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT NOT NULL,
                callsign TEXT NOT NULL,
                sent_rst TEXT NOT NULL,
                sent_exchange TEXT NOT NULL,
                received_rst TEXT NOT NULL,
                received_exchange TEXT NOT NULL,
                band_code INTEGER NOT NULL,
                mode_code INTEGER NOT NULL,
                rx_frequency_hz INTEGER NOT NULL,
                qth TEXT NOT NULL,
                name TEXT NOT NULL,
                remarks TEXT NOT NULL,
                status151_raw INTEGER NOT NULL,
                country TEXT NOT NULL,
                continent_code INTEGER NOT NULL,
                status161_raw INTEGER NOT NULL,
                multiplier1 TEXT NOT NULL,
                multiplier2 TEXT NOT NULL,
                points INTEGER NOT NULL,
                locator TEXT NOT NULL,
                latitude REAL NULL,
                longitude REAL NULL,
                grid2 TEXT NULL,
                grid4 TEXT NULL,
                grid6 TEXT NULL,
                grid8 TEXT NULL,
                iota_number INTEGER NOT NULL,
                iota_continent_code INTEGER NOT NULL,
                district TEXT NOT NULL,
                cq_zone INTEGER NOT NULL,
                itu_zone INTEGER NOT NULL,
                manager TEXT NOT NULL,
                cba INTEGER NOT NULL,
                manager_ok INTEGER NOT NULL,
                qsl_send_datetime TEXT NULL,
                award1 TEXT NOT NULL,
                count1 TEXT NOT NULL,
                award2 TEXT NOT NULL,
                count2 TEXT NOT NULL,
                station_profile_id INTEGER NULL REFERENCES station_profile(id) ON DELETE SET NULL,
                UNIQUE(source_file, source_record_index)
            );

            CREATE INDEX IF NOT EXISTS idx_qso_date_time ON qso(qso_date, start_time);
            CREATE INDEX IF NOT EXISTS idx_qso_callsign ON qso(callsign);
            CREATE INDEX IF NOT EXISTS idx_qso_band ON qso(band_code);
            CREATE INDEX IF NOT EXISTS idx_qso_mode ON qso(mode_code);
            CREATE INDEX IF NOT EXISTS idx_qso_country ON qso(country);
            CREATE INDEX IF NOT EXISTS idx_qso_locator ON qso(locator);
            CREATE INDEX IF NOT EXISTS idx_qso_qsl_status ON qso(status151_raw, status161_raw);

            CREATE TABLE IF NOT EXISTS qso_geo_cache (
                qso_id INTEGER NOT NULL REFERENCES qso(id) ON DELETE CASCADE,
                station_profile_id INTEGER NOT NULL REFERENCES station_profile(id) ON DELETE CASCADE,
                distance_km REAL NOT NULL,
                bearing_deg REAL NOT NULL,
                PRIMARY KEY(qso_id, station_profile_id)
            );
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Schema migration for databases created before v0.5.2.
        await EnsureColumnExistsAsync(connection, "qso", "grid8", "TEXT NULL", cancellationToken);
        await EnsureColumnExistsAsync(connection, "qso", "station_profile_id", "INTEGER NULL REFERENCES station_profile(id) ON DELETE SET NULL", cancellationToken);
        await EnsureColumnExistsAsync(connection, "qso", "dcl_status", "TEXT NULL", cancellationToken); 
        await EnsureColumnExistsAsync(connection, "qso", "dcl_dxcc", "INTEGER NULL", cancellationToken);
        await EnsureColumnExistsAsync(connection, "qso", "dcl_adif2", "INTEGER NULL", cancellationToken);
        await EnsureColumnExistsAsync(connection, "source_file", "station_profile_id", "INTEGER NULL REFERENCES station_profile(id) ON DELETE SET NULL", cancellationToken);
        await RenameColumnIfNeededAsync(connection, "qso", "frequency_hz", "rx_frequency_hz", cancellationToken);
    }

    public async Task<QsoImportResult> UpsertManyAsync(IEnumerable<QsoImportRecord> records, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var inserted = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingHash = await GetExistingHashAsync(connection, transaction, item.SourceFile, item.SourceRecordIndex, cancellationToken);
            if (existingHash == item.SourceRecordHash)
            {
                unchanged++;
                continue;
            }

            var exists = existingHash is not null;
            await UpsertAsync(connection, transaction, item, cancellationToken);
            await DeleteGeoCacheForQsoAsync(connection, transaction, item.SourceFile, item.SourceRecordIndex, cancellationToken);
            if (exists) updated++; else inserted++;
        }

        foreach (var group in records.GroupBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase))
        {
            await UpsertSourceAsync(connection, transaction, group.Key, group.LongCount(), cancellationToken);
        }

        transaction.Commit();

        var importedSources = records.Select(x => x.SourceFile).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var source in importedSources)
        {
            await using var profileCommand = connection.CreateCommand();
            profileCommand.CommandText = "SELECT station_profile_id FROM source_file WHERE full_path=$path AND station_profile_id IS NOT NULL;";
            profileCommand.Parameters.AddWithValue("$path", source);
            var value = await profileCommand.ExecuteScalarAsync(cancellationToken);
            if (value is not null && value != DBNull.Value) await RebuildGeoCacheForProfileAsync(connection, Convert.ToInt64(value), null, cancellationToken);
        }
        return new QsoImportResult(inserted, updated, unchanged);
    }

    public async Task<DclImportResult> ImportDclAdifAsync(
        string adifText,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var records = DclAdifParser.Parse(adifText);
        var confirmed = records.Where(x => DclAdifParser.IsConfirmedStatus(x.Status)).ToArray();
        var diagnostics = new List<DclDiagnosticEntry>();
        var matched = 0;
        var unmatched = 0;
        var ambiguous = 0;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The imported DCL file is a snapshot of DCL-confirmed QSOs.
        // Clear prior DCL state so removed/reclassified entries do not remain stale.
        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)transaction;
            clear.CommandText = "UPDATE qso SET dcl_status=NULL, dcl_dxcc=NULL, dcl_adif2=NULL;";
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var record in confirmed)
        {
            var bandCode = AdifBandToUcx(record.Band);
            var modeCodes = AdifModeToUcx(record.Mode);
            if (bandCode is null || modeCodes.Count == 0)
            {
                unmatched++;
                diagnostics.Add(new DclDiagnosticEntry(
                    "Nicht gefunden",
                    $"Band/Mode nicht zuordenbar ({record.Band}/{record.Mode})",
                    record.QsoDate, record.TimeOn, record.CallSign, record.Band, record.Mode,
                    record.Status, record.Dxcc, record.Adif2, record.StationCallSign, record.MyGridSquare,
                    Array.Empty<DclCandidateQso>()));
                continue;
            }

            await using var find = connection.CreateCommand();
            find.Transaction = (SqliteTransaction)transaction;
            find.CommandText = $"""
                SELECT id, qso_date, start_time, callsign, band_code, mode_code, source_file
                FROM qso
                WHERE UPPER(callsign)=UPPER($call)
                  AND qso_date=$date
                  AND substr(start_time,1,5)=$time
                  AND band_code=$band
                  AND mode_code IN ({string.Join(",", modeCodes.Select((_, i) => "$mode" + i))})
                ORDER BY id;
                """;
            find.Parameters.AddWithValue("$call", record.CallSign);
            find.Parameters.AddWithValue("$date", record.QsoDate);
            find.Parameters.AddWithValue("$time", record.TimeOn);
            find.Parameters.AddWithValue("$band", bandCode.Value);
            for (var i = 0; i < modeCodes.Count; i++)
                find.Parameters.AddWithValue("$mode" + i, modeCodes[i]);

            var exactCandidates = new List<DclCandidateQso>();
            await using (var reader = await find.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken))
                    exactCandidates.Add(new DclCandidateQso(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt32(4),
                        reader.GetInt32(5),
                        reader.GetString(6)));

            IReadOnlyList<DclCandidateQso> matchCandidates = exactCandidates;
            var matchReason = "exakt";

            if (matchCandidates.Count == 0)
            {
                matchCandidates = await FindDclToleranceCandidatesAsync(
                    connection, (SqliteTransaction)transaction, record, bandCode.Value, modeCodes, cancellationToken);
                matchReason = "innerhalb ±10 Minuten";
            }

            if (matchCandidates.Count == 0)
            {
                unmatched++;
                var nearby = await FindDclNearbyCandidatesAsync(connection, (SqliteTransaction)transaction, record, cancellationToken);
                diagnostics.Add(new DclDiagnosticEntry(
                    "Nicht gefunden",
                    ClassifyDclMismatch(record, nearby),
                    record.QsoDate, record.TimeOn, record.CallSign, record.Band, record.Mode,
                    record.Status, record.Dxcc, record.Adif2, record.StationCallSign, record.MyGridSquare,
                    nearby));
                continue;
            }

            if (matchCandidates.Count > 1)
            {
                ambiguous++;
                diagnostics.Add(new DclDiagnosticEntry(
                    "Manuell prüfen",
                    $"{matchCandidates.Count} passende UCX-QSOs ({matchReason})",
                    record.QsoDate, record.TimeOn, record.CallSign, record.Band, record.Mode,
                    record.Status, record.Dxcc, record.Adif2, record.StationCallSign, record.MyGridSquare,
                    matchCandidates));
                continue;
            }

            var matchedId = matchCandidates[0].Id;

            await using var update = connection.CreateCommand();
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = """
                UPDATE qso
                SET dcl_status=$status, dcl_dxcc=$dxcc, dcl_adif2=$adif2
                WHERE id=$id;
                """;
            update.Parameters.AddWithValue("$status", record.Status);
            update.Parameters.AddWithValue("$dxcc", int.TryParse(record.Dxcc, out var dxcc) ? dxcc : DBNull.Value);
            update.Parameters.AddWithValue("$adif2", int.TryParse(record.Adif2, out var adif2) ? adif2 : DBNull.Value);
            update.Parameters.AddWithValue("$id", matchedId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            matched++;
        }

        await transaction.CommitAsync(cancellationToken);
        return new DclImportResult(
            records.Count,
            confirmed.Length,
            matched,
            unmatched,
            ambiguous,
            records.Count - confirmed.Length,
            diagnostics);
    }

    private static async Task<IReadOnlyList<DclCandidateQso>> FindDclToleranceCandidatesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DclAdifRecord record,
        int bandCode,
        IReadOnlyList<int> modeCodes,
        CancellationToken cancellationToken)
    {
        if (!DateTime.TryParseExact(
                $"{record.QsoDate} {record.TimeOn}",
                "yyyy-MM-dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dclTime))
            return Array.Empty<DclCandidateQso>();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT id, qso_date, start_time, callsign, band_code, mode_code, source_file
            FROM qso
            WHERE UPPER(callsign)=UPPER($call)
              AND band_code=$band
              AND mode_code IN ({string.Join(",", modeCodes.Select((_, i) => "$tolmode" + i))})
              AND datetime(qso_date || ' ' || substr(start_time,1,5))
                    BETWEEN datetime($from) AND datetime($to)
            ORDER BY ABS(
                julianday(qso_date || ' ' || substr(start_time,1,5)) - julianday($target)
            ), id;
            """;
        command.Parameters.AddWithValue("$call", record.CallSign);
        command.Parameters.AddWithValue("$band", bandCode);
        command.Parameters.AddWithValue("$target", dclTime.ToString("yyyy-MM-dd HH:mm"));
        command.Parameters.AddWithValue("$from", dclTime.AddMinutes(-10).ToString("yyyy-MM-dd HH:mm"));
        command.Parameters.AddWithValue("$to", dclTime.AddMinutes(10).ToString("yyyy-MM-dd HH:mm"));
        for (var i = 0; i < modeCodes.Count; i++)
            command.Parameters.AddWithValue("$tolmode" + i, modeCodes[i]);

        var result = new List<DclCandidateQso>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new DclCandidateQso(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetString(6)));
        return result;
    }

    private static async Task<IReadOnlyList<DclCandidateQso>> FindDclNearbyCandidatesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DclAdifRecord record,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, qso_date, start_time, callsign, band_code, mode_code, source_file
            FROM qso
            WHERE UPPER(callsign)=UPPER($call)
            ORDER BY
                CASE WHEN qso_date=$date THEN 0 ELSE 1 END,
                ABS(julianday(qso_date) - julianday($date)),
                start_time
            LIMIT 12;
            """;
        command.Parameters.AddWithValue("$call", record.CallSign);
        command.Parameters.AddWithValue("$date", record.QsoDate);

        var result = new List<DclCandidateQso>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new DclCandidateQso(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetString(6)));
        return result;
    }

    private static string ClassifyDclMismatch(
        DclAdifRecord record,
        IReadOnlyList<DclCandidateQso> nearby)
    {
        if (nearby.Count == 0)
            return "Call nicht in der UcxStats-Datenbank";

        var sameDate = nearby.Where(x => x.QsoDate == record.QsoDate).ToArray();
        if (sameDate.Length == 0)
            return "Call vorhanden, aber nicht an diesem Datum";

        var bandCode = AdifBandToUcx(record.Band);
        var sameBand = bandCode is null
            ? Array.Empty<DclCandidateQso>()
            : sameDate.Where(x => x.BandCode == bandCode.Value).ToArray();
        if (sameBand.Length == 0)
            return "Call und Datum vorhanden, Band weicht ab";

        var modeCodes = AdifModeToUcx(record.Mode);
        var sameMode = sameBand.Where(x => modeCodes.Contains(x.ModeCode)).ToArray();
        if (sameMode.Length == 0)
            return "Call, Datum und Band vorhanden, Mode weicht ab";

        return "Call, Datum, Band und Mode vorhanden, UTC-Zeit weicht ab";
    }

    private static int? AdifBandToUcx(string band) => band.Trim().ToLowerInvariant() switch
    {
        "160m" => 0, "80m" => 1, "40m" => 2, "30m" => 3, "20m" => 4,
        "17m" => 5, "15m" => 6, "12m" => 7, "10m" => 8, "6m" => 9,
        "2m" => 10, "70cm" => 11, "23cm" => 12, "13cm" => 13,
        "9cm" => 14, "6cm" => 15, "3cm" => 16,
        _ => null
    };

    private static IReadOnlyList<int> AdifModeToUcx(string mode) => mode.Trim().ToUpperInvariant() switch
    {
        "CW" => [0],
        "SSB" => [1],
        "RTTY" => [2],
        "AM" => [3],
        "FM" => [4],
        "PSK31" => [5],
        "PKT" or "PACKET" => [7],
        "SAT" => [12],
        "FT8" => [30],
        _ => []
    };

    public async Task<IReadOnlyList<WaeQsoCandidate>> GetWaeQsoCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, source_file, qso_date, start_time, callsign, band_code, mode_code,
                   country, locator, status151_raw, status161_raw, COALESCE(dcl_status, ''), dcl_dxcc, dcl_adif2
            FROM qso
            ORDER BY qso_date, start_time, id;
            """;

        var result = new List<WaeQsoCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WaeQsoCandidate(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetInt32(13)));
        }

        return result;
    }

    public async Task<QueryResult> ExecuteReadOnlyQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL-Abfrage ist leer.", nameof(sql));

        var trimmed = sql.TrimStart();
        if (!(trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nur lesende SELECT/WITH/EXPLAIN-Abfragen sind erlaubt.");

        var builder = new SqliteConnectionStringBuilder(_connectionString)
        {
            Mode = SqliteOpenMode.ReadOnly
        };

        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyList<object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= 100000)
                throw new InvalidOperationException("Abfrage abgebrochen: mehr als 100.000 Ergebniszeilen.");
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return new QueryResult(columns, rows);
    }

    public async Task<long> GetTotalQsoCountAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM qso;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<SourceSummary>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT q.source_file, COUNT(*), s.last_imported_utc, s.station_profile_id, p.name
            FROM qso q
            LEFT JOIN source_file s ON s.full_path = q.source_file
            LEFT JOIN station_profile p ON p.id = s.station_profile_id
            GROUP BY q.source_file, s.last_imported_utc, s.station_profile_id, p.name
            ORDER BY q.source_file COLLATE NOCASE;
            """;
        var result = new List<SourceSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fullPath = reader.GetString(0);
            var lastImported = reader.IsDBNull(2)
                ? DateTime.MinValue
                : DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind);
            result.Add(new SourceSummary(fullPath, Path.GetFileName(fullPath), reader.GetInt64(1), lastImported,
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return result;
    }

    public async Task<IReadOnlyList<MapQsoPoint>> GetMapQsoPointsByIdsAsync(
        IReadOnlyCollection<long> qsoIds,
        CancellationToken cancellationToken = default)
    {
        if (qsoIds.Count == 0) return Array.Empty<MapQsoPoint>();

        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var names = new List<string>();
        var i = 0;
        foreach (var id in qsoIds)
        {
            var name = "$id" + i++;
            names.Add(name);
            command.Parameters.AddWithValue(name, id);
        }

        command.CommandText = $"""
            SELECT q.id,q.source_file,q.callsign,q.qso_date,q.start_time,q.band_code,q.mode_code,
                   q.locator,q.latitude,q.longitude,p.name,p.latitude,p.longitude,g.distance_km,g.bearing_deg
            FROM qso q
            LEFT JOIN source_file sf ON sf.full_path=q.source_file
            LEFT JOIN station_profile p ON p.id=COALESCE(q.station_profile_id,sf.station_profile_id)
            LEFT JOIN qso_geo_cache g ON g.qso_id=q.id AND g.station_profile_id=p.id
            WHERE q.id IN ({string.Join(",", names)})
              AND q.latitude IS NOT NULL AND q.longitude IS NOT NULL AND p.id IS NOT NULL
            ORDER BY q.qso_date,q.start_time,q.id;
            """;

        var result = new List<MapQsoPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new MapQsoPoint(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7), reader.GetDouble(8), reader.GetDouble(9),
                reader.GetString(10), reader.GetDouble(11), reader.GetDouble(12),
                reader.IsDBNull(13) ? null : reader.GetDouble(13),
                reader.IsDBNull(14) ? null : reader.GetDouble(14)));
        return result;
    }

    public async Task<IReadOnlyList<MapQsoPoint>> GetMapQsoPointsAsync(
        IReadOnlyCollection<string>? sourceFiles = null,
        DateTime? fromUtc = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var filters = new List<string> { "q.latitude IS NOT NULL", "q.longitude IS NOT NULL", "p.id IS NOT NULL" };
        if (sourceFiles is { Count: > 0 })
        {
            var names = new List<string>();
            var i = 0;
            foreach (var source in sourceFiles)
            {
                var name = "$src" + i++;
                names.Add(name);
                command.Parameters.AddWithValue(name, source);
            }
            filters.Add($"q.source_file IN ({string.Join(",", names)})");
        }
        if (fromUtc is not null)
        {
            filters.Add("(q.qso_date > $fromDate OR (q.qso_date = $fromDate AND q.start_time >= $fromTime))");
            command.Parameters.AddWithValue("$fromDate", fromUtc.Value.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("$fromTime", fromUtc.Value.ToString("HH:mm:ss"));
        }

        command.CommandText = $"""
            SELECT q.id,q.source_file,q.callsign,q.qso_date,q.start_time,q.band_code,q.mode_code,
                   q.locator,q.latitude,q.longitude,p.name,p.latitude,p.longitude,g.distance_km,g.bearing_deg
            FROM qso q
            LEFT JOIN source_file sf ON sf.full_path=q.source_file
            LEFT JOIN station_profile p ON p.id=COALESCE(q.station_profile_id,sf.station_profile_id)
            LEFT JOIN qso_geo_cache g ON g.qso_id=q.id AND g.station_profile_id=p.id
            WHERE {string.Join(" AND ", filters)}
            ORDER BY q.qso_date,q.start_time,q.id;
            """;

        var result = new List<MapQsoPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new MapQsoPoint(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7), reader.GetDouble(8), reader.GetDouble(9),
                reader.GetString(10), reader.GetDouble(11), reader.GetDouble(12),
                reader.IsDBNull(13) ? null : reader.GetDouble(13),
                reader.IsDBNull(14) ? null : reader.GetDouble(14)));
        return result;
    }

    public async Task<IReadOnlyList<StationProfile>> GetStationProfilesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,locator,latitude,longitude,elevation_m,antenna,antenna_height_m FROM station_profile ORDER BY name COLLATE NOCASE;";
        var result = new List<StationProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new StationProfile(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDouble(3), reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetDouble(7)));
        return result;
    }

    public async Task<long> SaveStationProfileAsync(StationProfile profile, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = profile.Id == 0
            ? "INSERT INTO station_profile(name,locator,latitude,longitude,elevation_m,antenna,antenna_height_m) VALUES($n,$l,$lat,$lon,$e,$a,$ah); SELECT last_insert_rowid();"
            : "UPDATE station_profile SET name=$n,locator=$l,latitude=$lat,longitude=$lon,elevation_m=$e,antenna=$a,antenna_height_m=$ah WHERE id=$id; SELECT $id;";
        command.Parameters.AddWithValue("$n", profile.Name); command.Parameters.AddWithValue("$l", profile.Locator);
        command.Parameters.AddWithValue("$lat", profile.Latitude); command.Parameters.AddWithValue("$lon", profile.Longitude);
        command.Parameters.AddWithValue("$e", (object?)profile.ElevationM ?? DBNull.Value); command.Parameters.AddWithValue("$a", profile.Antenna ?? string.Empty);
        command.Parameters.AddWithValue("$ah", (object?)profile.AntennaHeightM ?? DBNull.Value); command.Parameters.AddWithValue("$id", profile.Id);
        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        await RebuildGeoCacheForProfileAsync(connection, id, null, cancellationToken);
        return id;
    }

    public async Task DeleteStationProfileAsync(long id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM station_profile WHERE id=$id;"; command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task AssignStationProfileToSourceAsync(string sourceFile, long? stationProfileId, CancellationToken cancellationToken = default)
        => AssignStationProfileToSourcesAsync([sourceFile], stationProfileId, null, cancellationToken);

    public async Task AssignStationProfileToSourcesAsync(
        IReadOnlyCollection<string> sourceFiles,
        long? stationProfileId,
        IProgress<GeoRebuildProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceFiles.Count == 0) return;

        await InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using (var transaction = connection.BeginTransaction())
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE source_file SET station_profile_id=$id WHERE full_path=$path;";
            var idParameter = command.Parameters.Add("$id", SqliteType.Integer);
            var pathParameter = command.Parameters.Add("$path", SqliteType.Text);
            idParameter.Value = (object?)stationProfileId ?? DBNull.Value;

            foreach (var sourceFile in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pathParameter.Value = sourceFile;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }

        if (stationProfileId.HasValue)
            await RebuildGeoCacheForProfileAsync(connection, stationProfileId.Value, progress, cancellationToken);
    }

    private static async Task RebuildGeoCacheForProfileAsync(
        SqliteConnection connection,
        long profileId,
        IProgress<GeoRebuildProgress>? progress,
        CancellationToken cancellationToken)
    {
        double lat, lon;
        await using (var profileCommand = connection.CreateCommand())
        {
            profileCommand.CommandText = "SELECT latitude,longitude FROM station_profile WHERE id=$id;";
            profileCommand.Parameters.AddWithValue("$id", profileId);
            await using var reader = await profileCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return;
            lat = reader.GetDouble(0);
            lon = reader.GetDouble(1);
        }

        var rows = new List<(long Id, double Lat, double Lon)>();
        await using (var query = connection.CreateCommand())
        {
            query.CommandText = "SELECT id,latitude,longitude FROM qso WHERE latitude IS NOT NULL AND longitude IS NOT NULL AND (station_profile_id=$id OR (station_profile_id IS NULL AND source_file IN (SELECT full_path FROM source_file WHERE station_profile_id=$id)));";
            query.Parameters.AddWithValue("$id", profileId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add((reader.GetInt64(0), reader.GetDouble(1), reader.GetDouble(2)));
        }

        progress?.Report(new GeoRebuildProgress(0, rows.Count));

        using var transaction = connection.BeginTransaction();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM qso_geo_cache WHERE station_profile_id=$id;";
            delete.Parameters.AddWithValue("$id", profileId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT OR REPLACE INTO qso_geo_cache(qso_id,station_profile_id,distance_km,bearing_deg) VALUES($q,$p,$d,$b);";
        var qsoParameter = insert.Parameters.Add("$q", SqliteType.Integer);
        var profileParameter = insert.Parameters.Add("$p", SqliteType.Integer);
        var distanceParameter = insert.Parameters.Add("$d", SqliteType.Real);
        var bearingParameter = insert.Parameters.Add("$b", SqliteType.Real);
        profileParameter.Value = profileId;

        const int progressInterval = 100;
        for (var i = 0; i < rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[i];
            var (distance, bearing) = CalculateGeo(lat, lon, row.Lat, row.Lon);
            qsoParameter.Value = row.Id;
            distanceParameter.Value = distance;
            bearingParameter.Value = bearing;
            await insert.ExecuteNonQueryAsync(cancellationToken);

            var processed = i + 1;
            if (processed % progressInterval == 0 || processed == rows.Count)
                progress?.Report(new GeoRebuildProgress(processed, rows.Count));
        }

        transaction.Commit();
    }

    private static (double DistanceKm, double BearingDeg) CalculateGeo(double lat1, double lon1, double lat2, double lon2)
    {
        const double R=6371.0088; static double Rad(double d)=>d*Math.PI/180.0;
        var p1=Rad(lat1); var p2=Rad(lat2); var dp=Rad(lat2-lat1); var dl=Rad(lon2-lon1);
        var a=Math.Sin(dp/2)*Math.Sin(dp/2)+Math.Cos(p1)*Math.Cos(p2)*Math.Sin(dl/2)*Math.Sin(dl/2); var d=R*2*Math.Atan2(Math.Sqrt(a),Math.Sqrt(1-a));
        var y=Math.Sin(dl)*Math.Cos(p2); var x=Math.Cos(p1)*Math.Sin(p2)-Math.Sin(p1)*Math.Cos(p2)*Math.Cos(dl); var b=(Math.Atan2(y,x)*180/Math.PI+360)%360; return(d,b);
    }

    private static async Task UpsertSourceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceFile,
        long qsoCount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO source_file(full_path, file_name, qso_count, last_imported_utc)
            VALUES($path, $name, $count, $utc)
            ON CONFLICT(full_path) DO UPDATE SET
                file_name=excluded.file_name,
                qso_count=excluded.qso_count,
                last_imported_utc=excluded.last_imported_utc;
            """;
        command.Parameters.AddWithValue("$path", sourceFile);
        command.Parameters.AddWithValue("$name", Path.GetFileName(sourceFile));
        command.Parameters.AddWithValue("$count", qsoCount);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteGeoCacheForQsoAsync(SqliteConnection connection, SqliteTransaction transaction, string sourceFile, long sourceRecordIndex, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM qso_geo_cache WHERE qso_id=(SELECT id FROM qso WHERE source_file=$file AND source_record_index=$idx);";
        command.Parameters.AddWithValue("$file", sourceFile); command.Parameters.AddWithValue("$idx", sourceRecordIndex);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> GetExistingHashAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceFile,
        long sourceRecordIndex,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT source_record_hash FROM qso WHERE source_file=$file AND source_record_index=$idx;";
        command.Parameters.AddWithValue("$file", sourceFile);
        command.Parameters.AddWithValue("$idx", sourceRecordIndex);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QsoImportRecord item,
        CancellationToken cancellationToken)
    {
        var q = item.Qso;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO qso (
                source_file, source_record_index, source_record_hash, record_type_raw,
                qso_date, start_time, end_time, callsign,
                sent_rst, sent_exchange, received_rst, received_exchange,
                band_code, mode_code, rx_frequency_hz, qth, name, remarks,
                status151_raw, country, continent_code, status161_raw,
                multiplier1, multiplier2, points,
                locator, latitude, longitude, grid2, grid4, grid6, grid8,
                iota_number, iota_continent_code, district, cq_zone, itu_zone,
                manager, cba, manager_ok, qsl_send_datetime,
                award1, count1, award2, count2
            ) VALUES (
                $source_file, $source_record_index, $source_record_hash, $record_type_raw,
                $qso_date, $start_time, $end_time, $callsign,
                $sent_rst, $sent_exchange, $received_rst, $received_exchange,
                $band_code, $mode_code, $rx_frequency_hz, $qth, $name, $remarks,
                $status151_raw, $country, $continent_code, $status161_raw,
                $multiplier1, $multiplier2, $points,
                $locator, $latitude, $longitude, $grid2, $grid4, $grid6, $grid8,
                $iota_number, $iota_continent_code, $district, $cq_zone, $itu_zone,
                $manager, $cba, $manager_ok, $qsl_send_datetime,
                $award1, $count1, $award2, $count2
            )
            ON CONFLICT(source_file, source_record_index) DO UPDATE SET
                source_record_hash=excluded.source_record_hash,
                record_type_raw=excluded.record_type_raw,
                qso_date=excluded.qso_date,
                start_time=excluded.start_time,
                end_time=excluded.end_time,
                callsign=excluded.callsign,
                sent_rst=excluded.sent_rst,
                sent_exchange=excluded.sent_exchange,
                received_rst=excluded.received_rst,
                received_exchange=excluded.received_exchange,
                band_code=excluded.band_code,
                mode_code=excluded.mode_code,
                rx_frequency_hz=excluded.rx_frequency_hz,
                qth=excluded.qth,
                name=excluded.name,
                remarks=excluded.remarks,
                status151_raw=excluded.status151_raw,
                country=excluded.country,
                continent_code=excluded.continent_code,
                status161_raw=excluded.status161_raw,
                multiplier1=excluded.multiplier1,
                multiplier2=excluded.multiplier2,
                points=excluded.points,
                locator=excluded.locator,
                latitude=excluded.latitude,
                longitude=excluded.longitude,
                grid2=excluded.grid2,
                grid4=excluded.grid4,
                grid6=excluded.grid6,
                grid8=excluded.grid8,
                iota_number=excluded.iota_number,
                iota_continent_code=excluded.iota_continent_code,
                district=excluded.district,
                cq_zone=excluded.cq_zone,
                itu_zone=excluded.itu_zone,
                manager=excluded.manager,
                cba=excluded.cba,
                manager_ok=excluded.manager_ok,
                qsl_send_datetime=excluded.qsl_send_datetime,
                award1=excluded.award1,
                count1=excluded.count1,
                award2=excluded.award2,
                count2=excluded.count2;
            """;

        command.Parameters.AddWithValue("$source_file", item.SourceFile);
        command.Parameters.AddWithValue("$source_record_index", item.SourceRecordIndex);
        command.Parameters.AddWithValue("$source_record_hash", item.SourceRecordHash);
        command.Parameters.AddWithValue("$record_type_raw", q.RecordTypeRaw);
        command.Parameters.AddWithValue("$qso_date", q.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$start_time", q.StartTime.ToString("HH:mm:ss"));
        command.Parameters.AddWithValue("$end_time", q.EndTime.ToString("HH:mm:ss"));
        command.Parameters.AddWithValue("$callsign", q.CallSign);
        command.Parameters.AddWithValue("$sent_rst", q.SentRst);
        command.Parameters.AddWithValue("$sent_exchange", q.SentExchange);
        command.Parameters.AddWithValue("$received_rst", q.ReceivedRst);
        command.Parameters.AddWithValue("$received_exchange", q.ReceivedExchange);
        command.Parameters.AddWithValue("$band_code", q.BandCode);
        command.Parameters.AddWithValue("$mode_code", q.ModeCode);
        command.Parameters.AddWithValue("$rx_frequency_hz", checked((long)q.RxFrequencyHz));
        command.Parameters.AddWithValue("$qth", q.Qth);
        command.Parameters.AddWithValue("$name", q.Name);
        command.Parameters.AddWithValue("$remarks", q.Remarks);
        command.Parameters.AddWithValue("$status151_raw", q.Status151Raw);
        command.Parameters.AddWithValue("$country", q.Country);
        command.Parameters.AddWithValue("$continent_code", q.ContinentCode);
        command.Parameters.AddWithValue("$status161_raw", q.Status161Raw);
        command.Parameters.AddWithValue("$multiplier1", q.Multiplier1);
        command.Parameters.AddWithValue("$multiplier2", q.Multiplier2);
        command.Parameters.AddWithValue("$points", q.Points);
        command.Parameters.AddWithValue("$locator", q.Locator);
        command.Parameters.AddWithValue("$latitude", (object?)item.Latitude ?? DBNull.Value);
        command.Parameters.AddWithValue("$longitude", (object?)item.Longitude ?? DBNull.Value);
        command.Parameters.AddWithValue("$grid2", (object?)item.Grid2 ?? DBNull.Value);
        command.Parameters.AddWithValue("$grid4", (object?)item.Grid4 ?? DBNull.Value);
        command.Parameters.AddWithValue("$grid6", (object?)item.Grid6 ?? DBNull.Value);
        command.Parameters.AddWithValue("$grid8", (object?)item.Grid8 ?? DBNull.Value);
        command.Parameters.AddWithValue("$iota_number", q.IotaNumber);
        command.Parameters.AddWithValue("$iota_continent_code", q.IotaContinentCode);
        command.Parameters.AddWithValue("$district", q.District);
        command.Parameters.AddWithValue("$cq_zone", q.CqZone);
        command.Parameters.AddWithValue("$itu_zone", q.ItuZone);
        command.Parameters.AddWithValue("$manager", q.Manager);
        command.Parameters.AddWithValue("$cba", q.Cba ? 1 : 0);
        command.Parameters.AddWithValue("$manager_ok", q.ManagerOk ? 1 : 0);
        command.Parameters.AddWithValue("$qsl_send_datetime", q.QslSendDateTime?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$award1", q.Award1);
        command.Parameters.AddWithValue("$count1", q.Count1);
        command.Parameters.AddWithValue("$award2", q.Award2);
        command.Parameters.AddWithValue("$count2", q.Count2);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }


    private static async Task RenameColumnIfNeededAsync(
        SqliteConnection connection,
        string tableName,
        string oldColumnName,
        string newColumnName,
        CancellationToken cancellationToken)
    {
        var oldExists = false;
        var newExists = false;

        await using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await check.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(1);
                oldExists |= string.Equals(name, oldColumnName, StringComparison.OrdinalIgnoreCase);
                newExists |= string.Equals(name, newColumnName, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!oldExists || newExists)
            return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} RENAME COLUMN {oldColumnName} TO {newColumnName};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await reader.DisposeAsync();
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GetDefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "UcxStats", "ucxstats.db");
    }
}
