using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace UcxStats.Data.Database;

internal static class QsoScopeSql
{
    public static IReadOnlyList<string> AddFilters(
        SqliteCommand command,
        QsoScope scope,
        string alias = "q")
    {
        var filters = new List<string>();
        // One JSON parameter per selection avoids SQLite variable/expression-depth
        // limits when a map selects tens of thousands of source-record keys or IDs.
        if (scope.SourceFiles is not null)
        {
            command.Parameters.AddWithValue("$scope_sources", JsonSerializer.Serialize(scope.SourceFiles));
            filters.Add($"{alias}.source_file IN (SELECT value FROM json_each($scope_sources))");
        }
        if (scope.QsoIds is not null)
        {
            command.Parameters.AddWithValue("$scope_ids", JsonSerializer.Serialize(scope.QsoIds));
            filters.Add($"{alias}.id IN (SELECT value FROM json_each($scope_ids))");
        }
        if (scope.SourceRecords is not null)
        {
            command.Parameters.AddWithValue("$scope_records", JsonSerializer.Serialize(scope.SourceRecords));
            filters.Add($"({alias}.source_file,{alias}.source_record_index) IN " +
                "(SELECT json_extract(value,'$.SourceFile'),json_extract(value,'$.SourceRecordIndex') " +
                "FROM json_each($scope_records))");
        }
        if (scope.FromUtc is { } fromUtc)
        {
            filters.Add($"({alias}.qso_date > $scope_from_date OR " +
                        $"({alias}.qso_date = $scope_from_date AND {alias}.start_time >= $scope_from_time))");
            command.Parameters.AddWithValue("$scope_from_date", fromUtc.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("$scope_from_time", fromUtc.ToString("HH:mm:ss"));
        }
        return filters;
    }
}
