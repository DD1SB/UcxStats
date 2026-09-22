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
    public bool PaperQslReceived => (Status151Raw & 0x20) != 0;
    public bool EqslConfirmed => (Status151Raw & 0x40) != 0;
    public bool LotwConfirmed => (Status151Raw & 0x80) != 0;
    public bool ClubLogConfirmed => (Status161Raw & 0x20) != 0;
    public bool DclConfirmed => DclAdifParser.IsConfirmedStatus(DclStatus);
    public bool Confirmed => PaperQslReceived || EqslConfirmed || LotwConfirmed || ClubLogConfirmed || DclConfirmed;

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
