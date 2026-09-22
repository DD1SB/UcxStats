using UcxStats.Core.Ucx;

namespace UcxStats.Data.Database;

public sealed record QsoImportRecord
{
    public required string SourceFile { get; init; }
    public required long SourceRecordIndex { get; init; }
    public required string SourceRecordHash { get; init; }
    public required UcxQsoRecord Qso { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Grid2 { get; init; }
    public string? Grid4 { get; init; }
    public string? Grid6 { get; init; }
    public string? Grid8 { get; init; }
}

public sealed record QsoImportResult(int Inserted, int Updated, int Unchanged);
