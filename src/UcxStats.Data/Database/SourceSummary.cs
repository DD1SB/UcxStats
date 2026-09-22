namespace UcxStats.Data.Database;

public sealed record SourceSummary(
    string FullPath,
    string FileName,
    long QsoCount,
    DateTime LastImportedUtc,
    long? StationProfileId,
    string? StationProfileName);
