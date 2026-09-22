namespace UcxStats.Core.Ucx;

public sealed class UcxDiagnosticReport
{
    public List<UcxDiagnostic> Items { get; } = [];
    public int WarningCount => Items.Count(x => x.Severity == UcxDiagnosticSeverity.Warning);
    public int ErrorCount => Items.Count(x => x.Severity == UcxDiagnosticSeverity.Error);
    public int OkCount { get; internal set; }
}
