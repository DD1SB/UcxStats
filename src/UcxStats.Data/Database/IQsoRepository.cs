using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Database;

public interface IQsoRepository
{
    string DatabasePath { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<QsoImportResult> UpsertManyAsync(IEnumerable<QsoImportRecord> records, CancellationToken cancellationToken = default);
    Task<QsoImportResult> ReconcileSourceAsync(string sourceFile, IReadOnlyList<QsoImportRecord> records,
        CancellationToken cancellationToken = default, Func<QsoImportResult, CancellationToken, Task>? validateBeforeCommit = null);
    Task<DclImportResult> ImportDclAdifAsync(string adifText, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaeQsoCandidate>> GetWaeQsoCandidatesAsync(CancellationToken cancellationToken = default);
    Task<DxccIdentityAuditResult> GetDxccIdentityAuditAsync(CancellationToken cancellationToken = default);
    Task<QsoStatisticsSnapshot> GetMapStatisticsAsync(QsoScope scope, CancellationToken cancellationToken = default);
    Task<DxccStatusSnapshot> GetDxccStatusAsync(QsoScope scope, CancellationToken cancellationToken = default);
    Task<QueryResult> ExecuteReadOnlyQueryAsync(string sql, CancellationToken cancellationToken = default);
    Task<long> GetTotalQsoCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceSummary>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapQsoPoint>> GetMapQsoPointsByIdsAsync(IReadOnlyCollection<long> qsoIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapQsoPoint>> GetMapQsoPointsAsync(
        QsoScope scope,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapQsoPoint>> GetMapQsoPointsAsync(
        IReadOnlyCollection<string>? sourceFiles = null,
        DateTime? fromUtc = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StationProfile>> GetStationProfilesAsync(CancellationToken cancellationToken = default);
    Task<long> SaveStationProfileAsync(StationProfile profile, CancellationToken cancellationToken = default);
    Task DeleteStationProfileAsync(long id, CancellationToken cancellationToken = default);
    Task AssignStationProfileToSourceAsync(string sourceFile, long? stationProfileId, CancellationToken cancellationToken = default);
    Task AssignStationProfileToSourcesAsync(
        IReadOnlyCollection<string> sourceFiles,
        long? stationProfileId,
        IProgress<GeoRebuildProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
