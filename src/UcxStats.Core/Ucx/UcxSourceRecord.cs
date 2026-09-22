namespace UcxStats.Core.Ucx;

public sealed record UcxSourceRecord
{
    public required UcxQsoRecord Qso { get; init; }
    public required string RecordHash { get; init; }
}
