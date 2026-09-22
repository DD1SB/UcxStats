using UcxStats.Data.Confirmation;

namespace UcxStats.Data.Database;

public sealed record WaeQsoCandidate(
    long Id,
    string SourceFile,
    string QsoDate,
    string StartTime,
    string CallSign,
    int BandCode,
    int ModeCode,
    string Country,
    string Locator,
    int Status151Raw,
    int Status161Raw,
    string DclStatus,
    int? DclDxcc,
    int? DclAdif2)
{
    public QsoConfirmationFlags ConfirmationFlags =>
        QsoConfirmationEvaluator.GetFlags(Status151Raw, Status161Raw, DclStatus);

    public bool PaperQslReceived => ConfirmationFlags.HasFlag(QsoConfirmationFlags.Paper);
    public bool EqslConfirmed => ConfirmationFlags.HasFlag(QsoConfirmationFlags.Eqsl);
    public bool LotwConfirmed => ConfirmationFlags.HasFlag(QsoConfirmationFlags.Lotw);
    public bool ClubLogConfirmed => ConfirmationFlags.HasFlag(QsoConfirmationFlags.ClubLog);
    public bool DclConfirmed => ConfirmationFlags.HasFlag(QsoConfirmationFlags.Dcl);
    public bool Confirmed => QsoConfirmationEvaluator.IsConfirmedForExistingWaePolicy(ConfirmationFlags);

    public string ConfirmationText
    {
        get
        {
            var items = new List<string>();
            if (PaperQslReceived) items.Add("Papier");
            if (LotwConfirmed) items.Add("LoTW");
            if (EqslConfirmed) items.Add("eQSL");
            if (ClubLogConfirmed) items.Add("ClubLog");
            if (DclConfirmed) items.Add($"DCL:{DclStatus}");
            return string.Join(", ", items);
        }
    }
}
