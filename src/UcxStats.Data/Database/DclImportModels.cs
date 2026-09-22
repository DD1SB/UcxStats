namespace UcxStats.Data.Database;

public sealed record DclAdifRecord(
    string CallSign,
    string QsoDate,
    string TimeOn,
    string Band,
    string Mode,
    string Status,
    string StationCallSign,
    string MyGridSquare,
    string Dxcc,
    string Adif2);

public sealed record DclDiagnosticEntry(
    string Kind,
    string Reason,
    string QsoDate,
    string TimeOn,
    string CallSign,
    string Band,
    string Mode,
    string Status,
    string Dxcc,
    string Adif2,
    string StationCallSign,
    string MyGridSquare,
    IReadOnlyList<DclCandidateQso> Candidates);

public sealed record DclCandidateQso(
    long Id,
    string QsoDate,
    string StartTime,
    string CallSign,
    int BandCode,
    int ModeCode,
    string SourceFile);

public sealed record DclImportResult(
    int Records,
    int ConfirmedRecords,
    int Matched,
    int Unmatched,
    int Ambiguous,
    int IgnoredNotConfirmed,
    IReadOnlyList<DclDiagnosticEntry> Diagnostics);
