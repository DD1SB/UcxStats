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

        if (scope.SourceFiles is not null)
        {
            if (scope.SourceFiles.Count == 0)
            {
                filters.Add("1=0");
            }
            else
            {
                var parameters = new List<string>();
                for (var i = 0; i < scope.SourceFiles.Count; i++)
                {
                    var name = $"$scope_source_{i}";
                    parameters.Add(name);
                    command.Parameters.AddWithValue(name, scope.SourceFiles[i]);
                }
                filters.Add($"{alias}.source_file IN ({string.Join(",", parameters)})");
            }
        }

        if (scope.QsoIds is not null)
        {
            if (scope.QsoIds.Count == 0)
            {
                filters.Add("1=0");
            }
            else
            {
                var parameters = new List<string>();
                for (var i = 0; i < scope.QsoIds.Count; i++)
                {
                    var name = $"$scope_id_{i}";
                    parameters.Add(name);
                    command.Parameters.AddWithValue(name, scope.QsoIds[i]);
                }
                filters.Add($"{alias}.id IN ({string.Join(",", parameters)})");
            }
        }

        if (scope.SourceRecords is not null)
        {
            if (scope.SourceRecords.Count == 0)
            {
                filters.Add("1=0");
            }
            else
            {
                var pairs = new List<string>();
                for (var i = 0; i < scope.SourceRecords.Count; i++)
                {
                    var sourceName = $"$scope_record_source_{i}";
                    var indexName = $"$scope_record_index_{i}";
                    command.Parameters.AddWithValue(sourceName, scope.SourceRecords[i].SourceFile);
                    command.Parameters.AddWithValue(indexName, scope.SourceRecords[i].SourceRecordIndex);
                    pairs.Add($"({alias}.source_file={sourceName} AND {alias}.source_record_index={indexName})");
                }
                filters.Add($"({string.Join(" OR ", pairs)})");
            }
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
