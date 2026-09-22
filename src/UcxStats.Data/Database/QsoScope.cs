namespace UcxStats.Data.Database;

public sealed record QsoSourceRecordKey(string SourceFile, long SourceRecordIndex);

public sealed record QsoScope
{
    public IReadOnlyList<string>? SourceFiles { get; init; }
    public IReadOnlyList<long>? QsoIds { get; init; }
    public IReadOnlyList<QsoSourceRecordKey>? SourceRecords { get; init; }
    public DateTime? FromUtc { get; init; }

    public static QsoScope All { get; } = new();

    public static QsoScope ForSources(IEnumerable<string> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        return new() { SourceFiles = sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
    }

    public static QsoScope ForQsoIds(IEnumerable<long> qsoIds)
    {
        ArgumentNullException.ThrowIfNull(qsoIds);
        return new() { QsoIds = qsoIds.Distinct().ToArray() };
    }

    public static QsoScope ForSourceRecords(IEnumerable<QsoSourceRecordKey> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new() { SourceRecords = records.Distinct().ToArray() };
    }

    public QsoScope WithFromUtc(DateTime fromUtc) => this with
    {
        FromUtc = fromUtc.Kind == DateTimeKind.Local
            ? fromUtc.ToUniversalTime()
            : DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)
    };
}
