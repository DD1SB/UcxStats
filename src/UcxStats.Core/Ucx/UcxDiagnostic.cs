namespace UcxStats.Core.Ucx;

public enum UcxDiagnosticSeverity
{
    Warning,
    Error
}

public sealed record UcxDiagnostic(
    long RecordIndex,
    UcxDiagnosticSeverity Severity,
    string Message);
